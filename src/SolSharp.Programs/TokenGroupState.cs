using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>The fixed-size value stored by the SPL token-group interface for a group.</summary>
public sealed record TokenGroupState
{
    /// <summary>The serialized value length, excluding its surrounding Token-2022 or generic TLV header.</summary>
    public const int Length = (PublicKey.Length * 2) + (sizeof(ulong) * 2);

    /// <summary>The authority allowed to update the group, or <c>null</c> when the group is immutable.</summary>
    public required PublicKey? UpdateAuthority { get; init; }

    /// <summary>The mint represented by the group.</summary>
    public required PublicKey Mint { get; init; }

    /// <summary>The current number of members.</summary>
    public required ulong Size { get; init; }

    /// <summary>The maximum number of members.</summary>
    public required ulong MaximumSize { get; init; }

    /// <summary>Decodes an exact SPL token-group value.</summary>
    /// <param name="data">The value bytes, without a TLV header.</param>
    /// <returns>The decoded group, or <c>null</c> when <paramref name="data"/> does not have the exact layout length.</returns>
    public static TokenGroupState? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length != Length)
            return null;

        var authority = new PublicKey(data[..PublicKey.Length]);
        return new TokenGroupState
        {
            UpdateAuthority = authority == default ? null : authority,
            Mint = new PublicKey(data.Slice(PublicKey.Length, PublicKey.Length)),
            Size = BinaryPrimitives.ReadUInt64LittleEndian(data[(PublicKey.Length * 2)..]),
            MaximumSize = BinaryPrimitives.ReadUInt64LittleEndian(data[((PublicKey.Length * 2) + sizeof(ulong))..])
        };
    }
}

/// <summary>The fixed-size value stored by the SPL token-group interface for a group member.</summary>
public sealed record TokenGroupMemberState
{
    /// <summary>The serialized value length, excluding its surrounding Token-2022 or generic TLV header.</summary>
    public const int Length = (PublicKey.Length * 2) + sizeof(ulong);

    /// <summary>The mint represented by this member entry.</summary>
    public required PublicKey Mint { get; init; }

    /// <summary>The group to which this member belongs.</summary>
    public required PublicKey Group { get; init; }

    /// <summary>The member's one-based sequence number within the group.</summary>
    public required ulong MemberNumber { get; init; }

    /// <summary>Decodes an exact SPL token-group-member value.</summary>
    /// <param name="data">The value bytes, without a TLV header.</param>
    /// <returns>The decoded member, or <c>null</c> when <paramref name="data"/> does not have the exact layout length.</returns>
    public static TokenGroupMemberState? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length != Length)
            return null;

        return new TokenGroupMemberState
        {
            Mint = new PublicKey(data[..PublicKey.Length]),
            Group = new PublicKey(data.Slice(PublicKey.Length, PublicKey.Length)),
            MemberNumber = BinaryPrimitives.ReadUInt64LittleEndian(data[(PublicKey.Length * 2)..])
        };
    }
}
