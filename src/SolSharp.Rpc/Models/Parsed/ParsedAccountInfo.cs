using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>
/// A Solana account decoded with <c>jsonParsed</c> encoding, as returned by <c>getAccountInfo</c> and
/// <c>accountSubscribe</c>. When the owning program is recognized, <see cref="Program"/> and
/// <see cref="Parsed"/> carry the decoded fields; otherwise the raw bytes are exposed on <see cref="RawData"/>.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/http/getaccountinfo">getAccountInfo</seealso>
[JsonConverter(typeof(ParsedAccountInfoJsonConverter))]
public sealed record ParsedAccountInfo
{
    /// <summary>The account's lamport balance.</summary>
    public ulong Lamports { get; init; }

    /// <summary>The program that owns the account.</summary>
    public PublicKey Owner { get; init; }

    /// <summary>Whether the account holds an executable program.</summary>
    public bool Executable { get; init; }

    /// <summary>The epoch at which the account will next owe rent; rent-exempt accounts report <c>ulong.MaxValue</c>.</summary>
    public ulong RentEpoch { get; init; }

    /// <summary>The size of the account's data, in bytes, if the node reported it.</summary>
    public ulong? Space { get; init; }

    /// <summary>The owning program's short name when recognized (for example <c>"spl-token"</c>); otherwise <c>null</c>.</summary>
    public string? Program { get; init; }

    /// <summary>
    /// The node's parsed view of the account - an action type plus decoded fields - or <c>null</c> when the
    /// owning program was not recognized.
    /// </summary>
    public ParsedInstructionInfo? Parsed { get; init; }

    /// <summary>The raw account bytes, present only when the program was not recognized; otherwise <c>null</c>.</summary>
    public byte[]? RawData { get; init; }
}
