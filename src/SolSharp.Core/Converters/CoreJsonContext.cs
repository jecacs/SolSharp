using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Converters;

/// <summary>
/// Source-generated metadata for the Core wire primitives - the resolver behind
/// <see cref="SolanaJsonSerializer.Options"/>. The types carry their wire form via
/// <see cref="JsonConverterAttribute"/>, so this context exists to make serialization
/// reflection-free (Native AOT safe) rather than to define any mapping. It is public so consumers can
/// chain it into their own source-generated resolvers (via <c>JsonTypeInfoResolver.Combine</c>) instead
/// of re-registering the primitives themselves.
/// </summary>
[JsonSerializable(typeof(Commitment))]
[JsonSerializable(typeof(Commitment?))]
[JsonSerializable(typeof(PublicKey))]
[JsonSerializable(typeof(PublicKey[]))]
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public sealed partial class CoreJsonContext : JsonSerializerContext;
