using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

public static partial class Token2022Program
{
    private const byte ConfidentialTransferFeeExtensionDiscriminator = 37;

    /// <summary>Initializes confidential transfer-fee configuration on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="authority">The configuration authority, or <c>null</c> for immutable configuration.</param>
    /// <param name="withdrawAuthorityElGamalPublicKey">The exact 32-byte withdraw-authority ElGamal public-key POD.</param>
    /// <returns>The initialize-confidential-transfer-fee-config instruction.</returns>
    public static Instruction InitializeConfidentialTransferFeeConfig(
        PublicKey mint,
        PublicKey? authority,
        ReadOnlySpan<byte> withdrawAuthorityElGamalPublicKey)
    {
        var data = ConfidentialFeeData(innerDiscriminator: 0, payloadLength: PublicKey.Length + ElGamalPublicKeyLength);
        WriteMaybeNullPublicKey(data.AsSpan(2), authority, nameof(authority));
        CopyExactPod(
            data.AsSpan(2 + PublicKey.Length, ElGamalPublicKeyLength),
            withdrawAuthorityElGamalPublicKey,
            nameof(withdrawAuthorityElGamalPublicKey));
        return new() { ProgramId = ProgramId, Accounts = [AccountMeta.Writable(mint)], Data = data };
    }

    /// <summary>Withdraws confidential fees held by a mint into a confidential token account.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="destination">The writable confidential fee-receiver token account.</param>
    /// <param name="newDecryptableAvailableBalance">The exact 36-byte receiver balance after withdrawal.</param>
    /// <param name="authority">The withdraw-withheld authority or multisig authority.</param>
    /// <param name="proofLocation">The ciphertext-ciphertext equality proof location.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The confidential withdraw-withheld-from-mint instruction.</returns>
    public static Instruction WithdrawConfidentialWithheldTokensFromMint(
        PublicKey mint,
        PublicKey destination,
        ReadOnlySpan<byte> newDecryptableAvailableBalance,
        PublicKey authority,
        ConfidentialProofLocation proofLocation,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        var accounts = new List<AccountMeta> { AccountMeta.Writable(mint), AccountMeta.Writable(destination) };
        var offset = AppendConfidentialProofLocations(accounts, proofLocation)[0];
        AppendAuthority(accounts, authority, multisigSigners);
        var data = ConfidentialFeeData(innerDiscriminator: 1, payloadLength: 1 + DecryptableBalanceLength);
        data[2] = unchecked((byte)offset);
        CopyExactPod(
            data.AsSpan(3, DecryptableBalanceLength),
            newDecryptableAvailableBalance,
            nameof(newDecryptableAvailableBalance));
        return new() { ProgramId = ProgramId, Accounts = accounts, Data = data };
    }

    /// <summary>
    /// Withdraws confidential fees directly from token accounts. This mirrors the upstream stable builder,
    /// including its front-running sensitivity; harvesting to the mint first is generally preferable.
    /// </summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="destination">The writable confidential fee-receiver token account.</param>
    /// <param name="newDecryptableAvailableBalance">The exact 36-byte receiver balance after withdrawal.</param>
    /// <param name="authority">The withdraw-withheld authority or multisig authority.</param>
    /// <param name="sources">The writable source token accounts.</param>
    /// <param name="proofLocation">The ciphertext-ciphertext equality proof location.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The confidential withdraw-withheld-from-accounts instruction.</returns>
    public static Instruction WithdrawConfidentialWithheldTokensFromAccounts(
        PublicKey mint,
        PublicKey destination,
        ReadOnlySpan<byte> newDecryptableAvailableBalance,
        PublicKey authority,
        IReadOnlyList<PublicKey> sources,
        ConfidentialProofLocation proofLocation,
        IReadOnlyList<PublicKey>? multisigSigners = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count > byte.MaxValue)
            throw new ArgumentException("At most 255 confidential fee source accounts fit in this instruction.", nameof(sources));

        var accounts = new List<AccountMeta>(4 + sources.Count + (multisigSigners?.Count ?? 0))
        {
            AccountMeta.Writable(mint),
            AccountMeta.Writable(destination)
        };
        var offset = AppendConfidentialProofLocations(accounts, proofLocation)[0];
        AppendAuthority(accounts, authority, multisigSigners);
        for (var i = 0; i < sources.Count; i++)
            accounts.Add(AccountMeta.Writable(sources[i]));

        var data = ConfidentialFeeData(innerDiscriminator: 2, payloadLength: 2 + DecryptableBalanceLength);
        data[2] = checked((byte)sources.Count);
        data[3] = unchecked((byte)offset);
        CopyExactPod(
            data.AsSpan(4, DecryptableBalanceLength),
            newDecryptableAvailableBalance,
            nameof(newDecryptableAvailableBalance));
        return new() { ProgramId = ProgramId, Accounts = accounts, Data = data };
    }

    /// <summary>Permissionlessly harvests confidential withheld fees from token accounts into their mint.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="sources">The writable source token accounts.</param>
    /// <returns>The confidential harvest-withheld-to-mint instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    public static Instruction HarvestConfidentialWithheldTokensToMint(
        PublicKey mint,
        IReadOnlyList<PublicKey> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var accounts = new AccountMeta[1 + sources.Count];
        accounts[0] = AccountMeta.Writable(mint);
        for (var i = 0; i < sources.Count; i++)
            accounts[i + 1] = AccountMeta.Writable(sources[i]);
        return new()
        {
            ProgramId = ProgramId,
            Accounts = accounts,
            Data = ConfidentialFeeData(innerDiscriminator: 3)
        };
    }

    /// <summary>Enables accepting confidential fees harvested into a mint.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The mint authority or multisig authority.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The enable-confidential-harvest instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction EnableConfidentialHarvestToMint(
        PublicKey mint,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => ConfidentialFeeAuthorityInstruction(mint, authority, innerDiscriminator: 4, multisigSigners);

    /// <summary>Disables accepting confidential fees harvested into a mint.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="authority">The mint authority or multisig authority.</param>
    /// <param name="multisigSigners">Multisig member signers, or <c>null</c>/empty for a direct authority.</param>
    /// <returns>The disable-confidential-harvest instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction DisableConfidentialHarvestToMint(
        PublicKey mint,
        PublicKey authority,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => ConfidentialFeeAuthorityInstruction(mint, authority, innerDiscriminator: 5, multisigSigners);

    private static Instruction ConfidentialFeeAuthorityInstruction(
        PublicKey mint,
        PublicKey authority,
        byte innerDiscriminator,
        IReadOnlyList<PublicKey>? multisigSigners)
    {
        var accounts = new List<AccountMeta> { AccountMeta.Writable(mint) };
        AppendAuthority(accounts, authority, multisigSigners);
        return new()
        {
            ProgramId = ProgramId,
            Accounts = accounts,
            Data = ConfidentialFeeData(innerDiscriminator)
        };
    }

    private static byte[] ConfidentialFeeData(byte innerDiscriminator, int payloadLength = 0)
    {
        var data = new byte[2 + payloadLength];
        data[0] = ConfidentialTransferFeeExtensionDiscriminator;
        data[1] = innerDiscriminator;
        return data;
    }
}
