using Microsoft.Extensions.DependencyInjection;
using Indexer.Core.Abstractions;
using Indexer.Infrastructure.Persistence;

namespace Indexer.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the concrete adapters behind the Core ports.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(new IndexerOptions());
        services.AddSingleton<IDocumentSource, FileSystemDocumentSource>();

        // Writes the inverted index to the local SQLite file in searchv2/db that SearchAPI
        // reads. Swap for InMemoryIndexStore to run a crawl without touching disk.
        services.AddSingleton<IIndexStore, SqliteIndexStore>();
        return services;
    }
}
