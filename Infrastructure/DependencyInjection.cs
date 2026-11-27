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

        // Writes the inverted index to the PostgreSQL database that SearchAPI reads. Swap for
        // InMemoryIndexStore to run a crawl without a database.
        services.AddSingleton<IIndexStore>(_ => new PostgresIndexStore(SearchDatabase.ConnectionString));

        // The pipeline: a message bus between the stages and a writer that lets the database assign ids.
        services.AddSingleton(new RabbitMqOptions());
        services.AddSingleton<IMessageQueue, RabbitMqQueue>();
        services.AddSingleton<IDocumentWriter>(_ => new PostgresDocumentWriter(SearchDatabase.ConnectionString));
        return services;
    }
}
