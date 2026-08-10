using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>Reads and writes the untagged upstream account-data union.</summary>
public sealed class RpcAccountDataJsonConverter : JsonConverter<RpcAccountData>
{
    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <inheritdoc/>
    public override RpcAccountData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var data = document.RootElement;

        if (data.ValueKind is JsonValueKind.String)
            return new RpcAccountData.LegacyBinary(data.GetString()!);

        if (data.ValueKind is JsonValueKind.Array)
            return ReadEncoded(data);

        if (data.ValueKind is JsonValueKind.Object)
            return ReadParsed(data);

        throw new JsonException("Expected account data as a legacy string, an encoded tuple, or a parsed object.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, RpcAccountData value, JsonSerializerOptions options)
    {
        if (value is null)
            throw new JsonException("Account data cannot be null.");

        switch (value)
        {
            case RpcAccountData.LegacyBinary legacy:
                writer.WriteStringValue(legacy.EncodedData);
                return;
            case RpcAccountData.Encoded encoded:
                writer.WriteStartArray();
                writer.WriteStringValue(encoded.EncodedData);
                writer.WriteStringValue(encoded.Encoding.WireName);
                writer.WriteEndArray();
                return;
            case RpcAccountData.Parsed parsed:
                writer.WriteStartObject();
                writer.WriteString("program", parsed.Program);
                writer.WritePropertyName("parsed");
                parsed.Value.WriteTo(writer);
                writer.WriteNumber("space", parsed.Space);
                writer.WriteEndObject();
                return;
            default:
                throw new JsonException($"Unsupported account-data branch {value.GetType().FullName}.");
        }
    }

    private static RpcAccountData.Encoded ReadEncoded(JsonElement data)
    {
        if (data.GetArrayLength() != 2 ||
            data[0].ValueKind is not JsonValueKind.String ||
            data[1].ValueKind is not JsonValueKind.String)
        {
            throw new JsonException("Expected account data as a two-string [data, encoding] tuple.");
        }

        var wireEncoding = data[1].GetString();
        if (!RpcWireNames.TryAccountEncoding(wireEncoding, out var encoding))
            throw new JsonException($"Unknown account data encoding '{wireEncoding}'.");

        return new(data[0].GetString()!, encoding);
    }

    private static RpcAccountData.Parsed ReadParsed(JsonElement data)
    {
        if (!data.TryGetProperty("program", out var program) || program.ValueKind is not JsonValueKind.String ||
            !data.TryGetProperty("parsed", out var parsed) ||
            !data.TryGetProperty("space", out var space) || space.ValueKind is not JsonValueKind.Number ||
            !space.TryGetUInt64(out var parsedSpace))
        {
            throw new JsonException("Expected parsed account data with program, parsed, and unsigned space fields.");
        }

        return new(program.GetString()!, parsed.Clone(), parsedSpace);
    }
}
