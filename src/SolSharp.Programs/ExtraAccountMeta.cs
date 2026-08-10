using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>The kind of seed encoded in an SPL TLV extra-account metadata entry.</summary>
public enum ExtraAccountSeedKind
{
    /// <summary>Literal bytes embedded in the metadata.</summary>
    Literal,

    /// <summary>Bytes copied from the instruction data.</summary>
    InstructionData,

    /// <summary>The public key of an account at an earlier account-list index.</summary>
    AccountKey,

    /// <summary>Bytes copied from the data of an account at an earlier account-list index.</summary>
    AccountData
}

/// <summary>A seed configuration used to resolve an SPL extra-account PDA.</summary>
public sealed class ExtraAccountSeed
{
    private readonly byte[] _literalBytes;

    private ExtraAccountSeed(
        ExtraAccountSeedKind kind,
        byte[]? literalBytes = null,
        byte index = 0,
        byte length = 0,
        byte accountIndex = 0,
        byte dataIndex = 0)
    {
        Kind = kind;
        _literalBytes = literalBytes ?? [];
        Index = index;
        Length = length;
        AccountIndex = accountIndex;
        DataIndex = dataIndex;
    }

    /// <summary>The seed kind.</summary>
    public ExtraAccountSeedKind Kind { get; }

    /// <summary>The literal bytes for a <see cref="ExtraAccountSeedKind.Literal"/> seed.</summary>
    public ReadOnlyMemory<byte> LiteralBytes => _literalBytes;

    /// <summary>The instruction-data or account-key index, depending on <see cref="Kind"/>.</summary>
    public byte Index { get; }

    /// <summary>The number of bytes copied by an instruction-data or account-data seed.</summary>
    public byte Length { get; }

    /// <summary>The source account index for an account-data seed.</summary>
    public byte AccountIndex { get; }

    /// <summary>The byte offset within the source account data.</summary>
    public byte DataIndex { get; }

    internal int EncodedLength => Kind switch
    {
        ExtraAccountSeedKind.Literal => 2 + _literalBytes.Length,
        ExtraAccountSeedKind.InstructionData => 3,
        ExtraAccountSeedKind.AccountKey => 2,
        ExtraAccountSeedKind.AccountData => 4,
        _ => throw new InvalidOperationException("Unknown extra-account seed kind.")
    };

    /// <summary>Creates a literal PDA seed.</summary>
    /// <param name="bytes">The literal bytes; at most 30 bytes fit in one 32-byte address configuration.</param>
    /// <returns>The seed configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="bytes"/> contains more than 30 bytes.</exception>
    public static ExtraAccountSeed Literal(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > 30)
            throw new ArgumentException("A literal extra-account seed may contain at most 30 bytes.", nameof(bytes));
        return new(ExtraAccountSeedKind.Literal, [.. bytes]);
    }

    /// <summary>Creates a seed copied from instruction data.</summary>
    /// <param name="index">The starting byte offset.</param>
    /// <param name="length">The number of bytes to copy.</param>
    /// <returns>The seed configuration.</returns>
    public static ExtraAccountSeed FromInstructionData(byte index, byte length)
        => new(ExtraAccountSeedKind.InstructionData, index: index, length: length);

    /// <summary>Creates a seed from the public key at an account-list index.</summary>
    /// <param name="index">The account-list index.</param>
    /// <returns>The seed configuration.</returns>
    public static ExtraAccountSeed FromAccountKey(byte index)
        => new(ExtraAccountSeedKind.AccountKey, index: index);

    /// <summary>Creates a seed copied from an account's data.</summary>
    /// <param name="accountIndex">The source account-list index.</param>
    /// <param name="dataIndex">The starting byte offset in the account data.</param>
    /// <param name="length">The number of bytes to copy.</param>
    /// <returns>The seed configuration.</returns>
    public static ExtraAccountSeed FromAccountData(byte accountIndex, byte dataIndex, byte length)
        => new(
            ExtraAccountSeedKind.AccountData,
            accountIndex: accountIndex,
            dataIndex: dataIndex,
            length: length);

    /// <summary>Decodes the packed seeds from a 32-byte SPL address configuration.</summary>
    /// <param name="configuration">Exactly 32 configuration bytes.</param>
    /// <returns>The decoded seeds, or <c>null</c> when the configuration is malformed.</returns>
    public static IReadOnlyList<ExtraAccountSeed>? DecodeConfiguration(ReadOnlySpan<byte> configuration)
    {
        if (configuration.Length != ExtraAccountMeta.AddressConfigurationLength)
            return null;

        var seeds = new List<ExtraAccountSeed>();
        var offset = 0;
        while (offset < configuration.Length)
        {
            var discriminator = configuration[offset];
            if (discriminator == 0)
                return seeds;

            ExtraAccountSeed seed;
            switch (discriminator)
            {
                case 1:
                    if (offset + 2 > configuration.Length)
                        return null;
                    var literalLength = configuration[offset + 1];
                    if (offset + 2 + literalLength > configuration.Length)
                        return null;
                    seed = Literal(configuration.Slice(offset + 2, literalLength));
                    break;
                case 2:
                    if (offset + 3 > configuration.Length)
                        return null;
                    seed = FromInstructionData(configuration[offset + 1], configuration[offset + 2]);
                    break;
                case 3:
                    if (offset + 2 > configuration.Length)
                        return null;
                    seed = FromAccountKey(configuration[offset + 1]);
                    break;
                case 4:
                    if (offset + 4 > configuration.Length)
                        return null;
                    seed = FromAccountData(
                        configuration[offset + 1],
                        configuration[offset + 2],
                        configuration[offset + 3]);
                    break;
                default:
                    return null;
            }

            seeds.Add(seed);
            offset += seed.EncodedLength;
        }

        return seeds;
    }

    internal void WriteTo(Span<byte> destination)
    {
        if (destination.Length != EncodedLength)
            throw new ArgumentException("The seed destination has the wrong length.", nameof(destination));

        switch (Kind)
        {
            case ExtraAccountSeedKind.Literal:
                destination[0] = 1;
                destination[1] = checked((byte)_literalBytes.Length);
                _literalBytes.CopyTo(destination[2..]);
                break;
            case ExtraAccountSeedKind.InstructionData:
                destination[0] = 2;
                destination[1] = Index;
                destination[2] = Length;
                break;
            case ExtraAccountSeedKind.AccountKey:
                destination[0] = 3;
                destination[1] = Index;
                break;
            case ExtraAccountSeedKind.AccountData:
                destination[0] = 4;
                destination[1] = AccountIndex;
                destination[2] = DataIndex;
                destination[3] = Length;
                break;
            default:
                throw new InvalidOperationException("Unknown extra-account seed kind.");
        }
    }
}

/// <summary>
/// The 35-byte POD entry used by SPL TLV account resolution to describe a fixed account, a PDA, or a
/// public key copied from instruction/account data.
/// </summary>
public sealed class ExtraAccountMeta
{
    /// <summary>The byte length of an encoded metadata entry.</summary>
    public const int Length = 35;

    /// <summary>The byte length of the address configuration within an entry.</summary>
    public const int AddressConfigurationLength = 32;

    private const byte ExternalProgramBit = 0x80;
    private readonly byte[] _addressConfiguration;
    private readonly byte _signerByte;
    private readonly byte _writableByte;

    private ExtraAccountMeta(byte discriminator, byte[] addressConfiguration, byte signerByte, byte writableByte)
    {
        Discriminator = discriminator;
        _addressConfiguration = addressConfiguration;
        _signerByte = signerByte;
        _writableByte = writableByte;
    }

    /// <summary>
    /// The entry discriminator: 0 for a fixed key, 1 for an executing-program PDA, 2 for key data, or
    /// 128 plus an account index for an external-program PDA.
    /// </summary>
    public byte Discriminator { get; }

    /// <summary>The raw 32-byte address configuration.</summary>
    public ReadOnlyMemory<byte> AddressConfiguration => _addressConfiguration;

    /// <summary>Whether the resolved account requests signer privilege before off-chain de-escalation.</summary>
    public bool IsSigner => _signerByte != 0;

    /// <summary>Whether the resolved account requests writable privilege.</summary>
    public bool IsWritable => _writableByte != 0;

    internal ReadOnlySpan<byte> AddressConfigurationSpan => _addressConfiguration;

    /// <summary>Creates an entry for a fixed public key.</summary>
    /// <param name="publicKey">The required account key.</param>
    /// <param name="isSigner">Whether the entry requests signer privilege.</param>
    /// <param name="isWritable">Whether the entry requests writable privilege.</param>
    /// <returns>The metadata entry.</returns>
    public static ExtraAccountMeta FromPublicKey(PublicKey publicKey, bool isSigner, bool isWritable)
        => new(0, publicKey.ToBytes(), BoolByte(isSigner), BoolByte(isWritable));

    /// <summary>Creates an entry for a PDA owned by the program being invoked.</summary>
    /// <param name="seeds">The PDA seed configurations.</param>
    /// <param name="isSigner">Whether the entry requests signer privilege.</param>
    /// <param name="isWritable">Whether the entry requests writable privilege.</param>
    /// <returns>The metadata entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seeds"/> or an element is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The packed seed configuration exceeds 32 bytes.</exception>
    public static ExtraAccountMeta FromProgramDerivedAddress(
        IReadOnlyList<ExtraAccountSeed> seeds,
        bool isSigner,
        bool isWritable)
        => new(1, PackSeeds(seeds), BoolByte(isSigner), BoolByte(isWritable));

    /// <summary>Creates an entry for a PDA owned by a program at an account-list index.</summary>
    /// <param name="programIndex">The index of the external program account; must be at most 127.</param>
    /// <param name="seeds">The PDA seed configurations.</param>
    /// <param name="isSigner">Whether the entry requests signer privilege.</param>
    /// <param name="isWritable">Whether the entry requests writable privilege.</param>
    /// <returns>The metadata entry.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="programIndex"/> exceeds 127.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="seeds"/> or an element is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The packed seed configuration exceeds 32 bytes.</exception>
    public static ExtraAccountMeta FromExternalProgramDerivedAddress(
        byte programIndex,
        IReadOnlyList<ExtraAccountSeed> seeds,
        bool isSigner,
        bool isWritable)
    {
        if (programIndex >= ExternalProgramBit)
            throw new ArgumentOutOfRangeException(nameof(programIndex), programIndex, "An external program index must be at most 127.");
        return new(
            checked((byte)(ExternalProgramBit + programIndex)),
            PackSeeds(seeds),
            BoolByte(isSigner),
            BoolByte(isWritable));
    }

    /// <summary>Creates an entry for a public key stored in instruction data.</summary>
    /// <param name="dataIndex">The starting byte offset of the 32-byte key.</param>
    /// <param name="isSigner">Whether the entry requests signer privilege.</param>
    /// <param name="isWritable">Whether the entry requests writable privilege.</param>
    /// <returns>The metadata entry.</returns>
    public static ExtraAccountMeta FromInstructionDataPublicKey(byte dataIndex, bool isSigner, bool isWritable)
    {
        var configuration = new byte[AddressConfigurationLength];
        configuration[0] = 1;
        configuration[1] = dataIndex;
        return new(2, configuration, BoolByte(isSigner), BoolByte(isWritable));
    }

    /// <summary>Creates an entry for a public key stored in account data.</summary>
    /// <param name="accountIndex">The source account-list index.</param>
    /// <param name="dataIndex">The starting byte offset of the 32-byte key.</param>
    /// <param name="isSigner">Whether the entry requests signer privilege.</param>
    /// <param name="isWritable">Whether the entry requests writable privilege.</param>
    /// <returns>The metadata entry.</returns>
    public static ExtraAccountMeta FromAccountDataPublicKey(
        byte accountIndex,
        byte dataIndex,
        bool isSigner,
        bool isWritable)
    {
        var configuration = new byte[AddressConfigurationLength];
        configuration[0] = 2;
        configuration[1] = accountIndex;
        configuration[2] = dataIndex;
        return new(2, configuration, BoolByte(isSigner), BoolByte(isWritable));
    }

    /// <summary>Decodes one exact 35-byte SPL extra-account metadata entry.</summary>
    /// <param name="data">The encoded entry.</param>
    /// <returns>The entry, or <c>null</c> when <paramref name="data"/> is not exactly 35 bytes.</returns>
    public static ExtraAccountMeta? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length != Length)
            return null;
        return new(data[0], [.. data.Slice(1, AddressConfigurationLength)], data[33], data[34]);
    }

    /// <summary>Encodes this entry in the pinned SPL 35-byte POD layout.</summary>
    /// <returns>The encoded entry.</returns>
    public byte[] Encode()
    {
        var data = new byte[Length];
        data[0] = Discriminator;
        _addressConfiguration.CopyTo(data, 1);
        data[33] = _signerByte;
        data[34] = _writableByte;
        return data;
    }

    /// <summary>Attempts to read a fixed public key from this entry.</summary>
    /// <param name="publicKey">The fixed key on success.</param>
    /// <returns><c>true</c> when this is a fixed-key entry.</returns>
    public bool TryGetPublicKey(out PublicKey publicKey)
    {
        if (Discriminator == 0)
        {
            publicKey = new(_addressConfiguration);
            return true;
        }

        publicKey = default;
        return false;
    }

    /// <summary>Decodes this entry's PDA seeds.</summary>
    /// <returns>The seed list, or <c>null</c> when the entry is not a PDA or its configuration is malformed.</returns>
    public IReadOnlyList<ExtraAccountSeed>? DecodeSeeds()
        => Discriminator is 1 or >= ExternalProgramBit
            ? ExtraAccountSeed.DecodeConfiguration(_addressConfiguration)
            : null;

    private static byte BoolByte(bool value) => value ? (byte)1 : (byte)0;

    private static byte[] PackSeeds(IReadOnlyList<ExtraAccountSeed> seeds)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        var configuration = new byte[AddressConfigurationLength];
        var offset = 0;
        for (var i = 0; i < seeds.Count; i++)
        {
            var seed = seeds[i] ?? throw new ArgumentNullException(nameof(seeds), $"Seed at index {i} is null.");
            if (offset + seed.EncodedLength > configuration.Length)
                throw new ArgumentException("Packed extra-account seed configurations may occupy at most 32 bytes.", nameof(seeds));
            seed.WriteTo(configuration.AsSpan(offset, seed.EncodedLength));
            offset += seed.EncodedLength;
        }

        return configuration;
    }
}
