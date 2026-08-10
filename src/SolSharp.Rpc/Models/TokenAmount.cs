using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>An SPL token amount, as returned by token balance and supply queries.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/gettokenaccountbalance">getTokenAccountBalance</seealso>
public sealed record TokenAmount
{
    /// <summary>The raw amount in the token's base units.</summary>
    [JsonPropertyName("amount")]
    [JsonRequired]
    public string Amount
    {
        get => field ?? throw new InvalidOperationException("The token amount has not been initialized.");
        init => field = value ?? throw new JsonException("A token amount must carry its base-unit string.");
    }

    /// <summary>The number of base-10 digits to the right of the decimal point.</summary>
    [JsonPropertyName("decimals")]
    [JsonRequired]
    public byte Decimals { get; init; }

    /// <summary>The amount in UI units, or null if it cannot be represented.</summary>
    [JsonPropertyName("uiAmount")]
    [JsonRequired]
    public double? UiAmount { get; init; }

    /// <summary>The amount in UI units as a string.</summary>
    [JsonPropertyName("uiAmountString")]
    [JsonRequired]
    public string UiAmountString
    {
        get => field ?? throw new InvalidOperationException("The UI token amount has not been initialized.");
        init => field = value ?? throw new JsonException("A token amount must carry its UI amount string.");
    }
}
