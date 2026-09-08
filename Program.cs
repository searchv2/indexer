using Microsoft.Extensions.DependencyInjection;
using Indexer.Application;
using Indexer.Infrastructure;

// Composition root. Dependencies point inward: this console entry point depends on
// Application, which depends on Core; Infrastructure supplies the adapters behind Core's
// ports and is wired in only here.
var services = new ServiceCollection();
services.AddApplication();
services.AddInfrastructure();

using var provider = services.BuildServiceProvider();

// The console is one way to trigger a crawl. A future "enough documents uploaded" or
// batch-update trigger would be another adapter calling the same IIndexingService.
var indexing = provider.GetRequiredService<IIndexingService>();

var stats = indexing.Reindex();

Console.WriteLine($"DONE! used {stats.Elapsed.TotalMilliseconds}");
Console.WriteLine($"Indexed {stats.DocumentCount} documents");
Console.WriteLine($"Number of different words: {stats.DistinctWordCount}");
Console.WriteLine($"The first {stats.SampleWords.Count} is:");
foreach (var p in stats.SampleWords)
    Console.WriteLine($"<{p.Key}, {p.Value}>");
