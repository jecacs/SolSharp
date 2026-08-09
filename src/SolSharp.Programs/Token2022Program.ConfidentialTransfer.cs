using System.Buffers.Binary;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

public static partial class Token2022Program
{
    /// <summary>The byte length of an upstream ElGamal public-key POD.</summary>
    public const int ElGamalPublicKeyLength = 32;

    /// <summary>The byte length of an upstream ElGamal ciphertext POD.</summary>
    public const int ElGamalCiphertextLength = 64;

    /// <summary>The byte length of an upstream authenticated-encryption ciphertext/decryptable balance POD.</summary>
    public const int DecryptableBalanceLength = 36;

    private const byte ConfidentialTransferExtensionDiscriminator = 27;
    private static readonly PublicKey InstructionsSysvar = PublicKey.Parse(Sysvars.Instructions);

    /// <summary>Initializes confidential-transfer configuration on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="authority">The configuration authority, or <c>null</c> for none.</param>
    /// <param name="autoApproveNewAccounts">Whether newly configured accounts are immediately approved.</param>
    /// <param name="auditorElGamalPublicKey">An optional exact 32-byte ElGamal public-key POD.</param>
    /// <returns>The initialize-confidential-transfer-mint instruction.</returns>
    public static Instruction InitializeConfidentialTransferMint(
        PublicKey mint,
        PublicKey? authority,
        bool autoApproveNewAccounts,
        ReadOnlyMemory<byte>? auditorElGamalPublicKey = null)
    {
        var data = ConfidentialData(innerDiscriminator: 0, payloadLength: 65);
        WriteMaybeNullPublicKey(data.AsSpan(2), authority, nameof(authority));
        data[2 + PublicKey.Length] = autoApproveNewAccounts ? (byte)1 : (byte)0;
        WriteMaybeNullPod(
            data.AsSpan(3 + PublicKey.Length, ElGamalPublicKeyLength),
            auditorElGamalPublicKey,
            ElGamalPublicKeyLength,
            nameof(auditorElGamalPublicKey));
        return new Instruction { ProgramId = ProgramId, Accounts = [AccountMeta.Writable(mint)], Data = data };
    }

    /// <summary>Updates confidential-transfer configuration on a Token-2022 mint.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The current confidential-transfer mint authority.</param>
    /// <param name="autoApproveNewAccounts">Whether newly configured accounts are immediately approved.</param>
    /// <param name="auditorElGamalPublicKey">An optional exact 32-byte ElGamal public-key POD.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The update-confidential-transfer-mint instruction.</returns>
    public static Instruction UpdateConfidentialTransferMint(
        PublicKey mint,
        PublicKey authority,
        bool autoApproveNewAccounts,
        ReadOnlyMemory<byte>? auditorElGamalPublicKey = null,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var data = ConfidentialData(innerDiscriminator: 1, payloadLength: 33);
        data[2] = autoApproveNewAccounts ? (byte)1 : (byte)0;
        WriteMaybeNullPod(
            data.AsSpan(3, ElGamalPublicKeyLength),
            auditorElGamalPublicKey,
            ElGamalPublicKeyLength,
            nameof(auditorElGamalPublicKey));
        return ConfidentialAuthorityInstruction([AccountMeta.Writable(mint)], authority, data, multisigSigners);
    }

    /// <summary>
    /// Configures confidential transfers on a token account using a caller-provided decryptable zero balance
    /// and a precomputed ElGamal public-key-validity proof.
    /// </summary>
    /// <param name="tokenAccount">The writable token account.</param>
    /// <param name="mint">The readonly mint.</param>
    /// <param name="decryptableZeroBalance">The exact 36-byte authenticated-encryption ciphertext for zero.</param>
    /// <param name="maximumPendingBalanceCreditCounter">The maximum unapplied incoming-credit count.</param>
    /// <param name="authority">The account owner or multisig authority.</param>
    /// <param name="proofLocation">The validity proof instruction offset or pre-verified context account.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The configure-confidential-transfer-account instruction.</returns>
    public static Instruction ConfigureConfidentialTransferAccount(
        PublicKey tokenAccount,
        PublicKey mint,
        ReadOnlySpan<byte> decryptableZeroBalance,
        ulong maximumPendingBalanceCreditCounter,
        PublicKey authority,
        ConfidentialProofLocation proofLocation,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var accounts = new List<AccountMeta> { AccountMeta.Writable(tokenAccount), AccountMeta.Readonly(mint) };
        var offset = AppendConfidentialProofLocations(accounts, proofLocation)[0];
        AppendAuthority(accounts, authority, multisigSigners);
        var data = ConfidentialData(innerDiscriminator: 2, payloadLength: 45);
        CopyExactPod(data.AsSpan(2, DecryptableBalanceLength), decryptableZeroBalance, nameof(decryptableZeroBalance));
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(2 + DecryptableBalanceLength), maximumPendingBalanceCreditCounter);
        data[^1] = unchecked((byte)offset);
        return new Instruction { ProgramId = ProgramId, Accounts = accounts, Data = data };
    }

    /// <summary>Approves a configured confidential-transfer token account.</summary>
    /// <param name="tokenAccount">The writable token account to approve.</param>
    /// <param name="mint">The readonly mint.</param>
    /// <param name="authority">The confidential-transfer mint authority.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The approve-confidential-transfer-account instruction.</returns>
    public static Instruction ApproveConfidentialTransferAccount(
        PublicKey tokenAccount,
        PublicKey mint,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => ConfidentialAuthorityInstruction(
            [AccountMeta.Writable(tokenAccount), AccountMeta.Readonly(mint)],
            authority,
            ConfidentialData(innerDiscriminator: 3),
            multisigSigners);

    /// <summary>Empties a confidential available-balance ciphertext using a precomputed zero-ciphertext proof.</summary>
    /// <param name="tokenAccount">The writable token account.</param>
    /// <param name="authority">The account owner or multisig authority.</param>
    /// <param name="proofLocation">The proof instruction offset or pre-verified context account.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The empty-confidential-transfer-account instruction.</returns>
    public static Instruction EmptyConfidentialTransferAccount(
        PublicKey tokenAccount,
        PublicKey authority,
        ConfidentialProofLocation proofLocation,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var accounts = new List<AccountMeta> { AccountMeta.Writable(tokenAccount) };
        var offset = AppendConfidentialProofLocations(accounts, proofLocation)[0];
        AppendAuthority(accounts, authority, multisigSigners);
        var data = ConfidentialData(innerDiscriminator: 4, payloadLength: 1);
        data[2] = unchecked((byte)offset);
        return new Instruction { ProgramId = ProgramId, Accounts = accounts, Data = data };
    }

    /// <summary>Deposits non-confidential tokens into a confidential pending balance.</summary>
    /// <param name="tokenAccount">The writable token account.</param>
    /// <param name="mint">The readonly mint.</param>
    /// <param name="amount">The amount in raw token units.</param>
    /// <param name="decimals">The expected mint decimals.</param>
    /// <param name="authority">The account owner/delegate or multisig authority.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The confidential deposit instruction.</returns>
    public static Instruction DepositConfidentialTokens(
        PublicKey tokenAccount,
        PublicKey mint,
        ulong amount,
        byte decimals,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var data = ConfidentialData(innerDiscriminator: 5, payloadLength: 9);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(2), amount);
        data[^1] = decimals;
        return ConfidentialAuthorityInstruction(
            [AccountMeta.Writable(tokenAccount), AccountMeta.Readonly(mint)],
            authority,
            data,
            multisigSigners);
    }

    /// <summary>Withdraws from a confidential balance using caller-generated ciphertext and proofs.</summary>
    /// <param name="tokenAccount">The writable source token account.</param>
    /// <param name="mint">The readonly mint.</param>
    /// <param name="amount">The amount in raw token units.</param>
    /// <param name="decimals">The expected mint decimals.</param>
    /// <param name="newDecryptableAvailableBalance">The exact 36-byte post-withdraw decryptable balance.</param>
    /// <param name="authority">The source owner/delegate or multisig authority.</param>
    /// <param name="equalityProofLocation">The ciphertext-commitment equality proof location.</param>
    /// <param name="rangeProofLocation">The batched U64 range-proof location.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The confidential withdraw instruction.</returns>
    public static Instruction WithdrawConfidentialTokens(
        PublicKey tokenAccount,
        PublicKey mint,
        ulong amount,
        byte decimals,
        ReadOnlySpan<byte> newDecryptableAvailableBalance,
        PublicKey authority,
        ConfidentialProofLocation equalityProofLocation,
        ConfidentialProofLocation rangeProofLocation,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var accounts = new List<AccountMeta> { AccountMeta.Writable(tokenAccount), AccountMeta.Readonly(mint) };
        var offsets = AppendConfidentialProofLocations(accounts, equalityProofLocation, rangeProofLocation);
        AppendAuthority(accounts, authority, multisigSigners);
        var data = ConfidentialData(innerDiscriminator: 6, payloadLength: 47);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(2), amount);
        data[2 + sizeof(ulong)] = decimals;
        CopyExactPod(
            data.AsSpan(3 + sizeof(ulong), DecryptableBalanceLength),
            newDecryptableAvailableBalance,
            nameof(newDecryptableAvailableBalance));
        data[^2] = unchecked((byte)offsets[0]);
        data[^1] = unchecked((byte)offsets[1]);
        return new Instruction { ProgramId = ProgramId, Accounts = accounts, Data = data };
    }

    /// <summary>Transfers confidential tokens using caller-generated ciphertexts and split proofs.</summary>
    /// <param name="source">The writable source token account.</param>
    /// <param name="mint">The readonly mint.</param>
    /// <param name="destination">The writable destination token account.</param>
    /// <param name="newSourceDecryptableAvailableBalance">The exact 36-byte post-transfer source balance.</param>
    /// <param name="auditorCiphertextLow">The exact 64-byte low transfer-amount auditor ciphertext.</param>
    /// <param name="auditorCiphertextHigh">The exact 64-byte high transfer-amount auditor ciphertext.</param>
    /// <param name="authority">The source owner/delegate or multisig authority.</param>
    /// <param name="equalityProofLocation">The ciphertext-commitment equality proof location.</param>
    /// <param name="ciphertextValidityProofLocation">The batched three-handle validity-proof location.</param>
    /// <param name="rangeProofLocation">The batched U128 range-proof location.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The confidential transfer instruction.</returns>
    public static Instruction TransferConfidentialTokens(
        PublicKey source,
        PublicKey mint,
        PublicKey destination,
        ReadOnlySpan<byte> newSourceDecryptableAvailableBalance,
        ReadOnlySpan<byte> auditorCiphertextLow,
        ReadOnlySpan<byte> auditorCiphertextHigh,
        PublicKey authority,
        ConfidentialProofLocation equalityProofLocation,
        ConfidentialProofLocation ciphertextValidityProofLocation,
        ConfidentialProofLocation rangeProofLocation,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var accounts = new List<AccountMeta>
        {
            AccountMeta.Writable(source),
            AccountMeta.Readonly(mint),
            AccountMeta.Writable(destination)
        };
        var offsets = AppendConfidentialProofLocations(
            accounts,
            equalityProofLocation,
            ciphertextValidityProofLocation,
            rangeProofLocation);
        AppendAuthority(accounts, authority, multisigSigners);
        var data = ConfidentialTransferData(
            innerDiscriminator: 7,
            newSourceDecryptableAvailableBalance,
            auditorCiphertextLow,
            auditorCiphertextHigh,
            offsets,
            expectedProofCount: 3);
        return new Instruction { ProgramId = ProgramId, Accounts = accounts, Data = data };
    }

    /// <summary>Applies pending confidential credits to the available balance.</summary>
    /// <param name="tokenAccount">The writable token account.</param>
    /// <param name="expectedPendingBalanceCreditCounter">The expected pending-credit counter.</param>
    /// <param name="newDecryptableAvailableBalance">The exact 36-byte updated decryptable balance.</param>
    /// <param name="authority">The owner or multisig authority.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The apply-pending-confidential-balance instruction.</returns>
    public static Instruction ApplyConfidentialPendingBalance(
        PublicKey tokenAccount,
        ulong expectedPendingBalanceCreditCounter,
        ReadOnlySpan<byte> newDecryptableAvailableBalance,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var data = ConfidentialData(innerDiscriminator: 8, payloadLength: 44);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(2), expectedPendingBalanceCreditCounter);
        CopyExactPod(
            data.AsSpan(2 + sizeof(ulong), DecryptableBalanceLength),
            newDecryptableAvailableBalance,
            nameof(newDecryptableAvailableBalance));
        return ConfidentialAuthorityInstruction([AccountMeta.Writable(tokenAccount)], authority, data, multisigSigners);
    }

    /// <summary>Allows incoming confidential credits on a token account.</summary>
    /// <param name="tokenAccount">The writable token account.</param>
    /// <param name="authority">The owner or multisig authority.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The enable-confidential-credits instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction EnableConfidentialCredits(
        PublicKey tokenAccount,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => ConfidentialCreditInstruction(tokenAccount, authority, innerDiscriminator: 9, multisigSigners);

    /// <summary>Rejects incoming confidential credits on a token account.</summary>
    /// <param name="tokenAccount">The writable token account.</param>
    /// <param name="authority">The owner or multisig authority.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The disable-confidential-credits instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction DisableConfidentialCredits(
        PublicKey tokenAccount,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => ConfidentialCreditInstruction(tokenAccount, authority, innerDiscriminator: 10, multisigSigners);

    /// <summary>Allows incoming non-confidential credits on a confidential token account.</summary>
    /// <param name="tokenAccount">The writable token account.</param>
    /// <param name="authority">The owner or multisig authority.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The enable-non-confidential-credits instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction EnableNonConfidentialCredits(
        PublicKey tokenAccount,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => ConfidentialCreditInstruction(tokenAccount, authority, innerDiscriminator: 11, multisigSigners);

    /// <summary>Rejects incoming non-confidential credits on a confidential token account.</summary>
    /// <param name="tokenAccount">The writable token account.</param>
    /// <param name="authority">The owner or multisig authority.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The disable-non-confidential-credits instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction DisableNonConfidentialCredits(
        PublicKey tokenAccount,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => ConfidentialCreditInstruction(tokenAccount, authority, innerDiscriminator: 12, multisigSigners);

    /// <summary>Transfers confidential tokens with fees using caller-generated ciphertexts and five split proofs.</summary>
    /// <param name="source">The writable source token account.</param>
    /// <param name="mint">The readonly mint.</param>
    /// <param name="destination">The writable destination token account.</param>
    /// <param name="newSourceDecryptableAvailableBalance">The exact 36-byte post-transfer source balance.</param>
    /// <param name="auditorCiphertextLow">The exact 64-byte low transfer-amount auditor ciphertext.</param>
    /// <param name="auditorCiphertextHigh">The exact 64-byte high transfer-amount auditor ciphertext.</param>
    /// <param name="authority">The source owner/delegate or multisig authority.</param>
    /// <param name="equalityProofLocation">The ciphertext-commitment equality proof location.</param>
    /// <param name="transferAmountValidityProofLocation">The transfer-amount ciphertext validity-proof location.</param>
    /// <param name="feeSigmaProofLocation">The percentage-with-cap proof location.</param>
    /// <param name="feeCiphertextValidityProofLocation">The fee ciphertext validity-proof location.</param>
    /// <param name="rangeProofLocation">The batched U256 range-proof location.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The confidential transfer-with-fee instruction.</returns>
    public static Instruction TransferConfidentialTokensWithFee(
        PublicKey source,
        PublicKey mint,
        PublicKey destination,
        ReadOnlySpan<byte> newSourceDecryptableAvailableBalance,
        ReadOnlySpan<byte> auditorCiphertextLow,
        ReadOnlySpan<byte> auditorCiphertextHigh,
        PublicKey authority,
        ConfidentialProofLocation equalityProofLocation,
        ConfidentialProofLocation transferAmountValidityProofLocation,
        ConfidentialProofLocation feeSigmaProofLocation,
        ConfidentialProofLocation feeCiphertextValidityProofLocation,
        ConfidentialProofLocation rangeProofLocation,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var accounts = new List<AccountMeta>
        {
            AccountMeta.Writable(source),
            AccountMeta.Readonly(mint),
            AccountMeta.Writable(destination)
        };
        var offsets = AppendConfidentialProofLocations(
            accounts,
            equalityProofLocation,
            transferAmountValidityProofLocation,
            feeSigmaProofLocation,
            feeCiphertextValidityProofLocation,
            rangeProofLocation);
        AppendAuthority(accounts, authority, multisigSigners);
        var data = ConfidentialTransferData(
            innerDiscriminator: 13,
            newSourceDecryptableAvailableBalance,
            auditorCiphertextLow,
            auditorCiphertextHigh,
            offsets,
            expectedProofCount: 5);
        return new Instruction { ProgramId = ProgramId, Accounts = accounts, Data = data };
    }

    /// <summary>Configures a confidential-transfer account from a wallet's SPL ElGamal registry.</summary>
    /// <param name="tokenAccount">The writable token account.</param>
    /// <param name="mint">The readonly mint.</param>
    /// <param name="registryAccount">The readonly ElGamal registry account.</param>
    /// <param name="payer">An optional writable signer funding account reallocation.</param>
    /// <returns>The configure-account-with-registry instruction.</returns>
    public static Instruction ConfigureConfidentialTransferAccountWithRegistry(
        PublicKey tokenAccount,
        PublicKey mint,
        PublicKey registryAccount,
        PublicKey? payer = null)
    {
        var accounts = new List<AccountMeta>
        {
            AccountMeta.Writable(tokenAccount),
            AccountMeta.Readonly(mint),
            AccountMeta.Readonly(registryAccount)
        };
        if (payer is { } fundingAccount)
        {
            accounts.Add(AccountMeta.WritableSigner(fundingAccount));
            accounts.Add(AccountMeta.Readonly(SystemProgram.ProgramId));
        }

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = accounts,
            Data = ConfidentialData(innerDiscriminator: 14)
        };
    }

    private static Instruction ConfidentialCreditInstruction(
        PublicKey tokenAccount,
        PublicKey authority,
        byte innerDiscriminator,
        IReadOnlyList<PublicKey>? multisigSigners)
        => ConfidentialAuthorityInstruction(
            [AccountMeta.Writable(tokenAccount)],
            authority,
            ConfidentialData(innerDiscriminator),
            multisigSigners);

    private static Instruction ConfidentialAuthorityInstruction(
        IReadOnlyList<AccountMeta> initialAccounts,
        PublicKey authority,
        byte[] data,
        IReadOnlyList<PublicKey>? multisigSigners)
    {
        var accounts = new List<AccountMeta>(initialAccounts.Count + 1 + (multisigSigners?.Count ?? 0));
        accounts.AddRange(initialAccounts);
        AppendAuthority(accounts, authority, multisigSigners);
        return new Instruction { ProgramId = ProgramId, Accounts = accounts, Data = data };
    }

    private static byte[] ConfidentialData(byte innerDiscriminator, int payloadLength = 0)
    {
        var data = new byte[2 + payloadLength];
        data[0] = ConfidentialTransferExtensionDiscriminator;
        data[1] = innerDiscriminator;
        return data;
    }

    private static byte[] ConfidentialTransferData(
        byte innerDiscriminator,
        ReadOnlySpan<byte> newDecryptableAvailableBalance,
        ReadOnlySpan<byte> auditorCiphertextLow,
        ReadOnlySpan<byte> auditorCiphertextHigh,
        sbyte[] proofOffsets,
        int expectedProofCount)
    {
        if (proofOffsets.Length != expectedProofCount)
            throw new ArgumentException("The confidential instruction has the wrong number of proof offsets.", nameof(proofOffsets));

        var data = ConfidentialData(
            innerDiscriminator,
            DecryptableBalanceLength + (2 * ElGamalCiphertextLength) + expectedProofCount);
        var cursor = 2;
        CopyExactPod(
            data.AsSpan(cursor, DecryptableBalanceLength),
            newDecryptableAvailableBalance,
            nameof(newDecryptableAvailableBalance));
        cursor += DecryptableBalanceLength;
        CopyExactPod(data.AsSpan(cursor, ElGamalCiphertextLength), auditorCiphertextLow, nameof(auditorCiphertextLow));
        cursor += ElGamalCiphertextLength;
        CopyExactPod(data.AsSpan(cursor, ElGamalCiphertextLength), auditorCiphertextHigh, nameof(auditorCiphertextHigh));
        cursor += ElGamalCiphertextLength;
        for (var i = 0; i < proofOffsets.Length; i++)
            data[cursor + i] = unchecked((byte)proofOffsets[i]);
        return data;
    }

    private static sbyte[] AppendConfidentialProofLocations(
        List<AccountMeta> accounts,
        params ConfidentialProofLocation[] proofLocations)
    {
        ArgumentNullException.ThrowIfNull(proofLocations);
        for (var i = 0; i < proofLocations.Length; i++)
            ArgumentNullException.ThrowIfNull(proofLocations[i], nameof(proofLocations));

        if (proofLocations.Any(location => location.IsInstructionOffset))
            accounts.Add(AccountMeta.Readonly(InstructionsSysvar));

        var offsets = new sbyte[proofLocations.Length];
        for (var i = 0; i < proofLocations.Length; i++)
        {
            var location = proofLocations[i];
            if (location.IsInstructionOffset)
            {
                offsets[i] = location.InstructionOffset;
            }
            else
            {
                accounts.Add(AccountMeta.Readonly(location.ContextStateAccount!.Value));
                offsets[i] = 0;
            }
        }

        return offsets;
    }

    private static void CopyExactPod(Span<byte> destination, ReadOnlySpan<byte> source, string parameterName)
    {
        if (source.Length != destination.Length)
            throw new ArgumentException(
                $"The upstream POD must contain exactly {destination.Length} bytes, got {source.Length}.",
                parameterName);
        source.CopyTo(destination);
    }

    private static void WriteMaybeNullPod(
        Span<byte> destination,
        ReadOnlyMemory<byte>? source,
        int requiredLength,
        string parameterName)
    {
        if (destination.Length != requiredLength)
            throw new ArgumentException("The POD destination has the wrong length.", nameof(destination));
        if (source is null)
        {
            destination.Clear();
            return;
        }

        var sourceSpan = source.Value.Span;
        if (sourceSpan.Length != requiredLength)
            throw new ArgumentException(
                $"The upstream POD must contain exactly {requiredLength} bytes, got {sourceSpan.Length}.",
                parameterName);
        if (sourceSpan.IndexOfAnyExcept((byte)0) < 0)
            throw new ArgumentException(
                "The all-zero POD is reserved as the wire representation of null.",
                parameterName);
        sourceSpan.CopyTo(destination);
    }
}
