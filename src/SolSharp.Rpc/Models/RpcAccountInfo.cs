using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>
/// The exact untagged account-data union returned by Agave. Inspect the runtime branch to distinguish
/// legacy binary, explicitly encoded, and node-parsed data.
/// </summary>
[JsonConverter(typeof(RpcAccountDataJsonConverter))]
public abstract record RpcAccountData
{
    private RpcAccountData()
    {
    }

    /// <summary>The legacy <c>binary</c> response: a bare base58 string without an encoding tag.</summary>
    public sealed record LegacyBinary : RpcAccountData
    {
        internal LegacyBinary(string encodedData)
        {
            EncodedData = encodedData;
        }

        /// <summary>The base58 account data, or the upstream size-error text for oversized data.</summary>
        public string EncodedData { get; }
    }

    /// <summary>An explicitly encoded <c>[data, encoding]</c> tuple.</summary>
    public sealed record Encoded : RpcAccountData
    {
        internal Encoded(string encodedData, RpcAccountEncoding encoding)
        {
            EncodedData = encodedData;
            Encoding = encoding;
        }

        /// <summary>The encoded account data exactly as returned by the node.</summary>
        public string EncodedData { get; }

        /// <summary>The encoding tag carried by the tuple.</summary>
        public RpcAccountEncoding Encoding { get; }
    }

    /// <summary>A node-decoded account owned by a recognized program.</summary>
    public sealed record Parsed : RpcAccountData
    {
        internal Parsed(string program, JsonElement value, ulong space)
        {
            Program = program;
            Value = value;
            Space = space;
        }

        /// <summary>The owning program's short parser name, for example <c>spl-token</c>.</summary>
        public string Program { get; }

        /// <summary>The program-specific parsed JSON payload.</summary>
        public JsonElement Value { get; }

        /// <summary>The account-data size reported by the parsed payload.</summary>
        public ulong Space { get; }
    }
}

/// <summary>An account response that preserves every upstream account-data encoding branch.</summary>
public sealed record RpcAccountInfo
{
    /// <summary>The account's lamport balance.</summary>
    [JsonPropertyName("lamports")]
    public required ulong Lamports { get; init; }

    /// <summary>The program that owns the account.</summary>
    [JsonPropertyName("owner")]
    public required PublicKey Owner { get; init; }

    /// <summary>Whether the account holds an executable program.</summary>
    [JsonPropertyName("executable")]
    public required bool Executable { get; init; }

    /// <summary>The epoch at which the account will next owe rent.</summary>
    [JsonPropertyName("rentEpoch")]
    public required ulong RentEpoch { get; init; }

    /// <summary>The complete account-data length before a requested slice was applied, when reported.</summary>
    [JsonPropertyName("space")]
    public ulong? Space { get; init; }

    /// <summary>The account data in the exact branch returned by the node.</summary>
    [JsonPropertyName("data")]
    public required RpcAccountData Data { get; init; }
}

/// <summary>An account paired with its address, as returned by program and token-account scans.</summary>
public sealed record RpcProgramAccount
{
    /// <summary>The account address.</summary>
    [JsonPropertyName("pubkey")]
    public required PublicKey PublicKey { get; init; }

    /// <summary>The account with its exact upstream data branch.</summary>
    [JsonPropertyName("account")]
    public required RpcAccountInfo Account
    {
        get;
        init => field = value ?? throw new JsonException("A keyed RPC account must carry a non-null account.");
    }
}
