using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The cluster's inflation parameters, as returned by <c>getInflationGovernor</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getinflationgovernor">getInflationGovernor</seealso>
public sealed record InflationGovernor
{
    /// <summary>The initial inflation percentage from time 0.</summary>
    [JsonPropertyName("initial")]
    [JsonRequired]
    public double Initial { get; init; }

    /// <summary>The terminal inflation percentage.</summary>
    [JsonPropertyName("terminal")]
    [JsonRequired]
    public double Terminal { get; init; }

    /// <summary>The rate per year at which inflation is lowered (until the terminal rate).</summary>
    [JsonPropertyName("taper")]
    [JsonRequired]
    public double Taper { get; init; }

    /// <summary>The percentage of total inflation allocated to the foundation.</summary>
    [JsonPropertyName("foundation")]
    [JsonRequired]
    public double Foundation { get; init; }

    /// <summary>The duration of the foundation pool inflation, in years.</summary>
    [JsonPropertyName("foundationTerm")]
    [JsonRequired]
    public double FoundationTerm { get; init; }
}
