using System.Diagnostics.Metrics;

namespace Indexer.Application;

/// <summary>
/// The indexer's metrics, using only the built-in <c>System.Diagnostics.Metrics</c>; the OpenTelemetry
/// exporter that sends them to the Collector is wired in <c>Program.cs</c>. Workers report what they handle
/// as it happens. The pipeline Job reports its last run once, when it finishes: how long it took, how
/// many documents per second, and how many tokenizers and writers were connected.
/// </summary>
public static class IndexerMetrics
{
    public const string MeterName = "SearchV2.Indexer";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> StageDocuments = Meter.CreateCounter<long>(
        "searchv2.indexing.stage.documents", description: "Documents a pipeline stage has handled, by outcome.");

    // Per-document handling is milliseconds, so the default bucket bounds (which start at 5) would put everything in one bucket.
    private static readonly Histogram<double> StageDuration = Meter.CreateHistogram<double>(
        "searchv2.indexing.stage.duration", "s", "Time a pipeline stage spent on one document.",
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5],
        });

    private static PipelineResult? _lastRun;

    static IndexerMetrics()
    {
        Meter.CreateObservableGauge("searchv2.indexing.run.duration", () => Last(r => r.Elapsed.TotalSeconds), "s",
            "How long the last pipeline run took, from clearing the index to the last document stored.");
        Meter.CreateObservableGauge("searchv2.indexing.run.throughput", () => Last(r => r.Indexed / Math.Max(r.Elapsed.TotalSeconds, 0.001)), "{document}/s",
            "Documents stored per second during the last pipeline run.");
        Meter.CreateObservableGauge("searchv2.indexing.run.documents", () => LastOutcomes(), "{document}",
            "Documents of the last pipeline run, by outcome (indexed or failed).");
    }

    /// <summary>Counts one document handled by <paramref name="stage"/> and records how long it took.</summary>
    public static void RecordDocument(string stage, TimeSpan elapsed, bool succeeded)
    {
        StageDuration.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>("stage", stage));
        StageDocuments.Add(1, new KeyValuePair<string, object?>("stage", stage),
            new KeyValuePair<string, object?>("outcome", succeeded ? "ok" : "error"));
    }

    /// <summary>Remembers the finished run so the gauges report it. The tags say how many workers took part.</summary>
    public static void RecordRun(PipelineResult result) => _lastRun = result;

    private static IEnumerable<Measurement<double>> Last(Func<PipelineResult, double> value)
    {
        var run = _lastRun;
        if (run is not null)
            yield return new Measurement<double>(value(run), RunTags(run));
    }

    private static IEnumerable<Measurement<double>> LastOutcomes()
    {
        var run = _lastRun;
        if (run is null)
            yield break;
        yield return new Measurement<double>(run.Indexed, [.. RunTags(run), new("outcome", "indexed")]);
        yield return new Measurement<double>(run.Failed, [.. RunTags(run), new("outcome", "failed")]);
    }

    // The worker counts make runs comparable: a run with 1 + 1 workers and one with 4 + 4 are separate series.
    private static KeyValuePair<string, object?>[] RunTags(PipelineResult run) =>
    [
        new("tokenizers", run.Tokenizers.ToString()),
        new("writers", run.Writers.ToString()),
    ];
}
