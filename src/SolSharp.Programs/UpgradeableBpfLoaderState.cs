using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>The account variant used by the upgradeable BPF loader.</summary>
public enum UpgradeableBpfLoaderStateKind : uint
{
    /// <summary>The account is uninitialized.</summary>
    Uninitialized = 0,

    /// <summary>The account is a program-data staging buffer.</summary>
    Buffer = 1,

    /// <summary>The account is the executable program facade.</summary>
    Program = 2,

    /// <summary>The account stores deployed program bytes and upgrade metadata.</summary>
    ProgramData = 3
}

/// <summary>Decoded metadata and program bytes from an upgradeable-loader account.</summary>
public sealed class UpgradeableBpfLoaderState
{
    /// <summary>The uninitialized-state metadata size.</summary>
    public const int UninitializedMetadataLength = 4;

    /// <summary>The fixed runtime buffer metadata size, including the optional-authority slot.</summary>
    public const int BufferMetadataLength = 37;

    /// <summary>The executable Program metadata size.</summary>
    public const int ProgramMetadataLength = 36;

    /// <summary>The fixed runtime ProgramData metadata size, including the optional-authority slot.</summary>
    public const int ProgramDataMetadataLength = 45;
    private readonly byte[] _programBytes;

    private UpgradeableBpfLoaderState(
        UpgradeableBpfLoaderStateKind kind,
        PublicKey? authority,
        PublicKey? programDataAddress,
        ulong? slot,
        ReadOnlySpan<byte> programBytes)
    {
        Kind = kind;
        Authority = authority;
        ProgramDataAddress = programDataAddress;
        Slot = slot;
        _programBytes = [.. programBytes];
    }

    /// <summary>The decoded account variant.</summary>
    public UpgradeableBpfLoaderStateKind Kind { get; }

    /// <summary>The buffer or upgrade authority, or <c>null</c> for immutable accounts.</summary>
    public PublicKey? Authority { get; }

    /// <summary>The ProgramData address referenced by a Program account.</summary>
    public PublicKey? ProgramDataAddress { get; }

    /// <summary>The last modification slot for ProgramData.</summary>
    public ulong? Slot { get; }

    /// <summary>
    /// The bytes after the runtime's fixed Buffer or ProgramData metadata region: offset 37 or 45,
    /// respectively, including when <see cref="Authority"/> is <c>null</c>.
    /// </summary>
    public ReadOnlyMemory<byte> ProgramBytes => _programBytes;

    /// <summary>Decodes upgradeable-loader account data.</summary>
    /// <remarks>
    /// This decoder follows the raw loader runtime layout, whose Buffer and ProgramData payload offsets remain
    /// fixed at 37 and 45 when the authority option is <c>None</c>. Compact bincode and Agave's
    /// <c>jsonParsed</c> client decoder instead use 5-byte and 13-byte metadata in that case.
    /// </remarks>
    /// <param name="data">The complete account data.</param>
    /// <returns>The decoded state and any trailing program bytes.</returns>
    /// <exception cref="ArgumentException">The input is too short for its state variant.</exception>
    /// <exception cref="FormatException">The discriminator or optional-authority tag is invalid.</exception>
    public static UpgradeableBpfLoaderState Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < sizeof(uint))
            throw new ArgumentException("Upgradeable-loader state requires a four-byte discriminator.", nameof(data));

        var kindValue = BinaryPrimitives.ReadUInt32LittleEndian(data);
        return kindValue switch
        {
            0 => new(
                UpgradeableBpfLoaderStateKind.Uninitialized,
                null,
                null,
                null,
                []),
            1 => ParseBuffer(data),
            2 => ParseProgram(data),
            3 => ParseProgramData(data),
            _ => throw new FormatException($"Unknown upgradeable-loader state discriminator {kindValue}.")
        };
    }

    /// <summary>Attempts to decode upgradeable-loader account data.</summary>
    /// <param name="data">The complete account data.</param>
    /// <param name="state">The decoded state on success; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> when the account data is valid.</returns>
    public static bool TryParse(ReadOnlySpan<byte> data, out UpgradeableBpfLoaderState? state)
    {
        try
        {
            state = Parse(data);
            return true;
        }
        catch (ArgumentException)
        {
            state = null;
            return false;
        }
        catch (FormatException)
        {
            state = null;
            return false;
        }
    }

    private static UpgradeableBpfLoaderState ParseBuffer(ReadOnlySpan<byte> data)
    {
        EnsureLength(data, BufferMetadataLength);
        return new(
            UpgradeableBpfLoaderStateKind.Buffer,
            ReadOptionalPublicKey(data[4..], "buffer authority"),
            null,
            null,
            data[BufferMetadataLength..]);
    }

    private static UpgradeableBpfLoaderState ParseProgram(ReadOnlySpan<byte> data)
    {
        EnsureLength(data, ProgramMetadataLength);
        return new(
            UpgradeableBpfLoaderStateKind.Program,
            null,
            new PublicKey(data.Slice(4, PublicKey.Length)),
            null,
            []);
    }

    private static UpgradeableBpfLoaderState ParseProgramData(ReadOnlySpan<byte> data)
    {
        EnsureLength(data, ProgramDataMetadataLength);
        return new(
            UpgradeableBpfLoaderStateKind.ProgramData,
            ReadOptionalPublicKey(data[12..], "upgrade authority"),
            null,
            BinaryPrimitives.ReadUInt64LittleEndian(data[4..]),
            data[ProgramDataMetadataLength..]);
    }

    private static PublicKey? ReadOptionalPublicKey(ReadOnlySpan<byte> data, string description)
        => data[0] switch
        {
            0 => null,
            1 => new PublicKey(data.Slice(1, PublicKey.Length)),
            var tag => throw new FormatException($"Invalid {description} option tag {tag}.")
        };

    private static void EnsureLength(ReadOnlySpan<byte> data, int required)
    {
        if (data.Length < required)
            throw new ArgumentException($"State requires at least {required} bytes, got {data.Length}.", nameof(data));
    }
}
