using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>
/// One instruction in a <c>jsonParsed</c> transaction. When the node recognizes the program, <see cref="Program"/>
/// and <see cref="Parsed"/> are set; otherwise the raw <see cref="Accounts"/> and <see cref="Data"/> are. Both
/// forms always carry <see cref="ProgramId"/>, so no information is lost either way.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record ParsedInstruction
{
    /// <summary>The program that runs the instruction.</summary>
    [JsonPropertyName("programId")]
    public PublicKey ProgramId { get; init; }

    /// <summary>The program's short name when the node recognized it (for example <c>"system"</c>); otherwise <c>null</c>.</summary>
    [JsonPropertyName("program")]
    public string? Program { get; init; }

    /// <summary>The node's parsed view of the instruction, or <c>null</c> when the program was not recognized.</summary>
    [JsonPropertyName("parsed")]
    public ParsedInstructionInfo? Parsed { get; init; }

    /// <summary>The accounts passed to the instruction, present only when it was not parsed; otherwise <c>null</c>.</summary>
    [JsonPropertyName("accounts")]
    public IReadOnlyList<PublicKey>? Accounts { get; init; }

    /// <summary>The base58-encoded instruction data, present only when it was not parsed; otherwise <c>null</c>.</summary>
    [JsonPropertyName("data")]
    public string? Data { get; init; }

    /// <summary>The CPI stack height at which the instruction ran, if the node reported it.</summary>
    [JsonPropertyName("stackHeight")]
    public int? StackHeight { get; init; }
}
