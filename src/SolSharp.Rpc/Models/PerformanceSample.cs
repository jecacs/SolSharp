using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>A recent performance sample, as returned by <c>getRecentPerformanceSamples</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getrecentperformancesamples">getRecentPerformanceSamples</seealso>
public sealed record PerformanceSample
{
    /// <summary>The slot the sample was taken at.</summary>
    [JsonPropertyName("slot")]
    public ulong Slot { get; init; }

    /// <summary>The number of transactions processed during the sample period.</summary>
    [JsonPropertyName("numTransactions")]
    public ulong NumTransactions { get; init; }

    /// <summary>The number of non-vote transactions during the sample period, if the node reports it.</summary>
    [JsonPropertyName("numNonVoteTransactions")]
    public ulong? NumNonVoteTransactions { get; init; }

    /// <summary>The number of slots completed during the sample period.</summary>
    [JsonPropertyName("numSlots")]
    public ulong NumSlots { get; init; }

    /// <summary>The number of seconds in the sample window.</summary>
    [JsonPropertyName("samplePeriodSecs")]
    public ushort SamplePeriodSecs { get; init; }
}
