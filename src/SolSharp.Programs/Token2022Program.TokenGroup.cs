using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

public static partial class Token2022Program
{
    private static readonly byte[] InitializeTokenGroupDiscriminator = [0x79, 0x71, 0x6c, 0x27, 0x36, 0x33, 0x00, 0x04];
    private static readonly byte[] UpdateTokenGroupMaxSizeDiscriminator = [0x6c, 0x25, 0xab, 0x8f, 0xf8, 0x1e, 0x12, 0x6e];
    private static readonly byte[] UpdateTokenGroupAuthorityDiscriminator = [0xa1, 0x69, 0x58, 0x01, 0xed, 0xdd, 0xd8, 0xcb];
    private static readonly byte[] InitializeTokenGroupMemberDiscriminator = [0x98, 0x20, 0xde, 0xb0, 0xdf, 0xed, 0x74, 0x86];

    /// <summary>Initializes an SPL token-group entry, normally in a Token-2022 mint.</summary>
    /// <param name="group">The writable account that stores the group entry.</param>
    /// <param name="mint">The mint represented by the group entry.</param>
    /// <param name="mintAuthority">The mint authority; signs.</param>
    /// <param name="updateAuthority">The authority allowed to update the group, or <c>null</c> for an immutable group.</param>
    /// <param name="maximumSize">The maximum number of members.</param>
    /// <param name="programId">The program implementing the token-group interface; defaults to Token-2022.</param>
    /// <returns>The initialize-token-group instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="updateAuthority"/> is the all-zero address.</exception>
    public static Instruction InitializeTokenGroup(
        PublicKey group,
        PublicKey mint,
        PublicKey mintAuthority,
        PublicKey? updateAuthority,
        ulong maximumSize,
        PublicKey? programId = null)
    {
        var data = new byte[8 + PublicKey.Length + sizeof(ulong)];
        InitializeTokenGroupDiscriminator.CopyTo(data, 0);
        WriteMaybeNullPublicKey(data.AsSpan(8), updateAuthority, nameof(updateAuthority));
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(8 + PublicKey.Length), maximumSize);
        return new()
        {
            ProgramId = programId ?? ProgramId,
            Accounts =
            [
                AccountMeta.Writable(group),
                AccountMeta.Readonly(mint),
                AccountMeta.ReadonlySigner(mintAuthority)
            ],
            Data = data
        };
    }

    /// <summary>Updates the maximum number of members in an SPL token group.</summary>
    /// <param name="group">The writable group account.</param>
    /// <param name="updateAuthority">The current group update authority; signs.</param>
    /// <param name="maximumSize">The new maximum number of members.</param>
    /// <param name="programId">The program implementing the token-group interface; defaults to Token-2022.</param>
    /// <returns>The update-group-max-size instruction.</returns>
    public static Instruction UpdateTokenGroupMaxSize(
        PublicKey group,
        PublicKey updateAuthority,
        ulong maximumSize,
        PublicKey? programId = null)
    {
        var data = new byte[8 + sizeof(ulong)];
        UpdateTokenGroupMaxSizeDiscriminator.CopyTo(data, 0);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(8), maximumSize);
        return TokenGroupAuthorityInstruction(group, updateAuthority, data, programId);
    }

    /// <summary>Changes or permanently removes an SPL token group's update authority.</summary>
    /// <param name="group">The writable group account.</param>
    /// <param name="currentAuthority">The current group update authority; signs.</param>
    /// <param name="newAuthority">The new authority, or <c>null</c> to make the group immutable.</param>
    /// <param name="programId">The program implementing the token-group interface; defaults to Token-2022.</param>
    /// <returns>The update-group-authority instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="newAuthority"/> is the all-zero address.</exception>
    public static Instruction UpdateTokenGroupAuthority(
        PublicKey group,
        PublicKey currentAuthority,
        PublicKey? newAuthority,
        PublicKey? programId = null)
    {
        var data = new byte[8 + PublicKey.Length];
        UpdateTokenGroupAuthorityDiscriminator.CopyTo(data, 0);
        WriteMaybeNullPublicKey(data.AsSpan(8), newAuthority, nameof(newAuthority));
        return TokenGroupAuthorityInstruction(group, currentAuthority, data, programId);
    }

    /// <summary>Initializes an SPL token-group-member entry and adds it to a group.</summary>
    /// <param name="member">The writable account that stores the member entry.</param>
    /// <param name="memberMint">The mint represented by the member entry.</param>
    /// <param name="memberMintAuthority">The member mint authority; signs.</param>
    /// <param name="group">The writable group account.</param>
    /// <param name="groupUpdateAuthority">The group update authority; signs.</param>
    /// <param name="programId">The program implementing the token-group interface; defaults to Token-2022.</param>
    /// <returns>The initialize-token-group-member instruction.</returns>
    public static Instruction InitializeTokenGroupMember(
        PublicKey member,
        PublicKey memberMint,
        PublicKey memberMintAuthority,
        PublicKey group,
        PublicKey groupUpdateAuthority,
        PublicKey? programId = null)
        => new()
        {
            ProgramId = programId ?? ProgramId,
            Accounts =
            [
                AccountMeta.Writable(member),
                AccountMeta.Readonly(memberMint),
                AccountMeta.ReadonlySigner(memberMintAuthority),
                AccountMeta.Writable(group),
                AccountMeta.ReadonlySigner(groupUpdateAuthority)
            ],
            Data = [.. InitializeTokenGroupMemberDiscriminator]
        };

    private static Instruction TokenGroupAuthorityInstruction(
        PublicKey group,
        PublicKey updateAuthority,
        byte[] data,
        PublicKey? programId)
        => new()
        {
            ProgramId = programId ?? ProgramId,
            Accounts = [AccountMeta.Writable(group), AccountMeta.ReadonlySigner(updateAuthority)],
            Data = data
        };
}
