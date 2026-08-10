using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

public static partial class Token2022Program
{
    private const byte InterestBearingMintExtensionDiscriminator = 33;
    private const byte TransferHookExtensionDiscriminator = 36;
    private const byte MetadataPointerExtensionDiscriminator = 39;
    private const byte GroupPointerExtensionDiscriminator = 40;
    private const byte GroupMemberPointerExtensionDiscriminator = 41;
    private const byte ScaledUiAmountExtensionDiscriminator = 43;
    private const byte PausableExtensionDiscriminator = 44;
    private const byte PermissionedBurnExtensionDiscriminator = 46;

    /// <summary>Initializes interest accrual on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="rateAuthority">The authority allowed to update the rate, or <c>null</c> for an immutable rate.</param>
    /// <param name="rateBasisPoints">The signed annual interest rate in basis points.</param>
    /// <returns>The interest-bearing-mint initialize instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="rateAuthority"/> is the all-zero address.</exception>
    public static Instruction InitializeInterestBearingMint(
        PublicKey mint,
        PublicKey? rateAuthority,
        short rateBasisPoints)
    {
        var data = new byte[2 + PublicKey.Length + sizeof(short)];
        data[0] = InterestBearingMintExtensionDiscriminator;
        WriteMaybeNullPublicKey(data.AsSpan(2), rateAuthority, nameof(rateAuthority));
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(2 + PublicKey.Length), rateBasisPoints);
        return MintExtensionInstruction(mint, data);
    }

    /// <summary>Updates an interest-bearing Token-2022 mint's rate.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="rateAuthority">The rate authority or multisig authority.</param>
    /// <param name="rateBasisPoints">The new signed annual rate in basis points.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The interest-rate update instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction UpdateInterestRate(
        PublicKey mint,
        PublicKey rateAuthority,
        short rateBasisPoints,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var data = new byte[2 + sizeof(short)];
        data[0] = InterestBearingMintExtensionDiscriminator;
        data[1] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(2), rateBasisPoints);
        return AuthorityExtensionInstruction(mint, rateAuthority, data, multisigSigners);
    }

    /// <summary>Initializes a transfer-hook program pointer on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="authority">The pointer authority, or <c>null</c> for an immutable pointer.</param>
    /// <param name="transferHookProgramId">The program invoked during transfers, or <c>null</c> to disable the hook.</param>
    /// <returns>The transfer-hook initialize instruction.</returns>
    /// <exception cref="ArgumentException">An optional address is the all-zero address.</exception>
    public static Instruction InitializeTransferHook(
        PublicKey mint,
        PublicKey? authority,
        PublicKey? transferHookProgramId)
        => InitializePointerExtension(
            mint,
            TransferHookExtensionDiscriminator,
            authority,
            nameof(authority),
            transferHookProgramId,
            nameof(transferHookProgramId));

    /// <summary>Updates a Token-2022 mint's transfer-hook program pointer.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The pointer authority or multisig authority.</param>
    /// <param name="transferHookProgramId">The new hook program, or <c>null</c> to disable the hook.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The transfer-hook update instruction.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="transferHookProgramId"/> is all-zero, or <paramref name="multisigSigners"/> contains
    /// more than 11 accounts.
    /// </exception>
    public static Instruction UpdateTransferHook(
        PublicKey mint,
        PublicKey authority,
        PublicKey? transferHookProgramId,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => UpdatePointerExtension(
            mint,
            TransferHookExtensionDiscriminator,
            authority,
            transferHookProgramId,
            nameof(transferHookProgramId),
            multisigSigners);

    /// <summary>Initializes a token-metadata pointer on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="authority">The pointer authority, or <c>null</c> for an immutable pointer.</param>
    /// <param name="metadataAddress">The account holding metadata, or <c>null</c> when unset.</param>
    /// <returns>The metadata-pointer initialize instruction.</returns>
    /// <exception cref="ArgumentException">An optional address is the all-zero address.</exception>
    public static Instruction InitializeMetadataPointer(
        PublicKey mint,
        PublicKey? authority,
        PublicKey? metadataAddress)
        => InitializePointerExtension(
            mint,
            MetadataPointerExtensionDiscriminator,
            authority,
            nameof(authority),
            metadataAddress,
            nameof(metadataAddress));

    /// <summary>Updates a Token-2022 mint's metadata pointer.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The pointer authority or multisig authority.</param>
    /// <param name="metadataAddress">The new metadata account, or <c>null</c> when unset.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The metadata-pointer update instruction.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="metadataAddress"/> is all-zero, or <paramref name="multisigSigners"/> contains more
    /// than 11 accounts.
    /// </exception>
    public static Instruction UpdateMetadataPointer(
        PublicKey mint,
        PublicKey authority,
        PublicKey? metadataAddress,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => UpdatePointerExtension(
            mint,
            MetadataPointerExtensionDiscriminator,
            authority,
            metadataAddress,
            nameof(metadataAddress),
            multisigSigners);

    /// <summary>Initializes a token-group pointer on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="authority">The pointer authority, or <c>null</c> for an immutable pointer.</param>
    /// <param name="groupAddress">The account holding group configuration, or <c>null</c> when unset.</param>
    /// <returns>The group-pointer initialize instruction.</returns>
    /// <exception cref="ArgumentException">An optional address is the all-zero address.</exception>
    public static Instruction InitializeGroupPointer(
        PublicKey mint,
        PublicKey? authority,
        PublicKey? groupAddress)
        => InitializePointerExtension(
            mint,
            GroupPointerExtensionDiscriminator,
            authority,
            nameof(authority),
            groupAddress,
            nameof(groupAddress));

    /// <summary>Updates a Token-2022 mint's token-group pointer.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The pointer authority or multisig authority.</param>
    /// <param name="groupAddress">The new group account, or <c>null</c> when unset.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The group-pointer update instruction.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="groupAddress"/> is all-zero, or <paramref name="multisigSigners"/> contains more than
    /// 11 accounts.
    /// </exception>
    public static Instruction UpdateGroupPointer(
        PublicKey mint,
        PublicKey authority,
        PublicKey? groupAddress,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => UpdatePointerExtension(
            mint,
            GroupPointerExtensionDiscriminator,
            authority,
            groupAddress,
            nameof(groupAddress),
            multisigSigners);

    /// <summary>Initializes a token-group-member pointer on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="authority">The pointer authority, or <c>null</c> for an immutable pointer.</param>
    /// <param name="memberAddress">The account holding member configuration, or <c>null</c> when unset.</param>
    /// <returns>The group-member-pointer initialize instruction.</returns>
    /// <exception cref="ArgumentException">An optional address is the all-zero address.</exception>
    public static Instruction InitializeGroupMemberPointer(
        PublicKey mint,
        PublicKey? authority,
        PublicKey? memberAddress)
        => InitializePointerExtension(
            mint,
            GroupMemberPointerExtensionDiscriminator,
            authority,
            nameof(authority),
            memberAddress,
            nameof(memberAddress));

    /// <summary>Updates a Token-2022 mint's token-group-member pointer.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The pointer authority or multisig authority.</param>
    /// <param name="memberAddress">The new member account, or <c>null</c> when unset.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The group-member-pointer update instruction.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="memberAddress"/> is all-zero, or <paramref name="multisigSigners"/> contains more than
    /// 11 accounts.
    /// </exception>
    public static Instruction UpdateGroupMemberPointer(
        PublicKey mint,
        PublicKey authority,
        PublicKey? memberAddress,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => UpdatePointerExtension(
            mint,
            GroupMemberPointerExtensionDiscriminator,
            authority,
            memberAddress,
            nameof(memberAddress),
            multisigSigners);

    /// <summary>Initializes scaled UI amounts on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="authority">The multiplier authority, or <c>null</c> for an immutable multiplier.</param>
    /// <param name="multiplier">The initial IEEE-754 multiplier.</param>
    /// <returns>The scaled-UI-amount initialize instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="authority"/> is the all-zero address.</exception>
    public static Instruction InitializeScaledUiAmount(PublicKey mint, PublicKey? authority, double multiplier)
    {
        var data = new byte[2 + PublicKey.Length + sizeof(double)];
        data[0] = ScaledUiAmountExtensionDiscriminator;
        WriteMaybeNullPublicKey(data.AsSpan(2), authority, nameof(authority));
        WriteDouble(data.AsSpan(2 + PublicKey.Length), multiplier);
        return MintExtensionInstruction(mint, data);
    }

    /// <summary>Schedules a new scaled-UI multiplier for a Token-2022 mint.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The multiplier authority or multisig authority.</param>
    /// <param name="multiplier">The new IEEE-754 multiplier.</param>
    /// <param name="effectiveTimestamp">The Unix timestamp at which the multiplier takes effect.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The scaled-UI-amount update instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction UpdateScaledUiAmount(
        PublicKey mint,
        PublicKey authority,
        double multiplier,
        long effectiveTimestamp,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var data = new byte[2 + sizeof(double) + sizeof(long)];
        data[0] = ScaledUiAmountExtensionDiscriminator;
        data[1] = 1;
        WriteDouble(data.AsSpan(2), multiplier);
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(2 + sizeof(double)), effectiveTimestamp);
        return AuthorityExtensionInstruction(mint, authority, data, multisigSigners);
    }

    /// <summary>Initializes the pausable extension on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="authority">The authority allowed to pause and resume the mint.</param>
    /// <returns>The pausable initialize instruction.</returns>
    public static Instruction InitializePausableMint(PublicKey mint, PublicKey authority)
    {
        var data = new byte[2 + PublicKey.Length];
        data[0] = PausableExtensionDiscriminator;
        authority.CopyTo(data.AsSpan(2));
        return MintExtensionInstruction(mint, data);
    }

    /// <summary>Pauses minting, burning, and transferring for a Token-2022 mint.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The pause authority or multisig authority.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The pause instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction PauseMint(
        PublicKey mint,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => AuthorityExtensionInstruction(mint, authority, [PausableExtensionDiscriminator, 1], multisigSigners);

    /// <summary>Resumes minting, burning, and transferring for a Token-2022 mint.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The pause authority or multisig authority.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The resume instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction ResumeMint(
        PublicKey mint,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => AuthorityExtensionInstruction(mint, authority, [PausableExtensionDiscriminator, 2], multisigSigners);

    /// <summary>Initializes the permissioned-burn extension on a Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="authority">The authority whose signature is required for burns.</param>
    /// <returns>The permissioned-burn initialize instruction.</returns>
    public static Instruction InitializePermissionedBurn(PublicKey mint, PublicKey authority)
    {
        var data = new byte[2 + PublicKey.Length];
        data[0] = PermissionedBurnExtensionDiscriminator;
        authority.CopyTo(data.AsSpan(2));
        return MintExtensionInstruction(mint, data);
    }

    /// <summary>Burns tokens with approval from a mint's permissioned-burn authority.</summary>
    /// <param name="account">The writable token account to debit.</param>
    /// <param name="mint">The writable mint.</param>
    /// <param name="permissionedBurnAuthority">The configured permissioned-burn signer.</param>
    /// <param name="owner">The token account owner, delegate, or multisig authority.</param>
    /// <param name="amount">The raw token amount to burn.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct owner.</param>
    /// <returns>The permissioned burn instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction PermissionedBurn(
        PublicKey account,
        PublicKey mint,
        PublicKey permissionedBurnAuthority,
        PublicKey owner,
        ulong amount,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => PermissionedBurnCore(
            account,
            mint,
            permissionedBurnAuthority,
            owner,
            amount,
            decimals: null,
            multisigSigners);

    /// <summary>Burns tokens with permissioned approval while checking the mint's decimals.</summary>
    /// <param name="account">The writable token account to debit.</param>
    /// <param name="mint">The writable mint.</param>
    /// <param name="permissionedBurnAuthority">The configured permissioned-burn signer.</param>
    /// <param name="owner">The token account owner, delegate, or multisig authority.</param>
    /// <param name="amount">The raw token amount to burn.</param>
    /// <param name="decimals">The expected mint decimals.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct owner.</param>
    /// <returns>The checked permissioned burn instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction PermissionedBurnChecked(
        PublicKey account,
        PublicKey mint,
        PublicKey permissionedBurnAuthority,
        PublicKey owner,
        ulong amount,
        byte decimals,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => PermissionedBurnCore(
            account,
            mint,
            permissionedBurnAuthority,
            owner,
            amount,
            decimals,
            multisigSigners);

    private static Instruction InitializePointerExtension(
        PublicKey mint,
        byte outerDiscriminator,
        PublicKey? authority,
        string authorityParameterName,
        PublicKey? address,
        string addressParameterName)
    {
        var data = new byte[2 + (PublicKey.Length * 2)];
        data[0] = outerDiscriminator;
        WriteMaybeNullPublicKey(data.AsSpan(2), authority, authorityParameterName);
        WriteMaybeNullPublicKey(data.AsSpan(2 + PublicKey.Length), address, addressParameterName);
        return MintExtensionInstruction(mint, data);
    }

    private static Instruction UpdatePointerExtension(
        PublicKey mint,
        byte outerDiscriminator,
        PublicKey authority,
        PublicKey? address,
        string addressParameterName,
        IReadOnlyList<PublicKey>? multisigSigners)
    {
        var data = new byte[2 + PublicKey.Length];
        data[0] = outerDiscriminator;
        data[1] = 1;
        WriteMaybeNullPublicKey(data.AsSpan(2), address, addressParameterName);
        return AuthorityExtensionInstruction(mint, authority, data, multisigSigners);
    }

    private static Instruction PermissionedBurnCore(
        PublicKey account,
        PublicKey mint,
        PublicKey permissionedBurnAuthority,
        PublicKey owner,
        ulong amount,
        byte? decimals,
        IReadOnlyList<PublicKey>? multisigSigners)
    {
        var data = new byte[2 + sizeof(ulong) + (decimals is null ? 0 : 1)];
        data[0] = PermissionedBurnExtensionDiscriminator;
        data[1] = decimals is null ? (byte)1 : (byte)2;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(2), amount);
        if (decimals is { } decimalCount)
            data[^1] = decimalCount;

        var instruction = new Instruction
        {
            ProgramId = ProgramId,
            Accounts =
            [
                AccountMeta.Writable(account),
                AccountMeta.Writable(mint),
                AccountMeta.ReadonlySigner(permissionedBurnAuthority),
                AccountMeta.ReadonlySigner(owner)
            ],
            Data = data
        };
        return multisigSigners is { Count: > 0 }
            ? WithMultisigAuthority(instruction, authorityIndex: 3, multisigSigners)
            : instruction;
    }

    private static Instruction MintExtensionInstruction(PublicKey mint, byte[] data)
        => new()
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.Writable(mint)],
            Data = data
        };

    private static void WriteMaybeNullPublicKey(Span<byte> destination, PublicKey? publicKey, string parameterName)
    {
        if (publicKey is null)
        {
            destination[..PublicKey.Length].Clear();
            return;
        }

        if (publicKey.Value == default)
            throw new ArgumentException(
                "The all-zero address is reserved as the wire representation of null for this extension.",
                parameterName);
        publicKey.Value.CopyTo(destination);
    }

    private static void WriteDouble(Span<byte> destination, double value)
        => BinaryPrimitives.WriteInt64LittleEndian(destination, BitConverter.DoubleToInt64Bits(value));
}
