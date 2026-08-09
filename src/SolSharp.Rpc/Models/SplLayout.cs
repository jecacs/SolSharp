using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>
/// Helpers for the SPL Token "Pack" account layout, where an optional field (<c>COption</c>) is a 4-byte
/// little-endian tag (1 = present) followed by an always-present value that is only valid when the tag is 1.
/// </summary>
internal static class SplLayout
{
    public static bool TryReadCOptionPublicKey(ReadOnlySpan<byte> data, int offset, out PublicKey? value)
    {
        value = null;
        if (offset < 0 || data.Length - offset < sizeof(uint) + PublicKey.Length)
            return false;

        switch (BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]))
        {
            case 0:
                return true;
            case 1:
                value = new PublicKey(data.Slice(offset + sizeof(uint), PublicKey.Length));
                return true;
            default:
                return false;
        }
    }

    public static bool TryReadCOptionU64(ReadOnlySpan<byte> data, int offset, out ulong? value)
    {
        value = null;
        if (offset < 0 || data.Length - offset < sizeof(uint) + sizeof(ulong))
            return false;

        switch (BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]))
        {
            case 0:
                return true;
            case 1:
                value = BinaryPrimitives.ReadUInt64LittleEndian(data[(offset + sizeof(uint))..]);
                return true;
            default:
                return false;
        }
    }
}
