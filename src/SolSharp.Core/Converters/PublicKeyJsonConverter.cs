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
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a public-key string, got {reader.TokenType}.");

        // Reject oversized JSON tokens before GetString creates a potentially huge UTF-16 allocation.
        // A valid base58 character is one ASCII byte, or at most six bytes when written as \uXXXX.
        var rawLength = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
        var maximumRawLength = reader.ValueIsEscaped ? PublicKey.MaxBase58Length * 6L : PublicKey.MaxBase58Length;
        if (rawLength > maximumRawLength)
            throw new JsonException("Invalid public key base58 value.");

        var text = reader.GetString();
        if (PublicKey.TryParse(text, out var key))
            return key;

        throw new JsonException("Invalid public key base58 value.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, PublicKey value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
