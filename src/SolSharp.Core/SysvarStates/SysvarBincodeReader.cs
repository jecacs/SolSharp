using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.SysvarStates;

internal ref struct SysvarBincodeReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;
    private int _offset;

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

    public byte ReadByte() => ReadBytes(sizeof(byte))[0];

    public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(sizeof(ulong)));

    public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(sizeof(long)));

    public UInt128 ReadUInt128() => BinaryPrimitives.ReadUInt128LittleEndian(ReadBytes(16));

    public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadInt64());

    public Hash ReadHash() => new(ReadBytes(Hash.Length));

    public int ReadBoundedCount(int maximum, string collectionName)
    {
        var count = ReadUInt64();
        if (count > (ulong)maximum)
            throw Invalid($"{collectionName} count {count} exceeds the maximum of {maximum}.");

        return (int)count;
    }

    public readonly void EnsureEnd()
    {
        if (_offset != _data.Length)
            throw Invalid($"Account data has {_data.Length - _offset} trailing bytes.");
    }

    private ReadOnlySpan<byte> ReadBytes(int length)
    {
        if (length > _data.Length - _offset)
            throw Invalid("Account data is truncated.");

        var result = _data.Slice(_offset, length);
        _offset += length;
        return result;
    }

    private static ArgumentException Invalid(string message) => new(message);
}
