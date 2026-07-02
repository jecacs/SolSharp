using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>Cluster commitment for a block, as returned by <c>getBlockCommitment</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getblockcommitment">getBlockCommitment</seealso>
public sealed record BlockCommitment
{
    /// <summary>
    /// The amount of cluster stake in lamports that has voted on the block at each confirmation depth
    /// (index 0 is depth 1), or <c>null</c> for an unknown block.
    /// </summary>
    [JsonPropertyName("commitment")]
    public IReadOnlyList<ulong>? Commitment { get; init; }

    /// <summary>The total active stake in lamports for the current epoch.</summary>
    [JsonPropertyName("totalStake")]
    public ulong TotalStake { get; init; }
}
