using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Converters;

/// <summary>
/// Reads and writes <see cref="PublicKey"/> as its base58 string, the form Solana's JSON-RPC uses.
/// Public because a source-generated <c>JsonSerializerContext</c> can only materialize a
/// converter-attributed type when it can construct the converter (SYSLIB1220 otherwise), and consumers
/// register models carrying <see cref="PublicKey"/> properties in their own contexts.
/// </summary>
public sealed class PublicKeyJsonConverter : JsonConverter<PublicKey>
{
    /// <inheritdoc/>
    public override PublicKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (PublicKey.TryParse(text, out var key))
            return key;

        throw new JsonException($"Invalid public key: '{text}'.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, PublicKey value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
