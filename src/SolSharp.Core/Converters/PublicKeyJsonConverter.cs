using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Converters;

/// <summary>
/// Reads and writes <see cref="PublicKey"/> as its base58 string, the form Solana's JSON-RPC uses.
/// </summary>
internal sealed class PublicKeyJsonConverter : JsonConverter<PublicKey>
{
    public override PublicKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (PublicKey.TryParse(text, out var key))
            return key;

        throw new JsonException($"Invalid public key: '{text}'.");
    }

    public override void Write(Utf8JsonWriter writer, PublicKey value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
