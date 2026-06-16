using System.Net.Http.Json;
using SolSharp.Core.Converters;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;
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

    private async Task<T> SendAsync<T>(RpcRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient
            .PostAsJsonAsync(string.Empty, request, SolanaJsonSerializer.Options, cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<RpcResponse<T>>(SolanaJsonSerializer.Options, cancellationToken) ?? throw new RpcException(-1, "Empty response body.");

        if (payload.Error is not null)
            throw new RpcException(payload.Error.Code, payload.Error.Message);

        return payload.Result!;
    }
}
