using System.Text.Json;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc;

/// <summary>
/// A JSON-RPC batch: queue several calls, then submit them in one HTTP round-trip with
/// <see cref="ExecuteAsync"/>. Each queued method returns a task that completes when the batch executes -
/// await them only after <see cref="ExecuteAsync"/> has been started. A per-call node error faults only
/// that call's task (with an <see cref="RpcException"/>); a transport failure faults them all. Note that
/// some RPC providers disable or cap JSON-RPC batching.
/// </summary>
public sealed class RpcBatch
{
    private readonly SolanaRpcClient _client;
    private readonly List<RpcRequest> _requests = [];
    private readonly List<IPending> _pending = [];
    private bool _executed;

    internal RpcBatch(SolanaRpcClient client) => _client = client;

    /// <summary>The number of calls queued so far.</summary>
    public int Count => _requests.Count;

    /// <summary>Queues a <c>getBalance</c> call.</summary>
    /// <param name="account">The account to query.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <returns>The balance in lamports, once the batch executes.</returns>
    public Task<ulong> GetBalanceAsync(PublicKey account, Commitment commitment = Commitment.Confirmed)
        => Add(RpcRequests.GetBalance(account, commitment),
            static result => result.Deserialize(RpcJson.TypeInfo<RpcContextValue<ulong>>())!.Value);

    /// <summary>Queues a <c>getAccountInfo</c> call (base64 account data).</summary>
    /// <param name="account">The account to query.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <returns>The account, or <c>null</c> if it does not exist, once the batch executes.</returns>
    public Task<AccountInfo?> GetAccountInfoAsync(PublicKey account, Commitment commitment = Commitment.Confirmed)
        => Add(RpcRequests.GetAccountInfo(account, commitment),
            static result => result.Deserialize(RpcJson.TypeInfo<RpcContextValue<AccountInfo>>())!.Value);

    /// <summary>Queues a <c>getLatestBlockhash</c> call.</summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <returns>The blockhash and its last valid block height, once the batch executes.</returns>
    public Task<LatestBlockhash> GetLatestBlockhashAsync(Commitment commitment = Commitment.Confirmed)
        => Add(RpcRequests.GetLatestBlockhash(commitment),
            static result => result.Deserialize(RpcJson.TypeInfo<RpcContextValue<LatestBlockhash>>())!.Value!);

    /// <summary>Queues a <c>getSlot</c> call.</summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <returns>The current slot, once the batch executes.</returns>
    public Task<ulong> GetSlotAsync(Commitment commitment = Commitment.Confirmed)
        => Add(RpcRequests.GetSlot(commitment),
            static result => result.GetUInt64());

    /// <summary>Queues a <c>getTokenAccountBalance</c> call.</summary>
    /// <param name="tokenAccount">The token account to query.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <returns>The token balance, once the batch executes.</returns>
    public Task<TokenAmount> GetTokenAccountBalanceAsync(PublicKey tokenAccount, Commitment commitment = Commitment.Confirmed)
        => Add(RpcRequests.GetTokenAccountBalance(tokenAccount, commitment),
            static result => result.Deserialize(RpcJson.TypeInfo<RpcContextValue<TokenAmount>>())!.Value!);

    /// <summary>Queues a <c>sendTransaction</c> call - e.g. to submit several signed transactions in one round-trip.</summary>
    /// <param name="transaction">The signed transaction's serialized wire bytes.</param>
    /// <param name="options">Send options; node defaults are used when null.</param>
    /// <returns>The transaction signature (base58), once the batch executes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is <c>null</c>.</exception>
    public Task<string> SendTransactionAsync(byte[] transaction, SendTransactionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        options ??= new SendTransactionOptions();

        var encoded = Convert.ToBase64String(transaction);
        return Add(
            RpcRequests.SendTransaction(encoded, options.SkipPreflight, options.PreflightCommitment, options.MaxRetries, options.MinContextSlot),
            static result => result.GetString()!);
    }

    /// <summary>Submits every queued call as one JSON-RPC batch and completes their tasks.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>A task that completes once every queued call has been resolved.</returns>
    /// <exception cref="InvalidOperationException">The batch is empty or was already executed.</exception>
    /// <exception cref="RpcException">The node's reply was not a JSON-RPC batch response.</exception>
    /// <exception cref="HttpRequestException">The request failed at the transport level or returned a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (_executed)
            throw new InvalidOperationException("The batch was already executed; create a new batch per round-trip.");
        if (_requests.Count == 0)
            throw new InvalidOperationException("The batch is empty; queue at least one call first.");

        _executed = true;

        JsonElement root;
        try
        {
            root = await _client.SendBatchAsync(_requests, cancellationToken);

            if (root.ValueKind != JsonValueKind.Array)
                throw new RpcException(-1, $"Expected a JSON-RPC batch response array, got {root.ValueKind}.");
        }
        catch (Exception exception)
        {
            foreach (var pending in _pending)
                pending.Fail(exception);
            throw;
        }

        // Responses may arrive in any order; match them to the queued calls by id.
        var responses = new Dictionary<int, JsonElement>(_pending.Count);
        foreach (var element in root.EnumerateArray())
            if (element.TryGetProperty("id", out var id) && id.TryGetInt32(out var value))
                responses[value] = element;

        foreach (var pending in _pending)
        {
            if (responses.TryGetValue(pending.Id, out var response))
                pending.Complete(response);
            else
                pending.Fail(new RpcException(-1, $"The batch response contained no entry for request {pending.Id}."));
        }
    }

    private Task<T> Add<T>(RpcRequest request, Func<JsonElement, T> map)
    {
        if (_executed)
            throw new InvalidOperationException("The batch was already executed; create a new batch per round-trip.");

        var pending = new Pending<T>(_requests.Count + 1, map);
        _requests.Add(request with { Id = pending.Id });
        _pending.Add(pending);
        return pending.Source.Task;
    }

    private interface IPending
    {
        int Id { get; }

        void Complete(JsonElement response);

        void Fail(Exception exception);
    }

    private sealed class Pending<T>(int id, Func<JsonElement, T> map) : IPending
    {
        public TaskCompletionSource<T> Source { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Id { get; } = id;

        public void Complete(JsonElement response)
        {
            if (response.TryGetProperty("error", out var error) && error.ValueKind is not JsonValueKind.Null)
            {
                var code = error.ValueKind == JsonValueKind.Object &&
                           error.TryGetProperty("code", out var codeElement) &&
                           codeElement.TryGetInt32(out var codeValue)
                    ? codeValue
                    : -1;
                var message = error.ValueKind == JsonValueKind.Object &&
                              error.TryGetProperty("message", out var messageElement) &&
                              messageElement.ValueKind == JsonValueKind.String
                    ? messageElement.GetString()!
                    : error.GetRawText();

                Source.TrySetException(new RpcException(code, message));
                return;
            }

            try
            {
                if (!response.TryGetProperty("result", out var result))
                    throw new RpcException(-1, $"The batch response entry for request {Id} carried neither a result nor an error.");

                Source.TrySetResult(map(result));
            }
            catch (Exception exception)
            {
                Source.TrySetException(exception);
            }
        }

        public void Fail(Exception exception) => Source.TrySetException(exception);
    }
}
