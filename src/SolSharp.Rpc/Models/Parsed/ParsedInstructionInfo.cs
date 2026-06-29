using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>The node's parsed view of an instruction it recognized: an action type and its decoded fields.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
[JsonConverter(typeof(ParsedInstructionInfoJsonConverter))]
public sealed record ParsedInstructionInfo
{
    /// <summary>The instruction's action type, as named by the node's parser (for example <c>"transfer"</c>).</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// The decoded instruction fields as raw JSON, kept untyped so callers can read whatever fields the
    /// specific instruction type carries (for example <c>source</c>, <c>destination</c>, <c>lamports</c>).
    /// </summary>
    [JsonPropertyName("info")]
    public JsonElement Info { get; init; }
}
