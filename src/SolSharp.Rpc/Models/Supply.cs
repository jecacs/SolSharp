using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>Cluster token supply totals (in lamports), as returned by <c>getSupply</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getsupply">getSupply</seealso>
public sealed record Supply
{
    /// <summary>The total supply.</summary>
    [JsonPropertyName("total")]
    [JsonRequired]
    public ulong Total { get; init; }

    /// <summary>The circulating supply.</summary>
    [JsonPropertyName("circulating")]
    [JsonRequired]
    public ulong Circulating { get; init; }

    /// <summary>The non-circulating supply.</summary>
    [JsonPropertyName("nonCirculating")]
    [JsonRequired]
    public ulong NonCirculating { get; init; }

    /// <summary>
    /// Accounts excluded from circulating supply, or an empty list when the request set
    /// <c>excludeNonCirculatingAccountsList</c>.
    /// </summary>
    [JsonPropertyName("nonCirculatingAccounts")]
    [JsonRequired]
    public IReadOnlyList<PublicKey> NonCirculatingAccounts
    {
        get => field ??
            throw new InvalidOperationException("The non-circulating account list has not been initialized.");
        init => field = value
            ?? throw new JsonException("A supply result must carry its non-circulating account list.");
    }
}
