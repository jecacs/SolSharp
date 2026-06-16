using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Encoding;

/// <summary>
/// A forward-only reader for Borsh-encoded data - the format Anchor and many Solana programs use for
/// account state and instruction arguments. Reads little-endian integers, bools, 32-byte public keys,
/// fixed and length-prefixed byte sequences, UTF-8 strings, and Option / Vec prefixes. Every read is
/// bounds-checked and advances the cursor.
/// </summary>
public ref struct BorshReader
{
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>Creates a reader positioned at the start of <paramref name="data"/>.</summary>
    /// <param name="data">The Borsh-encoded bytes.</param>
    public BorshReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        Position = 0;
    }

    /// <summary>The number of bytes consumed so far.</summary>
    public int Position { get; private set; }

    /// <summary>The number of bytes left to read.</summary>
    public readonly int Remaining => _data.Length - Position;

    /// <summary>Reads a <see cref="byte"/> (u8).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public byte ReadU8() => Take(1)[0];

    /// <summary>Reads an <see cref="sbyte"/> (i8).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public sbyte ReadI8() => (sbyte)Take(1)[0];

    /// <summary>Reads a little-endian <see cref="ushort"/> (u16).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public ushort ReadU16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));

    /// <summary>Reads a little-endian <see cref="short"/> (i16).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public short ReadI16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));

    /// <summary>Reads a little-endian <see cref="uint"/> (u32).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public uint ReadU32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

    /// <summary>Reads a little-endian <see cref="int"/> (i32).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public int ReadI32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));

    /// <summary>Reads a little-endian <see cref="ulong"/> (u64).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public ulong ReadU64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));

    /// <summary>Reads a little-endian <see cref="long"/> (i64).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public long ReadI64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));

    /// <summary>Reads a little-endian <see cref="UInt128"/> (u128).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public UInt128 ReadU128() => BinaryPrimitives.ReadUInt128LittleEndian(Take(16));

    /// <summary>Reads a little-endian <see cref="Int128"/> (i128).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public Int128 ReadI128() => BinaryPrimitives.ReadInt128LittleEndian(Take(16));

    /// <summary>Reads a Borsh bool: a single byte, where any non-zero value is <c>true</c>.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public bool ReadBool() => Take(1)[0] != 0;

    /// <summary>Reads a 32-byte <see cref="PublicKey"/>.</summary>
    /// <returns>The public key.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public PublicKey ReadPublicKey() => new(Take(PublicKey.Length));

    /// <summary>Reads a length-prefixed UTF-8 string (a u32 length, then that many bytes).</summary>
    /// <returns>The decoded string.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public string ReadString() => System.Text.Encoding.UTF8.GetString(Take(ReadLength()));

    /// <summary>Reads <paramref name="count"/> raw bytes without copying.</summary>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>A view over the bytes read.</returns>
    /// <exception cref="FormatException"><paramref name="count"/> is negative or exceeds the remaining bytes.</exception>
    public ReadOnlySpan<byte> ReadBytes(int count) => Take(count);

    /// <summary>Reads a length-prefixed byte vector (a u32 length, then that many bytes) into a new array.</summary>
    /// <returns>The bytes read.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public byte[] ReadByteVector() => Take(ReadLength()).ToArray();

    /// <summary>
    /// Reads a u32 length prefix - as used by Borsh Vec and String - returning it as an <see cref="int"/>.
    /// Read that many elements (or bytes) next.
    /// </summary>
    /// <returns>The length.</returns>
    /// <exception cref="FormatException">The length does not fit in an <see cref="int"/>, or there are not enough bytes left.</exception>
    public int ReadLength()
    {
        var length = ReadU32();
        if (length > int.MaxValue)
            throw new FormatException($"Borsh length {length} does not fit in a 32-bit signed integer.");

        return (int)length;
    }

    /// <summary>Reads a Borsh Option tag: one byte, <c>0</c> for None and non-zero for Some. Read the value next when this returns <c>true</c>.</summary>
    /// <returns><c>true</c> if a value follows (Some); <c>false</c> for None.</returns>
    /// <exception cref="FormatException">There are not enough bytes left.</exception>
    public bool ReadOption() => Take(1)[0] != 0;

    /// <summary>Skips <paramref name="count"/> bytes.</summary>
    /// <param name="count">The number of bytes to skip.</param>
    /// <exception cref="FormatException"><paramref name="count"/> is negative or exceeds the remaining bytes.</exception>
    public void Skip(int count) => Take(count);

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count < 0 || count > Remaining)
            throw new FormatException($"Borsh read of {count} bytes exceeds the remaining {Remaining} bytes.");

        var slice = _data.Slice(Position, count);
        Position += count;
        return slice;
    }
}
