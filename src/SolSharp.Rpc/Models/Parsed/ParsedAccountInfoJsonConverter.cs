using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

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
            if (data.TryGetProperty("program", out var programElement))
                program = programElement.GetString();

            if (data.TryGetProperty("parsed", out var parsedElement) && parsedElement.ValueKind is not JsonValueKind.Null)
                parsed = parsedElement.Deserialize<ParsedInstructionInfo>(options);

            if (data.TryGetProperty("space", out var dataSpace) && dataSpace.ValueKind is JsonValueKind.Number)
                space = dataSpace.GetUInt64();
        }
        else if (data.ValueKind is JsonValueKind.Array && data.GetArrayLength() > 0)
        {
            rawData = Convert.FromBase64String(data[0].GetString() ?? string.Empty);
        }

        if (space is null && root.TryGetProperty("space", out var topSpace) && topSpace.ValueKind is JsonValueKind.Number)
            space = topSpace.GetUInt64();

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
