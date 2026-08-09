using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Streaming;

/// <summary>
/// A slot-lifecycle notification payload from <c>slotsUpdatesSubscribe</c> - one update per stage a
/// slot moves through (see <see cref="Type"/>).
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/websocket/slotsupdatessubscribe">slotsUpdatesSubscribe</seealso>
public sealed record SlotsUpdate : IJsonOnDeserialized
{
    private string? _type;

    /// <summary>The slot the update is about.</summary>
    [JsonPropertyName("slot")]
    [JsonRequired]
    public ulong Slot { get; init; }

    /// <summary>
    /// The update type: <c>firstShredReceived</c>, <c>completed</c>, <c>createdBank</c>, <c>frozen</c>,
    /// <c>dead</c>, <c>optimisticConfirmation</c>, or <c>root</c>. The pinned closed set is validated
    /// after deserialization; the string preserves the exact wire name.
    /// </summary>
    [JsonPropertyName("type")]
    [JsonRequired]
    public string Type
    {
        get => _type ?? throw new InvalidOperationException("The slot-update type has not been initialized.");
        init => _type = value ?? throw new JsonException("A slot update must carry its type.");
    }

    /// <summary>The update's Unix timestamp in milliseconds.</summary>
    [JsonPropertyName("timestamp")]
    [JsonRequired]
    public ulong Timestamp { get; init; }

    /// <summary>The parent slot; only present on <c>createdBank</c> updates.</summary>
    [JsonPropertyName("parent")]
    public ulong? Parent { get; init; }

    /// <summary>Why the slot died; only present on <c>dead</c> updates.</summary>
    [JsonPropertyName("err")]
    public string? Error { get; init; }

    /// <summary>Transaction counts for the slot; only present on <c>frozen</c> updates.</summary>
    [JsonPropertyName("stats")]
    public SlotsUpdateStats? Stats { get; init; }

    /// <inheritdoc/>
    public void OnDeserialized()
    {
        switch (Type)
        {
            case "firstShredReceived":
            case "completed":
            case "optimisticConfirmation":
            case "root":
                break;
            case "createdBank" when Parent is not null:
                break;
            case "frozen" when Stats is not null:
                break;
            case "dead" when Error is not null:
                break;
            case "createdBank":
                throw new JsonException("A createdBank slot update must carry its parent.");
            case "frozen":
                throw new JsonException("A frozen slot update must carry transaction statistics.");
            case "dead":
                throw new JsonException("A dead slot update must carry its error.");
            default:
                throw new JsonException($"Unknown slot update type '{Type}'.");
        }
    }
}

/// <summary>The per-slot transaction counts attached to a <c>frozen</c> <see cref="SlotsUpdate"/>.</summary>
public sealed record SlotsUpdateStats
{
    /// <summary>The number of transaction entries in the slot.</summary>
    [JsonPropertyName("numTransactionEntries")]
    [JsonRequired]
    public ulong NumTransactionEntries { get; init; }

    /// <summary>The number of successful transactions in the slot.</summary>
    [JsonPropertyName("numSuccessfulTransactions")]
    [JsonRequired]
    public ulong NumSuccessfulTransactions { get; init; }

    /// <summary>The number of failed transactions in the slot.</summary>
    [JsonPropertyName("numFailedTransactions")]
    [JsonRequired]
    public ulong NumFailedTransactions { get; init; }

    /// <summary>The largest number of transactions in a single entry.</summary>
    [JsonPropertyName("maxTransactionsPerEntry")]
    [JsonRequired]
    public ulong MaxTransactionsPerEntry { get; init; }
}
