using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>
/// Reads the <c>parsed</c> view of an instruction. Most programs return a <c>{ "type", "info" }</c> object,
/// but a few (notably spl-memo) return a bare value - the memo string - instead. The bare value is kept on
/// <see cref="ParsedInstructionInfo.Info"/> with an empty <see cref="ParsedInstructionInfo.Type"/>, so the node
/// never makes deserialization throw and nothing is dropped. Inbound only.
/// </summary>
internal sealed class ParsedInstructionInfoJsonConverter : JsonConverter<ParsedInstructionInfo>
{
    public override ParsedInstructionInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.ValueKind is not JsonValueKind.Object)
            return new ParsedInstructionInfo { Info = root.Clone() };

        return new ParsedInstructionInfo
        {
            Type = root.TryGetProperty("type", out var type) ? type.GetString() ?? string.Empty : string.Empty,
            Info = root.TryGetProperty("info", out var info) ? info.Clone() : root.Clone()
        };
    }

    public override void Write(Utf8JsonWriter writer, ParsedInstructionInfo value, JsonSerializerOptions options)
        => throw new NotSupportedException("ParsedInstructionInfo is decoded from node responses and is not serialized.");
}
