using System.Net.Http.Json;
using System.Text.Json;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Models.Parsed;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc;

/// <summary>
/// Minimal Solana JSON-RPC client over HTTP. The supplied <see cref="HttpClient"/> must have its
/// BaseAddress set to the RPC endpoint. Read methods only for now; throws <see cref="RpcException"/>
/// on a node-level error.
/// </summary>
public class SolanaRpcClient(HttpClient httpClient)
{
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
        var result = await SendAsync<RpcContextValue<LatestBlockhash>>(RpcRequests
            .GetLatestBlockhash(commitment), cancellationToken);

        return result.Value!;
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
        var result = await SendAsync<RpcContextValue<ulong>>(RpcRequests
            .GetBalance(account, commitment), cancellationToken);

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

        return result.Value!;
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

        return result.Value!;
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
    /// <param name="options">Send options (skip preflight, retries, commitment); node defaults are used when null.</param>
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
    /// Simulates a transaction without submitting it, returning its logs, compute units, and any error.
    /// See <see href="https://solana.com/docs/rpc/http/simulatetransaction">simulateTransaction</see>.
    /// </summary>
    /// <param name="transaction">The transaction's serialized wire bytes; base64-encoded for the request.</param>
    /// <param name="options">Simulation options (signature verification, blockhash replacement, commitment); node defaults are used when null.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The simulation result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is <c>null</c>.</exception>
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

        var encoded = Convert.ToBase64String(transaction);
        var result = await SendAsync<RpcContextValue<SimulateTransactionResult>>(
            RpcRequests.SimulateTransaction(encoded, options.SigVerify, options.ReplaceRecentBlockhash, options.Commitment, options.MinContextSlot),
            cancellationToken);

        return result.Value!;
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
        var result = await SendAsync<RpcContextValue<AccountInfo>>(
            RpcRequests.GetAccountInfo(account, commitment, dataSlice), cancellationToken);

        return result.Value;
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

        return result.Value!;
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
        return await SendAsync<SignatureInfo[]>(
            RpcRequests.GetSignaturesForAddress(address, options.Limit, options.Before, options.Until, options.Commitment, options.MinContextSlot),
            cancellationToken);
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
        return await SendAsync<ProgramAccount[]>(
            RpcRequests.GetProgramAccounts(programId, options.Commitment, options.Filters, options.DataSlice, options.MinContextSlot),
            cancellationToken);
    }

    /// <summary>
    /// Fetches and decodes an on-chain Address Lookup Table account. Returns <c>null</c> if nothing exists
    /// at <paramref name="tableAddress"/> or the account is not an initialized lookup table.
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
        var account = await GetAccountInfoAsync(tableAddress, commitment, cancellationToken: cancellationToken);
        return account is null ? null : AddressLookupTable.Decode(account.Data);
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
            RpcRequests.GetTokenAccountsByOwner(owner, mint, commitment), cancellationToken);

        return result.Value!;
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
        => await SendAsync<PrioritizationFee[]>(RpcRequests.GetRecentPrioritizationFees(accounts ?? []), cancellationToken);

    /// <summary>
    /// Returns a confirmed transaction by signature, or <c>null</c> if the cluster has not seen it. Supports
    /// versioned (v0) transactions. See
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
        => SendAsync<TransactionResponse?>(RpcRequests.GetTransaction(signature, commitment), cancellationToken);

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

        return result.Value!;
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

        var deadline = DateTimeOffset.UtcNow + (timeout ?? DefaultConfirmationTimeout);
        var target = CommitmentRank(commitment);

        while (true)
        {
            var statuses = await GetSignatureStatusesAsync([signature], searchTransactionHistory: false, cancellationToken);
            var status = statuses.Count > 0 ? statuses[0] : null;
            if (status is not null && StatusRank(status.ConfirmationStatus) >= target)
                return status;

            if (DateTimeOffset.UtcNow >= deadline)
                throw new TimeoutException($"Transaction {signature} was not confirmed at {commitment} within the timeout.");

            await Task.Delay(ConfirmationPollInterval, cancellationToken);
        }
    }

    /// <summary>
    /// Submits a signed transaction and waits until it reaches <paramref name="commitment"/>. Returns the
    /// signature on success; throws <see cref="TransactionFailedException"/> if the transaction lands but fails
    /// on-chain, so a returned signature always means success.
    /// </summary>
    /// <param name="transaction">The signed transaction's serialized wire bytes.</param>
    /// <param name="options">Send options; node defaults are used when null.</param>
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

    // processed < confirmed < finalized; the Commitment enum is not declared in that order, so rank explicitly.
    private static int CommitmentRank(Commitment commitment) => commitment switch
    {
        Commitment.Processed => 0,
        Commitment.Confirmed => 1,
        Commitment.Finalized => 2,
        _ => 1
    };

    private static int StatusRank(string? confirmationStatus) => confirmationStatus switch
    {
        "processed" => 0,
        "confirmed" => 1,
        "finalized" => 2,
        _ => -1
    };

    private static readonly TimeSpan DefaultConfirmationTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ConfirmationPollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Fetches and decodes an SPL Token mint account, or returns <c>null</c> if nothing exists at
    /// <paramref name="mint"/> or the account is too short to be a mint.
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
        return account is null ? null : Mint.Decode(account.Data);
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
        return account is null ? null : NonceAccount.Decode(account.Data);
    }

    /// <summary>
    /// Fetches and decodes an SPL Token account, or returns <c>null</c> if nothing exists at
    /// <paramref name="tokenAccount"/> or the account is too short to be a token account.
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
        return account is null ? null : TokenAccount.Decode(account.Data);
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
        var result = await SendAsync<RpcContextValue<Supply>>(RpcRequests.GetSupply(commitment), cancellationToken);
        return result.Value!;
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

        return result.Value!;
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
        => SendAsync<Block?>(RpcRequests.GetBlock(slot, commitment), cancellationToken);

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
    public Task<ParsedTransaction?> GetParsedTransactionAsync(
        string signature,
        Commitment? commitment = null,
        CancellationToken cancellationToken = default)
        => SendAsync<ParsedTransaction?>(
            RpcRequests.GetParsedTransaction(signature, commitment ?? Commitment.Confirmed), cancellationToken);

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
    {
        var block = await SendAsync<ParsedBlock?>(
            RpcRequests.GetParsedBlock(slot, commitment ?? Commitment.Confirmed), cancellationToken);

        if (block is null)
            return null;

        var transactions = block.Transactions
            .Select(transaction => transaction with { Slot = slot, BlockTime = block.BlockTime })
            .ToArray();

        return block with { Transactions = transactions };
    }

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

    /// <summary>
    /// Returns the inflation / staking reward paid to each of <paramref name="addresses"/> for an epoch.
    /// See <see href="https://solana.com/docs/rpc/http/getinflationreward">getInflationReward</see>.
    /// </summary>
    /// <param name="addresses">The addresses to look up rewards for.</param>
    /// <param name="epoch">The epoch to query, or <c>null</c> for the previous epoch.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The reward for each address in order; an entry is <c>null</c> when that address earned no reward.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<IReadOnlyList<InflationReward?>> GetInflationRewardAsync(
        IReadOnlyList<PublicKey> addresses,
        ulong? epoch = null,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<InflationReward?>>(
            RpcRequests.GetInflationReward(addresses, epoch, commitment), cancellationToken);

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
    public Task<IReadOnlyDictionary<string, IReadOnlyList<int>>?> GetLeaderScheduleAsync(
        ulong? slot = null,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyDictionary<string, IReadOnlyList<int>>?>(
            RpcRequests.GetLeaderSchedule(slot, commitment), cancellationToken);

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

    /// <summary>
    /// Returns information about the nodes participating in the cluster.
    /// See <see href="https://solana.com/docs/rpc/http/getclusternodes">getClusterNodes</see>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The cluster nodes and their network addresses.</returns>
    /// <exception cref="RpcException">The node returned a JSON-RPC error.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<IReadOnlyList<ClusterNode>> GetClusterNodesAsync(CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<ClusterNode>>(RpcRequests.GetClusterNodes(), cancellationToken);

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
        var result = await SendAsync<RpcContextValue<ParsedAccountInfo>>(
            RpcRequests.GetParsedAccountInfo(account, commitment ?? Commitment.Confirmed), cancellationToken);

        return result.Value;
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

        return result.Value!;
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
        => SendAsync<long?>(RpcRequests.GetBlockTime(slot), cancellationToken);

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

        return result.Value!;
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
        => await SendAsync<PerformanceSample[]>(RpcRequests.GetRecentPerformanceSamples(limit), cancellationToken);

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
            RpcRequests.GetTokenAccountsByDelegate(delegateAccount, mint, commitment), cancellationToken);

        return result.Value!;
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
        using var response = await httpClient
            .PostAsJsonAsync(string.Empty, requests, RpcJson.TypeInfo<IReadOnlyList<RpcRequest>>(), cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }

    private async Task<T> SendAsync<T>(RpcRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient
            .PostAsJsonAsync(string.Empty, request, RpcJson.TypeInfo<RpcRequest>(), cancellationToken);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return DeserializeEnvelope<T>(body, request.Id);
    }

    // Validates the envelope and extracts the result in a single pass: a Utf8JsonReader walk checks
    // jsonrpc/id/error and records the span of the result value, which is then deserialized directly -
    // no intermediate JsonElement DOM of the (possibly multi-megabyte) result.
    private static T DeserializeEnvelope<T>(byte[] body, int requestId)
    {
        var span = body.AsSpan();
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
        var idMatches = false;
        var hasResult = false;
        var resultStart = 0;
        var resultLength = 0;
        RpcError? error = null;

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("result"u8))
            {
                reader.Read();
                hasResult = true;
                resultStart = (int)reader.TokenStartIndex;
                reader.Skip();
                resultLength = (int)reader.BytesConsumed - resultStart;
            }
            else if (reader.ValueTextEquals("error"u8))
            {
                error = JsonSerializer.Deserialize(ref reader, RpcJson.TypeInfo<RpcError>());
            }
            else if (reader.ValueTextEquals("jsonrpc"u8))
            {
                reader.Read();
                version = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            }
            else if (reader.ValueTextEquals("id"u8))
            {
                reader.Read();
                idMatches = reader.TokenType == JsonTokenType.Number
                    && reader.TryGetInt32(out var id)
                    && id == requestId;
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        // The node's error is the most useful diagnostic there is, so it outranks envelope strictness:
        // per JSON-RPC 2.0 an unprocessable request is answered with "id": null, and some gateways pad
        // error responses with "result": null - neither may mask the real code and message.
        if (error is not null)
            throw new RpcException(error.Code, error.Message);
        if (version != "2.0")
            throw new RpcException(-1, "Invalid JSON-RPC response version.");
        if (!idMatches)
            throw new RpcException(-1, "JSON-RPC response id did not match the request id.");
        if (!hasResult)
            throw new RpcException(-1, "JSON-RPC response contained neither a result nor an error.");

        return JsonSerializer.Deserialize(span.Slice(resultStart, resultLength), RpcJson.TypeInfo<T>())!;
    }
}
