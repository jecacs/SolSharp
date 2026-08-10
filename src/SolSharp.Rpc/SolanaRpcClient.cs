using System.Buffers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Models.Parsed;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc;

/// <summary>
/// Solana JSON-RPC client over HTTP for chain reads, transaction submission and simulation, confirmation,
/// and supported node operations. The supplied <see cref="HttpClient"/> must have its BaseAddress set to
/// the RPC endpoint. Node-level errors are surfaced as <see cref="RpcException"/>.
/// </summary>
public class SolanaRpcClient
{
    internal static readonly HttpRequestOptionsKey<bool> DisableRetriesKey = new("SolSharp.DisableRetries");

    private static readonly PublicKey SystemProgramOwner = PublicKey.Parse(SolanaProgramIds.SystemProgram);
    private static readonly PublicKey TokenProgramOwner = PublicKey.Parse(SolanaProgramIds.TokenProgram);
    private static readonly PublicKey Token2022ProgramOwner = PublicKey.Parse(SolanaProgramIds.Token2022Program);
    private static readonly PublicKey AddressLookupTableProgramOwner = PublicKey.Parse(SolanaProgramIds.AddressLookupTableProgram);

    private static readonly TimeSpan DefaultConfirmationTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ConfirmationPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1d);

    private readonly HttpClient _httpClient;
    private readonly int _maximumResponseContentLength;

    /// <summary>Creates a client with the default 128 MiB HTTP response-body limit.</summary>
    /// <param name="httpClient">The HTTP client whose base address points at a Solana JSON-RPC endpoint.</param>
    public SolanaRpcClient(HttpClient httpClient)
        : this(httpClient, SolanaRpcOptions.DefaultMaximumResponseContentLength)
    {
    }

    /// <summary>Creates a client with an explicit HTTP response-body limit.</summary>
    /// <param name="httpClient">The HTTP client whose base address points at a Solana JSON-RPC endpoint.</param>
    /// <param name="maximumResponseContentLength">Maximum decoded response body size, in bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumResponseContentLength"/> is not positive.</exception>
    public SolanaRpcClient(HttpClient httpClient, int maximumResponseContentLength)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (maximumResponseContentLength <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maximumResponseContentLength), maximumResponseContentLength, "The response-content limit must be positive.");

        _httpClient = httpClient;
        _maximumResponseContentLength = maximumResponseContentLength;
    }

    /// <summary>Creates a client from dependency-injection options.</summary>
    /// <param name="httpClient">The HTTP client whose base address points at a Solana JSON-RPC endpoint.</param>
    /// <param name="options">The configured RPC options.</param>
    [ActivatorUtilitiesConstructor]
    public SolanaRpcClient(HttpClient httpClient, IOptions<SolanaRpcOptions> options)
        : this(httpClient, (options ?? throw new ArgumentNullException(nameof(options))).Value.MaximumResponseContentLength)
    {
    }

    /// <summary>
    /// Returns the latest blockhash, used as the recent blockhash when building a transaction.
    /// See <see href="https://solana.com/docs/rpc/http/getlatestblockhash">getLatestBlockhash</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The blockhash and the last block height at which it stays valid.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<LatestBlockhash> GetLatestBlockhashAsync(
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<LatestBlockhash>>(
            RpcRequests.GetLatestBlockhash(commitment),
            cancellationToken);

        return RequireContextValue(result);
    }

    /// <summary>Returns the latest blockhash with explicit commitment and minimum-context-slot options.</summary>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The blockhash and the last block height at which it stays valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<LatestBlockhash> GetLatestBlockhashWithOptionsAsync(
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = await SendAsync<RpcContextValue<LatestBlockhash>>(
            RpcRequests.GetLatestBlockhash(options.Commitment, options.MinContextSlot), cancellationToken);

        return RequireContextValue(result);
    }

    /// <summary>
    /// Returns the lamport balance of the given account at the requested commitment.
    /// See <see href="https://solana.com/docs/rpc/http/getbalance">getBalance</see>.
    /// </summary>
    /// <param name="account">The account to query.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The balance in lamports.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ulong> GetBalanceAsync(
        PublicKey account,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<ulong>>(
            RpcRequests.GetBalance(account, commitment),
            cancellationToken);

        return result.Value;
    }

    /// <summary>Returns an account balance with explicit commitment and minimum-context-slot options.</summary>
    /// <param name="account">The account to query.</param>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The balance in lamports.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ulong> GetBalanceWithOptionsAsync(
        PublicKey account,
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = await SendAsync<RpcContextValue<ulong>>(
            RpcRequests.GetBalance(account, options.Commitment, options.MinContextSlot), cancellationToken);

        return result.Value;
    }

    /// <summary>
    /// Returns the slot that has reached the given commitment level.
    /// See <see href="https://solana.com/docs/rpc/http/getslot">getSlot</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The current slot.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<ulong> GetSlotAsync(
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendAsync<ulong>(RpcRequests.GetSlot(commitment), cancellationToken);

    /// <summary>Returns the current slot with explicit commitment and minimum-context-slot options.</summary>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The current slot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<ulong> GetSlotWithOptionsAsync(
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendAsync<ulong>(RpcRequests.GetSlot(options.Commitment, options.MinContextSlot), cancellationToken);
    }

    /// <summary>
    /// Returns whether the node reports itself healthy ("ok").
    /// See <see href="https://solana.com/docs/rpc/http/gethealth">getHealth</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns><c>true</c> if the node is healthy.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error (an unhealthy node responds with an error).</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<bool> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var status = await SendAsync<string>(RpcRequests.GetHealth(), cancellationToken);
        return status == "ok";
    }

    /// <summary>
    /// Returns the Solana version running on the node.
    /// See <see href="https://solana.com/docs/rpc/http/getversion">getVersion</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The node's version information.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<RpcVersion> GetVersionAsync(CancellationToken cancellationToken = default)
        => await SendAsync<RpcVersion>(RpcRequests.GetVersion(), cancellationToken);

    /// <summary>
    /// Returns the current block height.
    /// See <see href="https://solana.com/docs/rpc/http/getblockheight">getBlockHeight</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The current block height.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<ulong> GetBlockHeightAsync(
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendAsync<ulong>(RpcRequests.GetBlockHeight(commitment), cancellationToken);

    /// <summary>Returns the current block height with explicit commitment and minimum-context-slot options.</summary>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The current block height.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<ulong> GetBlockHeightWithOptionsAsync(
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendAsync<ulong>(
            RpcRequests.GetBlockHeight(options.Commitment, options.MinContextSlot), cancellationToken);
    }

    /// <summary>
    /// Returns the number of transactions the cluster has processed.
    /// See <see href="https://solana.com/docs/rpc/http/gettransactioncount">getTransactionCount</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The total transaction count.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<ulong> GetTransactionCountAsync(
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendAsync<ulong>(RpcRequests.GetTransactionCount(commitment), cancellationToken);

    /// <summary>Returns the transaction count with explicit commitment and minimum-context-slot options.</summary>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The total transaction count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<ulong> GetTransactionCountWithOptionsAsync(
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendAsync<ulong>(
            RpcRequests.GetTransactionCount(options.Commitment, options.MinContextSlot), cancellationToken);
    }

    /// <summary>
    /// Returns the token balance of an SPL token account.
    /// See <see href="https://solana.com/docs/rpc/http/gettokenaccountbalance">getTokenAccountBalance</see>.
    /// </summary>
    /// <param name="account">The token account to query.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The token amount held by the account.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<TokenAmount> GetTokenAccountBalanceAsync(
        PublicKey account,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<TokenAmount>>(
            RpcRequests.GetTokenAccountBalance(account, commitment), cancellationToken);

        return RequireContextValue(result);
    }

    /// <summary>
    /// Returns the total supply of an SPL token mint.
    /// See <see href="https://solana.com/docs/rpc/http/gettokensupply">getTokenSupply</see>.
    /// </summary>
    /// <param name="mint">The token mint to query.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The total token supply.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<TokenAmount> GetTokenSupplyAsync(
        PublicKey mint,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<TokenAmount>>(
            RpcRequests.GetTokenSupply(mint, commitment), cancellationToken);

        return RequireContextValue(result);
    }

    /// <summary>
    /// Returns the minimum lamport balance required to make an account of the given size rent-exempt.
    /// See <see href="https://solana.com/docs/rpc/http/getminimumbalanceforrentexemption">getMinimumBalanceForRentExemption</see>.
    /// </summary>
    /// <param name="dataLength">The account's data length in bytes.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The minimum balance in lamports.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<ulong> GetMinimumBalanceForRentExemptionAsync(
        long dataLength,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendAsync<ulong>(RpcRequests.GetMinimumBalanceForRentExemption(dataLength, commitment), cancellationToken);

    /// <summary>
    /// Submits a fully signed transaction to the cluster.
    /// See <see href="https://solana.com/docs/rpc/http/sendtransaction">sendTransaction</see>.
    /// </summary>
    /// <param name="transaction">The signed transaction's serialized wire bytes; base64-encoded for the request.</param>
    /// <param name="options">Send options (skip preflight, retries, commitment); client defaults are used when <c>null</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The transaction signature (base58).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node rejected the transaction.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<string> SendTransactionAsync(
        byte[] transaction,
        SendTransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        options ??= new SendTransactionOptions();

        var encoded = Convert.ToBase64String(transaction);
        return SendAsync<string>(
            RpcRequests.SendTransaction(encoded, options.SkipPreflight, options.PreflightCommitment, options.MaxRetries, options.MinContextSlot),
            cancellationToken);
    }

    /// <summary>
    /// Simulates a transaction without submitting it, returning its error, logs, resource usage, optional
    /// account states, inner instructions, balances, fee, loaded addresses, blockhash replacement, and return data.
    /// See <see href="https://solana.com/docs/rpc/http/simulatetransaction">simulateTransaction</see>.
    /// </summary>
    /// <param name="transaction">The transaction's serialized wire bytes; base64-encoded for the request.</param>
    /// <param name="options">
    /// Simulation options including signature verification, blockhash replacement, commitment, requested
    /// post-simulation accounts and their encoding, and parsed inner instructions; client defaults are used when <c>null</c>.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The simulation result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><see cref="SimulateTransactionOptions.SigVerify"/> and <see cref="SimulateTransactionOptions.ReplaceRecentBlockhash"/> are both enabled.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Post-simulation accounts were requested and <see cref="SimulateTransactionOptions.AccountsEncoding"/> is unsupported by Agave's simulation account path.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<SimulateTransactionResult> SimulateTransactionAsync(
        byte[] transaction,
        SimulateTransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        options ??= new SimulateTransactionOptions();
        if (options.SigVerify && options.ReplaceRecentBlockhash)
            throw new ArgumentException(
                "Signature verification cannot be combined with recent-blockhash replacement.", nameof(options));
        if (options.Accounts is not null && options.AccountsEncoding is not (
            RpcAccountEncoding.Base64 or RpcAccountEncoding.JsonParsed or RpcAccountEncoding.Base64Zstd))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.AccountsEncoding,
                "Simulation accounts support only base64, jsonParsed, and base64+zstd encoding.");
        }

        var encoded = Convert.ToBase64String(transaction);
        var result = await SendAsync<RpcContextValue<SimulateTransactionResult>>(
            RpcRequests.SimulateTransaction(
                encoded,
                options.SigVerify,
                options.ReplaceRecentBlockhash,
                options.Commitment,
                options.MinContextSlot,
                options.Accounts,
                options.AccountsEncoding,
                options.InnerInstructions),
            cancellationToken);

        return RequireContextValue(result);
    }

    /// <summary>
    /// Returns the account at the given address, or <c>null</c> if it does not exist. Account data is
    /// requested as base64 and exposed decoded on <see cref="AccountInfo.Data"/>.
    /// See <see href="https://solana.com/docs/rpc/http/getaccountinfo">getAccountInfo</see>.
    /// </summary>
    /// <param name="account">The account to query.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="dataSlice">Return only this slice of the account's data; the whole account when null.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The account, or <c>null</c> if nothing exists at <paramref name="account"/>.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<AccountInfo?> GetAccountInfoAsync(
        PublicKey account,
        Commitment commitment = Commitment.Confirmed,
        DataSlice? dataSlice = null,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<AccountInfo?>>(
            RpcRequests.GetAccountInfo(account, commitment, dataSlice), cancellationToken);

        return result.Value;
    }

    /// <summary>
    /// Returns an account with the exact upstream encoding, slicing, commitment, and minimum-context-slot
    /// configuration. The result preserves the node's legacy, encoded-tuple, or parsed account-data branch.
    /// </summary>
    /// <param name="account">The account to query.</param>
    /// <param name="options">The exact upstream account configuration.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The account, or <c>null</c> if it does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<RpcAccountInfo?> GetAccountInfoWithOptionsAsync(
        PublicKey account,
        RpcAccountInfoOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = await GetAccountInfoWithOptionsAndContextAsync(account, options, cancellationToken);
        return result.Value;
    }

    /// <summary>
    /// Returns an exact-encoding account response together with the slot context used by the node.
    /// </summary>
    /// <param name="account">The account to query.</param>
    /// <param name="options">The exact upstream account configuration.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The context-wrapped account; its value is <c>null</c> when the account does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<RpcContextValue<RpcAccountInfo?>> GetAccountInfoWithOptionsAndContextAsync(
        PublicKey account,
        RpcAccountInfoOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendAsync<RpcContextValue<RpcAccountInfo?>>(
            RpcRequests.GetAccountInfo(
                account,
                options.Commitment,
                options.DataSlice,
                options.MinContextSlot,
                options.Encoding),
            cancellationToken);
    }

    /// <summary>Returns an account together with the slot context used by the node.</summary>
    /// <param name="account">The account to query.</param>
    /// <param name="options">Commitment, data-slice, and minimum-context-slot options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The context-wrapped account; its value is <c>null</c> when the account does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<RpcContextValue<AccountInfo?>> GetAccountInfoWithContextAsync(
        PublicKey account,
        GetAccountInfoOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendAsync<RpcContextValue<AccountInfo?>>(
            RpcRequests.GetAccountInfo(
                account,
                options.Commitment,
                options.DataSlice,
                options.MinContextSlot),
            cancellationToken);
    }

    /// <summary>
    /// Returns the accounts at the given addresses, in the same order. Each entry is <c>null</c> when no
    /// account exists at the corresponding address. See
    /// <see href="https://solana.com/docs/rpc/http/getmultipleaccounts">getMultipleAccounts</see>.
    /// </summary>
    /// <param name="accounts">The accounts to query.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>One entry per requested address, in order; an entry is <c>null</c> when that account does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="accounts"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<AccountInfo?>> GetMultipleAccountsAsync(
        IReadOnlyList<PublicKey> accounts,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        var result = await SendAsync<RpcContextValue<AccountInfo?[]>>(
            RpcRequests.GetMultipleAccounts(accounts, commitment), cancellationToken);

        return RequireContextValue(result);
    }

    /// <summary>
    /// Returns multiple accounts with the exact upstream account configuration while preserving every account-data
    /// encoding branch and missing entries.
    /// </summary>
    /// <param name="accounts">The accounts to query.</param>
    /// <param name="options">The exact upstream account configuration.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>One account entry per requested address, preserving missing entries as <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="accounts"/> or <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<RpcAccountInfo?>> GetMultipleAccountsWithOptionsAsync(
        IReadOnlyList<PublicKey> accounts,
        RpcAccountInfoOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = await GetMultipleAccountsWithOptionsAndContextAsync(accounts, options, cancellationToken);
        return RequireContextValue(result);
    }

    /// <summary>
    /// Returns exact-encoding accounts together with the slot context used by the node.
    /// </summary>
    /// <param name="accounts">The accounts to query.</param>
    /// <param name="options">The exact upstream account configuration.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The context-wrapped account array, preserving missing entries as <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="accounts"/> or <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<RpcContextValue<RpcAccountInfo?[]>> GetMultipleAccountsWithOptionsAndContextAsync(
        IReadOnlyList<PublicKey> accounts,
        RpcAccountInfoOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(options);
        var result = await SendAsync<RpcContextValue<RpcAccountInfo?[]>>(
            RpcRequests.GetMultipleAccounts(
                accounts,
                options.Commitment,
                options.DataSlice,
                options.MinContextSlot,
                options.Encoding),
            cancellationToken);
        RequireContextValue(result);
        return result;
    }

    /// <summary>Returns multiple accounts together with the slot context used by the node.</summary>
    /// <param name="accounts">The accounts to query.</param>
    /// <param name="options">Commitment, data-slice, and minimum-context-slot options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The context-wrapped account array.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="accounts"/> or <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<RpcContextValue<AccountInfo?[]>> GetMultipleAccountsWithContextAsync(
        IReadOnlyList<PublicKey> accounts,
        GetAccountInfoOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(options);
        var result = await SendAsync<RpcContextValue<AccountInfo?[]>>(
            RpcRequests.GetMultipleAccounts(
                accounts,
                options.Commitment,
                options.DataSlice,
                options.MinContextSlot),
            cancellationToken);
        RequireContextValue(result);
        return result;
    }

    /// <summary>
    /// Returns the confirmed transaction signatures that involve <paramref name="address"/>, newest first.
    /// See <see href="https://solana.com/docs/rpc/http/getsignaturesforaddress">getSignaturesForAddress</see>.
    /// </summary>
    /// <param name="address">The account to list signatures for.</param>
    /// <param name="options">Paging and commitment options; node defaults are used when null.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The matching signatures, newest first; empty when none.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<SignatureInfo>> GetSignaturesForAddressAsync(
        PublicKey address,
        GetSignaturesForAddressOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new GetSignaturesForAddressOptions();
        var result = await SendAsync<SignatureInfo[]>(
            RpcRequests.GetSignaturesForAddress(address, options.Limit, options.Before, options.Until, options.Commitment, options.MinContextSlot),
            cancellationToken);

        return RequireNonNullEntries(result, "signature list");
    }

    /// <summary>
    /// Returns every account owned by <paramref name="programId"/>, optionally narrowed by filters. Account
    /// data is requested as base64 and exposed decoded on <see cref="AccountInfo.Data"/>. This can be a heavy
    /// call on busy programs; prefer adding filters. See
    /// <see href="https://solana.com/docs/rpc/http/getprogramaccounts">getProgramAccounts</see>.
    /// </summary>
    /// <param name="programId">The owning program to enumerate accounts for.</param>
    /// <param name="options">Filters and commitment; node defaults are used when null.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The owned accounts that match every filter; empty when none.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<ProgramAccount>> GetProgramAccountsAsync(
        PublicKey programId,
        GetProgramAccountsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new GetProgramAccountsOptions();
        var request = RpcRequests.GetProgramAccounts(
            programId,
            options.Commitment,
            options.Filters,
            options.DataSlice,
            options.MinContextSlot,
            options.WithContext,
            options.SortResults);

        if (options.WithContext is true)
        {
            var contextual = await SendAsync<RpcContextValue<ProgramAccount[]>>(request, cancellationToken);
            return RequireNonNullKeyedAccounts(RequireContextValue(contextual));
        }

        return RequireNonNullKeyedAccounts(await SendAsync<ProgramAccount[]>(request, cancellationToken));
    }

    /// <summary>
    /// Returns program-owned accounts with the exact upstream encoding, filters, slicing, context-shape, and
    /// sorting configuration. Each account preserves its legacy, encoded-tuple, or parsed data branch.
    /// </summary>
    /// <param name="programId">The owning program to enumerate accounts for.</param>
    /// <param name="options">The exact upstream program-account configuration.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The matching accounts; the context wrapper is unwrapped when requested.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<RpcProgramAccount>> GetProgramAccountsWithOptionsAsync(
        PublicKey programId,
        RpcProgramAccountsOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var request = RpcRequests.GetProgramAccounts(
            programId,
            options.Commitment,
            options.Filters,
            options.DataSlice,
            options.MinContextSlot,
            options.WithContext,
            options.SortResults,
            options.Encoding);

        if (options.WithContext is true)
        {
            var contextual = await GetProgramAccountsWithOptionsAndContextAsync(
                programId, options, cancellationToken);
            return RequireContextValue(contextual);
        }

        return RequireNonNullKeyedAccounts(
            await SendAsync<RpcProgramAccount[]>(request, cancellationToken));
    }

    /// <summary>
    /// Returns exact-encoding program accounts in the upstream <c>{ context, value }</c> response shape.
    /// </summary>
    /// <param name="programId">The owning program to enumerate accounts for.</param>
    /// <param name="options">The exact upstream program-account configuration; context wrapping is forced.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The matching accounts together with the slot context used by the node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<RpcContextValue<RpcProgramAccount[]>> GetProgramAccountsWithOptionsAndContextAsync(
        PublicKey programId,
        RpcProgramAccountsOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = await SendAsync<RpcContextValue<RpcProgramAccount[]>>(
            RpcRequests.GetProgramAccounts(
                programId,
                options.Commitment,
                options.Filters,
                options.DataSlice,
                options.MinContextSlot,
                withContext: true,
                options.SortResults,
                options.Encoding),
            cancellationToken);
        RequireNonNullKeyedAccounts(result.Value);
        return result;
    }

    /// <summary>
    /// Returns every account owned by a program in the upstream <c>{ context, value }</c> shape.
    /// </summary>
    /// <param name="programId">The owning program to enumerate accounts for.</param>
    /// <param name="options">Filters, account configuration, and sorting options; node defaults are used when <c>null</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The matching accounts together with the slot context used by the node.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<RpcContextValue<ProgramAccount[]>> GetProgramAccountsWithContextAsync(
        PublicKey programId,
        GetProgramAccountsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new GetProgramAccountsOptions();
        var result = await SendAsync<RpcContextValue<ProgramAccount[]>>(
            RpcRequests.GetProgramAccounts(
                programId,
                options.Commitment,
                options.Filters,
                options.DataSlice,
                options.MinContextSlot,
                withContext: true,
                options.SortResults),
            cancellationToken);
        RequireNonNullKeyedAccounts(result.Value);
        return result;
    }

    /// <summary>
    /// Fetches and decodes an on-chain Address Lookup Table account. Returns <c>null</c> if nothing exists
    /// at <paramref name="tableAddress"/> or the account is not an initialized lookup table. The returned
    /// <see cref="AddressLookupTable.Addresses"/> excludes addresses appended in the response's context slot.
    /// See <see href="https://solana.com/docs/rpc/http/getaccountinfo">getAccountInfo</see>.
    /// </summary>
    /// <param name="tableAddress">The lookup table account's address.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The decoded table, or <c>null</c> if it does not exist or is not a lookup table.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<AddressLookupTable?> GetAddressLookupTableAsync(
        PublicKey tableAddress,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAccountInfoWithContextAsync(
            tableAddress,
            new GetAccountInfoOptions { Commitment = commitment },
            cancellationToken);
        var account = response.Value;
        if (account is not { Executable: false } || account.Owner != AddressLookupTableProgramOwner)
            return null;

        return response.Context is { } context
            ? AddressLookupTable.Decode(account.Data, context.Slot)
            : AddressLookupTable.Decode(account.Data);
    }

    /// <summary>
    /// Returns information about the cluster's current epoch and slot.
    /// See <see href="https://solana.com/docs/rpc/http/getepochinfo">getEpochInfo</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The current epoch information.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<EpochInfo> GetEpochInfoAsync(
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => await SendAsync<EpochInfo>(RpcRequests.GetEpochInfo(commitment), cancellationToken);

    /// <summary>Returns epoch information with explicit commitment and minimum-context-slot options.</summary>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The current epoch information.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<EpochInfo> GetEpochInfoWithOptionsAsync(
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendAsync<EpochInfo>(
            RpcRequests.GetEpochInfo(options.Commitment, options.MinContextSlot), cancellationToken);
    }

    /// <summary>
    /// Returns whether a blockhash is still valid for use as a transaction's recent blockhash.
    /// See <see href="https://solana.com/docs/rpc/http/isblockhashvalid">isBlockhashValid</see>.
    /// </summary>
    /// <param name="blockhash">The blockhash (base58) to check.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns><c>true</c> if the blockhash is still valid.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<bool> IsBlockhashValidAsync(
        string blockhash,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<bool>>(RpcRequests.IsBlockhashValid(blockhash, commitment), cancellationToken);
        return result.Value;
    }

    /// <summary>Checks blockhash validity with explicit commitment and minimum-context-slot options.</summary>
    /// <param name="blockhash">The blockhash (base58) to check.</param>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns><c>true</c> if the blockhash is still valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<bool> IsBlockhashValidWithOptionsAsync(
        string blockhash,
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = await SendAsync<RpcContextValue<bool>>(
            RpcRequests.IsBlockhashValid(blockhash, options.Commitment, options.MinContextSlot), cancellationToken);

        return result.Value;
    }

    /// <summary>
    /// Returns the fee the cluster would charge to process a message, or <c>null</c> if its blockhash has expired.
    /// See <see href="https://solana.com/docs/rpc/http/getfeeformessage">getFeeForMessage</see>.
    /// </summary>
    /// <param name="message">The message's serialized wire bytes; base64-encoded for the request.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The fee in lamports, or <c>null</c> if the message's blockhash is no longer found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ulong?> GetFeeForMessageAsync(
        byte[] message,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var encoded = Convert.ToBase64String(message);
        var result = await SendAsync<RpcContextValue<ulong?>>(RpcRequests.GetFeeForMessage(encoded, commitment), cancellationToken);
        return result.Value;
    }

    /// <summary>Returns a message fee with explicit commitment and minimum-context-slot options.</summary>
    /// <param name="message">The message's serialized wire bytes.</param>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The fee in lamports, or <c>null</c> if the recent blockhash expired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> or <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ulong?> GetFeeForMessageWithOptionsAsync(
        byte[] message,
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);

        var encoded = Convert.ToBase64String(message);
        var result = await SendAsync<RpcContextValue<ulong?>>(
            RpcRequests.GetFeeForMessage(encoded, options.Commitment, options.MinContextSlot), cancellationToken);

        return result.Value;
    }

    /// <summary>
    /// Requests an airdrop of lamports to an account (test clusters only).
    /// See <see href="https://solana.com/docs/rpc/http/requestairdrop">requestAirdrop</see>.
    /// </summary>
    /// <param name="account">The account to fund.</param>
    /// <param name="lamports">The amount to airdrop, in lamports.</param>
    /// <param name="commitment">The commitment level to confirm the airdrop at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The airdrop transaction signature (base58).</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error (for example, on mainnet where airdrops are unavailable).</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<string> RequestAirdropAsync(
        PublicKey account,
        ulong lamports,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendAsync<string>(RpcRequests.RequestAirdrop(account, lamports, commitment), cancellationToken);

    /// <summary>Requests an airdrop with an optional caller-selected recent blockhash.</summary>
    /// <param name="account">The account to fund.</param>
    /// <param name="lamports">The amount to airdrop, in lamports.</param>
    /// <param name="options">Recent-blockhash and commitment options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The airdrop transaction signature (base58).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<string> RequestAirdropWithOptionsAsync(
        PublicKey account,
        ulong lamports,
        RequestAirdropOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendAsync<string>(
            RpcRequests.RequestAirdrop(account, lamports, options.Commitment, options.RecentBlockhash), cancellationToken);
    }

    /// <summary>
    /// Returns the SPL token accounts owned by <paramref name="owner"/> for a specific <paramref name="mint"/>.
    /// Account data is requested as base64 and exposed decoded on <see cref="AccountInfo.Data"/>.
    /// See <see href="https://solana.com/docs/rpc/http/gettokenaccountsbyowner">getTokenAccountsByOwner</see>.
    /// </summary>
    /// <param name="owner">The account that owns the token accounts.</param>
    /// <param name="mint">The token mint to filter by.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The matching token accounts (usually zero or one); empty when none.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<ProgramAccount>> GetTokenAccountsByOwnerAsync(
        PublicKey owner,
        PublicKey mint,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<ProgramAccount[]>>(
            RpcRequests.GetTokenAccountsByOwner(
                owner,
                TokenAccountsFilter.ByMint(mint),
                commitment),
            cancellationToken);

        return RequireNonNullKeyedAccounts(RequireContextValue(result));
    }

    /// <summary>
    /// Returns token accounts owned by an address, filtered by either mint or SPL Token program.
    /// </summary>
    /// <param name="owner">The account that owns the token accounts.</param>
    /// <param name="filter">The mutually exclusive mint or token-program filter.</param>
    /// <param name="options">Base64 slicing, commitment, and minimum-context-slot options; node defaults when <c>null</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The matching token accounts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<ProgramAccount>> GetTokenAccountsByOwnerWithFilterAsync(
        PublicKey owner,
        TokenAccountsFilter filter,
        GetAccountInfoOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await GetTokenAccountsByOwnerWithContextAsync(owner, filter, options, cancellationToken);
        return RequireNonNullKeyedAccounts(RequireContextValue(result));
    }

    /// <summary>
    /// Returns filtered token accounts with the exact upstream account encoding and read configuration.
    /// </summary>
    /// <param name="owner">The account that owns the token accounts.</param>
    /// <param name="filter">The mutually exclusive mint or token-program filter.</param>
    /// <param name="options">The exact upstream account configuration.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The matching token accounts with exact account-data branches.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> or <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<RpcProgramAccount>> GetTokenAccountsByOwnerWithOptionsAsync(
        PublicKey owner,
        TokenAccountsFilter filter,
        RpcAccountInfoOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = await GetTokenAccountsByOwnerWithOptionsAndContextAsync(
            owner, filter, options, cancellationToken);

        return RequireContextValue(result);
    }

    /// <summary>
    /// Returns exact-encoding filtered token accounts together with the slot context used by the node.
    /// </summary>
    /// <param name="owner">The account that owns the token accounts.</param>
    /// <param name="filter">The mutually exclusive mint or token-program filter.</param>
    /// <param name="options">The exact upstream account configuration.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The context-wrapped matching token accounts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> or <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<RpcContextValue<RpcProgramAccount[]>> GetTokenAccountsByOwnerWithOptionsAndContextAsync(
        PublicKey owner,
        TokenAccountsFilter filter,
        RpcAccountInfoOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(options);
        var result = await SendAsync<RpcContextValue<RpcProgramAccount[]>>(
            RpcRequests.GetTokenAccountsByOwner(
                owner,
                filter,
                options.Commitment,
                options.DataSlice,
                options.MinContextSlot,
                options.Encoding),
            cancellationToken);
        RequireNonNullKeyedAccounts(result.Value);
        return result;
    }

    /// <summary>Returns filtered token accounts owned by an address together with their slot context.</summary>
    /// <param name="owner">The account that owns the token accounts.</param>
    /// <param name="filter">The mutually exclusive mint or token-program filter.</param>
    /// <param name="options">Base64 slicing, commitment, and minimum-context-slot options; node defaults when <c>null</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The context-wrapped matching token accounts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<RpcContextValue<ProgramAccount[]>> GetTokenAccountsByOwnerWithContextAsync(
        PublicKey owner,
        TokenAccountsFilter filter,
        GetAccountInfoOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        options ??= new GetAccountInfoOptions();
        var result = await SendAsync<RpcContextValue<ProgramAccount[]>>(
            RpcRequests.GetTokenAccountsByOwner(
                owner,
                filter,
                options.Commitment,
                options.DataSlice,
                options.MinContextSlot),
            cancellationToken);
        RequireNonNullKeyedAccounts(result.Value);
        return result;
    }

    /// <summary>
    /// Returns a sample of recent per-slot prioritization fees, useful for choosing a compute-unit price.
    /// See <see href="https://solana.com/docs/rpc/http/getrecentprioritizationfees">getRecentPrioritizationFees</see>.
    /// </summary>
    /// <param name="accounts">Optional accounts to scope the sample to (the writable accounts a transaction will lock); cluster-wide when null.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The recent prioritization fees, one entry per sampled slot.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<PrioritizationFee>> GetRecentPrioritizationFeesAsync(
        IReadOnlyList<PublicKey>? accounts = null,
        CancellationToken cancellationToken = default)
        => RequireNonNullEntries(
            await SendAsync<PrioritizationFee[]>(RpcRequests.GetRecentPrioritizationFees(accounts ?? []), cancellationToken),
            "prioritization-fee list");

    /// <summary>
    /// Returns a confirmed transaction by signature, or <c>null</c> if the cluster has not seen it. Supports
    /// legacy and versioned-v0 transactions. Use <see cref="GetTransactionWithMaxVersionAsync"/> to opt into
    /// a newer numeric version. See
    /// <see href="https://solana.com/docs/rpc/http/gettransaction">getTransaction</see>.
    /// </summary>
    /// <param name="signature">The transaction signature (base58).</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The transaction and its execution metadata, or <c>null</c> if it was not found.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<TransactionResponse?> GetTransactionAsync(
        string signature,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendNullableAsync<TransactionResponse?>(RpcRequests.GetTransaction(signature, commitment, 0), cancellationToken);

    /// <summary>
    /// Returns a confirmed transaction while explicitly opting into a newer transaction version. The
    /// transaction is returned as wire bytes on <see cref="TransactionResponse.Transaction"/>; callers must
    /// only advertise versions whose wire format they can handle.
    /// </summary>
    /// <param name="signature">The transaction signature (base58).</param>
    /// <param name="maxSupportedTransactionVersion">
    /// The highest numeric transaction version the caller accepts. This byte is sent unchanged; the node
    /// decides whether the requested transaction is available at that version.
    /// </param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The transaction and its execution metadata, or <c>null</c> if it was not found.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<TransactionResponse?> GetTransactionWithMaxVersionAsync(
        string signature,
        byte maxSupportedTransactionVersion,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendNullableAsync<TransactionResponse?>(
            RpcRequests.GetTransaction(signature, commitment, maxSupportedTransactionVersion), cancellationToken);

    /// <summary>
    /// Returns a transaction using the exact upstream encoding, commitment, and transaction-version
    /// configuration. Because the encoding changes the transaction field's JSON schema, the result is exposed
    /// without lossy projection as a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="signature">The transaction signature (base58).</param>
    /// <param name="options">The exact upstream transaction configuration.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The configured transaction JSON, or <c>null</c> if the transaction was not found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<JsonElement?> GetTransactionWithOptionsAsync(
        string signature,
        GetTransactionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendNullableAsync<JsonElement?>(
            RpcRequests.GetTransaction(
                signature,
                options.Commitment,
                options.MaxSupportedTransactionVersion,
                options.Encoding),
            cancellationToken);
    }

    /// <summary>
    /// Returns the processing status of each signature, in order; an entry is <c>null</c> if the cluster has
    /// no record of that signature. See
    /// <see href="https://solana.com/docs/rpc/http/getsignaturestatuses">getSignatureStatuses</see>.
    /// </summary>
    /// <param name="signatures">The transaction signatures (base58) to look up.</param>
    /// <param name="searchTransactionHistory">When <c>true</c>, also searches the node's long-term transaction history.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>One status per requested signature, in order; an entry is <c>null</c> when the signature is unknown.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signatures"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<SignatureStatus?>> GetSignatureStatusesAsync(
        IReadOnlyList<string> signatures,
        bool searchTransactionHistory = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signatures);

        var result = await SendAsync<RpcContextValue<SignatureStatus?[]>>(
            RpcRequests.GetSignatureStatuses(signatures, searchTransactionHistory), cancellationToken);

        return RequireContextValue(result);
    }

    /// <summary>
    /// Polls <c>getSignatureStatuses</c> until the transaction reaches <paramref name="commitment"/> (or higher),
    /// or the timeout elapses. A confirmed-but-failed transaction is returned, not thrown - inspect
    /// <see cref="SignatureStatus.IsError"/>.
    /// </summary>
    /// <param name="signature">The transaction signature (base58) to confirm.</param>
    /// <param name="commitment">The commitment level to wait for.</param>
    /// <param name="timeout">How long to wait before giving up; defaults to 60 seconds.</param>
    /// <param name="cancellationToken">A token to cancel the wait.</param>
    /// <returns>The signature's status once it reaches <paramref name="commitment"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signature"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative and not infinite.</exception>
    /// <exception cref="TimeoutException">The transaction did not reach <paramref name="commitment"/> in time.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<SignatureStatus> ConfirmTransactionAsync(
        string signature,
        Commitment commitment = Commitment.Confirmed,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signature);

        var confirmationTimeout = timeout ?? DefaultConfirmationTimeout;
        if (confirmationTimeout < TimeSpan.Zero && confirmationTimeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "The confirmation timeout must be non-negative or infinite.");

        using var timeoutCts = new CancellationTokenSource();
        var timeoutTask = confirmationTimeout == Timeout.InfiniteTimeSpan
            ? Task.CompletedTask
            : CancelAfterAsync(timeoutCts, confirmationTimeout);

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var target = CommitmentRank(commitment);

            try
            {
                while (true)
                {
                    var statuses = await GetSignatureStatusesAsync(
                        [signature], searchTransactionHistory: false, linkedCts.Token);
                    var status = statuses.Count > 0 ? statuses[0] : null;
                    if (status is not null && StatusRank(status) >= target)
                        return status;

                    await Task.Delay(ConfirmationPollInterval, linkedCts.Token);
                }
            }
            catch (OperationCanceledException exception)
                when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Transaction {signature} was not confirmed at {commitment} within the timeout.", exception);
            }
        }
        finally
        {
            await timeoutCts.CancelAsync();
            await timeoutTask;
        }
    }

    /// <summary>
    /// Submits a signed transaction and waits until it reaches <paramref name="commitment"/>. Returns the
    /// signature on success; throws <see cref="TransactionFailedException"/> if the transaction lands but fails
    /// on-chain, so a returned signature always means success.
    /// </summary>
    /// <param name="transaction">The signed transaction's serialized wire bytes.</param>
    /// <param name="options">Send options; client defaults are used when <c>null</c>.</param>
    /// <param name="commitment">The commitment level to wait for.</param>
    /// <param name="timeout">How long to wait for confirmation before giving up; defaults to 60 seconds.</param>
    /// <param name="cancellationToken">A token to cancel the send or wait.</param>
    /// <returns>The confirmed transaction's signature (base58).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is <c>null</c>.</exception>
    /// <exception cref="TransactionFailedException">The transaction was confirmed but failed on-chain.</exception>
    /// <exception cref="TimeoutException">The transaction was not confirmed in time.</exception>
    /// <exception cref="RpcException">The node rejected the transaction or returned a JSON-RPC error.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<string> SendAndConfirmTransactionAsync(
        byte[] transaction,
        SendTransactionOptions? options = null,
        Commitment commitment = Commitment.Confirmed,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var signature = await SendTransactionAsync(transaction, options, cancellationToken);
        var status = await ConfirmTransactionAsync(signature, commitment, timeout, cancellationToken);

        if (status.IsError)
            throw new TransactionFailedException(signature, status.Err);

        return signature;
    }

    /// <summary>
    /// Fetches and decodes a classic Token or Token-2022 mint account, or returns <c>null</c> if nothing
    /// exists at <paramref name="mint"/> or its owner and data layout do not identify a mint.
    /// </summary>
    /// <param name="mint">The mint account's address.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The decoded mint, or <c>null</c> if it does not exist or is not a mint account.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<Mint?> GetMintAsync(
        PublicKey mint,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var account = await GetAccountInfoAsync(mint, commitment, cancellationToken: cancellationToken);
        if (account is not { Executable: false })
            return null;

        if (account.Owner == TokenProgramOwner)
            return account.Data.Length == Mint.Length ? Mint.Decode(account.Data) : null;

        return account.Owner == Token2022ProgramOwner ? Mint.Decode(account.Data) : null;
    }

    /// <summary>
    /// Fetches and decodes a durable nonce account, or returns <c>null</c> if nothing exists at
    /// <paramref name="nonceAccount"/> or the account is not an initialized nonce. The result's
    /// <see cref="NonceAccount.Nonce"/> is the recent-blockhash value for a nonce-anchored transaction.
    /// </summary>
    /// <param name="nonceAccount">The nonce account's address.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The decoded nonce account, or <c>null</c> if it does not exist or is not an initialized nonce.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<NonceAccount?> GetNonceAccountAsync(
        PublicKey nonceAccount,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var account = await GetAccountInfoAsync(nonceAccount, commitment, cancellationToken: cancellationToken);
        return account is { Executable: false } && account.Owner == SystemProgramOwner
            ? NonceAccount.Decode(account.Data)
            : null;
    }

    /// <summary>
    /// Fetches and decodes a classic Token or Token-2022 holding account, or returns <c>null</c> if nothing
    /// exists at <paramref name="tokenAccount"/> or its owner and data layout do not identify one.
    /// </summary>
    /// <param name="tokenAccount">The token account's address.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The decoded token account, or <c>null</c> if it does not exist or is not a token account.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<TokenAccount?> GetTokenAccountAsync(
        PublicKey tokenAccount,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var account = await GetAccountInfoAsync(tokenAccount, commitment, cancellationToken: cancellationToken);
        if (account is not { Executable: false })
            return null;

        if (account.Owner == TokenProgramOwner)
            return account.Data.Length == TokenAccount.Length ? TokenAccount.Decode(account.Data) : null;

        return account.Owner == Token2022ProgramOwner ? TokenAccount.Decode(account.Data) : null;
    }

    /// <summary>
    /// Returns the slot leaders for <paramref name="limit"/> slots starting at <paramref name="startSlot"/>.
    /// See <see href="https://solana.com/docs/rpc/http/getslotleaders">getSlotLeaders</see>.
    /// </summary>
    /// <param name="startSlot">The first slot to return the leader for.</param>
    /// <param name="limit">The number of consecutive slots to return leaders for (max 5000).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The leader identity for each slot, in order.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<PublicKey>> GetSlotLeadersAsync(
        ulong startSlot,
        ulong limit,
        CancellationToken cancellationToken = default)
        => await SendAsync<PublicKey[]>(RpcRequests.GetSlotLeaders(startSlot, limit), cancellationToken);

    /// <summary>
    /// Returns the Alpenglow genesis block certificate, or <c>null</c> while the cluster is still using
    /// Tower BFT consensus.
    /// See <see href="https://solana.com/docs/rpc/http/getaggenesiscert">getAgGenesisCert</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The Alpenglow genesis certificate, or <c>null</c> when Alpenglow is not active.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<AgGenesisCertificate?> GetAgGenesisCertificateAsync(
        CancellationToken cancellationToken = default)
        => SendNullableAsync<AgGenesisCertificate?>(RpcRequests.GetAgGenesisCert(), cancellationToken);

    /// <summary>
    /// Returns the cluster's total, circulating, and non-circulating token supply.
    /// See <see href="https://solana.com/docs/rpc/http/getsupply">getSupply</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The supply totals, in lamports.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<Supply> GetSupplyAsync(
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<Supply>>(
            RpcRequests.GetSupply(commitment, excludeNonCirculatingAccountsList: true), cancellationToken);
        return RequireContextValue(result);
    }

    /// <summary>Returns cluster supply with explicit control over the non-circulating account list.</summary>
    /// <param name="options">Commitment and account-list exclusion options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The supply totals and, unless excluded, non-circulating account addresses.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<Supply> GetSupplyWithOptionsAsync(
        GetSupplyOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = await SendAsync<RpcContextValue<Supply>>(
            RpcRequests.GetSupply(options.Commitment, options.ExcludeNonCirculatingAccountsList), cancellationToken);

        return RequireContextValue(result);
    }

    /// <summary>
    /// Returns the 20 largest accounts holding a given token mint, by balance.
    /// See <see href="https://solana.com/docs/rpc/http/gettokenlargestaccounts">getTokenLargestAccounts</see>.
    /// </summary>
    /// <param name="mint">The token mint to query.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The largest token accounts, largest first.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<TokenLargestAccount>> GetTokenLargestAccountsAsync(
        PublicKey mint,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<TokenLargestAccount[]>>(
            RpcRequests.GetTokenLargestAccounts(mint, commitment), cancellationToken);

        return RequireNonNullEntries(RequireContextValue(result), "token-largest-account list");
    }

    /// <summary>
    /// Returns a confirmed block by slot (with transaction signatures only), or <c>null</c> if the slot was
    /// skipped. See <see href="https://solana.com/docs/rpc/http/getblock">getBlock</see>.
    /// </summary>
    /// <param name="slot">The slot to fetch the block for.</param>
    /// <param name="commitment">The commitment level to query at (<c>processed</c> is not supported by the node).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The block, or <c>null</c> if the slot was skipped and produced no block.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<Block?> GetBlockAsync(
        ulong slot,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendNullableAsync<Block?>(RpcRequests.GetBlock(slot, commitment, 0), cancellationToken);

    /// <summary>
    /// Returns a confirmed signatures-only block while explicitly opting into a newer transaction version.
    /// This is required when the block contains a version newer than v0, even though only signatures are
    /// requested. See <see href="https://solana.com/docs/rpc/http/getblock">getBlock</see>.
    /// </summary>
    /// <param name="slot">The slot to fetch the block for.</param>
    /// <param name="maxSupportedTransactionVersion">The highest numeric transaction version the caller accepts.</param>
    /// <param name="commitment">The commitment level to query at (<c>processed</c> is not supported by the node).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The block, or <c>null</c> if the slot was skipped and produced no block.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<Block?> GetBlockWithMaxVersionAsync(
        ulong slot,
        byte maxSupportedTransactionVersion,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendNullableAsync<Block?>(
            RpcRequests.GetBlock(slot, commitment, maxSupportedTransactionVersion), cancellationToken);

    /// <summary>
    /// Returns a block using the exact upstream encoding, transaction-details, rewards, commitment, and
    /// transaction-version configuration. Because those choices change the JSON schema, the result is exposed
    /// without lossy projection as a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="slot">The slot to fetch the block for.</param>
    /// <param name="options">The exact upstream block configuration.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The configured block JSON, or <c>null</c> if the slot was skipped.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<JsonElement?> GetBlockWithOptionsAsync(
        ulong slot,
        GetBlockOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendNullableAsync<JsonElement?>(
            RpcRequests.GetBlock(
                slot,
                options.Commitment,
                options.MaxSupportedTransactionVersion,
                options.Encoding,
                options.TransactionDetails,
                options.Rewards),
            cancellationToken);
    }

    /// <summary>
    /// Returns a confirmed transaction decoded by the node into <c>jsonParsed</c> form - recognized
    /// instructions, token balances and logs without local Borsh decoding - or <c>null</c> if not found.
    /// See <see href="https://solana.com/docs/rpc/http/gettransaction">getTransaction</see>.
    /// </summary>
    /// <param name="signature">The transaction signature (base58) to fetch.</param>
    /// <param name="commitment">The commitment level to query at; defaults to <see cref="Commitment.Confirmed"/> when <c>null</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The parsed transaction, or <c>null</c> if no transaction with that signature was found.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ParsedTransaction?> GetParsedTransactionAsync(
        string signature,
        Commitment? commitment = null,
        CancellationToken cancellationToken = default)
    {
        var transaction = await SendNullableAsync<ParsedTransaction?>(
            RpcRequests.GetParsedTransaction(signature, commitment ?? Commitment.Confirmed, 0), cancellationToken);
        return RequireConfirmedParsedTransaction(transaction);
    }

    /// <summary>
    /// Returns a node-decoded transaction while explicitly opting into a newer numeric transaction version.
    /// For V1, <see cref="ParsedMessage.TransactionConfig"/> exposes the message's execution configuration.
    /// </summary>
    /// <param name="signature">The transaction signature (base58) to fetch.</param>
    /// <param name="maxSupportedTransactionVersion">The highest numeric transaction version the caller accepts.</param>
    /// <param name="commitment">The commitment level to query at; defaults to <see cref="Commitment.Confirmed"/> when <c>null</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The parsed transaction, or <c>null</c> if no transaction with that signature was found.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ParsedTransaction?> GetParsedTransactionWithMaxVersionAsync(
        string signature,
        byte maxSupportedTransactionVersion,
        Commitment? commitment = null,
        CancellationToken cancellationToken = default)
    {
        var transaction = await SendNullableAsync<ParsedTransaction?>(
            RpcRequests.GetParsedTransaction(
                signature,
                commitment ?? Commitment.Confirmed,
                maxSupportedTransactionVersion),
            cancellationToken);
        return RequireConfirmedParsedTransaction(transaction);
    }

    /// <summary>
    /// Returns a confirmed block whose transactions are decoded by the node into <c>jsonParsed</c> form, or
    /// <c>null</c> if the slot was skipped. Each transaction's <see cref="ParsedTransaction.Slot"/> and
    /// <see cref="ParsedTransaction.BlockTime"/> are filled in from the block.
    /// See <see href="https://solana.com/docs/rpc/http/getblock">getBlock</see>.
    /// </summary>
    /// <param name="slot">The slot to fetch the block for.</param>
    /// <param name="commitment">The commitment level to query at; defaults to <see cref="Commitment.Confirmed"/> when <c>null</c> (<c>processed</c> is not supported by the node).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The block with parsed transactions, or <c>null</c> if the slot was skipped and produced no block.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ParsedBlock?> GetParsedBlockAsync(
        ulong slot,
        Commitment? commitment = null,
        CancellationToken cancellationToken = default)
        => await GetParsedBlockCoreAsync(slot, 0, commitment, cancellationToken);

    /// <summary>
    /// Returns a node-decoded block while explicitly opting into a newer numeric transaction version.
    /// For V1 messages, <see cref="ParsedMessage.TransactionConfig"/> exposes the embedded execution settings.
    /// </summary>
    /// <param name="slot">The slot to fetch the block for.</param>
    /// <param name="maxSupportedTransactionVersion">The highest numeric transaction version the caller accepts.</param>
    /// <param name="commitment">The commitment level to query at; defaults to <see cref="Commitment.Confirmed"/> when <c>null</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The block with parsed transactions, or <c>null</c> if the slot was skipped and produced no block.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ParsedBlock?> GetParsedBlockWithMaxVersionAsync(
        ulong slot,
        byte maxSupportedTransactionVersion,
        Commitment? commitment = null,
        CancellationToken cancellationToken = default)
        => await GetParsedBlockCoreAsync(slot, maxSupportedTransactionVersion, commitment, cancellationToken);

    /// <summary>
    /// Returns the cluster's vote accounts, split into current and delinquent.
    /// See <see href="https://solana.com/docs/rpc/http/getvoteaccounts">getVoteAccounts</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The current and delinquent vote accounts.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<VoteAccounts> GetVoteAccountsAsync(
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendAsync<VoteAccounts>(RpcRequests.GetVoteAccounts(commitment), cancellationToken);

    /// <summary>Returns vote accounts using the complete upstream vote-account configuration.</summary>
    /// <param name="options">Vote address, commitment, and delinquency options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The matching current and delinquent vote accounts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<VoteAccounts> GetVoteAccountsWithOptionsAsync(
        GetVoteAccountsOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendAsync<VoteAccounts>(
            RpcRequests.GetVoteAccounts(
                options.Commitment,
                options.VotePublicKey,
                options.KeepUnstakedDelinquents,
                options.DelinquentSlotDistance),
            cancellationToken);
    }

    /// <summary>
    /// Returns the inflation / staking reward paid to each of <paramref name="addresses"/> for an epoch.
    /// See <see href="https://solana.com/docs/rpc/http/getinflationreward">getInflationReward</see>.
    /// </summary>
    /// <param name="addresses">The addresses to look up rewards for.</param>
    /// <param name="epoch">The epoch to query, or <c>null</c> for the previous epoch.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The reward for each address in order; an entry is <c>null</c> when that address earned no reward.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="addresses"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<IReadOnlyList<InflationReward?>> GetInflationRewardAsync(
        IReadOnlyList<PublicKey> addresses,
        ulong? epoch = null,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return SendAsync<IReadOnlyList<InflationReward?>>(
            RpcRequests.GetInflationReward(addresses, epoch, commitment), cancellationToken);
    }

    /// <summary>Returns inflation rewards with explicit epoch and minimum-context-slot options.</summary>
    /// <param name="addresses">The addresses to look up rewards for.</param>
    /// <param name="options">Epoch, commitment, and minimum-context-slot options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The reward for each address in order; missing rewards are <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="addresses"/> or <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<IReadOnlyList<InflationReward?>> GetInflationRewardWithOptionsAsync(
        IReadOnlyList<PublicKey> addresses,
        GetInflationRewardOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(options);
        return SendAsync<IReadOnlyList<InflationReward?>>(
            RpcRequests.GetInflationReward(
                addresses,
                options.Epoch,
                options.Commitment,
                options.MinContextSlot),
            cancellationToken);
    }

    /// <summary>
    /// Returns the leader schedule for an epoch - a map of validator identity to the slot indices (relative to
    /// the start of the epoch) it leads - or <c>null</c> if the epoch has no schedule.
    /// See <see href="https://solana.com/docs/rpc/http/getleaderschedule">getLeaderSchedule</see>.
    /// </summary>
    /// <param name="slot">A slot in the epoch to query, or <c>null</c> for the current epoch.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>A map of validator identity (base58) to its leader slot indices, or <c>null</c> if unavailable.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<IReadOnlyDictionary<string, IReadOnlyList<ulong>>?> GetLeaderScheduleAsync(
        ulong? slot = null,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendNullableAsync<IReadOnlyDictionary<string, IReadOnlyList<ulong>>?>(
            RpcRequests.GetLeaderSchedule(slot, commitment), cancellationToken);

    /// <summary>Returns a leader schedule with optional validator-identity filtering.</summary>
    /// <param name="options">Slot, identity, and commitment options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The leader schedule, or <c>null</c> when unavailable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<IReadOnlyDictionary<string, IReadOnlyList<ulong>>?> GetLeaderScheduleWithOptionsAsync(
        GetLeaderScheduleOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendNullableAsync<IReadOnlyDictionary<string, IReadOnlyList<ulong>>?>(
            RpcRequests.GetLeaderSchedule(options.Slot, options.Commitment, options.Identity), cancellationToken);
    }

    /// <summary>
    /// Returns the confirmed block slots from <paramref name="startSlot"/> through <paramref name="endSlot"/>
    /// (inclusive). See <see href="https://solana.com/docs/rpc/http/getblocks">getBlocks</see>.
    /// </summary>
    /// <param name="startSlot">The first slot of the range.</param>
    /// <param name="endSlot">The last slot of the range, or <c>null</c> for the latest confirmed block (capped at 500,000 slots ahead).</param>
    /// <param name="commitment">The commitment level to query at (<c>processed</c> is not supported by the node).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The slots that produced a block, in ascending order.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<IReadOnlyList<ulong>> GetBlocksAsync(
        ulong startSlot,
        ulong? endSlot = null,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<ulong>>(RpcRequests.GetBlocks(startSlot, endSlot, commitment), cancellationToken);

    /// <summary>Returns confirmed block slots with explicit commitment and minimum-context-slot options.</summary>
    /// <param name="startSlot">The first slot of the range.</param>
    /// <param name="endSlot">The last slot of the range, or <c>null</c> for the latest confirmed block.</param>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The slots that produced a block, in ascending order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<IReadOnlyList<ulong>> GetBlocksWithOptionsAsync(
        ulong startSlot,
        ulong? endSlot,
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendAsync<IReadOnlyList<ulong>>(
            RpcRequests.GetBlocks(startSlot, endSlot, options.Commitment, options.MinContextSlot), cancellationToken);
    }

    /// <summary>
    /// Returns information about the nodes participating in the cluster.
    /// See <see href="https://solana.com/docs/rpc/http/getclusternodes">getClusterNodes</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The cluster nodes and their network addresses.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<ClusterNode>> GetClusterNodesAsync(CancellationToken cancellationToken = default)
        => RequireNonNullEntries(
            await SendAsync<ClusterNode[]>(RpcRequests.GetClusterNodes(), cancellationToken),
            "cluster-node list");

    /// <summary>
    /// Returns the account at <paramref name="account"/> decoded with <c>jsonParsed</c> encoding, or <c>null</c>
    /// if it does not exist. See <see href="https://solana.com/docs/rpc/http/getaccountinfo">getAccountInfo</see>.
    /// </summary>
    /// <param name="account">The account to fetch.</param>
    /// <param name="commitment">The commitment level to query at; defaults to <see cref="Commitment.Confirmed"/> when <c>null</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The parsed account, or <c>null</c> if no account exists at <paramref name="account"/>.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ParsedAccountInfo?> GetParsedAccountInfoAsync(
        PublicKey account,
        Commitment? commitment = null,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<ParsedAccountInfo?>>(
            RpcRequests.GetParsedAccountInfo(account, commitment ?? Commitment.Confirmed), cancellationToken);

        return result.Value;
    }

    /// <summary>Returns a parsed account with explicit commitment and minimum-context-slot options.</summary>
    /// <param name="account">The account to fetch.</param>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The parsed account, or <c>null</c> if it does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ParsedAccountInfo?> GetParsedAccountInfoWithOptionsAsync(
        PublicKey account,
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = await GetParsedAccountInfoWithContextAsync(account, options, cancellationToken);
        return result.Value;
    }

    /// <summary>Returns a parsed account together with the slot context used by the node.</summary>
    /// <param name="account">The account to fetch.</param>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The context-wrapped parsed account; its value is <c>null</c> when the account does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<RpcContextValue<ParsedAccountInfo?>> GetParsedAccountInfoWithContextAsync(
        PublicKey account,
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendAsync<RpcContextValue<ParsedAccountInfo?>>(
            RpcRequests.GetParsedAccountInfo(account, options.Commitment, options.MinContextSlot), cancellationToken);
    }

    /// <summary>
    /// Returns how much cluster stake has voted on a block at each confirmation depth, along with the
    /// epoch's total active stake.
    /// See <see href="https://solana.com/docs/rpc/http/getblockcommitment">getBlockCommitment</see>.
    /// </summary>
    /// <param name="slot">The slot of the block to query.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The block's commitment; its <see cref="BlockCommitment.Commitment"/> is <c>null</c> for an unknown block.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<BlockCommitment> GetBlockCommitmentAsync(ulong slot, CancellationToken cancellationToken = default)
        => SendAsync<BlockCommitment>(RpcRequests.GetBlockCommitment(slot), cancellationToken);

    /// <summary>
    /// Returns recent block production: leader slots and blocks produced per validator identity,
    /// optionally narrowed to one identity and/or a slot range (the current epoch when omitted).
    /// See <see href="https://solana.com/docs/rpc/http/getblockproduction">getBlockProduction</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="identity">Only return production for this validator identity; all validators when null.</param>
    /// <param name="firstSlot">The first slot of the range to query; the current epoch when null.</param>
    /// <param name="lastSlot">The last slot of the range; the highest slot when null. Ignored unless <paramref name="firstSlot"/> is set.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The block production per identity and the covered slot range.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<BlockProduction> GetBlockProductionAsync(
        Commitment commitment = Commitment.Confirmed,
        PublicKey? identity = null,
        ulong? firstSlot = null,
        ulong? lastSlot = null,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<BlockProduction>>(
            RpcRequests.GetBlockProduction(commitment, identity, firstSlot, lastSlot), cancellationToken);

        return RequireContextValue(result);
    }

    /// <summary>
    /// Returns a block's estimated production time as a Unix timestamp, or <c>null</c> when the node has
    /// no timestamp for it. See <see href="https://solana.com/docs/rpc/http/getblocktime">getBlockTime</see>.
    /// </summary>
    /// <param name="slot">The slot of the block to query.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The block time in Unix seconds, or <c>null</c> when unavailable.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error (for example, for a slot it has not processed).</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<long?> GetBlockTimeAsync(ulong slot, CancellationToken cancellationToken = default)
        => SendNullableAsync<long?>(RpcRequests.GetBlockTime(slot), cancellationToken);

    /// <summary>
    /// Returns up to <paramref name="limit"/> confirmed block slots starting at <paramref name="startSlot"/>.
    /// See <see href="https://solana.com/docs/rpc/http/getblockswithlimit">getBlocksWithLimit</see>.
    /// </summary>
    /// <param name="startSlot">The first slot of the range.</param>
    /// <param name="limit">The maximum number of slots to return (max 500,000).</param>
    /// <param name="commitment">The commitment level to query at (<c>processed</c> is not supported by the node).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The slots that produced a block, in ascending order.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<IReadOnlyList<ulong>> GetBlocksWithLimitAsync(
        ulong startSlot,
        ulong limit,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<ulong>>(RpcRequests.GetBlocksWithLimit(startSlot, limit, commitment), cancellationToken);

    /// <summary>Returns a limited block-slot range with explicit commitment and minimum-context-slot options.</summary>
    /// <param name="startSlot">The first slot of the range.</param>
    /// <param name="limit">The maximum number of slots to return.</param>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The slots that produced a block, in ascending order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<IReadOnlyList<ulong>> GetBlocksWithLimitWithOptionsAsync(
        ulong startSlot,
        ulong limit,
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendAsync<IReadOnlyList<ulong>>(
            RpcRequests.GetBlocksWithLimit(
                startSlot,
                limit,
                options.Commitment,
                options.MinContextSlot),
            cancellationToken);
    }

    /// <summary>
    /// Returns the cluster's epoch schedule (epoch length, warmup, leader-schedule offset).
    /// See <see href="https://solana.com/docs/rpc/http/getepochschedule">getEpochSchedule</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The epoch schedule.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<EpochSchedule> GetEpochScheduleAsync(CancellationToken cancellationToken = default)
        => SendAsync<EpochSchedule>(RpcRequests.GetEpochSchedule(), cancellationToken);

    /// <summary>
    /// Returns the slot of the lowest confirmed block the node has not purged from its ledger.
    /// See <see href="https://solana.com/docs/rpc/http/getfirstavailableblock">getFirstAvailableBlock</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The first available block's slot.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<ulong> GetFirstAvailableBlockAsync(CancellationToken cancellationToken = default)
        => SendAsync<ulong>(RpcRequests.GetFirstAvailableBlock(), cancellationToken);

    /// <summary>
    /// Returns the cluster's genesis hash (base58), which identifies the network.
    /// See <see href="https://solana.com/docs/rpc/http/getgenesishash">getGenesisHash</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The genesis hash (base58).</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<string> GetGenesisHashAsync(CancellationToken cancellationToken = default)
        => SendAsync<string>(RpcRequests.GetGenesisHash(), cancellationToken);

    /// <summary>
    /// Returns the highest slots the node has snapshots for.
    /// See <see href="https://solana.com/docs/rpc/http/gethighestsnapshotslot">getHighestSnapshotSlot</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The full and (when present) incremental snapshot slots.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error (for example, when it has no snapshot).</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<HighestSnapshotSlot> GetHighestSnapshotSlotAsync(CancellationToken cancellationToken = default)
        => SendAsync<HighestSnapshotSlot>(RpcRequests.GetHighestSnapshotSlot(), cancellationToken);

    /// <summary>
    /// Returns the identity public key of the node being queried.
    /// See <see href="https://solana.com/docs/rpc/http/getidentity">getIdentity</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The node's identity public key.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<PublicKey> GetIdentityAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<NodeIdentity>(RpcRequests.GetIdentity(), cancellationToken);
        return result.Identity;
    }

    /// <summary>
    /// Returns the cluster's inflation governor (the parameters inflation is computed from).
    /// See <see href="https://solana.com/docs/rpc/http/getinflationgovernor">getInflationGovernor</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The inflation parameters.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<InflationGovernor> GetInflationGovernorAsync(
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendAsync<InflationGovernor>(RpcRequests.GetInflationGovernor(commitment), cancellationToken);

    /// <summary>
    /// Returns the inflation values for the current epoch.
    /// See <see href="https://solana.com/docs/rpc/http/getinflationrate">getInflationRate</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The current inflation rate split.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<InflationRate> GetInflationRateAsync(CancellationToken cancellationToken = default)
        => SendAsync<InflationRate>(RpcRequests.GetInflationRate(), cancellationToken);

    /// <summary>
    /// Returns the 20 largest accounts by lamport balance, optionally restricted to one side of the
    /// circulating-supply split. Results may be cached up to two hours by the node.
    /// See <see href="https://solana.com/docs/rpc/http/getlargestaccounts">getLargestAccounts</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="filter">Restrict results to circulating or non-circulating accounts; both when null.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The largest accounts, largest first.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<LargestAccount>> GetLargestAccountsAsync(
        Commitment commitment = Commitment.Confirmed,
        LargestAccountsFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<LargestAccount[]>>(
            RpcRequests.GetLargestAccounts(commitment, filter), cancellationToken);

        return RequireNonNullEntries(RequireContextValue(result), "largest-account list");
    }

    /// <summary>Returns the largest accounts with explicit filtering and server-side sorting control.</summary>
    /// <param name="options">Commitment, circulating-supply filter, and sorting options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The largest accounts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<LargestAccount>> GetLargestAccountsWithOptionsAsync(
        GetLargestAccountsOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = await SendAsync<RpcContextValue<LargestAccount[]>>(
            RpcRequests.GetLargestAccounts(options.Commitment, options.Filter, options.SortResults), cancellationToken);

        return RequireNonNullEntries(RequireContextValue(result), "largest-account list");
    }

    /// <summary>
    /// Returns the highest slot seen from retransmitted shreds.
    /// See <see href="https://solana.com/docs/rpc/http/getmaxretransmitslot">getMaxRetransmitSlot</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The slot number.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<ulong> GetMaxRetransmitSlotAsync(CancellationToken cancellationToken = default)
        => SendAsync<ulong>(RpcRequests.GetMaxRetransmitSlot(), cancellationToken);

    /// <summary>
    /// Returns the highest slot seen from shreds inserted into the ledger.
    /// See <see href="https://solana.com/docs/rpc/http/getmaxshredinsertslot">getMaxShredInsertSlot</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The slot number.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<ulong> GetMaxShredInsertSlotAsync(CancellationToken cancellationToken = default)
        => SendAsync<ulong>(RpcRequests.GetMaxShredInsertSlot(), cancellationToken);

    /// <summary>
    /// Returns recent performance samples (transactions and slots per sampled window), newest first.
    /// See <see href="https://solana.com/docs/rpc/http/getrecentperformancesamples">getRecentPerformanceSamples</see>.
    /// </summary>
    /// <param name="limit">The number of samples to return (max 720); the node default when null.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The performance samples, newest first.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<PerformanceSample>> GetRecentPerformanceSamplesAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
        => RequireNonNullEntries(
            await SendAsync<PerformanceSample[]>(RpcRequests.GetRecentPerformanceSamples(limit), cancellationToken),
            "performance-sample list");

    /// <summary>
    /// Returns the identity of the current slot leader.
    /// See <see href="https://solana.com/docs/rpc/http/getslotleader">getSlotLeader</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The current slot leader's identity public key.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<PublicKey> GetSlotLeaderAsync(
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendAsync<PublicKey>(RpcRequests.GetSlotLeader(commitment), cancellationToken);

    /// <summary>Returns the current slot leader with explicit commitment and minimum-context-slot options.</summary>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The current slot leader's identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<PublicKey> GetSlotLeaderWithOptionsAsync(
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SendAsync<PublicKey>(
            RpcRequests.GetSlotLeader(options.Commitment, options.MinContextSlot), cancellationToken);
    }

    /// <summary>
    /// Returns the cluster's minimum stake delegation, in lamports.
    /// See <see href="https://solana.com/docs/rpc/http/getstakeminimumdelegation">getStakeMinimumDelegation</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The minimum delegation in lamports.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ulong> GetStakeMinimumDelegationAsync(
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<ulong>>(
            RpcRequests.GetStakeMinimumDelegation(commitment), cancellationToken);

        return result.Value;
    }

    /// <summary>Returns minimum stake delegation with explicit commitment and minimum-context-slot options.</summary>
    /// <param name="options">The context options sent to the node.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The minimum delegation in lamports.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ulong> GetStakeMinimumDelegationWithOptionsAsync(
        RpcContextOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = await SendAsync<RpcContextValue<ulong>>(
            RpcRequests.GetStakeMinimumDelegation(options.Commitment, options.MinContextSlot), cancellationToken);

        return result.Value;
    }

    /// <summary>
    /// Returns the SPL token accounts approved to <paramref name="delegateAccount"/> for a specific
    /// <paramref name="mint"/>. Account data is requested as base64 and exposed decoded on
    /// <see cref="AccountInfo.Data"/>.
    /// See <see href="https://solana.com/docs/rpc/http/gettokenaccountsbydelegate">getTokenAccountsByDelegate</see>.
    /// </summary>
    /// <param name="delegateAccount">The delegate the token accounts are approved to.</param>
    /// <param name="mint">The token mint to filter by.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The matching token accounts; empty when none.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<ProgramAccount>> GetTokenAccountsByDelegateAsync(
        PublicKey delegateAccount,
        PublicKey mint,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<RpcContextValue<ProgramAccount[]>>(
            RpcRequests.GetTokenAccountsByDelegate(
                delegateAccount,
                TokenAccountsFilter.ByMint(mint),
                commitment),
            cancellationToken);

        return RequireNonNullKeyedAccounts(RequireContextValue(result));
    }

    /// <summary>
    /// Returns token accounts approved to a delegate, filtered by either mint or SPL Token program.
    /// </summary>
    /// <param name="delegateAccount">The delegate the token accounts are approved to.</param>
    /// <param name="filter">The mutually exclusive mint or token-program filter.</param>
    /// <param name="options">Base64 slicing, commitment, and minimum-context-slot options; node defaults when <c>null</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The matching token accounts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<ProgramAccount>> GetTokenAccountsByDelegateWithFilterAsync(
        PublicKey delegateAccount,
        TokenAccountsFilter filter,
        GetAccountInfoOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await GetTokenAccountsByDelegateWithContextAsync(
            delegateAccount, filter, options, cancellationToken);

        return RequireNonNullKeyedAccounts(RequireContextValue(result));
    }

    /// <summary>
    /// Returns filtered delegated token accounts with the exact upstream account encoding and read configuration.
    /// </summary>
    /// <param name="delegateAccount">The delegate the token accounts are approved to.</param>
    /// <param name="filter">The mutually exclusive mint or token-program filter.</param>
    /// <param name="options">The exact upstream account configuration.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The matching token accounts with exact account-data branches.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> or <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<RpcProgramAccount>> GetTokenAccountsByDelegateWithOptionsAsync(
        PublicKey delegateAccount,
        TokenAccountsFilter filter,
        RpcAccountInfoOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = await GetTokenAccountsByDelegateWithOptionsAndContextAsync(
            delegateAccount, filter, options, cancellationToken);

        return RequireContextValue(result);
    }

    /// <summary>
    /// Returns exact-encoding delegated token accounts together with the slot context used by the node.
    /// </summary>
    /// <param name="delegateAccount">The delegate the token accounts are approved to.</param>
    /// <param name="filter">The mutually exclusive mint or token-program filter.</param>
    /// <param name="options">The exact upstream account configuration.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The context-wrapped matching token accounts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> or <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<RpcContextValue<RpcProgramAccount[]>> GetTokenAccountsByDelegateWithOptionsAndContextAsync(
        PublicKey delegateAccount,
        TokenAccountsFilter filter,
        RpcAccountInfoOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(options);
        var result = await SendAsync<RpcContextValue<RpcProgramAccount[]>>(
            RpcRequests.GetTokenAccountsByDelegate(
                delegateAccount,
                filter,
                options.Commitment,
                options.DataSlice,
                options.MinContextSlot,
                options.Encoding),
            cancellationToken);
        RequireNonNullKeyedAccounts(result.Value);
        return result;
    }

    /// <summary>Returns filtered delegated token accounts together with their slot context.</summary>
    /// <param name="delegateAccount">The delegate the token accounts are approved to.</param>
    /// <param name="filter">The mutually exclusive mint or token-program filter.</param>
    /// <param name="options">Base64 slicing, commitment, and minimum-context-slot options; node defaults when <c>null</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The context-wrapped matching token accounts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> is <c>null</c>.</exception>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<RpcContextValue<ProgramAccount[]>> GetTokenAccountsByDelegateWithContextAsync(
        PublicKey delegateAccount,
        TokenAccountsFilter filter,
        GetAccountInfoOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        options ??= new GetAccountInfoOptions();
        var result = await SendAsync<RpcContextValue<ProgramAccount[]>>(
            RpcRequests.GetTokenAccountsByDelegate(
                delegateAccount,
                filter,
                options.Commitment,
                options.DataSlice,
                options.MinContextSlot),
            cancellationToken);
        RequireNonNullKeyedAccounts(result.Value);
        return result;
    }

    /// <summary>
    /// Returns the lowest slot the node has information about in its ledger (wire method
    /// <c>minimumLedgerSlot</c>). This slot can rise over time as the node purges older ledger data.
    /// See <see href="https://solana.com/docs/rpc/http/minimumledgerslot">minimumLedgerSlot</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The minimum ledger slot.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<ulong> GetMinimumLedgerSlotAsync(CancellationToken cancellationToken = default)
        => SendAsync<ulong>(RpcRequests.MinimumLedgerSlot(), cancellationToken);

    /// <summary>
    /// Creates an empty JSON-RPC batch bound to this client. Queue calls on it, then submit them in one
    /// HTTP round-trip with <see cref="RpcBatch.ExecuteAsync"/>.
    /// </summary>
    /// <returns>A new, empty batch.</returns>
    public RpcBatch CreateBatch() => new(this);

    internal async Task<JsonElement> SendBatchAsync(IReadOnlyList<RpcRequest> requests, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = JsonContent.Create(requests, RpcJson.TypeInfo<IReadOnlyList<RpcRequest>>())
        };
        using var response = await _httpClient.SendAsync(
            message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var body = await ReadResponseBodyAsync(response.Content, cancellationToken);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    // processed < confirmed < finalized; the Commitment enum is not declared in that order, so rank explicitly.
    private static int CommitmentRank(Commitment commitment) => commitment switch
    {
        Commitment.Processed => 0,
        Commitment.Confirmed => 1,
        Commitment.Finalized => 2,
        _ => 1
    };

    private static int StatusRank(SignatureStatus status)
    {
        if (status.ConfirmationStatus is not null)
        {
            return status.ConfirmationStatus switch
            {
                "processed" => 0,
                "confirmed" => 1,
                "finalized" => 2,
                _ => -1
            };
        }

        // Older nodes omitted confirmationStatus. Match the upstream confirmation loop exactly:
        // zero or one confirmation is still processed, more than one is confirmed, and null is rooted/finalized.
        return status.Confirmations switch
        {
            null => 2,
            > 1 => 1,
            _ => 0
        };
    }

    private static async Task CancelAfterAsync(CancellationTokenSource source, TimeSpan timeout)
    {
        try
        {
            while (timeout > MaximumTimerDelay)
            {
                await Task.Delay(MaximumTimerDelay, source.Token);
                timeout -= MaximumTimerDelay;
            }

            await Task.Delay(timeout, source.Token);
            await source.CancelAsync();
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            // Confirmation finished or caller cancellation won; the timeout task only needs to stop.
        }
    }

    private static ParsedTransaction? RequireConfirmedParsedTransaction(ParsedTransaction? transaction)
    {
        if (transaction is not null && transaction.Slot is null)
            throw new JsonException("A getTransaction response must carry a slot and block-time member.");

        return transaction;
    }

    private static T[] RequireNonNullKeyedAccounts<T>(T[]? accounts)
        where T : class
    {
        if (accounts is null)
            throw new JsonException("An RPC keyed-account list cannot be null.");

        if (Array.Exists(accounts, static account => account is null))
            throw new JsonException("An RPC keyed-account list cannot contain null entries.");

        return accounts;
    }

    private static T[] RequireNonNullEntries<T>(T[]? values, string valueName)
        where T : class
    {
        if (values is null)
            throw new JsonException($"An RPC {valueName} cannot be null.");

        if (Array.Exists(values, static value => value is null))
            throw new JsonException($"An RPC {valueName} cannot contain null entries.");

        return values;
    }

    private static T RequireContextValue<T>(RpcContextValue<T> result)
    {
        if (result.Value is null)
            throw new JsonException("The RPC context wrapper carried null for a non-null value contract.");

        return result.Value;
    }

    // Validates the envelope and extracts the result in a single pass: a Utf8JsonReader walk checks
    // jsonrpc/id/error and records the span of the result value, which is then deserialized directly -
    // no intermediate JsonElement DOM of the (possibly multi-megabyte) result.
    private static T DeserializeEnvelope<T>(ReadOnlySpan<byte> body, int requestId, bool allowNullResult)
    {
        var span = body;
        // ReadFromJsonAsync tolerated a UTF-8 BOM; Utf8JsonReader rejects it.
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
            span = span[3..];

        if (span.IsEmpty)
            throw new RpcException(-1, "Empty response body.");

        var reader = new Utf8JsonReader(span);
        reader.Read();
        if (reader.TokenType == JsonTokenType.Null)
            throw new RpcException(-1, "Empty response body.");
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("The JSON-RPC response is not a JSON object.");

        string? version = null;
        var hasId = false;
        var idIsNull = false;
        var idMatches = false;
        var hasResult = false;
        var resultIsNull = false;
        var resultStart = 0;
        var resultLength = 0;
        RpcError? error = null;

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("result"u8))
            {
                reader.Read();
                hasResult = true;
                resultIsNull = reader.TokenType == JsonTokenType.Null;
                resultStart = (int)reader.TokenStartIndex;
                reader.Skip();
                resultLength = (int)reader.BytesConsumed - resultStart;
            }
            else if (reader.ValueTextEquals("error"u8))
            {
                reader.Read();
                if (reader.TokenType != JsonTokenType.Null)
                {
                    using var errorDocument = JsonDocument.ParseValue(ref reader);
                    var errorElement = errorDocument.RootElement;
                    if (errorElement.ValueKind != JsonValueKind.Object ||
                        !errorElement.TryGetProperty("code", out var codeElement) ||
                        codeElement.ValueKind != JsonValueKind.Number ||
                        !codeElement.TryGetInt32(out var code) ||
                        !errorElement.TryGetProperty("message", out var messageElement) ||
                        messageElement.ValueKind != JsonValueKind.String)
                    {
                        throw new RpcException(-1, "JSON-RPC response carried a malformed error object.");
                    }

                    error = new RpcError
                    {
                        Code = code,
                        Message = messageElement.GetString()!,
                        Data = errorElement.TryGetProperty("data", out var dataElement)
                            ? dataElement.Clone()
                            : null
                    };
                }
            }
            else if (reader.ValueTextEquals("jsonrpc"u8))
            {
                reader.Read();
                version = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                reader.Skip();
            }
            else if (reader.ValueTextEquals("id"u8))
            {
                reader.Read();
                hasId = true;
                idIsNull = reader.TokenType == JsonTokenType.Null;
                idMatches = reader.TokenType == JsonTokenType.Number
                    && reader.TryGetInt32(out var id)
                    && id == requestId;
                reader.Skip();
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        // A second top-level value after the envelope means the body is not one JSON-RPC response.
        if (reader.Read())
            throw new JsonException("The JSON-RPC response carries trailing content.");

        if (version != "2.0")
            throw new RpcException(-1, "Invalid JSON-RPC response version.");

        // An unprocessable request legitimately carries "id": null. A numeric error id must still
        // correlate to this request; otherwise a multiplexing proxy could surface another call's error.
        if (error is not null)
        {
            if (!hasId || (!idIsNull && !idMatches))
                throw new RpcException(-1, "JSON-RPC response id did not match the request id.");

            if (hasResult && !resultIsNull)
                throw new RpcException(-1, "JSON-RPC response contained both a non-null result and an error.");

            // Some gateways pad an error response with "result": null. Once version and correlation are
            // valid, the node's concrete error remains the most useful diagnostic.
            throw new RpcException(error.Code, error.Message, error.Data);
        }

        if (!idMatches)
            throw new RpcException(-1, "JSON-RPC response id did not match the request id.");
        if (!hasResult)
            throw new RpcException(-1, "JSON-RPC response contained neither a result nor an error.");

        var result = JsonSerializer.Deserialize(span.Slice(resultStart, resultLength), RpcJson.TypeInfo<T>());
        if (!allowNullResult && result is null)
            throw new JsonException("The JSON-RPC method returned null for a non-null result contract.");

        return result!;
    }

    private async Task<ParsedBlock?> GetParsedBlockCoreAsync(
        ulong slot,
        byte maxSupportedTransactionVersion,
        Commitment? commitment,
        CancellationToken cancellationToken)
    {
        var block = await SendNullableAsync<ParsedBlock?>(
            RpcRequests.GetParsedBlock(
                slot,
                commitment ?? Commitment.Confirmed,
                maxSupportedTransactionVersion),
            cancellationToken);

        if (block is null)
            return null;

        // getBlock emits the flattened transaction vector in ledger order. Upstream derives
        // transactionIndex from this same zero-based order when serving getTransaction.
        var transactions = block.Transactions
            .Select((transaction, index) => transaction with
            {
                Slot = slot,
                BlockTime = block.BlockTime,
                TransactionIndex = (uint)index
            })
            .ToArray();

        return block with { Transactions = transactions };
    }

    private async Task<T> SendAsync<T>(RpcRequest request, CancellationToken cancellationToken)
        => await SendCoreAsync<T>(request, allowNullResult: false, cancellationToken);

    private async Task<T> SendNullableAsync<T>(RpcRequest request, CancellationToken cancellationToken)
        => await SendCoreAsync<T>(request, allowNullResult: true, cancellationToken);

    private async Task<T> SendCoreAsync<T>(
        RpcRequest request,
        bool allowNullResult,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = JsonContent.Create(request, RpcJson.TypeInfo<RpcRequest>())
        };
        if (request.Method == RpcMethods.RequestAirdrop)
            message.Options.Set(DisableRetriesKey, true);

        using var response = await _httpClient.SendAsync(
            message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var body = await ReadResponseBodyAsync(response.Content, cancellationToken);
        return DeserializeEnvelope<T>(body.Span, request.Id, allowNullResult);
    }

    private async Task<ReadOnlyMemory<byte>> ReadResponseBodyAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var contentLength = content.Headers.ContentLength;
        if (contentLength is { } declaredLength && declaredLength > _maximumResponseContentLength)
            throw ResponseTooLarge(declaredLength);

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var initialCapacity = contentLength is > 0
            ? Math.Min((int)contentLength.Value, 64 * 1024)
            : Math.Min(16 * 1024, _maximumResponseContentLength);
        using var body = new MemoryStream(initialCapacity);
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);

        try
        {
            long total = 0;
            while (true)
            {
                var remaining = _maximumResponseContentLength - total;
                var requested = (int)Math.Min(buffer.Length, remaining + 1);
                var read = await stream.ReadAsync(
                    buffer.AsMemory(0, requested), cancellationToken);
                if (read == 0)
                    return body.GetBuffer().AsMemory(0, checked((int)total));

                total += read;
                if (total > _maximumResponseContentLength)
                    throw ResponseTooLarge(total);

                body.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private HttpRequestException ResponseTooLarge(long receivedLength)
        => new(
            $"The JSON-RPC response body is {receivedLength} bytes, exceeding the configured "
            + $"{_maximumResponseContentLength}-byte limit ({nameof(SolanaRpcOptions.MaximumResponseContentLength)}).");
}
