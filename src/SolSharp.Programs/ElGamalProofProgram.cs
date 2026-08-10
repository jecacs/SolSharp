using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>The instruction discriminators of the native ZK ElGamal proof program.</summary>
public enum ElGamalProofInstruction : byte
{
    /// <summary>Closes a proof-context state account.</summary>
    CloseContextState = 0,

    /// <summary>Verifies a zero-ciphertext proof.</summary>
    VerifyZeroCiphertext = 1,

    /// <summary>Verifies equality between two ElGamal ciphertexts.</summary>
    VerifyCiphertextCiphertextEquality = 2,

    /// <summary>Verifies equality between an ElGamal ciphertext and a Pedersen commitment.</summary>
    VerifyCiphertextCommitmentEquality = 3,

    /// <summary>Verifies knowledge of the secret key for an ElGamal public key.</summary>
    VerifyPubkeyValidity = 4,

    /// <summary>Verifies a percentage-with-cap relation.</summary>
    VerifyPercentageWithCap = 5,

    /// <summary>Verifies a batched 64-bit range proof.</summary>
    VerifyBatchedRangeProofU64 = 6,

    /// <summary>Verifies a batched 128-bit range proof.</summary>
    VerifyBatchedRangeProofU128 = 7,

    /// <summary>Verifies a batched 256-bit range proof.</summary>
    VerifyBatchedRangeProofU256 = 8,

    /// <summary>Verifies a grouped ciphertext with two decryption handles.</summary>
    VerifyGroupedCiphertext2HandlesValidity = 9,

    /// <summary>Verifies two grouped ciphertexts with two decryption handles.</summary>
    VerifyBatchedGroupedCiphertext2HandlesValidity = 10,

    /// <summary>Verifies a grouped ciphertext with three decryption handles.</summary>
    VerifyGroupedCiphertext3HandlesValidity = 11,

    /// <summary>Verifies two grouped ciphertexts with three decryption handles.</summary>
    VerifyBatchedGroupedCiphertext3HandlesValidity = 12
}

/// <summary>
/// Builds native ZK ElGamal proof-program instructions from already-generated proof POD bytes.
/// Proof creation is intentionally outside this API; the supplied bytes must come from a compatible
/// cryptographic implementation.
/// </summary>
public static class ElGamalProofProgram
{
    /// <summary>The native ZK ElGamal proof program address.</summary>
    public static readonly PublicKey ProgramId = PublicKey.Parse("ZkE1Gama1Proof11111111111111111111111111111");

    /// <summary>Closes a proof-context account and sends its lamports to a destination.</summary>
    /// <param name="contextStateAccount">The writable proof-context account.</param>
    /// <param name="destination">The writable lamport destination.</param>
    /// <param name="contextStateAuthority">The context owner; signs.</param>
    /// <returns>The close-context-state instruction.</returns>
    public static Instruction CloseContextState(
        PublicKey contextStateAccount,
        PublicKey destination,
        PublicKey contextStateAuthority)
        => new()
        {
            ProgramId = ProgramId,
            Accounts =
            [
                AccountMeta.Writable(contextStateAccount),
                AccountMeta.Writable(destination),
                AccountMeta.ReadonlySigner(contextStateAuthority)
            ],
            Data = [(byte)ElGamalProofInstruction.CloseContextState]
        };

    /// <summary>Builds a verification instruction with the complete precomputed proof POD in instruction data.</summary>
    /// <param name="proofInstruction">The proof verifier to invoke.</param>
    /// <param name="proofData">The complete upstream proof-data POD, excluding the one-byte discriminator.</param>
    /// <param name="contextStateAccount">An optional writable account in which to store the verified context.</param>
    /// <param name="contextStateAuthority">The context owner, required exactly when a context account is supplied.</param>
    /// <returns>The proof verification instruction.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="proofInstruction"/> is not a verifier.</exception>
    /// <exception cref="ArgumentException">Only one of the two context arguments is supplied.</exception>
    public static Instruction VerifyProof(
        ElGamalProofInstruction proofInstruction,
        ReadOnlySpan<byte> proofData,
        PublicKey? contextStateAccount = null,
        PublicKey? contextStateAuthority = null)
    {
        ValidateProofInstruction(proofInstruction);
        var expectedLength = GetProofDataLength(proofInstruction);
        if (proofData.Length != expectedLength)
            throw new ArgumentException(
                $"The {proofInstruction} proof-data POD must contain exactly {expectedLength} bytes, got {proofData.Length}.",
                nameof(proofData));
        var data = new byte[proofData.Length + 1];
        data[0] = (byte)proofInstruction;
        proofData.CopyTo(data.AsSpan(1));
        return new()
        {
            ProgramId = ProgramId,
            Accounts = ContextAccounts(contextStateAccount, contextStateAuthority),
            Data = data
        };
    }

    /// <summary>Builds a verifier that reads a precomputed proof POD from an account at a byte offset.</summary>
    /// <param name="proofInstruction">The proof verifier to invoke.</param>
    /// <param name="proofAccount">The readonly account containing proof bytes.</param>
    /// <param name="proofDataOffset">The byte offset of the proof POD within the account.</param>
    /// <param name="contextStateAccount">An optional writable account in which to store the verified context.</param>
    /// <param name="contextStateAuthority">The context owner, required exactly when a context account is supplied.</param>
    /// <returns>The proof verification instruction.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="proofInstruction"/> is not a verifier.</exception>
    /// <exception cref="ArgumentException">Only one of the two context arguments is supplied.</exception>
    public static Instruction VerifyProofFromAccount(
        ElGamalProofInstruction proofInstruction,
        PublicKey proofAccount,
        uint proofDataOffset,
        PublicKey? contextStateAccount = null,
        PublicKey? contextStateAuthority = null)
    {
        ValidateProofInstruction(proofInstruction);
        var accounts = new List<AccountMeta> { AccountMeta.Readonly(proofAccount) };
        accounts.AddRange(ContextAccounts(contextStateAccount, contextStateAuthority));
        var data = new byte[1 + sizeof(uint)];
        data[0] = (byte)proofInstruction;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(1), proofDataOffset);
        return new() { ProgramId = ProgramId, Accounts = accounts, Data = data };
    }

    /// <summary>Decodes the instruction discriminator from native proof-program data.</summary>
    /// <param name="data">Instruction data.</param>
    /// <param name="proofInstruction">The decoded instruction on success.</param>
    /// <returns><c>true</c> when the first byte is a defined proof-program discriminator.</returns>
    public static bool TryDecodeInstruction(ReadOnlySpan<byte> data, out ElGamalProofInstruction proofInstruction)
    {
        if (!data.IsEmpty && Enum.IsDefined((ElGamalProofInstruction)data[0]))
        {
            proofInstruction = (ElGamalProofInstruction)data[0];
            return true;
        }

        proofInstruction = default;
        return false;
    }

    /// <summary>Gets the exact upstream proof-data POD length for a verifier.</summary>
    /// <param name="proofInstruction">A proof verification discriminator.</param>
    /// <returns>The required POD byte length, excluding the discriminator.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="proofInstruction"/> is not a verifier.</exception>
    public static int GetProofDataLength(ElGamalProofInstruction proofInstruction)
    {
        ValidateProofInstruction(proofInstruction);
        return proofInstruction switch
        {
            ElGamalProofInstruction.VerifyZeroCiphertext => 192,
            ElGamalProofInstruction.VerifyCiphertextCiphertextEquality => 416,
            ElGamalProofInstruction.VerifyCiphertextCommitmentEquality => 320,
            ElGamalProofInstruction.VerifyPubkeyValidity => 96,
            ElGamalProofInstruction.VerifyPercentageWithCap => 360,
            ElGamalProofInstruction.VerifyBatchedRangeProofU64 => 936,
            ElGamalProofInstruction.VerifyBatchedRangeProofU128 => 1000,
            ElGamalProofInstruction.VerifyBatchedRangeProofU256 => 1064,
            ElGamalProofInstruction.VerifyGroupedCiphertext2HandlesValidity => 320,
            ElGamalProofInstruction.VerifyBatchedGroupedCiphertext2HandlesValidity => 416,
            ElGamalProofInstruction.VerifyGroupedCiphertext3HandlesValidity => 416,
            ElGamalProofInstruction.VerifyBatchedGroupedCiphertext3HandlesValidity => 544,
            _ => throw new ArgumentOutOfRangeException(nameof(proofInstruction), proofInstruction, "Unknown proof verifier.")
        };
    }

    private static IReadOnlyList<AccountMeta> ContextAccounts(
        PublicKey? contextStateAccount,
        PublicKey? contextStateAuthority)
    {
        if (contextStateAccount.HasValue != contextStateAuthority.HasValue)
            throw new ArgumentException("A proof context account and its authority must be supplied together.");
        return contextStateAccount is { } account
            ? [AccountMeta.Writable(account), AccountMeta.Readonly(contextStateAuthority!.Value)]
            : [];
    }

    private static void ValidateProofInstruction(ElGamalProofInstruction proofInstruction)
    {
        if (!Enum.IsDefined(proofInstruction) || proofInstruction == ElGamalProofInstruction.CloseContextState)
            throw new ArgumentOutOfRangeException(
                nameof(proofInstruction),
                proofInstruction,
                "The instruction must be a defined proof verification discriminator.");
    }
}
