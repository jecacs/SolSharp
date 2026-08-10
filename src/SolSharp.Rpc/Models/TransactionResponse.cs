using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>The closed transaction-version union returned by Solana RPC: <c>"legacy"</c> or a numeric <c>u8</c>.</summary>
[JsonConverter(typeof(RpcTransactionVersionJsonConverter))]
public readonly record struct RpcTransactionVersion
{
    private const ushort MaximumEncodedNumber = byte.MaxValue + 1;
    private const ushort LegacyValue = MaximumEncodedNumber + 1;

    private readonly ushort _value;

    private RpcTransactionVersion(ushort value) => _value = value;

    /// <summary>The legacy transaction version.</summary>
    public static RpcTransactionVersion Legacy => new(LegacyValue);

    /// <summary>Whether this value represents a legacy transaction.</summary>
    public bool IsLegacy => _value == LegacyValue;

    /// <summary>The numeric transaction version, or <c>null</c> for a legacy or uninitialized value.</summary>
    public byte? Number => _value is >= 1 and <= MaximumEncodedNumber ? (byte)(_value - 1) : null;

    /// <summary>Creates a numeric transaction version.</summary>
    /// <param name="number">The numeric <c>u8</c> transaction version.</param>
    /// <returns>The corresponding numeric transaction version.</returns>
    public static RpcTransactionVersion FromNumber(byte number) => new((ushort)(number + 1));
}

/// <summary>Reads and writes the exact Solana RPC transaction-version union.</summary>
public sealed class RpcTransactionVersionJsonConverter : JsonConverter<RpcTransactionVersion>
{
    /// <inheritdoc/>
    public override RpcTransactionVersion Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String && reader.GetString() == "legacy")
            return RpcTransactionVersion.Legacy;

        if (reader.TokenType is JsonTokenType.Number && reader.TryGetByte(out var number))
            return RpcTransactionVersion.FromNumber(number);

        throw new JsonException("A transaction version must be \"legacy\" or a u8 integer.");
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        RpcTransactionVersion value,
        JsonSerializerOptions options)
    {
        if (value.IsLegacy)
        {
            writer.WriteStringValue("legacy");
            return;
        }

        if (value.Number is { } number)
        {
            writer.WriteNumberValue(number);
            return;
        }

        throw new JsonException("An uninitialized transaction version cannot be serialized.");
    }
}

/// <summary>A confirmed transaction as returned by <c>getTransaction</c>: where it landed, its bytes, and how it executed.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/gettransaction">getTransaction</seealso>
public sealed record TransactionResponse
{
    /// <summary>The slot the transaction was processed in.</summary>
    [JsonPropertyName("slot")]
    [JsonRequired]
    public ulong Slot { get; init; }

    /// <summary>The estimated production time as Unix seconds, or <c>null</c> if not available.</summary>
    [JsonPropertyName("blockTime")]
    [JsonRequired]
    public long? BlockTime { get; init; }

    /// <summary>The transaction's index within the block, when reported.</summary>
    [JsonPropertyName("transactionIndex")]
    public uint? TransactionIndex { get; init; }

    /// <summary>
    /// The wire transaction version, or <c>null</c> when the node omitted it.
    /// </summary>
    [JsonPropertyName("version")]
    public RpcTransactionVersion? Version { get; init; }

    /// <summary>
    /// The transaction's wire bytes, decoded from the node's base64 form. Legacy, v0, and V1 bytes can be
    /// passed to <c>Transaction.Deserialize</c> (in SolSharp.Programs) to read their messages, accounts,
    /// and instructions.
    /// </summary>
    [JsonPropertyName("transaction")]
    [JsonConverter(typeof(Base64TupleJsonConverter))]
    [JsonRequired]
    public byte[] Transaction
    {
        get => field!;
        init => field = value ?? throw new JsonException("A transaction response must carry non-null wire bytes.");
    }

    /// <summary>Execution metadata: fee, balances, token balances, logs, inner instructions, and any error.</summary>
    [JsonPropertyName("meta")]
    [JsonRequired]
    public TransactionMeta? Meta { get; init; }
}

/// <summary>The execution metadata attached to a confirmed transaction.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/gettransaction">getTransaction</seealso>
public sealed record TransactionMeta : IJsonOnDeserialized
{
    /// <summary>The transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonPropertyName("err")]
    [JsonRequired]
    public JsonElement? Err { get; init; }

    /// <summary>
    /// The deprecated result-shaped status field retained by the node for compatibility; prefer
    /// <see cref="Err"/> or <see cref="Error"/> for new code.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonRequired]
    public JsonElement Status
    {
        get;
        init => field = value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? throw new JsonException("Transaction metadata must carry a non-null status.")
            : value;
    }

    /// <summary>The fee charged, in lamports.</summary>
    [JsonPropertyName("fee")]
    [JsonRequired]
    public ulong Fee { get; init; }

    /// <summary>Account lamport balances before the transaction, indexed by the message's account list.</summary>
    [JsonPropertyName("preBalances")]
    [JsonRequired]
    public IReadOnlyList<ulong> PreBalances
    {
        get => field!;
        init => field = value ?? throw new JsonException("Transaction metadata must carry pre-balances.");
    }

    /// <summary>Account lamport balances after the transaction, indexed by the message's account list.</summary>
    [JsonPropertyName("postBalances")]
    [JsonRequired]
    public IReadOnlyList<ulong> PostBalances
    {
        get => field!;
        init => field = value ?? throw new JsonException("Transaction metadata must carry post-balances.");
    }

    /// <summary>SPL token balances before the transaction, for the accounts that hold tokens.</summary>
    [JsonPropertyName("preTokenBalances")]
    public IReadOnlyList<TokenBalance>? PreTokenBalances { get; init; }

    /// <summary>SPL token balances after the transaction, for the accounts that hold tokens.</summary>
    [JsonPropertyName("postTokenBalances")]
    public IReadOnlyList<TokenBalance>? PostTokenBalances { get; init; }

    /// <summary>The inner (CPI) instructions invoked, grouped by their top-level instruction; <c>null</c> if the node omitted them.</summary>
    [JsonPropertyName("innerInstructions")]
    public IReadOnlyList<InnerInstructionGroup>? InnerInstructions { get; init; }

    /// <summary>The accounts a versioned transaction loaded from address lookup tables, or <c>null</c> for a legacy transaction.</summary>
    [JsonPropertyName("loadedAddresses")]
    public LoadedAddresses? LoadedAddresses { get; init; }

    /// <summary>The log lines the transaction emitted, if the node returned them.</summary>
    [JsonPropertyName("logMessages")]
    public IReadOnlyList<string>? LogMessages { get; init; }

    /// <summary>The compute units the transaction consumed, if the node reported it.</summary>
    [JsonPropertyName("computeUnitsConsumed")]
    public ulong? ComputeUnitsConsumed { get; init; }

    /// <summary>The transaction cost units, when reported.</summary>
    [JsonPropertyName("costUnits")]
    public ulong? CostUnits { get; init; }

    /// <summary>Data returned by a program, or <c>null</c> when no program set return data.</summary>
    [JsonPropertyName("returnData")]
    public TransactionReturnData? ReturnData { get; init; }

    /// <summary>Rewards and debits recorded while processing the transaction, when reported.</summary>
    [JsonPropertyName("rewards")]
    public IReadOnlyList<Reward>? Rewards { get; init; }

    /// <summary>True when the transaction failed (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };

    /// <summary>The decoded transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonIgnore]
    public TransactionError? Error => TransactionError.Parse(Err);

    /// <inheritdoc/>
    public void OnDeserialized()
    {
        TransactionStatusValidator.Validate(Err, Status);
        RpcCollectionValidator.ValidateOptional(PreTokenBalances, "pre-token balances");
        RpcCollectionValidator.ValidateOptional(PostTokenBalances, "post-token balances");
        RpcCollectionValidator.ValidateOptional(InnerInstructions, "inner-instruction groups");
        RpcCollectionValidator.ValidateOptional(LogMessages, "log messages");
        RpcCollectionValidator.ValidateOptional(Rewards, "rewards");
    }
}

/// <summary>A pre- or post-execution SPL token balance snapshot from a transaction's metadata.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record TokenBalance
{
    /// <summary>The index, into the transaction's account list, of the token account this balance is for.</summary>
    [JsonPropertyName("accountIndex")]
    [JsonRequired]
    public byte AccountIndex { get; init; }

    /// <summary>The token mint.</summary>
    [JsonPropertyName("mint")]
    [JsonRequired]
    public PublicKey Mint { get; init; }

    /// <summary>The token account's owner, if the node reported it.</summary>
    [JsonPropertyName("owner")]
    public PublicKey? Owner { get; init; }

    /// <summary>The token program that owns the account (SPL Token or Token-2022), if the node reported it.</summary>
    [JsonPropertyName("programId")]
    public PublicKey? ProgramId { get; init; }

    /// <summary>The balance, in base units and as a UI amount.</summary>
    [JsonPropertyName("uiTokenAmount")]
    [JsonRequired]
    public TokenAmount UiTokenAmount
    {
        get => field!;
        init => field = value ?? throw new JsonException("A token balance must carry a UI token amount.");
    }
}

/// <summary>The inner (CPI) instructions invoked under one top-level instruction.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record InnerInstructionGroup
{
    /// <summary>The index of the top-level instruction these inner instructions were invoked from.</summary>
    [JsonPropertyName("index")]
    [JsonRequired]
    public byte Index { get; init; }

    /// <summary>The inner instructions, in invocation order.</summary>
    [JsonPropertyName("instructions")]
    [JsonRequired]
    public IReadOnlyList<InnerInstruction> Instructions
    {
        get => field!;
        init
        {
            if (value is null || value.Any(static instruction => instruction is null))
                throw new JsonException("Inner instructions must carry only non-null instructions.");

            field = value;
        }
    }
}

/// <summary>One compiled inner instruction, as returned with base64 transaction encoding.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record InnerInstruction
{
    /// <summary>The index, into the transaction's account list, of the invoked program.</summary>
    [JsonPropertyName("programIdIndex")]
    [JsonRequired]
    public byte ProgramIdIndex { get; init; }

    /// <summary>The indices, into the transaction's account list, of the accounts passed to the instruction.</summary>
    [JsonPropertyName("accounts")]
    [JsonRequired]
    public IReadOnlyList<byte> Accounts
    {
        get => field!;
        init => field = value ?? throw new JsonException("A compiled instruction must carry account indexes.");
    }

    /// <summary>The instruction data, base58-encoded.</summary>
    [JsonPropertyName("data")]
    [JsonRequired]
    public string Data
    {
        get => field!;
        init => field = value ?? throw new JsonException("A compiled instruction must carry data.");
    }

    /// <summary>The CPI stack height at which the instruction ran, if the node reported it.</summary>
    [JsonPropertyName("stackHeight")]
    [JsonRequired]
    public uint? StackHeight { get; init; }
}

/// <summary>The accounts a versioned transaction loaded from address lookup tables.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record LoadedAddresses
{
    /// <summary>The writable accounts loaded from lookup tables.</summary>
    [JsonPropertyName("writable")]
    [JsonRequired]
    public IReadOnlyList<PublicKey> Writable
    {
        get => field!;
        init => field = value ?? throw new JsonException("Loaded addresses must carry a writable list.");
    }

    /// <summary>The read-only accounts loaded from lookup tables.</summary>
    [JsonPropertyName("readonly")]
    [JsonRequired]
    public IReadOnlyList<PublicKey> Readonly
    {
        get => field!;
        init => field = value ?? throw new JsonException("Loaded addresses must carry a read-only list.");
    }
}

internal static class TransactionStatusValidator
{
    internal static void Validate(JsonElement? error, JsonElement status)
    {
        if (status.ValueKind is not JsonValueKind.Object)
            throw new JsonException("Transaction status must be an Ok or Err object.");

        var properties = status.EnumerateObject().ToArray();
        if (properties.Length != 1)
            throw new JsonException("Transaction status must carry exactly one Ok or Err variant.");

        var statusVariant = properties[0];
        var hasError = error is { ValueKind: not JsonValueKind.Null };
        if (statusVariant.NameEquals("Ok"))
        {
            if (statusVariant.Value.ValueKind is not JsonValueKind.Null || hasError)
                throw new JsonException("A successful transaction status must be {\"Ok\":null} with a null err field.");

            return;
        }

        if (!statusVariant.NameEquals("Err") || statusVariant.Value.ValueKind is JsonValueKind.Null || !hasError)
            throw new JsonException("A failed transaction status must carry the same non-null error as the err field.");

        if (statusVariant.Value.GetRawText() != error!.Value.GetRawText())
            throw new JsonException("Transaction status and err must carry the same error value.");
    }
}

internal static class RpcCollectionValidator
{
    internal static void ValidateOptional<T>(IReadOnlyList<T>? values, string fieldName)
        where T : class
    {
        if (values is not null && values.Any(static value => value is null))
            throw new JsonException($"RPC {fieldName} cannot contain null entries.");
    }
}
