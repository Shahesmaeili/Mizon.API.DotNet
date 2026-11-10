using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;

namespace Mizon.API.Client;

public static class MizonApiServiceCollectionExtensions
{
    public static IServiceCollection AddMizonApi(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient(nameof(MizonApi), client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                EnableMultipleHttp2Connections = true,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        // A singleton API keeps the session/token. Resolve the configured named client,
        // rather than replacing a typed-client registration with an unconfigured HttpClient.
        services.TryAddSingleton(sp => new MizonApi(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(MizonApi)),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(), ownsHttpClient: true));
        services.TryAddSingleton(sp => new MizonSocket(sp.GetRequiredService<MizonApi>(), Endpoints.MIZON_HUB_URL));
        return services;
    }
}
