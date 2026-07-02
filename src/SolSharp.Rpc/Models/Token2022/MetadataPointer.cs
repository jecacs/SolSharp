using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models.Token2022;

/// <summary>
/// The mint's <see cref="ExtensionType.MetadataPointer"/> extension: where the mint's token metadata lives.
/// When <see cref="MetadataAddress"/> is the mint itself, the metadata is in the mint's own
/// <see cref="ExtensionType.TokenMetadata"/> extension.
/// </summary>
public sealed record MetadataPointer
{
    /// <summary>The serialized size of the extension value, in bytes (64).</summary>
    public const int Length = 64;

    /// <summary>The authority allowed to move the pointer, or <c>null</c> if it is immutable.</summary>
    public required PublicKey? Authority { get; init; }

    /// <summary>The account holding the metadata, or <c>null</c> when unset.</summary>
    public required PublicKey? MetadataAddress { get; init; }

    internal static MetadataPointer Decode(ReadOnlySpan<byte> data) => new()
    {
        Authority = Token2022Layout.ReadOptionalKey(data),
        MetadataAddress = Token2022Layout.ReadOptionalKey(data[32..])
    };
}
