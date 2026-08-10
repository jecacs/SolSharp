using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Builds SPL ElGamal public-key registry interface instructions.</summary>
public static class ElGamalRegistryProgram
{
    /// <summary>The encoded ElGamal registry state length.</summary>
    public const int RegistryStateLength = PublicKey.Length * 2;

    /// <summary>The SPL ElGamal registry program address.</summary>
    public static readonly PublicKey ProgramId = PublicKey.Parse("regVYJW7tcT8zipN5YiBvHsvR5jXW1uLFxaHSbugABg");

    private static readonly byte[] RegistrySeed = [.. "elgamal-registry"u8];
    private static readonly PublicKey InstructionsSysvar = PublicKey.Parse(Sysvars.Instructions);

    /// <summary>Derives the canonical registry PDA for a wallet owner.</summary>
    /// <param name="owner">The wallet owner.</param>
    /// <returns>The registry PDA.</returns>
    public static PublicKey GetRegistryAddress(PublicKey owner)
        => ProgramDerivedAddress.FindProgramAddress([RegistrySeed, owner.ToBytes()], ProgramId).Address;

    /// <summary>Creates a registry whose ElGamal key is certified by a precomputed validity proof.</summary>
    /// <param name="owner">The wallet owner; signs.</param>
    /// <param name="proofLocation">The proof instruction offset or pre-verified context account.</param>
    /// <returns>The create-registry instruction.</returns>
    public static Instruction CreateRegistry(PublicKey owner, ConfidentialProofLocation proofLocation)
    {
        ArgumentNullException.ThrowIfNull(proofLocation);
        var accounts = new List<AccountMeta>
        {
            AccountMeta.Writable(GetRegistryAddress(owner)),
            AccountMeta.ReadonlySigner(owner),
            AccountMeta.Readonly(SystemProgram.ProgramId)
        };
        var offset = AppendProofAccount(accounts, proofLocation);
        return new() { ProgramId = ProgramId, Accounts = accounts, Data = [0, unchecked((byte)offset)] };
    }

    /// <summary>Updates a registry with an ElGamal key certified by a precomputed validity proof.</summary>
    /// <param name="owner">The wallet owner; signs.</param>
    /// <param name="proofLocation">The proof instruction offset or pre-verified context account.</param>
    /// <returns>The update-registry instruction.</returns>
    public static Instruction UpdateRegistry(PublicKey owner, ConfidentialProofLocation proofLocation)
    {
        ArgumentNullException.ThrowIfNull(proofLocation);
        var accounts = new List<AccountMeta> { AccountMeta.Writable(GetRegistryAddress(owner)) };
        var offset = AppendProofAccount(accounts, proofLocation);
        accounts.Add(AccountMeta.ReadonlySigner(owner));
        return new() { ProgramId = ProgramId, Accounts = accounts, Data = [1, unchecked((byte)offset)] };
    }

    /// <summary>Decodes the fixed 64-byte registry state.</summary>
    /// <param name="data">Exactly 64 bytes: owner followed by ElGamal public-key POD bytes.</param>
    /// <returns>The decoded registry, or <c>null</c> for the wrong length.</returns>
    public static ElGamalRegistryState? DecodeState(ReadOnlySpan<byte> data)
        => data.Length == RegistryStateLength
            ? new ElGamalRegistryState(new(data[..PublicKey.Length]), [.. data[PublicKey.Length..]])
            : null;

    private static sbyte AppendProofAccount(List<AccountMeta> accounts, ConfidentialProofLocation proofLocation)
    {
        if (proofLocation.IsInstructionOffset)
        {
            accounts.Add(AccountMeta.Readonly(InstructionsSysvar));
            return proofLocation.InstructionOffset;
        }

        accounts.Add(AccountMeta.Readonly(proofLocation.ContextStateAccount!.Value));
        return 0;
    }
}

/// <summary>An SPL ElGamal registry account decoded without interpreting its 32-byte cryptographic key.</summary>
/// <param name="owner">The wallet owner associated with the registry.</param>
/// <param name="elGamalPublicKey">The exact 32-byte ElGamal public-key POD.</param>
public sealed class ElGamalRegistryState(PublicKey owner, byte[] elGamalPublicKey)
{
    /// <summary>The wallet owner.</summary>
    public PublicKey Owner { get; } = owner;

    /// <summary>The exact 32-byte ElGamal public-key POD.</summary>
    public ReadOnlyMemory<byte> ElGamalPublicKey { get; } = elGamalPublicKey;
}
