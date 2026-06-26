using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>A Solana account, as returned by <c>getAccountInfo</c> and <c>getMultipleAccounts</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getaccountinfo">getAccountInfo</seealso>
[JsonConverter(typeof(AccountInfoJsonConverter))]
public sealed record AccountInfo
{
    /// <summary>The account's lamport balance.</summary>
    public ulong Lamports { get; init; }

    /// <summary>The program that owns the account.</summary>
    public PublicKey Owner { get; init; }

    /// <summary>Whether the account holds an executable program.</summary>
    public bool Executable { get; init; }

    /// <summary>
    /// The epoch at which the account will next owe rent. Rent-exempt accounts - effectively all
    /// accounts today - report <c>ulong.MaxValue</c>, meaning rent is never collected.
    /// </summary>
    public ulong RentEpoch { get; init; }

    /// <summary>The account's raw data, decoded from the node's base64 encoding.</summary>
    public byte[] Data { get; init; } = [];
}
