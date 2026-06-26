using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>The message of a <c>jsonParsed</c> transaction: its accounts, instructions, and recent blockhash.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record ParsedMessage
{
    /// <summary>The accounts the transaction references, in index order, each with its role flags.</summary>
    [JsonPropertyName("accountKeys")]
    public IReadOnlyList<ParsedAccountKey> AccountKeys { get; init; } = [];

    /// <summary>The top-level instructions, in execution order.</summary>
    [JsonPropertyName("instructions")]
    public IReadOnlyList<ParsedInstruction> Instructions { get; init; } = [];

    /// <summary>The recent blockhash the transaction was built against (base58).</summary>
    [JsonPropertyName("recentBlockhash")]
    public string RecentBlockhash { get; init; } = string.Empty;
}
