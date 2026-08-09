using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>One of a mint's largest token accounts, as returned by <c>getTokenLargestAccounts</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/gettokenlargestaccounts">getTokenLargestAccounts</seealso>
public sealed record TokenLargestAccount
{
    private string? _amount;
    private string? _uiAmountString;

    /// <summary>The token account's address.</summary>
    [JsonPropertyName("address")]
    [JsonRequired]
    public PublicKey Address { get; init; }

    /// <summary>The balance in base units, as a string (it can exceed <see cref="ulong"/>).</summary>
    [JsonPropertyName("amount")]
    [JsonRequired]
    public string Amount
    {
        get => _amount ?? throw new InvalidOperationException("The token-account amount has not been initialized.");
        init => _amount = value ?? throw new JsonException("A largest-token-account entry must carry its amount.");
    }

    /// <summary>The mint's decimals.</summary>
    [JsonPropertyName("decimals")]
    [JsonRequired]
    public byte Decimals { get; init; }

    /// <summary>The balance scaled by the mint's decimals as a JSON number, or <c>null</c>.</summary>
    [JsonPropertyName("uiAmount")]
    [JsonRequired]
    public double? UiAmount { get; init; }

    /// <summary>The balance scaled by the decimals, as a human-readable string.</summary>
    [JsonPropertyName("uiAmountString")]
    [JsonRequired]
    public string UiAmountString
    {
        get => _uiAmountString ?? throw new InvalidOperationException("The UI token-account amount has not been initialized.");
        init => _uiAmountString = value ?? throw new JsonException("A largest-token-account entry must carry its UI amount string.");
    }
}
