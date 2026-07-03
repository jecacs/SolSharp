using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Streaming;

/// <summary>
/// A slot-lifecycle notification payload from <c>slotsUpdatesSubscribe</c> - one update per stage a
/// slot moves through (see <see cref="Type"/>).
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/websocket/slotsupdatessubscribe">slotsUpdatesSubscribe</seealso>
public sealed record SlotsUpdate
{
    /// <summary>The slot the update is about.</summary>
    [JsonPropertyName("slot")]
    public ulong Slot { get; init; }

    /// <summary>
    /// The update type: <c>firstShredReceived</c>, <c>completed</c>, <c>createdBank</c>, <c>frozen</c>,
    /// <c>dead</c>, <c>optimisticConfirmation</c>, or <c>root</c>. Kept as a string so new node-side
    /// stages do not break deserialization.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>The update's Unix timestamp in milliseconds.</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    /// <summary>The parent slot; only present on <c>createdBank</c> updates.</summary>
    [JsonPropertyName("parent")]
    public ulong? Parent { get; init; }

    /// <summary>Why the slot died; only present on <c>dead</c> updates.</summary>
    [JsonPropertyName("err")]
    public string? Error { get; init; }

    /// <summary>Transaction counts for the slot; only present on <c>frozen</c> updates.</summary>
    [JsonPropertyName("stats")]
    public SlotsUpdateStats? Stats { get; init; }
}

/// <summary>The per-slot transaction counts attached to a <c>frozen</c> <see cref="SlotsUpdate"/>.</summary>
public sealed record SlotsUpdateStats
{
    /// <summary>The number of transaction entries in the slot.</summary>
    [JsonPropertyName("numTransactionEntries")]
    public ulong NumTransactionEntries { get; init; }

    /// <summary>The number of successful transactions in the slot.</summary>
    [JsonPropertyName("numSuccessfulTransactions")]
    public ulong NumSuccessfulTransactions { get; init; }

    /// <summary>The number of failed transactions in the slot.</summary>
    [JsonPropertyName("numFailedTransactions")]
    public ulong NumFailedTransactions { get; init; }

    /// <summary>The largest number of transactions in a single entry.</summary>
    [JsonPropertyName("maxTransactionsPerEntry")]
    public ulong MaxTransactionsPerEntry { get; init; }
}
