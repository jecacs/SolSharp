using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>A per-slot prioritization fee observation from <c>getRecentPrioritizationFees</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getrecentprioritizationfees">getRecentPrioritizationFees</seealso>
public sealed record PrioritizationFee
{
    /// <summary>The slot the fee was observed in.</summary>
    [JsonPropertyName("slot")]
    public ulong Slot { get; init; }

    /// <summary>The prioritization fee paid, in micro-lamports per compute unit.</summary>
    [JsonPropertyName("prioritizationFee")]
    public ulong Fee { get; init; }
}
