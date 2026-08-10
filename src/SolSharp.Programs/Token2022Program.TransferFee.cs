using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

public static partial class Token2022Program
{
    private const byte TransferFeeExtensionDiscriminator = 26;

    /// <summary>Initializes transfer-fee configuration on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="transferFeeConfigAuthority">The authority allowed to update fees, or <c>null</c> for immutable fees.</param>
    /// <param name="withdrawWithheldAuthority">The authority allowed to withdraw withheld fees, or <c>null</c>.</param>
    /// <param name="basisPoints">The transfer fee in basis points.</param>
    /// <param name="maximumFee">The maximum fee in raw token units.</param>
    /// <returns>The transfer-fee-config initialize instruction.</returns>
    public static Instruction InitializeTransferFeeConfig(
        PublicKey mint,
        PublicKey? transferFeeConfigAuthority,
        PublicKey? withdrawWithheldAuthority,
        ushort basisPoints,
        ulong maximumFee)
    {
        var data = new List<byte> { TransferFeeExtensionDiscriminator, 0 };
        AppendOptionalPublicKey(data, transferFeeConfigAuthority);
        AppendOptionalPublicKey(data, withdrawWithheldAuthority);
        Span<byte> numbers = stackalloc byte[sizeof(ushort) + sizeof(ulong)];
        BinaryPrimitives.WriteUInt16LittleEndian(numbers, basisPoints);
        BinaryPrimitives.WriteUInt64LittleEndian(numbers[sizeof(ushort)..], maximumFee);
        data.AddRange(numbers.ToArray());
        return MintExtensionInstruction(mint, [.. data]);
    }

    /// <summary>Transfers tokens while checking the mint decimals and expected transfer fee.</summary>
    /// <param name="source">The writable source token account.</param>
    /// <param name="mint">The readonly mint.</param>
    /// <param name="destination">The writable destination token account.</param>
    /// <param name="authority">The source authority or multisig authority.</param>
    /// <param name="amount">The raw token amount to transfer.</param>
    /// <param name="decimals">The expected mint decimals.</param>
    /// <param name="fee">The expected fee in raw token units.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The transferCheckedWithFee instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction TransferCheckedWithFee(
        PublicKey source,
        PublicKey mint,
        PublicKey destination,
        PublicKey authority,
        ulong amount,
        byte decimals,
        ulong fee,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var data = new byte[2 + sizeof(ulong) + sizeof(byte) + sizeof(ulong)];
        data[0] = TransferFeeExtensionDiscriminator;
        data[1] = 1;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(2), amount);
        data[2 + sizeof(ulong)] = decimals;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(3 + sizeof(ulong)), fee);
        var instruction = new Instruction
        {
            ProgramId = ProgramId,
            Accounts =
            [
                AccountMeta.Writable(source),
                AccountMeta.Readonly(mint),
                AccountMeta.Writable(destination),
                AccountMeta.ReadonlySigner(authority)
            ],
            Data = data
        };
        return multisigSigners is { Count: > 0 }
            ? WithMultisigAuthority(instruction, authorityIndex: 3, multisigSigners)
            : instruction;
    }

    /// <summary>Withdraws all transfer fees withheld on a Token-2022 mint to a token account.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="destination">The writable fee-receiver token account.</param>
    /// <param name="authority">The withdraw-withheld authority or multisig authority.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The withdraw-withheld-from-mint instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction WithdrawWithheldTokensFromMint(
        PublicKey mint,
        PublicKey destination,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var instruction = new Instruction
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.Writable(mint), AccountMeta.Writable(destination), AccountMeta.ReadonlySigner(authority)],
            Data = [TransferFeeExtensionDiscriminator, 2]
        };
        return multisigSigners is { Count: > 0 }
            ? WithMultisigAuthority(instruction, authorityIndex: 2, multisigSigners)
            : instruction;
    }

    /// <summary>Withdraws fees withheld across Token-2022 accounts into one destination account.</summary>
    /// <param name="mint">The readonly mint.</param>
    /// <param name="destination">The writable fee-receiver token account.</param>
    /// <param name="authority">The withdraw-withheld authority or multisig authority.</param>
    /// <param name="sources">The writable source token accounts to harvest.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The withdraw-withheld-from-accounts instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="sources"/> has more than 255 accounts, or <paramref name="multisigSigners"/> has more
    /// than 11 accounts.
    /// </exception>
    public static Instruction WithdrawWithheldTokensFromAccounts(
        PublicKey mint,
        PublicKey destination,
        PublicKey authority,
        IReadOnlyList<PublicKey> sources,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count > byte.MaxValue)
            throw new ArgumentException("At most 255 source accounts fit in this instruction.", nameof(sources));

        var accounts = new List<AccountMeta>(3 + sources.Count + (multisigSigners?.Count ?? 0))
        {
            AccountMeta.Readonly(mint),
            AccountMeta.Writable(destination)
        };
        AppendAuthority(accounts, authority, multisigSigners);
        for (var i = 0; i < sources.Count; i++)
            accounts.Add(AccountMeta.Writable(sources[i]));

        return new()
        {
            ProgramId = ProgramId,
            Accounts = accounts,
            Data = [TransferFeeExtensionDiscriminator, 3, (byte)sources.Count]
        };
    }

    /// <summary>Permissionlessly harvests withheld fees from token accounts into their Token-2022 mint.</summary>
    /// <param name="mint">The writable mint receiving withheld fees.</param>
    /// <param name="sources">The writable token accounts to harvest.</param>
    /// <returns>The harvest-withheld-to-mint instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <c>null</c>.</exception>
    public static Instruction HarvestWithheldTokensToMint(PublicKey mint, IReadOnlyList<PublicKey> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var accounts = new AccountMeta[sources.Count + 1];
        accounts[0] = AccountMeta.Writable(mint);
        for (var i = 0; i < sources.Count; i++)
            accounts[i + 1] = AccountMeta.Writable(sources[i]);
        return new()
        {
            ProgramId = ProgramId,
            Accounts = accounts,
            Data = [TransferFeeExtensionDiscriminator, 4]
        };
    }

    /// <summary>Schedules a new transfer fee for a Token-2022 mint.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The transfer-fee authority or multisig authority.</param>
    /// <param name="basisPoints">The new transfer fee in basis points.</param>
    /// <param name="maximumFee">The new maximum fee in raw token units.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The set-transfer-fee instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction SetTransferFee(
        PublicKey mint,
        PublicKey authority,
        ushort basisPoints,
        ulong maximumFee,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var data = new byte[2 + sizeof(ushort) + sizeof(ulong)];
        data[0] = TransferFeeExtensionDiscriminator;
        data[1] = 5;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2), basisPoints);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(2 + sizeof(ushort)), maximumFee);
        return AuthorityExtensionInstruction(mint, authority, data, multisigSigners);
    }

    private static void AppendOptionalPublicKey(List<byte> data, PublicKey? publicKey)
    {
        if (publicKey is null)
        {
            data.Add(0);
            return;
        }

        data.Add(1);
        data.AddRange(publicKey.Value.ToBytes());
    }

    private static void AppendAuthority(
        List<AccountMeta> accounts,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners)
    {
        if (multisigSigners is not { Count: > 0 })
        {
            accounts.Add(AccountMeta.ReadonlySigner(authority));
            return;
        }

        if (multisigSigners.Count > MaxMultisigSigners)
            throw new ArgumentException(
                $"A Token-2022 multisig supports at most {MaxMultisigSigners} member signers.",
                nameof(multisigSigners));
        accounts.Add(AccountMeta.Readonly(authority));
        for (var i = 0; i < multisigSigners.Count; i++)
            accounts.Add(AccountMeta.ReadonlySigner(multisigSigners[i]));
    }
}
