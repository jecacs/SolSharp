namespace SolSharp.Rpc.Models.Token2022;

/// <summary>
/// One raw TLV entry of a Token-2022 extended account: the extension type and its value bytes. Use the
/// typed getters on <see cref="TokenExtensionSet"/> for the common extensions, or decode
/// <see cref="Data"/> by hand for the rest.
/// </summary>
public sealed record TokenExtension
{
    /// <summary>The extension type (the TLV tag). Unknown future types are preserved as their numeric value.</summary>
    public required ExtensionType Type { get; init; }

    /// <summary>The extension's raw value bytes (the TLV value).</summary>
    public required byte[] Data { get; init; }
}
