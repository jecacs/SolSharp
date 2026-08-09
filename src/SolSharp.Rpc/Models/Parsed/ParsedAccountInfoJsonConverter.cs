using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>
/// Reads a <c>jsonParsed</c> account, whose <c>data</c> is either a <c>{ program, parsed, space }</c> object
/// (recognized program) or a <c>[base64, "base64"]</c> tuple (unrecognized). Inbound only - these come from
/// node responses and are never serialized back.
/// </summary>
internal sealed class ParsedAccountInfoJsonConverter : JsonConverter<ParsedAccountInfo>
{
    public override ParsedAccountInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var data = root.GetProperty("data");

        string? program = null;
        ParsedInstructionInfo? parsed = null;
        byte[]? rawData = null;
        ulong? space = null;

        if (data.ValueKind is JsonValueKind.Object)
        {
            if (!data.TryGetProperty("program", out var programElement) ||
                programElement.ValueKind is not JsonValueKind.String)
            {
                throw new JsonException("Parsed account data must carry its string program.");
            }

            if (!data.TryGetProperty("parsed", out var parsedElement))
                throw new JsonException("Parsed account data must carry its parsed value.");

            if (!data.TryGetProperty("space", out var dataSpace) ||
                dataSpace.ValueKind is not JsonValueKind.Number ||
                !dataSpace.TryGetUInt64(out var parsedSpace))
            {
                throw new JsonException("Parsed account data must carry its space as a u64 value.");
            }

            program = programElement.GetString();
            parsed = parsedElement.ValueKind is JsonValueKind.Null
                ? null
                : parsedElement.Deserialize(options.GetTypeInfo<ParsedInstructionInfo>());
            space = parsedSpace;
        }
        else if (data.ValueKind is JsonValueKind.Array)
        {
            rawData = AccountInfoJsonConverter.DecodeBase64Tuple(data);
        }
        else
        {
            throw new JsonException("Expected parsed account data as an object or a [data, encoding] array.");
        }

        var topLevelSpace = AccountInfoJsonConverter.ReadOptionalSpace(root);
        space ??= topLevelSpace;

        return new ParsedAccountInfo
        {
            Lamports = root.GetProperty("lamports").GetUInt64(),
            Owner = new PublicKey(root.GetProperty("owner").GetString()!),
            Executable = root.GetProperty("executable").GetBoolean(),
            RentEpoch = root.GetProperty("rentEpoch").GetUInt64(),
            Space = space,
            Program = program,
            Parsed = parsed,
            RawData = rawData
        };
    }

    public override void Write(Utf8JsonWriter writer, ParsedAccountInfo value, JsonSerializerOptions options)
        => throw new NotSupportedException("ParsedAccountInfo is decoded from node responses and is not serialized.");
}
