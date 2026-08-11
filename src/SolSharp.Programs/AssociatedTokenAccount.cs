using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Derives Associated Token Account (ATA) addresses - the canonical token account a wallet holds for a mint.</summary>
public static class AssociatedTokenAccount
{
    /// <summary>The Associated Token Account program's address.</summary>
    public static readonly PublicKey ProgramId = PublicKey.Parse(SolanaProgramIds.AssociatedTokenProgram);

    private static readonly PublicKey DefaultTokenProgram = PublicKey.Parse(SolanaProgramIds.TokenProgram);

    /// <summary>Decodes an ATA instruction tag.</summary>
    /// <param name="data">
    /// Complete ATA instruction data. Empty data is the original encoding the program still accepts and
    /// decodes as <c>Create</c>, so historical instructions classify the same way the program does.
    /// </param>
    /// <returns><c>Create</c>, <c>CreateIdempotent</c>, or <c>RecoverNested</c>; otherwise <c>null</c>.</returns>
    public static string? DecodeInstructionData(ReadOnlySpan<byte> data)
        => data.Length switch
        {
            0 => "Create",
            1 => data[0] switch
            {
                0 => "Create",
                1 => "CreateIdempotent",
                2 => "RecoverNested",
                _ => null
            },
            _ => null
        };

    /// <summary>Derives the associated token account address holding <paramref name="mint"/> for <paramref name="owner"/>.</summary>
    /// <param name="owner">The wallet that owns the token account.</param>
    /// <param name="mint">The token mint.</param>
    /// <param name="tokenProgram">The token program; SPL Token by default, or pass Token-2022 for its mints.</param>
    /// <returns>The associated token account address.</returns>
    public static PublicKey GetAddress(PublicKey owner, PublicKey mint, PublicKey? tokenProgram = null)
    {
        var program = tokenProgram ?? DefaultTokenProgram;
        byte[][] seeds = [owner.ToBytes(), program.ToBytes(), mint.ToBytes()];
        return ProgramDerivedAddress.FindProgramAddress(seeds, ProgramId).Address;
    }

    /// <summary>Builds the instruction that creates the associated token account for <paramref name="owner"/> and <paramref name="mint"/>.</summary>
    /// <param name="payer">The account that funds the new token account; signs the transaction.</param>
    /// <param name="owner">The wallet that will own the token account.</param>
    /// <param name="mint">The token mint.</param>
    /// <param name="tokenProgram">The token program; SPL Token by default, or pass Token-2022 for its mints.</param>
    /// <returns>The create-account instruction.</returns>
    public static Instruction Create(PublicKey payer, PublicKey owner, PublicKey mint, PublicKey? tokenProgram = null)
        => Build(payer, owner, mint, tokenProgram, data: [0]);

    /// <summary>
    /// Builds the idempotent create instruction: like <see cref="Create"/>, but succeeds as a no-op when the
    /// associated token account already exists instead of failing the transaction.
    /// </summary>
    /// <param name="payer">The account that funds the new token account; signs the transaction.</param>
    /// <param name="owner">The wallet that will own the token account.</param>
    /// <param name="mint">The token mint.</param>
    /// <param name="tokenProgram">The token program; SPL Token by default, or pass Token-2022 for its mints.</param>
    /// <returns>The createIdempotent instruction.</returns>
    public static Instruction CreateIdempotent(PublicKey payer, PublicKey owner, PublicKey mint, PublicKey? tokenProgram = null)
        => Build(payer, owner, mint, tokenProgram, data: [1]);

    /// <summary>
    /// Recovers tokens and lamports from an accidentally nested associated token account into the wallet's
    /// canonical associated token account.
    /// </summary>
    /// <param name="wallet">The wallet that owns the outer associated token account; writable and signs.</param>
    /// <param name="ownerMint">The mint of the outer associated token account.</param>
    /// <param name="nestedMint">The mint held by the nested associated token account.</param>
    /// <param name="tokenProgram">The token program; SPL Token by default, or pass Token-2022 for its mints.</param>
    /// <returns>The recover-nested instruction.</returns>
    public static Instruction RecoverNested(
        PublicKey wallet,
        PublicKey ownerMint,
        PublicKey nestedMint,
        PublicKey? tokenProgram = null)
    {
        var program = tokenProgram ?? DefaultTokenProgram;
        var ownerAssociatedAccount = GetAddress(wallet, ownerMint, program);
        var destinationAssociatedAccount = GetAddress(wallet, nestedMint, program);
        var nestedAssociatedAccount = GetAddress(ownerAssociatedAccount, nestedMint, program);

        return new()
        {
            ProgramId = ProgramId,
            Accounts =
            [
                AccountMeta.Writable(nestedAssociatedAccount),
                AccountMeta.Readonly(nestedMint),
                AccountMeta.Writable(destinationAssociatedAccount),
                AccountMeta.Readonly(ownerAssociatedAccount),
                AccountMeta.Readonly(ownerMint),
                AccountMeta.WritableSigner(wallet),
                AccountMeta.Readonly(program)
            ],
            Data = [2]
        };
    }

    // Create and CreateIdempotent differ only in the instruction tag: [0] is Create, [1] is CreateIdempotent.
    private static Instruction Build(PublicKey payer, PublicKey owner, PublicKey mint, PublicKey? tokenProgram, byte[] data)
    {
        var program = tokenProgram ?? DefaultTokenProgram;
        var address = GetAddress(owner, mint, program);

        return new()
        {
            ProgramId = ProgramId,
            Accounts =
            [
                AccountMeta.WritableSigner(payer),
                AccountMeta.Writable(address),
                AccountMeta.Readonly(owner),
                AccountMeta.Readonly(mint),
                AccountMeta.Readonly(SystemProgram.ProgramId),
                AccountMeta.Readonly(program)
            ],
            Data = data
        };
    }
}
