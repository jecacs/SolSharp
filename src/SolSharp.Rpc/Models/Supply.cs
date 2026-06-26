using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>Cluster token supply totals (in lamports), as returned by <c>getSupply</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getsupply">getSupply</seealso>
public sealed record Supply
{
    /// <summary>The total supply.</summary>
    [JsonPropertyName("total")]
    public ulong Total { get; init; }

    /// <summary>The circulating supply.</summary>
    [JsonPropertyName("circulating")]
    public ulong Circulating { get; init; }

    /// <summary>The non-circulating supply.</summary>
    [JsonPropertyName("nonCirculating")]
    public ulong NonCirculating { get; init; }
}
