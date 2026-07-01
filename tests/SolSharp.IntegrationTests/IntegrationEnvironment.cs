using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using SolSharp.Rpc.Protocol;

namespace SolSharp.IntegrationTests;

/// <summary>
/// Shared configuration and flakiness handling for the live integration tests. The endpoints default to
/// the public Solana mainnet cluster and can be overridden with the <c>SOLSHARP_RPC_URL</c> and
/// <c>SOLSHARP_WS_URL</c> environment variables (for example to point at a private QuickNode / Helius node).
/// </summary>
internal static class IntegrationEnvironment
{
    /// <summary>The public mainnet JSON-RPC endpoint used when <c>SOLSHARP_RPC_URL</c> is not set.</summary>
    public const string DefaultHttpEndpoint = "https://api.mainnet-beta.solana.com";

    /// <summary>The public mainnet WebSocket endpoint used when <c>SOLSHARP_WS_URL</c> is not set.</summary>
    public const string DefaultWsEndpoint = "wss://api.mainnet-beta.solana.com";

    /// <summary>The HTTP JSON-RPC endpoint the tests talk to.</summary>
    public static string HttpEndpoint => Resolve("SOLSHARP_RPC_URL", DefaultHttpEndpoint);

    /// <summary>The WebSocket endpoint the tests talk to.</summary>
    public static string WsEndpoint => Resolve("SOLSHARP_WS_URL", DefaultWsEndpoint);

    private static string Resolve(string variable, string fallback)
        => Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? value : fallback;

    /// <summary>
    /// Runs an RPC call, turning a transient failure (a rate limit, timeout, or node hiccup) into an
    /// inconclusive result rather than a failure - a busy public node should not turn the suite red. A
    /// non-transient exception (a parsing bug, say) is left to propagate and fail the test.
    /// </summary>
    /// <typeparam name="T">The call's result type.</typeparam>
    /// <param name="call">The RPC call to run.</param>
    /// <returns>The call's result.</returns>
    public static async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (Exception exception) when (IsTransient(exception))
        {
            Assert.Inconclusive($"Skipped: the RPC endpoint was unavailable or rate-limited ({Describe(exception)}).");
            throw; // unreachable: Assert.Inconclusive always throws.
        }
    }

    /// <summary>
    /// Whether <paramref name="exception"/> reflects a transient transport problem (a rate limit, timeout,
    /// broken connection or socket, rejected WebSocket handshake, resilience-pipeline rejection, or an
    /// RPC-level error) as opposed to a real defect.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><c>true</c> when the failure should be treated as transient.</returns>
    public static bool IsTransient(Exception exception)
        => exception is HttpRequestException or TaskCanceledException or TimeoutException or OperationCanceledException or RpcException
               or WebSocketException or SocketException
           || exception.GetType().FullName?.StartsWith("Polly.", StringComparison.Ordinal) == true
           || (exception.InnerException is { } inner && IsTransient(inner));

    /// <summary>Rethrows <paramref name="exception"/> unless it is transient, in which case the test is marked inconclusive.</summary>
    /// <param name="exception">The exception captured from a network operation.</param>
    public static void RethrowOrInconclusive(Exception exception)
    {
        if (IsTransient(exception))
            Assert.Inconclusive($"Skipped: the endpoint was unavailable or rate-limited ({Describe(exception)}).");
        else
            ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private static string Describe(Exception exception) => $"{exception.GetType().Name}: {exception.Message}";
}
