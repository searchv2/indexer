using Microsoft.Extensions.DependencyInjection;

namespace Indexer.Application;

public static class DependencyInjection
{
    /// <summary>Registers the indexer's use cases.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<IIndexingService, IndexingService>();
        return services;
    }
}
