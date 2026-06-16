using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>
/// Reads and writes <see cref="AccountInfo"/> in the node's shape, where the account data is a
/// <c>[base64String, "base64"]</c> pair rather than a plain value.
/// </summary>
public sealed class AccountInfoJsonConverter : JsonConverter<AccountInfo>
{
    public override AccountInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        var data = root.GetProperty("data");
        var bytes = data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0
            ? Convert.FromBase64String(data[0].GetString() ?? string.Empty)
            : [];

        return new AccountInfo
        {
            Lamports = root.GetProperty("lamports").GetUInt64(),
            Owner = new PublicKey(root.GetProperty("owner").GetString()!),
            Executable = root.GetProperty("executable").GetBoolean(),
            RentEpoch = root.GetProperty("rentEpoch").GetUInt64(),
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

        writer.WriteStartArray("data");
        writer.WriteStringValue(Convert.ToBase64String(value.Data));
        writer.WriteStringValue("base64");
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
