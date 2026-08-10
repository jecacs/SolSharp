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

    private interface IPending
    {
        int Id { get; }

        void Complete(JsonElement response);

        void Fail(Exception exception);
    }

    /// <summary>The number of calls queued so far.</summary>
    public int Count => _requests.Count;

    /// <summary>Queues a <c>getBalance</c> call.</summary>
    /// <param name="account">The account to query.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <returns>The balance in lamports, once the batch executes.</returns>
    public Task<ulong> GetBalanceAsync(PublicKey account, Commitment commitment = Commitment.Confirmed)
        => Add(
            RpcRequests.GetBalance(account, commitment),
            static result => RequireContextValue<ulong>(result));

    /// <summary>Queues a <c>getAccountInfo</c> call (base64 account data).</summary>
    /// <param name="account">The account to query.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <returns>The account, or <c>null</c> if it does not exist, once the batch executes.</returns>
    public Task<AccountInfo?> GetAccountInfoAsync(PublicKey account, Commitment commitment = Commitment.Confirmed)
        => Add(
            RpcRequests.GetAccountInfo(account, commitment),
            static result => DeserializeContext<AccountInfo?>(result).Value);

    /// <summary>Queues a <c>getLatestBlockhash</c> call.</summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <returns>The blockhash and its last valid block height, once the batch executes.</returns>
    public Task<LatestBlockhash> GetLatestBlockhashAsync(Commitment commitment = Commitment.Confirmed)
        => Add(
            RpcRequests.GetLatestBlockhash(commitment),
            static result => RequireContextValue<LatestBlockhash>(result));

    /// <summary>Queues a <c>getSlot</c> call.</summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <returns>The current slot, once the batch executes.</returns>
    public Task<ulong> GetSlotAsync(Commitment commitment = Commitment.Confirmed)
        => Add(
            RpcRequests.GetSlot(commitment),
            static result => result.GetUInt64());

    /// <summary>Queues a <c>getTokenAccountBalance</c> call.</summary>
    /// <param name="tokenAccount">The token account to query.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <returns>The token balance, once the batch executes.</returns>
    public Task<TokenAmount> GetTokenAccountBalanceAsync(PublicKey tokenAccount, Commitment commitment = Commitment.Confirmed)
        => Add(
            RpcRequests.GetTokenAccountBalance(tokenAccount, commitment),
            static result => RequireContextValue<TokenAmount>(result));

    /// <summary>Queues a <c>sendTransaction</c> call - e.g. to submit several signed transactions in one round-trip.</summary>
    /// <param name="transaction">The signed transaction's serialized wire bytes.</param>
    /// <param name="options">Send options; client defaults are used when <c>null</c>.</param>
    /// <returns>The transaction signature (base58), once the batch executes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is <c>null</c>.</exception>
    public Task<string> SendTransactionAsync(byte[] transaction, SendTransactionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        options ??= new();

        var encoded = Convert.ToBase64String(transaction);
        return Add(
            RpcRequests.SendTransaction(encoded, options.SkipPreflight, options.PreflightCommitment, options.MaxRetries, options.MinContextSlot),
            static result => result.ValueKind == JsonValueKind.String
                ? result.GetString()!
                : throw new JsonException("sendTransaction returned a non-string result."));
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

        try
        {
            var root = await _client.SendBatchAsync(_requests, cancellationToken);

            if (root.ValueKind != JsonValueKind.Array)
                throw new RpcException(-1, $"Expected a JSON-RPC batch response array, got {root.ValueKind}.");

            // Responses may arrive in any order. Validate the complete envelope before resolving calls so
            // malformed, duplicate, or injected ids cannot leave some TaskCompletionSources pending forever.
            var expectedIds = _pending.Select(static pending => pending.Id).ToHashSet();
            var responses = new Dictionary<int, JsonElement>(_pending.Count);
            foreach (var element in root.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    throw new RpcException(-1, $"Expected each JSON-RPC batch entry to be an object, got {element.ValueKind}.");

                if (!element.TryGetProperty("jsonrpc", out var version) ||
                    version.ValueKind != JsonValueKind.String ||
                    version.GetString() != "2.0")
                    throw new RpcException(-1, "A batch response entry carried an invalid JSON-RPC version.");

                if (!element.TryGetProperty("id", out var id) ||
                    id.ValueKind != JsonValueKind.Number ||
                    !id.TryGetInt32(out var value))
                    throw new RpcException(-1, "A batch response entry carried no valid integer id.");
                if (!expectedIds.Contains(value))
                    throw new RpcException(-1, $"The batch response contained unknown request id {value}.");
                if (!responses.TryAdd(value, element))
                    throw new RpcException(-1, $"The batch response contained duplicate request id {value}.");

                var hasResult = element.TryGetProperty("result", out _);
                var hasError = element.TryGetProperty("error", out var error) &&
                               error.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
                if (hasResult == hasError)
                    throw new RpcException(
                        -1,
                        $"The batch response entry for request {value} must carry exactly one of result or error.");
                if (hasError &&
                    (error.ValueKind != JsonValueKind.Object ||
                     !error.TryGetProperty("code", out var code) ||
                     code.ValueKind != JsonValueKind.Number ||
                     !code.TryGetInt32(out _) ||
                     !error.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.String))
                    throw new RpcException(
                        -1,
                        $"The batch response entry for request {value} carried a malformed error object.");
            }

            foreach (var pending in _pending)
            {
                if (responses.TryGetValue(pending.Id, out var response))
                    pending.Complete(response);
                else
                    pending.Fail(new RpcException(-1, $"The batch response contained no entry for request {pending.Id}."));
            }
        }
        catch (Exception exception)
        {
            foreach (var pending in _pending)
                pending.Fail(exception);
            throw;
        }
    }

    private static RpcContextValue<T> DeserializeContext<T>(JsonElement result)
        => result.Deserialize(RpcJson.TypeInfo<RpcContextValue<T>>())
           ?? throw new JsonException("A batched RPC method returned a null context wrapper.");

    private static T RequireContextValue<T>(JsonElement result)
    {
        var context = DeserializeContext<T>(result);
        if (context.Value is null)
            throw new JsonException("A batched RPC context wrapper carried null for a non-null value contract.");

        return context.Value;
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

    private sealed class Pending<T>(int id, Func<JsonElement, T> map) : IPending
    {
        public TaskCompletionSource<T> Source { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Id { get; } = id;

        public void Complete(JsonElement response)
        {
            try
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
                    var data = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("data", out var dataElement)
                        ? dataElement
                        : (JsonElement?)null;

                    Source.TrySetException(new RpcException(code, message, data));
                    return;
                }

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
