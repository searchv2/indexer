using Microsoft.Extensions.DependencyInjection;

namespace Indexer.Application;

public static class DependencyInjection
{
    /// <summary>Registers the indexer's use cases.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<IIndexingService, IndexingService>();
        services.AddSingleton(new PipelineOptions());
        services.AddTransient<IndexingPipeline>();
        services.AddTransient<TokenizerWorker>();
        services.AddTransient<WriterWorker>();
        return services;
    }
}
