using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The cluster's inflation parameters, as returned by <c>getInflationGovernor</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getinflationgovernor">getInflationGovernor</seealso>
public sealed record InflationGovernor
{
    /// <summary>The initial inflation percentage from time 0.</summary>
    [JsonPropertyName("initial")]
    public double Initial { get; init; }

    /// <summary>The terminal inflation percentage.</summary>
    [JsonPropertyName("terminal")]
    public double Terminal { get; init; }

    /// <summary>The rate per year at which inflation is lowered (until the terminal rate).</summary>
    [JsonPropertyName("taper")]
    public double Taper { get; init; }

    /// <summary>The percentage of total inflation allocated to the foundation.</summary>
    [JsonPropertyName("foundation")]
    public double Foundation { get; init; }

    /// <summary>The duration of the foundation pool inflation, in years.</summary>
    [JsonPropertyName("foundationTerm")]
    public double FoundationTerm { get; init; }
}
