using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>A program-owned account decoded with <c>jsonParsed</c> encoding.</summary>
/// <seealso href="https://solana.com/docs/rpc/websocket/programsubscribe">programSubscribe</seealso>
public sealed record ParsedProgramAccount
{
    /// <summary>The account address.</summary>
    [JsonPropertyName("pubkey")]
    public required PublicKey PublicKey { get; init; }

    /// <summary>The node-decoded account state.</summary>
    [JsonPropertyName("account")]
    public required ParsedAccountInfo Account
    {
        get => field!;
        init => field = value ?? throw new JsonException("A parsed keyed account must carry a non-null account.");
    }
}
