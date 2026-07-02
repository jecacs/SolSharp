using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models.Token2022;

/// <summary>Shared readers for the Token-2022 extension layouts.</summary>
internal static class Token2022Layout
{
    /// <summary>
    /// Reads an <c>OptionalNonZeroPubkey</c>: 32 bytes that mean "none" when they are all zero (unlike a
    /// <c>COption</c>, there is no tag byte).
    /// </summary>
    public static PublicKey? ReadOptionalKey(ReadOnlySpan<byte> bytes)
    {
        var key = new PublicKey(bytes[..PublicKey.Length]);
        return key == default ? null : key;
    }
}
