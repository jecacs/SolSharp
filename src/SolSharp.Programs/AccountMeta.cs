using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// A reference to an account used by an <see cref="Instruction"/>, with how the runtime must treat it:
/// whether the account has to sign the transaction and whether the instruction may write to it.
/// </summary>
/// <param name="publicKey">The account address.</param>
/// <param name="isSigner">Whether the account must sign the transaction.</param>
/// <param name="isWritable">Whether the instruction may modify the account.</param>
public readonly struct AccountMeta(PublicKey publicKey, bool isSigner, bool isWritable)
{
    /// <summary>The account's address.</summary>
    public PublicKey PublicKey { get; } = publicKey;

    /// <summary>Whether this account must sign the transaction.</summary>
    public bool IsSigner { get; } = isSigner;

    /// <summary>Whether the instruction may modify this account.</summary>
    public bool IsWritable { get; } = isWritable;

    /// <summary>A writable account that must sign.</summary>
    /// <param name="publicKey">The account address.</param>
    /// <returns>A signer + writable <see cref="AccountMeta"/>.</returns>
    public static AccountMeta WritableSigner(PublicKey publicKey) => new(publicKey, isSigner: true, isWritable: true);

    /// <summary>A read-only account that must sign.</summary>
    /// <param name="publicKey">The account address.</param>
    /// <returns>A signer, read-only <see cref="AccountMeta"/>.</returns>
    public static AccountMeta ReadonlySigner(PublicKey publicKey) => new(publicKey, isSigner: true, isWritable: false);

    /// <summary>A writable account that does not sign.</summary>
    /// <param name="publicKey">The account address.</param>
    /// <returns>A non-signer, writable <see cref="AccountMeta"/>.</returns>
    public static AccountMeta Writable(PublicKey publicKey) => new(publicKey, isSigner: false, isWritable: true);

    /// <summary>A read-only account that does not sign.</summary>
    /// <param name="publicKey">The account address.</param>
    /// <returns>A non-signer, read-only <see cref="AccountMeta"/>.</returns>
    public static AccountMeta Readonly(PublicKey publicKey) => new(publicKey, isSigner: false, isWritable: false);
}
