using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>One of a mint's largest token accounts, as returned by <c>getTokenLargestAccounts</c>.</summary>
public sealed record TokenLargestAccount
{
    /// <summary>The token account's address.</summary>
    [JsonPropertyName("address")]
    public PublicKey Address { get; init; }

    /// <summary>The balance in base units, as a string (it can exceed <see cref="ulong"/>).</summary>
    [JsonPropertyName("amount")]
    public string Amount { get; init; } = "0";

    /// <summary>The mint's decimals.</summary>
    [JsonPropertyName("decimals")]
    public byte Decimals { get; init; }

    /// <summary>The balance scaled by the decimals, as a human-readable string.</summary>
    [JsonPropertyName("uiAmountString")]
    public string? UiAmountString { get; init; }
}
