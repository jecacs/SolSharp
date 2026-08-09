using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>
/// Reads a <c>jsonParsed</c> transaction from the node's shape, where the signatures and message sit under a
/// nested <c>transaction</c> object and the slot, block time, and metadata sit alongside it. Inbound only -
/// these come from node responses and are never serialized back.
/// </summary>
internal sealed class ParsedTransactionJsonConverter : JsonConverter<ParsedTransaction>
{
    public override ParsedTransaction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind is not JsonValueKind.Object ||
            !root.TryGetProperty("transaction", out var transaction) ||
            transaction.ValueKind is not JsonValueKind.Object)
        {
            throw new JsonException("A parsed transaction must carry a non-null transaction object.");
        }

        if (!transaction.TryGetProperty("signatures", out var signatures) ||
            signatures.ValueKind is not JsonValueKind.Array)
        {
            throw new JsonException("A parsed transaction must carry a non-null signatures array.");
        }

        var parsedSignatures = signatures.Deserialize(options.GetTypeInfo<IReadOnlyList<string>>());
        if (parsedSignatures is null || parsedSignatures.Any(static signature => signature is null))
            throw new JsonException("A parsed transaction must carry only non-null signatures.");

        if (!transaction.TryGetProperty("message", out var message) ||
            message.ValueKind is not JsonValueKind.Object)
        {
            throw new JsonException("A parsed transaction must carry a non-null message object.");
        }

        var parsedMessage = message.Deserialize(options.GetTypeInfo<ParsedMessage>())
            ?? throw new JsonException("A parsed transaction must carry a non-null message object.");

        if (!root.TryGetProperty("meta", out var meta))
            throw new JsonException("A parsed transaction must carry a metadata member.");

        ParsedTransactionMeta? parsedMeta = null;
        if (meta.ValueKind is not JsonValueKind.Null)
        {
            if (meta.ValueKind is not JsonValueKind.Object)
                throw new JsonException("Parsed transaction metadata must be an object or null.");

            parsedMeta = meta.Deserialize(options.GetTypeInfo<ParsedTransactionMeta>())
                ?? throw new JsonException("Parsed transaction metadata must be an object or null.");
        }

        var hasSlot = root.TryGetProperty("slot", out var slot);
        var hasBlockTime = root.TryGetProperty("blockTime", out var blockTime);
        if (hasSlot != hasBlockTime)
            throw new JsonException("A confirmed parsed transaction must carry both slot and block time.");

        ulong? parsedSlot = null;
        if (hasSlot)
        {
            if (slot.ValueKind is not JsonValueKind.Number || !slot.TryGetUInt64(out var slotValue))
                throw new JsonException("A parsed transaction slot must be an unsigned 64-bit integer.");

            parsedSlot = slotValue;
        }

        long? parsedBlockTime = null;
        if (hasBlockTime && blockTime.ValueKind is not JsonValueKind.Null)
        {
            if (blockTime.ValueKind is not JsonValueKind.Number || !blockTime.TryGetInt64(out var blockTimeValue))
                throw new JsonException("A parsed transaction block time must be a signed 64-bit integer or null.");

            parsedBlockTime = blockTimeValue;
        }

        uint? parsedTransactionIndex = null;
        if (root.TryGetProperty("transactionIndex", out var transactionIndex))
        {
            if (transactionIndex.ValueKind is not JsonValueKind.Number ||
                !transactionIndex.TryGetUInt32(out var transactionIndexValue))
            {
                throw new JsonException("A parsed transaction index must be an unsigned 32-bit integer.");
            }

            parsedTransactionIndex = transactionIndexValue;
        }

        return new ParsedTransaction
        {
            Signatures = parsedSignatures,
            Message = parsedMessage,
            Meta = parsedMeta,
            Slot = parsedSlot,
            BlockTime = parsedBlockTime,
            TransactionIndex = parsedTransactionIndex,
            Version = root.TryGetProperty("version", out var version) && version.ValueKind is not JsonValueKind.Null
                ? version.Deserialize(options.GetTypeInfo<RpcTransactionVersion>())
                : null
        };
    }

    public override void Write(Utf8JsonWriter writer, ParsedTransaction value, JsonSerializerOptions options)
        => throw new NotSupportedException("ParsedTransaction is decoded from node responses and is not serialized.");
}
