using System.Buffers.Binary;

namespace SolSharp.Programs;

/// <summary>The canonical nine-byte state stored by a Solana runtime feature account.</summary>
public sealed record FeatureAccountState
{
    /// <summary>The fixed feature-account allocation used by the pinned SDK.</summary>
    public const int DataLength = 9;

    private FeatureAccountState(ulong? activatedAt)
    {
        ActivatedAt = activatedAt;
    }

    /// <summary>
    /// The slot at which the feature became active, or <c>null</c> while activation has only been requested.
    /// </summary>
    public ulong? ActivatedAt { get; }

    /// <summary><c>true</c> when the feature account records an activation slot.</summary>
    public bool IsActive => ActivatedAt.HasValue;

    /// <summary>Decodes a feature account's bincode state.</summary>
    /// <param name="data">At least nine account-data bytes.</param>
    /// <returns>The decoded feature state.</returns>
    /// <exception cref="ArgumentException">The data is too short or has a non-canonical option tag.</exception>
    public static FeatureAccountState Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < DataLength)
            throw new ArgumentException($"Feature account data must be at least {DataLength} bytes.", nameof(data));

        return data[0] switch
        {
            0 => new((ulong?)null),
            1 => new(BinaryPrimitives.ReadUInt64LittleEndian(data[1..])),
            _ => throw new ArgumentException("Feature activation option tag must be 0 or 1.", nameof(data))
        };
    }

    /// <summary>Tries to decode a feature account without throwing.</summary>
    /// <param name="data">The account data.</param>
    /// <param name="state">The decoded state on success; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> when the state is canonical.</returns>
    public static bool TryParse(ReadOnlySpan<byte> data, out FeatureAccountState? state)
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
    }
}
