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
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 2)
            throw new JsonException("Expected base64 binary data as a two-element array.");
        if (root[0].ValueKind != JsonValueKind.String)
            throw new JsonException("Expected base64 binary data as a string.");
        if (root[1].ValueKind != JsonValueKind.String || root[1].GetString() != "base64")
            throw new JsonException("Expected binary data encoding base64.");

        try
        {
            return Convert.FromBase64String(root[0].GetString()!);
        }
        catch (FormatException exception)
        {
            throw new JsonException("Binary data is not valid base64.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(Convert.ToBase64String(value));
        writer.WriteStringValue("base64");
        writer.WriteEndArray();
    }
}
