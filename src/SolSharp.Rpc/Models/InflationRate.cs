using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The inflation values for the current epoch, as returned by <c>getInflationRate</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getinflationrate">getInflationRate</seealso>
public sealed record InflationRate
{
    /// <summary>The total inflation percentage.</summary>
    [JsonPropertyName("total")]
    [JsonRequired]
    public double Total { get; init; }

    /// <summary>The portion of inflation allocated to validators.</summary>
    [JsonPropertyName("validator")]
    [JsonRequired]
    public double Validator { get; init; }

    /// <summary>The portion of inflation allocated to the foundation.</summary>
    [JsonPropertyName("foundation")]
    [JsonRequired]
    public double Foundation { get; init; }

    /// <summary>The epoch the values are valid for.</summary>
    [JsonPropertyName("epoch")]
    [JsonRequired]
    public ulong Epoch { get; init; }
}
