using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>An address/balance entry, as returned by <c>getLargestAccounts</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getlargestaccounts">getLargestAccounts</seealso>
public sealed record LargestAccount
{
    /// <summary>The account's address.</summary>
    [JsonPropertyName("address")]
    [JsonRequired]
    public PublicKey Address { get; init; }

    /// <summary>The account's balance in lamports.</summary>
    [JsonPropertyName("lamports")]
    [JsonRequired]
    public ulong Lamports { get; init; }
}
