namespace SolSharp.Programs;

/// <summary>
/// Inline compute and fee configuration carried by a SIMD-0385 V1 transaction message. A missing
/// compute-unit or loaded-account-data limit means zero; a missing heap size means 32 KiB.
/// </summary>
/// <remarks>
/// An empty configuration is valid wire data, but its zero compute-unit and loaded-account-data limits
/// normally make it unsuitable for submission. Set the limits required by the transaction before sending it.
/// </remarks>
public sealed record TransactionConfigV1
{
    /// <summary>
    /// The optional total priority fee in lamports. This is a total fee, not micro-lamports per
    /// compute unit; a missing value means zero.
    /// </summary>
    public ulong? PriorityFee { get; init; }

    /// <summary>The optional maximum compute units; a missing value means zero.</summary>
    public uint? ComputeUnitLimit { get; init; }

    /// <summary>The optional maximum loaded account-data bytes; a missing value means zero.</summary>
    public uint? LoadedAccountsDataSizeLimit { get; init; }

    /// <summary>
    /// The optional transaction heap size in bytes. A present value must be a multiple of 1024 from
    /// 32 KiB through 256 KiB; a missing value means 32 KiB.
    /// </summary>
    public uint? HeapSize { get; init; }
}
