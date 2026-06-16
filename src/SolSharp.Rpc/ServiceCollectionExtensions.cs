using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SolSharp.Rpc;

/// <summary>Dependency-injection registration for <see cref="SolanaRpcClient"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SolanaRpcClient"/> as a typed <see cref="HttpClient"/> configured from the supplied options.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">Configures the <see cref="SolanaRpcOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddSolanaRpc(this IServiceCollection services, Action<SolanaRpcOptions> configure)
    {
        services
            .AddOptions<SolanaRpcOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .Validate(
                options => Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri)
                           && uri.Scheme is "http" or "https",
                "SolanaRpcOptions.Endpoint must be an absolute http(s) URL.")
            .ValidateOnStart();

        services.AddHttpClient<SolanaRpcClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<SolanaRpcOptions>>().Value;
            client.BaseAddress = new Uri(options.Endpoint);
        });

        return services;
    }

    /// <summary>
    /// Registers <see cref="SolanaRpcClient"/> as a typed <see cref="HttpClient"/> pointed at <paramref name="endpoint"/>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="endpoint">The JSON-RPC HTTP endpoint URL.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddSolanaRpc(this IServiceCollection services, string endpoint)
        => services.AddSolanaRpc(options => options.Endpoint = endpoint);
}
