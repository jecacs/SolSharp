using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>
/// Reads and writes <see cref="AccountInfo"/> in the node's shape, where the account data is a
/// <c>[base64String, "base64"]</c> pair rather than a plain value.
/// </summary>
internal sealed class AccountInfoJsonConverter : JsonConverter<AccountInfo>
{
    public override AccountInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        var bytes = DecodeBase64Tuple(root.GetProperty("data"));

        return new AccountInfo
        {
            Lamports = root.GetProperty("lamports").GetUInt64(),
            Owner = new PublicKey(root.GetProperty("owner").GetString()!),
            Executable = root.GetProperty("executable").GetBoolean(),
            RentEpoch = root.GetProperty("rentEpoch").GetUInt64(),
            Space = root.TryGetProperty("space", out var space) && space.ValueKind is JsonValueKind.Number
                ? space.GetUInt64()
                : null,
            Data = bytes
        };
    }

    public override void Write(Utf8JsonWriter writer, AccountInfo value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("lamports", value.Lamports);
        writer.WriteString("owner", value.Owner.ToString());
        writer.WriteBoolean("executable", value.Executable);
        writer.WriteNumber("rentEpoch", value.RentEpoch);
        if (value.Space is { } space)
            writer.WriteNumber("space", space);

        writer.WriteStartArray("data");
        writer.WriteStringValue(Convert.ToBase64String(value.Data));
        writer.WriteStringValue("base64");
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    internal static byte[] DecodeBase64Tuple(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() != 2)
            throw new JsonException("Expected account data as a two-element [data, encoding] array.");
        if (data[0].ValueKind != JsonValueKind.String)
            throw new JsonException("Expected account data as a string.");
        if (data[1].ValueKind != JsonValueKind.String || data[1].GetString() != "base64")
            throw new JsonException("Expected account data encoding base64.");

        try
        {
            return Convert.FromBase64String(data[0].GetString()!);
        }
        catch (FormatException exception)
        {
            throw new JsonException("Account data is not valid base64.", exception);
        }
    }
}
