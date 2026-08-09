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
    private const string StrictModeVariable = "SOLSHARP_INTEGRATION_STRICT";

    /// <summary>The public mainnet JSON-RPC endpoint used when <c>SOLSHARP_RPC_URL</c> is not set.</summary>
    public const string DefaultHttpEndpoint = "https://api.mainnet-beta.solana.com";

    /// <summary>The public mainnet WebSocket endpoint used when <c>SOLSHARP_WS_URL</c> is not set.</summary>
    public const string DefaultWsEndpoint = "wss://api.mainnet-beta.solana.com";

    /// <summary>The public devnet JSON-RPC endpoint used when <c>SOLSHARP_DEVNET_RPC_URL</c> is not set.</summary>
    public const string DefaultDevnetHttpEndpoint = "https://api.devnet.solana.com";

    /// <summary>The canonical genesis hash of the Solana devnet cluster.</summary>
    public const string DevnetGenesisHash = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";

    /// <summary>The HTTP JSON-RPC endpoint the read tests talk to.</summary>
    public static string HttpEndpoint => Resolve("SOLSHARP_RPC_URL", DefaultHttpEndpoint);

    /// <summary>The WebSocket endpoint the tests talk to.</summary>
    public static string WsEndpoint => Resolve("SOLSHARP_WS_URL", DefaultWsEndpoint);

    /// <summary>
    /// The HTTP JSON-RPC endpoint the write tests talk to. Always a devnet endpoint: the write suite
    /// airdrops and submits transactions, which must never target mainnet.
    /// </summary>
    public static string DevnetHttpEndpoint => Resolve("SOLSHARP_DEVNET_RPC_URL", DefaultDevnetHttpEndpoint);

    private static bool IsStrict =>
        Environment.GetEnvironmentVariable(StrictModeVariable) is { } value
        && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    private static string Resolve(string variable, string fallback)
        => Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? value : fallback;

    /// <summary>
    /// Runs an RPC call, turning a transient failure (a rate limit, timeout, or node hiccup) into an
    /// inconclusive result rather than a failure - a busy public node should not turn the suite red. A
    /// non-transient exception (a parsing bug, say) is left to propagate and fail the test. Setting
    /// <c>SOLSHARP_INTEGRATION_STRICT</c> to <c>true</c> or <c>1</c> also propagates transient failures.
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
            if (IsStrict)
                ExceptionDispatchInfo.Capture(exception).Throw();

            Assert.Inconclusive($"Skipped: the RPC endpoint was unavailable or rate-limited ({Describe(exception)}).");
            throw; // unreachable: Assert.Inconclusive always throws.
        }
    }

    /// <summary>
    /// Whether <paramref name="exception"/> reflects a transient transport problem (a rate limit, timeout,
    /// broken connection or socket, rejected WebSocket handshake, resilience-pipeline rejection, or a
    /// specifically transient RPC error) as opposed to a real defect.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><c>true</c> when the failure should be treated as transient.</returns>
    public static bool IsTransient(Exception exception)
        => exception is HttpRequestException or TaskCanceledException or TimeoutException or OperationCanceledException
               or WebSocketException or SocketException
           || exception is RpcException { Code: -32603 or -32004 or -32005 or -32007 or -32009 or -32014 or -32016 }
           || exception.GetType().FullName?.StartsWith("Polly.", StringComparison.Ordinal) == true
           || (exception.InnerException is { } inner && IsTransient(inner));

    /// <summary>
    /// Rejects an endpoint whose genesis hash is not the canonical devnet hash. Write tests call this
    /// before generating a payer or requesting an airdrop, so an accidental mainnet/testnet override
    /// cannot submit a transaction.
    /// </summary>
    /// <param name="actualGenesisHash">The endpoint's <c>getGenesisHash</c> result.</param>
    /// <exception cref="InvalidOperationException">The endpoint is not the canonical devnet cluster.</exception>
    public static void ValidateDevnetGenesisHash(string actualGenesisHash)
    {
        if (!string.Equals(actualGenesisHash, DevnetGenesisHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The write-test endpoint is not Solana devnet (expected genesis {DevnetGenesisHash}, "
                + $"received {actualGenesisHash}). No write was attempted.");
        }
    }

    /// <summary>
    /// Rethrows <paramref name="exception"/> unless it is transient, in which case the test is marked
    /// inconclusive. Strict integration mode also rethrows transient exceptions.
    /// </summary>
    /// <param name="exception">The exception captured from a network operation.</param>
    public static void RethrowOrInconclusive(Exception exception)
    {
        if (IsTransient(exception))
        {
            if (IsStrict)
                ExceptionDispatchInfo.Capture(exception).Throw();

            Assert.Inconclusive($"Skipped: the endpoint was unavailable or rate-limited ({Describe(exception)}).");
        }
        else
            ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private static string Describe(Exception exception) => $"{exception.GetType().Name}: {exception.Message}";
}
