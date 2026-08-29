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

        var bytes = DecodeBase64Tuple(ReadRequiredProperty(root, "data"));

        return new()
        {
            Lamports = ReadRequiredUInt64(root, "lamports"),
            Owner = ReadRequiredPublicKey(root, "owner"),
            Executable = ReadRequiredBoolean(root, "executable"),
            RentEpoch = ReadRequiredUInt64(root, "rentEpoch"),
            Space = ReadOptionalSpace(root),
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

    internal static ulong? ReadOptionalSpace(JsonElement account)
    {
        if (!account.TryGetProperty("space", out var space) || space.ValueKind is JsonValueKind.Null)
            return null;

        if (space.ValueKind is JsonValueKind.Number && space.TryGetUInt64(out var value))
            return value;

        throw new JsonException("Account space must be null or a u64 value.");
    }

    internal static bool ReadRequiredBoolean(JsonElement value, string propertyName)
    {
        var property = ReadRequiredProperty(value, propertyName);
        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return property.GetBoolean();

        throw new JsonException($"Account {propertyName} must be a boolean value.");
    }

    internal static JsonElement ReadRequiredProperty(JsonElement value, string propertyName)
    {
        if (value.ValueKind is JsonValueKind.Object && value.TryGetProperty(propertyName, out var property))
            return property;

        throw new JsonException($"Account is missing mandatory property {propertyName}.");
    }

    internal static PublicKey ReadRequiredPublicKey(JsonElement value, string propertyName)
    {
        var property = ReadRequiredProperty(value, propertyName);
        if (property.ValueKind is JsonValueKind.String &&
            PublicKey.TryParse(property.GetString(), out var publicKey))
        {
            return publicKey;
        }

        throw new JsonException($"Account {propertyName} must be a valid public key.");
    }

    internal static ulong ReadRequiredUInt64(JsonElement value, string propertyName)
    {
        var property = ReadRequiredProperty(value, propertyName);
        if (property.ValueKind is JsonValueKind.Number && property.TryGetUInt64(out var number))
            return number;

        throw new JsonException($"Account {propertyName} must be a u64 value.");
    }
}
