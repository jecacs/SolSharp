using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>An SPL token amount, as returned by token balance and supply queries.</summary>
public sealed record TokenAmount
{
    /// <summary>The raw amount in the token's base units.</summary>
    [JsonPropertyName("amount")]
    public string Amount { get; init; } = string.Empty;

    /// <summary>The number of base-10 digits to the right of the decimal point.</summary>
    [JsonPropertyName("decimals")]
    public int Decimals { get; init; }

    /// <summary>The amount in UI units, or null if it cannot be represented.</summary>
    [JsonPropertyName("uiAmount")]
    public decimal? UiAmount { get; init; }

    /// <summary>The amount in UI units as a string.</summary>
    [JsonPropertyName("uiAmountString")]
    public string? UiAmountString { get; init; }
}
