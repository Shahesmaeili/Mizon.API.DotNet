using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Mizon.API.Client;

public static class MizonApiServiceCollectionExtensions
{
    public static IServiceCollection AddMizonApi(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient<MizonApi>(client => { })
             .ConfigurePrimaryHttpMessageHandler(() =>
             {
                 var handler = new SocketsHttpHandler
                 {
                     AutomaticDecompression = DecompressionMethods.All,
                     EnableMultipleHttp2Connections = true,
                 };


                 return handler;
             });



        services.AddSingleton<MizonApi>();



        // Register MizonSocket as singleton. It will use MizonApi to read the current token.
        services.AddSingleton(sp =>
        {
            var api = sp.GetRequiredService<MizonApi>();
            return new MizonSocket(api, Endpoints.MIZON_HUB_URL);
        });



        return services;
    }
}
