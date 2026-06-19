using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>
/// Reads and writes a <c>[base64String, "base64"]</c> pair - the shape the node uses for binary fields such
/// as a transaction's wire bytes - as a plain <see cref="byte"/> array.
/// </summary>
internal sealed class Base64TupleJsonConverter : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0
            ? Convert.FromBase64String(root[0].GetString() ?? string.Empty)
            : null;
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(Convert.ToBase64String(value));
        writer.WriteStringValue("base64");
        writer.WriteEndArray();
    }
}
