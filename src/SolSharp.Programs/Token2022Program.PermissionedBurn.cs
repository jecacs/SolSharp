using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

public static partial class Token2022Program
{
    private const byte PermissionedConfidentialBurnInstructionDiscriminator = 3;

    /// <summary>
    /// Burns tokens from a confidential balance with approval from the mint's permissioned-burn authority,
    /// using caller-generated ciphertexts and precomputed split proofs.
    /// </summary>
    /// <param name="tokenAccount">The writable token account whose confidential balance is debited.</param>
    /// <param name="mint">The writable Token-2022 mint.</param>
    /// <param name="permissionedBurnAuthority">The configured permissioned-burn signer.</param>
    /// <param name="newDecryptableAvailableBalance">
    /// The exact 36-byte post-burn authenticated-encryption ciphertext for the available balance.
    /// </param>
    /// <param name="auditorCiphertextLow">The exact 64-byte low burn-amount auditor ciphertext.</param>
    /// <param name="auditorCiphertextHigh">The exact 64-byte high burn-amount auditor ciphertext.</param>
    /// <param name="owner">The token-account owner, delegate, or multisig authority.</param>
    /// <param name="equalityProofLocation">The ciphertext-commitment equality-proof location.</param>
    /// <param name="ciphertextValidityProofLocation">
    /// The batched three-handle ciphertext-validity-proof location.
    /// </param>
    /// <param name="rangeProofLocation">The batched U128 range-proof location.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct owner.</param>
    /// <returns>
    /// The permissioned confidential-burn instruction. The caller composes any referenced proof-verification
    /// instructions separately.
    /// </returns>
    /// <exception cref="ArgumentNullException">A proof location is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// A ciphertext POD has an invalid length, or <paramref name="multisigSigners"/> contains more than 11 accounts.
    /// </exception>
    public static Instruction BurnPermissionedConfidentialTokens(
        PublicKey tokenAccount,
        PublicKey mint,
        PublicKey permissionedBurnAuthority,
        ReadOnlySpan<byte> newDecryptableAvailableBalance,
        ReadOnlySpan<byte> auditorCiphertextLow,
        ReadOnlySpan<byte> auditorCiphertextHigh,
        PublicKey owner,
        ConfidentialProofLocation equalityProofLocation,
        ConfidentialProofLocation ciphertextValidityProofLocation,
        ConfidentialProofLocation rangeProofLocation,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var accounts = new List<AccountMeta> { AccountMeta.Writable(tokenAccount), AccountMeta.Writable(mint) };
        var offsets = AppendConfidentialProofLocations(
            accounts,
            equalityProofLocation,
            ciphertextValidityProofLocation,
            rangeProofLocation);
        accounts.Add(AccountMeta.ReadonlySigner(permissionedBurnAuthority));
        AppendAuthority(accounts, owner, multisigSigners);

        var data = new byte[2 + DecryptableBalanceLength + (2 * ElGamalCiphertextLength) + offsets.Length];
        data[0] = PermissionedBurnExtensionDiscriminator;
        data[1] = PermissionedConfidentialBurnInstructionDiscriminator;
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
        for (var i = 0; i < offsets.Length; i++)
            data[cursor + i] = unchecked((byte)offsets[i]);

        return new() { ProgramId = ProgramId, Accounts = accounts, Data = data };
    }
}
