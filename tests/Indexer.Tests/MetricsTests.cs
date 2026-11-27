using System.Diagnostics.Metrics;
using Indexer.Application;
using Indexer.Core.Messages;

namespace Indexer.Tests;

/// <summary>Records the indexer's metrics while a test runs. The Meter is static, so one listener per test.</summary>
internal sealed class MetricCapture : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<(string Name, double Value, Dictionary<string, string?> Tags)> _measurements = new();

    public MetricCapture()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == IndexerMetrics.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((i, v, tags, _) => Add(i.Name, v, tags));
        _listener.SetMeasurementEventCallback<double>((i, v, tags, _) => Add(i.Name, v, tags));
        _listener.Start();
    }

    private void Add(string name, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copy = tags.ToArray().ToDictionary(t => t.Key, t => t.Value?.ToString());
        lock (_measurements) _measurements.Add((name, value, copy));
    }

    /// <summary>Asks the observable gauges (the last run) to report now.</summary>
    public void Observe() => _listener.RecordObservableInstruments();

    public IReadOnlyList<(string Name, double Value, Dictionary<string, string?> Tags)> Of(string name)
    {
        lock (_measurements) return _measurements.Where(m => m.Name == name).ToList();
    }

    public void Dispose() => _listener.Dispose();
}

[Collection("Indexer metrics are process-wide")]
public class MetricsTests
{
    [Fact]
    public async Task A_handled_document_is_counted_and_timed_per_stage()
    {
        var source = new FakeSource();
        source.Files["/docs/a.txt"] = "apple banana";
        using var capture = new MetricCapture();

        await new TokenizerWorker(source, new FakeQueue()).HandleAsync(new DocumentDiscovered("/docs/a.txt", "x"), default);
        await new WriterWorker(new FakeWriter(), new FakeQueue())
            .HandleAsync(new DocumentTokenized("/docs/a.txt", "x", new[] { "apple" }), default);

        var counted = capture.Of("searchv2.indexing.stage.documents");
        Assert.Contains(counted, m => m.Tags["stage"] == "tokenizer" && m.Tags["outcome"] == "ok" && m.Value == 1);
        Assert.Contains(counted, m => m.Tags["stage"] == "writer" && m.Tags["outcome"] == "ok" && m.Value == 1);
        Assert.Equal(2, capture.Of("searchv2.indexing.stage.duration").Count);
    }

    [Fact]
    public async Task A_failed_document_is_counted_as_an_error()
    {
        using var capture = new MetricCapture();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new TokenizerWorker(new FakeSource(), new FakeQueue()).HandleAsync(new DocumentDiscovered("/docs/missing.txt", "x"), default));

        Assert.Contains(capture.Of("searchv2.indexing.stage.documents"),
            m => m.Tags["stage"] == "tokenizer" && m.Tags["outcome"] == "error");
    }

    [Fact]
    public async Task A_finished_run_reports_its_duration_throughput_and_worker_counts()
    {
        var source = new FakeSource();
        for (var i = 0; i < 4; i++) source.Files[$"/docs/{i}.txt"] = "x";
        var queue = new FakeQueue
        {
            Consumers = { [Queues.Discovered] = 3, [Queues.Tokenized] = 2 },
        };
        var writer = new FakeWriter { Count = 4 };
        using var capture = new MetricCapture();

        var result = await new IndexingPipeline(source, queue, writer, new PipelineOptions()).RunAsync(_ => { }, default);
        capture.Observe();

        Assert.Equal(3, result.Tokenizers);
        Assert.Equal(2, result.Writers);
        var duration = Assert.Single(capture.Of("searchv2.indexing.run.duration"));
        Assert.Equal("3", duration.Tags["tokenizers"]);
        Assert.Equal("2", duration.Tags["writers"]);
        Assert.True(duration.Value >= 0);
        Assert.Single(capture.Of("searchv2.indexing.run.throughput"));
        var documents = capture.Of("searchv2.indexing.run.documents");
        Assert.Contains(documents, m => m.Tags["outcome"] == "indexed" && m.Value == 4);
        Assert.Contains(documents, m => m.Tags["outcome"] == "failed" && m.Value == 0);
    }
}
