using Microsoft.Extensions.DependencyInjection;

namespace Exchange.Infrastructure.Caching;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureCaching(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IAppCache, MemoryAppCache>();
        services.AddSingleton<ICacheVersionStore, MemoryCacheVersionStore>();
        services.AddSingleton<ICacheKeyFactory, CacheKeyFactory>();
        services.AddSingleton<ICacheInvalidationService, CacheInvalidationService>();
        services.AddSingleton<IScopedAppCache, ScopedAppCache>();
        return services;
    }
}
