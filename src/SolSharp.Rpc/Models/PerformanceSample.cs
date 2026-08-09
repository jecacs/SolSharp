using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>A recent performance sample, as returned by <c>getRecentPerformanceSamples</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getrecentperformancesamples">getRecentPerformanceSamples</seealso>
public sealed record PerformanceSample
{
    /// <summary>The slot the sample was taken at.</summary>
    [JsonPropertyName("slot")]
    [JsonRequired]
    public ulong Slot { get; init; }

    /// <summary>The number of transactions processed during the sample period.</summary>
    [JsonPropertyName("numTransactions")]
    [JsonRequired]
    public ulong NumTransactions { get; init; }

    /// <summary>The number of non-vote transactions during the sample period, if the node reports it.</summary>
    [JsonPropertyName("numNonVoteTransactions")]
    public ulong? NumNonVoteTransactions { get; init; }

    /// <summary>The number of slots completed during the sample period.</summary>
    [JsonPropertyName("numSlots")]
    [JsonRequired]
    public ulong NumSlots { get; init; }

    /// <summary>The number of seconds in the sample window.</summary>
    [JsonPropertyName("samplePeriodSecs")]
    [JsonRequired]
    public ushort SamplePeriodSecs { get; init; }
}
