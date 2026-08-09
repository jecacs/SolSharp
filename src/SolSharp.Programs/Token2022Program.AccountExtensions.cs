using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

public static partial class Token2022Program
{
    private const byte DefaultAccountStateExtensionDiscriminator = 28;
    private const byte MemoTransferExtensionDiscriminator = 30;
    private const byte CpiGuardExtensionDiscriminator = 34;

    /// <summary>Initializes the default state applied to new accounts of a Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="state">The default account state.</param>
    /// <returns>The default-account-state initialize instruction.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is not defined.</exception>
    public static Instruction InitializeDefaultAccountState(PublicKey mint, DefaultTokenAccountState state)
        => new()
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.Writable(mint)],
            Data = DefaultAccountStateData(innerDiscriminator: 0, state)
        };

    /// <summary>Updates the default state applied to new accounts of a Token-2022 mint.</summary>
    /// <param name="mint">The writable mint.</param>
    /// <param name="freezeAuthority">The mint's freeze authority or multisig authority.</param>
    /// <param name="state">The new default account state.</param>
    /// <param name="multisigSigners">
    /// Multisig member signers, or <c>null</c>/empty when <paramref name="freezeAuthority"/> signs directly.
    /// </param>
    /// <returns>The default-account-state update instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is not defined.</exception>
    public static Instruction UpdateDefaultAccountState(
        PublicKey mint,
        PublicKey freezeAuthority,
        DefaultTokenAccountState state,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => AuthorityExtensionInstruction(
            mint,
            freezeAuthority,
            DefaultAccountStateData(innerDiscriminator: 1, state),
            multisigSigners);

    /// <summary>Requires a memo on every incoming transfer to a Token-2022 account.</summary>
    /// <param name="account">The writable token account.</param>
    /// <param name="owner">The account owner or multisig owner.</param>
    /// <param name="multisigSigners">
    /// Multisig member signers, or <c>null</c>/empty when <paramref name="owner"/> signs directly.
    /// </param>
    /// <returns>The memo-transfer enable instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction EnableRequiredTransferMemos(
        PublicKey account,
        PublicKey owner,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => AuthorityExtensionInstruction(
            account,
            owner,
            [MemoTransferExtensionDiscriminator, 0],
            multisigSigners);

    /// <summary>Stops requiring memos on incoming transfers to a Token-2022 account.</summary>
    /// <param name="account">The writable token account.</param>
    /// <param name="owner">The account owner or multisig owner.</param>
    /// <param name="multisigSigners">
    /// Multisig member signers, or <c>null</c>/empty when <paramref name="owner"/> signs directly.
    /// </param>
    /// <returns>The memo-transfer disable instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction DisableRequiredTransferMemos(
        PublicKey account,
        PublicKey owner,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => AuthorityExtensionInstruction(
            account,
            owner,
            [MemoTransferExtensionDiscriminator, 1],
            multisigSigners);

    /// <summary>Enables the Token-2022 CPI guard on a token account.</summary>
    /// <param name="account">The writable token account.</param>
    /// <param name="owner">The account owner or multisig owner.</param>
    /// <param name="multisigSigners">
    /// Multisig member signers, or <c>null</c>/empty when <paramref name="owner"/> signs directly.
    /// </param>
    /// <returns>The CPI-guard enable instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction EnableCpiGuard(
        PublicKey account,
        PublicKey owner,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => AuthorityExtensionInstruction(account, owner, [CpiGuardExtensionDiscriminator, 0], multisigSigners);

    /// <summary>Disables the Token-2022 CPI guard on a token account.</summary>
    /// <param name="account">The writable token account.</param>
    /// <param name="owner">The account owner or multisig owner.</param>
    /// <param name="multisigSigners">
    /// Multisig member signers, or <c>null</c>/empty when <paramref name="owner"/> signs directly.
    /// </param>
    /// <returns>The CPI-guard disable instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> contains more than 11 accounts.</exception>
    public static Instruction DisableCpiGuard(
        PublicKey account,
        PublicKey owner,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => AuthorityExtensionInstruction(account, owner, [CpiGuardExtensionDiscriminator, 1], multisigSigners);

    private static byte[] DefaultAccountStateData(byte innerDiscriminator, DefaultTokenAccountState state)
    {
        if ((byte)state > (byte)DefaultTokenAccountState.Frozen)
            throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown token account state.");
        return [DefaultAccountStateExtensionDiscriminator, innerDiscriminator, (byte)state];
    }

    private static Instruction AuthorityExtensionInstruction(
        PublicKey target,
        PublicKey authority,
        byte[] data,
        IReadOnlyList<PublicKey>? multisigSigners)
    {
        var instruction = new Instruction
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.Writable(target), AccountMeta.ReadonlySigner(authority)],
            Data = data
        };
        return multisigSigners is { Count: > 0 }
            ? WithMultisigAuthority(instruction, authorityIndex: 1, multisigSigners)
            : instruction;
    }
}
