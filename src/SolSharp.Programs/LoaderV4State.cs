using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>The deployment status stored in a Loader V4 program account.</summary>
public enum LoaderV4Status : ulong
{
    /// <summary>The program is in writable maintenance mode.</summary>
    Retracted = 0,

    /// <summary>The program is deployed and executable.</summary>
    Deployed = 1,

    /// <summary>The program is executable and permanently immutable.</summary>
    Finalized = 2
}

/// <summary>Decoded native-layout metadata and program bytes from a Loader V4 account.</summary>
public sealed class LoaderV4State
{
    private readonly byte[] _programBytes;

    private LoaderV4State(
        ulong slot,
        PublicKey authorityOrNextVersion,
        LoaderV4Status status,
        ReadOnlySpan<byte> programBytes)
    {
        Slot = slot;
        AuthorityOrNextVersion = authorityOrNextVersion;
        Status = status;
        _programBytes = programBytes.ToArray();
    }

    /// <summary>The native metadata header length.</summary>
    public const int MetadataLength = 48;

    /// <summary>The slot in which the account was last deployed, retracted, or initialized.</summary>
    public ulong Slot { get; }

    /// <summary>The management authority, or the next-version address for a finalized program.</summary>
    public PublicKey AuthorityOrNextVersion { get; }

    /// <summary>The program deployment status.</summary>
    public LoaderV4Status Status { get; }

    /// <summary>The ELF bytes following the 48-byte native metadata header.</summary>
    public ReadOnlyMemory<byte> ProgramBytes => _programBytes;

    /// <summary>Decodes Loader V4 account data using its fixed native layout.</summary>
    /// <param name="data">The complete program-account data.</param>
    /// <returns>The decoded state.</returns>
    /// <exception cref="ArgumentException">The input is shorter than 48 bytes.</exception>
    /// <exception cref="FormatException">The status value is unknown.</exception>
    public static LoaderV4State Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < MetadataLength)
            throw new ArgumentException($"Loader V4 state requires at least {MetadataLength} bytes.", nameof(data));

        var statusValue = BinaryPrimitives.ReadUInt64LittleEndian(data[40..]);
        if (statusValue > (ulong)LoaderV4Status.Finalized)
            throw new FormatException($"Unknown Loader V4 status {statusValue}.");

        return new LoaderV4State(
            BinaryPrimitives.ReadUInt64LittleEndian(data),
            new PublicKey(data.Slice(8, PublicKey.Length)),
            (LoaderV4Status)statusValue,
            data[MetadataLength..]);
    }

    /// <summary>Attempts to decode Loader V4 account data.</summary>
    /// <param name="data">The complete program-account data.</param>
    /// <param name="state">The decoded state on success; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> when the data contains a valid Loader V4 header.</returns>
    public static bool TryParse(ReadOnlySpan<byte> data, out LoaderV4State? state)
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
}
