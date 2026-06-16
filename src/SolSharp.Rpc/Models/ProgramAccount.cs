using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>One entry from <c>getProgramAccounts</c>: an account paired with the program that owns it.</summary>
public sealed record ProgramAccount
{
    /// <summary>The account's address.</summary>
    [JsonPropertyName("pubkey")]
    public required PublicKey PublicKey { get; init; }

    /// <summary>The account itself: lamports, owner, decoded data, and so on.</summary>
    [JsonPropertyName("account")]
    public required AccountInfo Account { get; init; }
}
