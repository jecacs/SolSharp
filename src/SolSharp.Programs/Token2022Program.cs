using System.Buffers.Binary;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// Builds Token-2022-specific instructions. Base SPL Token operations remain available through
/// <see cref="TokenProgram"/> by supplying <see cref="ProgramId"/> as the token-program override.
/// </summary>
public static partial class Token2022Program
{
    /// <summary>The Token-2022 program address.</summary>
    public static readonly PublicKey ProgramId = PublicKey.Parse(SolanaProgramIds.Token2022Program);
    private const byte GetAccountDataSizeDiscriminator = 21;
    private const byte InitializeMintCloseAuthorityDiscriminator = 25;
    private const byte ReallocateDiscriminator = 29;
    private const byte CreateNativeMintDiscriminator = 31;
    private const byte InitializeNonTransferableMintDiscriminator = 32;
    private const byte InitializePermanentDelegateDiscriminator = 35;
    private const int MaxMultisigSigners = 11;

    private static readonly PublicKey NativeMint = PublicKey.Parse(Mints.WrappedSol);

    /// <summary>Requests the token-account size for a mint plus additional Token-2022 extensions.</summary>
    /// <param name="mint">The mint whose existing configuration contributes required account extensions.</param>
    /// <param name="extensionTypes">Additional extension types to include in the returned size.</param>
    /// <returns>The Token-2022 getAccountDataSize instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="extensionTypes"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An element is not a defined Token-2022 extension type.</exception>
    public static Instruction GetAccountDataSize(
        PublicKey mint,
        IReadOnlyList<Token2022ExtensionType> extensionTypes)
        => new()
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.Readonly(mint)],
            Data = ExtensionTypesData(GetAccountDataSizeDiscriminator, extensionTypes)
        };

    /// <summary>Initializes the close authority extension on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="closeAuthority">The authority allowed to close the mint, or <c>null</c> for none.</param>
    /// <returns>The initializeMintCloseAuthority instruction.</returns>
    public static Instruction InitializeMintCloseAuthority(PublicKey mint, PublicKey? closeAuthority)
        => new()
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.Writable(mint)],
            Data = OptionalPublicKeyData(InitializeMintCloseAuthorityDiscriminator, closeAuthority)
        };

    /// <summary>Reallocates a token account to make room for additional Token-2022 extensions.</summary>
    /// <param name="account">The writable token account to resize.</param>
    /// <param name="payer">The writable signer funding any additional rent.</param>
    /// <param name="owner">The token account owner; signs.</param>
    /// <param name="extensionTypes">The complete extension types to add.</param>
    /// <returns>The reallocate instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="extensionTypes"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An element is not a defined Token-2022 extension type.</exception>
    public static Instruction Reallocate(
        PublicKey account,
        PublicKey payer,
        PublicKey owner,
        IReadOnlyList<Token2022ExtensionType> extensionTypes)
        => new()
        {
            ProgramId = ProgramId,
            Accounts =
            [
                AccountMeta.Writable(account),
                AccountMeta.WritableSigner(payer),
                AccountMeta.Readonly(SystemProgram.ProgramId),
                AccountMeta.ReadonlySigner(owner)
            ],
            Data = ExtensionTypesData(ReallocateDiscriminator, extensionTypes)
        };

    /// <summary>Reallocates a token account using a multisig owner.</summary>
    /// <param name="account">The writable token account to resize.</param>
    /// <param name="payer">The writable signer funding any additional rent.</param>
    /// <param name="owner">The multisig owner account.</param>
    /// <param name="extensionTypes">The complete extension types to add.</param>
    /// <param name="multisigSigners">The multisig member accounts that sign, in account order.</param>
    /// <returns>The reallocate instruction.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="extensionTypes"/> or <paramref name="multisigSigners"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="multisigSigners"/> is empty or contains more than 11 accounts.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An extension element is not a defined Token-2022 type.</exception>
    public static Instruction Reallocate(
        PublicKey account,
        PublicKey payer,
        PublicKey owner,
        IReadOnlyList<Token2022ExtensionType> extensionTypes,
        IReadOnlyList<PublicKey> multisigSigners)
        => WithMultisigAuthority(Reallocate(account, payer, owner, extensionTypes), authorityIndex: 3, multisigSigners);

    /// <summary>Creates the Token-2022 native wrapped-SOL mint.</summary>
    /// <param name="payer">The writable system-account signer funding native-mint creation.</param>
    /// <returns>The createNativeMint instruction.</returns>
    public static Instruction CreateNativeMint(PublicKey payer)
        => new()
        {
            ProgramId = ProgramId,
            Accounts =
            [
                AccountMeta.WritableSigner(payer),
                AccountMeta.Writable(NativeMint),
                AccountMeta.Readonly(SystemProgram.ProgramId)
            ],
            Data = [CreateNativeMintDiscriminator]
        };

    /// <summary>Initializes the non-transferable extension on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <returns>The initializeNonTransferableMint instruction.</returns>
    public static Instruction InitializeNonTransferableMint(PublicKey mint)
        => WritableMintInstruction(mint, InitializeNonTransferableMintDiscriminator);

    /// <summary>Initializes the permanent delegate extension on a new Token-2022 mint.</summary>
    /// <param name="mint">The uninitialized writable mint.</param>
    /// <param name="delegate">The permanent delegate encoded in instruction data.</param>
    /// <returns>The initializePermanentDelegate instruction.</returns>
    public static Instruction InitializePermanentDelegate(PublicKey mint, PublicKey @delegate)
    {
        var data = new byte[PublicKey.Length + 1];
        data[0] = InitializePermanentDelegateDiscriminator;
        @delegate.CopyTo(data.AsSpan(1));
        return new() { ProgramId = ProgramId, Accounts = [AccountMeta.Writable(mint)], Data = data };
    }

    private static Instruction WritableMintInstruction(PublicKey mint, byte discriminator)
        => new() { ProgramId = ProgramId, Accounts = [AccountMeta.Writable(mint)], Data = [discriminator] };

    private static byte[] ExtensionTypesData(
        byte discriminator,
        IReadOnlyList<Token2022ExtensionType> extensionTypes)
    {
        ArgumentNullException.ThrowIfNull(extensionTypes);
        var data = new byte[1 + (extensionTypes.Count * sizeof(ushort))];
        data[0] = discriminator;
        for (var i = 0; i < extensionTypes.Count; i++)
        {
            var extensionType = extensionTypes[i];
            if ((ushort)extensionType > (ushort)Token2022ExtensionType.PermissionedBurn)
                throw new ArgumentOutOfRangeException(
                    nameof(extensionTypes),
                    extensionType,
                    $"Unknown Token-2022 extension type at index {i}.");
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(1 + (i * sizeof(ushort))), (ushort)extensionType);
        }

        return data;
    }

    private static byte[] OptionalPublicKeyData(byte discriminator, PublicKey? publicKey)
    {
        if (publicKey is null)
            return [discriminator, 0];

        var data = new byte[PublicKey.Length + 2];
        data[0] = discriminator;
        data[1] = 1;
        publicKey.Value.CopyTo(data.AsSpan(2));
        return data;
    }

    private static Instruction WithMultisigAuthority(
        Instruction instruction,
        int authorityIndex,
        IReadOnlyList<PublicKey> multisigSigners)
    {
        ArgumentNullException.ThrowIfNull(multisigSigners);
        if (multisigSigners.Count is < 1 or > MaxMultisigSigners)
            throw new ArgumentException(
                $"A Token-2022 multisig requires between 1 and {MaxMultisigSigners} member signers.",
                nameof(multisigSigners));

        var accounts = new AccountMeta[instruction.Accounts.Count + multisigSigners.Count];
        for (var i = 0; i < instruction.Accounts.Count; i++)
            accounts[i] = instruction.Accounts[i];
        accounts[authorityIndex] = AccountMeta.Readonly(instruction.Accounts[authorityIndex].PublicKey);
        for (var i = 0; i < multisigSigners.Count; i++)
            accounts[instruction.Accounts.Count + i] = AccountMeta.ReadonlySigner(multisigSigners[i]);

        return new() { ProgramId = instruction.ProgramId, Accounts = accounts, Data = [.. instruction.Data] };
    }
}
