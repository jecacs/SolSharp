using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

public static partial class Token2022Program
{
    private const byte ConfidentialMintBurnExtensionDiscriminator = 42;

    /// <summary>Initializes confidential mint/burn state on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="supplyElGamalPublicKey">The exact 32-byte confidential-supply ElGamal public-key POD.</param>
    /// <param name="decryptableSupply">The exact 36-byte initial decryptable-supply POD.</param>
    /// <returns>The initialize-confidential-mint-burn instruction.</returns>
    public static Instruction InitializeConfidentialMintBurn(
        PublicKey mint,
        ReadOnlySpan<byte> supplyElGamalPublicKey,
        ReadOnlySpan<byte> decryptableSupply)
    {
        var data = ConfidentialMintBurnData(
            innerDiscriminator: 0,
            payloadLength: ElGamalPublicKeyLength + DecryptableBalanceLength);
        CopyExactPod(data.AsSpan(2, ElGamalPublicKeyLength), supplyElGamalPublicKey, nameof(supplyElGamalPublicKey));
        CopyExactPod(
            data.AsSpan(2 + ElGamalPublicKeyLength, DecryptableBalanceLength),
            decryptableSupply,
            nameof(decryptableSupply));
        return new Instruction { ProgramId = ProgramId, Accounts = [AccountMeta.Writable(mint)], Data = data };
    }

    /// <summary>Rotates the confidential-supply ElGamal public key using a precomputed equality proof.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The confidential mint authority or multisig authority.</param>
    /// <param name="newSupplyElGamalPublicKey">The exact 32-byte new supply public-key POD.</param>
    /// <param name="proofLocation">The ciphertext-ciphertext equality proof location.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The rotate-confidential-supply-key instruction.</returns>
    public static Instruction RotateConfidentialSupplyElGamalPublicKey(
        PublicKey mint,
        PublicKey authority,
        ReadOnlySpan<byte> newSupplyElGamalPublicKey,
        ConfidentialProofLocation proofLocation,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var accounts = new List<AccountMeta> { AccountMeta.Writable(mint) };
        var offset = AppendConfidentialProofLocations(accounts, proofLocation)[0];
        AppendAuthority(accounts, authority, multisigSigners);
        var data = ConfidentialMintBurnData(innerDiscriminator: 1, payloadLength: ElGamalPublicKeyLength + 1);
        CopyExactPod(data.AsSpan(2, ElGamalPublicKeyLength), newSupplyElGamalPublicKey, nameof(newSupplyElGamalPublicKey));
        data[^1] = unchecked((byte)offset);
        return new Instruction { ProgramId = ProgramId, Accounts = accounts, Data = data };
    }

    /// <summary>Updates the mint's decryptable confidential supply.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The confidential mint authority or multisig authority.</param>
    /// <param name="newDecryptableSupply">The exact 36-byte updated decryptable-supply POD.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The update-decryptable-supply instruction.</returns>
    public static Instruction UpdateConfidentialDecryptableSupply(
        PublicKey mint,
        PublicKey authority,
        ReadOnlySpan<byte> newDecryptableSupply,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var data = ConfidentialMintBurnData(innerDiscriminator: 2, payloadLength: DecryptableBalanceLength);
        CopyExactPod(data.AsSpan(2), newDecryptableSupply, nameof(newDecryptableSupply));
        return ConfidentialAuthorityInstruction([AccountMeta.Writable(mint)], authority, data, multisigSigners);
    }

    /// <summary>Mints into a confidential balance using caller-generated ciphertexts and three split proofs.</summary>
    /// <param name="tokenAccount">The writable destination token account.</param>
    /// <param name="mint">The writable mint.</param>
    /// <param name="newDecryptableSupply">The exact 36-byte post-mint decryptable supply.</param>
    /// <param name="auditorCiphertextLow">The exact 64-byte low mint-amount auditor ciphertext.</param>
    /// <param name="auditorCiphertextHigh">The exact 64-byte high mint-amount auditor ciphertext.</param>
    /// <param name="authority">The confidential mint authority or multisig authority.</param>
    /// <param name="equalityProofLocation">The ciphertext-commitment equality proof location.</param>
    /// <param name="ciphertextValidityProofLocation">The batched three-handle validity-proof location.</param>
    /// <param name="rangeProofLocation">The batched U128 range-proof location.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The confidential mint instruction.</returns>
    public static Instruction MintConfidentialTokens(
        PublicKey tokenAccount,
        PublicKey mint,
        ReadOnlySpan<byte> newDecryptableSupply,
        ReadOnlySpan<byte> auditorCiphertextLow,
        ReadOnlySpan<byte> auditorCiphertextHigh,
        PublicKey authority,
        ConfidentialProofLocation equalityProofLocation,
        ConfidentialProofLocation ciphertextValidityProofLocation,
        ConfidentialProofLocation rangeProofLocation,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => ConfidentialMintOrBurn(
            tokenAccount,
            mint,
            newDecryptableSupply,
            auditorCiphertextLow,
            auditorCiphertextHigh,
            authority,
            equalityProofLocation,
            ciphertextValidityProofLocation,
            rangeProofLocation,
            innerDiscriminator: 3,
            multisigSigners);

    /// <summary>Burns from a confidential balance using caller-generated ciphertexts and three split proofs.</summary>
    /// <param name="tokenAccount">The writable source token account.</param>
    /// <param name="mint">The writable mint.</param>
    /// <param name="newDecryptableAvailableBalance">The exact 36-byte post-burn source balance.</param>
    /// <param name="auditorCiphertextLow">The exact 64-byte low burn-amount auditor ciphertext.</param>
    /// <param name="auditorCiphertextHigh">The exact 64-byte high burn-amount auditor ciphertext.</param>
    /// <param name="authority">The account owner/delegate or multisig authority.</param>
    /// <param name="equalityProofLocation">The ciphertext-commitment equality proof location.</param>
    /// <param name="ciphertextValidityProofLocation">The batched three-handle validity-proof location.</param>
    /// <param name="rangeProofLocation">The batched U128 range-proof location.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The confidential burn instruction.</returns>
    public static Instruction BurnConfidentialTokens(
        PublicKey tokenAccount,
        PublicKey mint,
        ReadOnlySpan<byte> newDecryptableAvailableBalance,
        ReadOnlySpan<byte> auditorCiphertextLow,
        ReadOnlySpan<byte> auditorCiphertextHigh,
        PublicKey authority,
        ConfidentialProofLocation equalityProofLocation,
        ConfidentialProofLocation ciphertextValidityProofLocation,
        ConfidentialProofLocation rangeProofLocation,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => ConfidentialMintOrBurn(
            tokenAccount,
            mint,
            newDecryptableAvailableBalance,
            auditorCiphertextLow,
            auditorCiphertextHigh,
            authority,
            equalityProofLocation,
            ciphertextValidityProofLocation,
            rangeProofLocation,
            innerDiscriminator: 4,
            multisigSigners);

    /// <summary>Applies a pending confidential burn amount to the mint supply.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The confidential mint authority or multisig authority.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The apply-pending-confidential-burn instruction.</returns>
    public static Instruction ApplyPendingConfidentialBurn(
        PublicKey mint,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => ConfidentialAuthorityInstruction(
            [AccountMeta.Writable(mint)],
            authority,
            ConfidentialMintBurnData(innerDiscriminator: 5),
            multisigSigners);

    private static Instruction ConfidentialMintOrBurn(
        PublicKey tokenAccount,
        PublicKey mint,
        ReadOnlySpan<byte> newDecryptableBalance,
        ReadOnlySpan<byte> auditorCiphertextLow,
        ReadOnlySpan<byte> auditorCiphertextHigh,
        PublicKey authority,
        ConfidentialProofLocation equalityProofLocation,
        ConfidentialProofLocation ciphertextValidityProofLocation,
        ConfidentialProofLocation rangeProofLocation,
        byte innerDiscriminator,
        IReadOnlyList<PublicKey>? multisigSigners)
    {
        var accounts = new List<AccountMeta> { AccountMeta.Writable(tokenAccount), AccountMeta.Writable(mint) };
        var offsets = AppendConfidentialProofLocations(
            accounts,
            equalityProofLocation,
            ciphertextValidityProofLocation,
            rangeProofLocation);
        AppendAuthority(accounts, authority, multisigSigners);
        var data = ConfidentialMintBurnData(
            innerDiscriminator,
            payloadLength: DecryptableBalanceLength + (2 * ElGamalCiphertextLength) + 3);
        var cursor = 2;
        CopyExactPod(data.AsSpan(cursor, DecryptableBalanceLength), newDecryptableBalance, nameof(newDecryptableBalance));
        cursor += DecryptableBalanceLength;
        CopyExactPod(data.AsSpan(cursor, ElGamalCiphertextLength), auditorCiphertextLow, nameof(auditorCiphertextLow));
        cursor += ElGamalCiphertextLength;
        CopyExactPod(data.AsSpan(cursor, ElGamalCiphertextLength), auditorCiphertextHigh, nameof(auditorCiphertextHigh));
        cursor += ElGamalCiphertextLength;
        for (var i = 0; i < offsets.Length; i++)
            data[cursor + i] = unchecked((byte)offsets[i]);
        return new Instruction { ProgramId = ProgramId, Accounts = accounts, Data = data };
    }

    private static byte[] ConfidentialMintBurnData(byte innerDiscriminator, int payloadLength = 0)
    {
        var data = new byte[2 + payloadLength];
        data[0] = ConfidentialMintBurnExtensionDiscriminator;
        data[1] = innerDiscriminator;
        return data;
    }
}
