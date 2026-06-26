using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>An account referenced by a <c>jsonParsed</c> transaction, with its role flags.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record ParsedAccountKey
{
    /// <summary>The account address.</summary>
    [JsonPropertyName("pubkey")]
    public PublicKey Pubkey { get; init; }

    /// <summary>Whether the account signed the transaction.</summary>
    [JsonPropertyName("signer")]
    public bool Signer { get; init; }

    /// <summary>Whether the account is writable.</summary>
    [JsonPropertyName("writable")]
    public bool Writable { get; init; }

    /// <summary>
    /// Where the key came from - <c>"transaction"</c> for a static key or <c>"lookupTable"</c> for one loaded
    /// from an address lookup table; <c>null</c> if the node did not report it.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }
}
