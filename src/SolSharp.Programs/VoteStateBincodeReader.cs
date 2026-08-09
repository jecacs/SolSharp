using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

internal ref struct VoteStateBincodeReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;
    private int _offset;

    public byte ReadByte() => ReadBytes(sizeof(byte))[0];

    public bool ReadBool()
    {
        var value = ReadByte();
        return value switch
        {
            0 => false,
            1 => true,
            _ => throw Invalid("Boolean values must use the canonical 0 or 1 encoding.")
        };
    }

    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(sizeof(ushort)));

    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(sizeof(uint)));

    public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(sizeof(ulong)));

    public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(sizeof(long)));

    public PublicKey ReadPublicKey() => new(ReadBytes(PublicKey.Length));

    public ReadOnlyMemory<byte>? ReadOptionalBlsPublicKey()
    {
        var tag = ReadByte();
        return tag switch
        {
            0 => null,
            1 => ReadBytes(VoteStateVersions.BlsPublicKeyLength).ToArray(),
            _ => throw Invalid("The optional BLS public-key tag must be 0 or 1.")
        };
    }

    public ulong? ReadOptionalUInt64()
    {
        var tag = ReadByte();
        return tag switch
        {
            0 => null,
            1 => ReadUInt64(),
            _ => throw Invalid("The optional slot tag must be 0 or 1.")
        };
    }

    public int ReadBoundedCount(int maximum, string collectionName)
    {
        var count = ReadUInt64();
        if (count > (ulong)maximum)
            throw Invalid($"{collectionName} count {count} exceeds the maximum of {maximum}.");

        return (int)count;
    }

    private ReadOnlySpan<byte> ReadBytes(int length)
    {
        if (length > _data.Length - _offset)
            throw Invalid("Vote account data is truncated.");

        var result = _data.Slice(_offset, length);
        _offset += length;
        return result;
    }

    private static ArgumentException Invalid(string message) => new(message);
}
