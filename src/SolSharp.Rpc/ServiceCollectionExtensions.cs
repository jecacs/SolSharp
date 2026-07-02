using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SolSharp.Rpc.Streaming;

namespace SolSharp.Rpc;

/// <summary>Dependency-injection registration for <see cref="SolanaRpcClient"/> and <see cref="SolanaWsClient"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SolanaWsClient"/> as a singleton, wired to the container's
    /// <see cref="ILoggerFactory"/> when one is registered. The consumer still opens the connection with
    /// <see cref="SolanaWsClient.ConnectAsync"/>; the container disposes the client on shutdown.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="options">Connection options (auto-reconnect policy); defaults are used when <c>null</c>.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddSolanaWs(this IServiceCollection services, SolanaWsClientOptions? options = null)
        => services.AddSingleton(provider =>
            new SolanaWsClient(options ?? new SolanaWsClientOptions(), provider.GetService<ILoggerFactory>()));

    /// <summary>
    /// Registers <see cref="SolanaRpcClient"/> as a typed <see cref="HttpClient"/> with a standard
    /// resilience pipeline (retry on transient failures and HTTP 429, with backoff and a request timeout).
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">Configures the <see cref="SolanaRpcOptions"/>.</param>
    /// <param name="configureResilience">Optionally tunes the resilience pipeline (retries, timeouts, circuit breaker).</param>
    /// <returns>The <see cref="IHttpClientBuilder"/>, so the caller can keep configuring the client (headers, timeout, handlers).</returns>
    public static IHttpClientBuilder AddSolanaRpc(
        this IServiceCollection services,
        Action<SolanaRpcOptions> configure,
        Action<HttpStandardResilienceOptions>? configureResilience = null)
    {
        // Validated with an explicit predicate rather than ValidateDataAnnotations: the DataAnnotations
        // validator reflects over the options type (RequiresUnreferencedCode, not AOT-safe), and this
        // single check subsumes [Required] and [Url] for the one property anyway.
        services
            .AddOptions<SolanaRpcOptions>()
            .Configure(configure)
            .Validate(
                options => Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri)
                           && uri.Scheme is "http" or "https",
                "SolanaRpcOptions.Endpoint must be an absolute http(s) URL.")
            .ValidateOnStart();

        var builder = services.AddHttpClient<SolanaRpcClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<SolanaRpcOptions>>().Value;
            client.BaseAddress = new Uri(options.Endpoint);
        });

        var resilience = builder.AddStandardResilienceHandler();
        if (configureResilience is not null)
            resilience.Configure(configureResilience);

        return builder;
    }

    /// <summary>
    /// Registers <see cref="SolanaRpcClient"/> as a typed <see cref="HttpClient"/> pointed at <paramref name="endpoint"/>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="endpoint">The JSON-RPC HTTP endpoint URL.</param>
    /// <returns>The <see cref="IHttpClientBuilder"/>, so the caller can keep configuring the client.</returns>
    public static IHttpClientBuilder AddSolanaRpc(this IServiceCollection services, string endpoint)
        => services.AddSolanaRpc(options => options.Endpoint = endpoint);
}
