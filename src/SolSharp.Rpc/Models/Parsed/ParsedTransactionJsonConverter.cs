using System.Text.Json;
using System.Text.Json.Serialization;

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
        var transaction = root.GetProperty("transaction");

        return new ParsedTransaction
        {
            Signatures = transaction.TryGetProperty("signatures", out var signatures)
                ? signatures.Deserialize<IReadOnlyList<string>>(options) ?? []
                : [],
            Message = transaction.GetProperty("message").Deserialize<ParsedMessage>(options) ?? new ParsedMessage(),
            Meta = root.TryGetProperty("meta", out var meta) && meta.ValueKind is not JsonValueKind.Null
                ? meta.Deserialize<ParsedTransactionMeta>(options)
                : null,
            Slot = root.TryGetProperty("slot", out var slot) && slot.ValueKind is JsonValueKind.Number
                ? slot.GetUInt64()
                : null,
            BlockTime = root.TryGetProperty("blockTime", out var blockTime) && blockTime.ValueKind is JsonValueKind.Number
                ? blockTime.GetInt64()
                : null
        };
    }

    public override void Write(Utf8JsonWriter writer, ParsedTransaction value, JsonSerializerOptions options)
        => throw new NotSupportedException("ParsedTransaction is decoded from node responses and is not serialized.");
}
