using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>
/// Helpers for the SPL Token "Pack" account layout, where an optional field (<c>COption</c>) is a 4-byte
/// little-endian tag (1 = present) followed by an always-present value that is only valid when the tag is 1.
/// </summary>
internal static class SplLayout
{
    public static PublicKey? ReadCOptionPublicKey(ReadOnlySpan<byte> data, int offset)
        => BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]) == 1
            ? new PublicKey(data.Slice(offset + sizeof(uint), PublicKey.Length))
            : null;

    public static ulong? ReadCOptionU64(ReadOnlySpan<byte> data, int offset)
        => BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]) == 1
            ? BinaryPrimitives.ReadUInt64LittleEndian(data[(offset + sizeof(uint))..])
            : null;
}
