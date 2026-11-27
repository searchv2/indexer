using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Indexer.Application;
using Indexer.Infrastructure;

// Composition root. Dependencies point inward: this console entry point depends on
// Application, which depends on Core; Infrastructure supplies the adapters behind Core's
// ports and is wired in only here.
var services = new ServiceCollection();
services.AddApplication();
services.AddInfrastructure();

await using var provider = services.BuildServiceProvider();

// Metrics go to the OpenTelemetry Collector when OTEL_EXPORTER_OTLP_ENDPOINT is set (Kubernetes); without it
// (docker compose, a local run) nothing is exported. OTEL_SERVICE_NAME names the role in Grafana. Disposing the
// provider at the end of the program flushes the last values, which matters for the short-lived pipeline Job.
using var meterProvider = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"))
    ? null
    : Sdk.CreateMeterProviderBuilder()
        .AddMeter(IndexerMetrics.MeterName)
        .AddOtlpExporter()
        .Build();

// One image, several roles (first argument, or INDEXER_ROLE):
//   batch      (default) crawl and index in this one process - no message bus needed
//   pipeline   coordinate a reindex through the message bus: reset the index, publish every file, wait for the workers
//   tokenizer  worker: read a discovered file and extract its words
//   writer     worker: store a tokenized document in PostgreSQL
// The console is one way to trigger a crawl. A future "enough documents uploaded" or
// batch-update trigger would be another adapter calling the same use case.
var role = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("INDEXER_ROLE") ?? "batch";

using var shutdown = new CancellationTokenSource();
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
{
    context.Cancel = true;
    shutdown.Cancel();
});
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

switch (role)
{
    case "batch":
        PrintStatistics(provider.GetRequiredService<IIndexingService>().Reindex());
        break;

    case "pipeline":
        var result = await provider.GetRequiredService<IndexingPipeline>().RunAsync(Console.WriteLine, shutdown.Token);
        Console.WriteLine($"DONE! used {result.Elapsed.TotalMilliseconds}");
        Console.WriteLine($"Published {result.Published} documents");
        Console.WriteLine($"Indexed {result.Indexed} documents");
        Console.WriteLine($"Failed (dead-lettered) {result.Failed} documents");
        Console.WriteLine($"Workers: {result.Tokenizers} tokenizers, {result.Writers} writers");
        if (result.Failed > 0)
            return 1;
        break;

    case "tokenizer":
        Console.WriteLine("Tokenizer worker started.");
        await provider.GetRequiredService<TokenizerWorker>().RunAsync(shutdown.Token);
        Console.WriteLine("Tokenizer worker stopped.");
        break;

    case "writer":
        Console.WriteLine("Writer worker started.");
        await provider.GetRequiredService<WriterWorker>().RunAsync(shutdown.Token);
        Console.WriteLine("Writer worker stopped.");
        break;

    default:
        Console.Error.WriteLine($"Unknown role '{role}'. Use batch, pipeline, tokenizer or writer.");
        return 2;
}

return 0;

static void PrintStatistics(Indexer.Core.Entities.IndexStatistics stats)
{
    Console.WriteLine($"DONE! used {stats.Elapsed.TotalMilliseconds}");
    Console.WriteLine($"Indexed {stats.DocumentCount} documents");
    Console.WriteLine($"Number of different words: {stats.DistinctWordCount}");
    Console.WriteLine($"The first {stats.SampleWords.Count} is:");
    foreach (var p in stats.SampleWords)
        Console.WriteLine($"<{p.Key}, {p.Value}>");
}
