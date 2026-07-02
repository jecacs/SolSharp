using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models.Token2022;

/// <summary>One transfer-fee schedule entry: the epoch it takes effect, the fee cap, and the rate.</summary>
public sealed record TransferFee
{
    /// <summary>The first epoch the fee takes effect.</summary>
    public required ulong Epoch { get; init; }

    /// <summary>The maximum fee per transfer, in the token's base units.</summary>
    public required ulong MaximumFee { get; init; }

    /// <summary>The fee rate in basis points of the transfer amount (100 = 1%).</summary>
    public required ushort BasisPoints { get; init; }

    // The 18-byte spl_token_2022 TransferFee pod: epoch u64, maximum_fee u64, basis points u16.
    internal const int Length = 18;

    internal static TransferFee Decode(ReadOnlySpan<byte> data) => new()
    {
        Epoch = BinaryPrimitives.ReadUInt64LittleEndian(data),
        MaximumFee = BinaryPrimitives.ReadUInt64LittleEndian(data[8..]),
        BasisPoints = BinaryPrimitives.ReadUInt16LittleEndian(data[16..])
    };
}

/// <summary>
/// The mint's <see cref="ExtensionType.TransferFeeConfig"/> extension: the fee schedule (an older and a
/// newer entry, switched on <see cref="TransferFee.Epoch"/>) and the authorities that manage it.
/// </summary>
public sealed record TransferFeeConfig
{
    /// <summary>The serialized size of the extension value, in bytes (108).</summary>
    public const int Length = 32 + 32 + 8 + TransferFee.Length + TransferFee.Length;

    /// <summary>The authority allowed to change the fee, or <c>null</c> if the fee is immutable.</summary>
    public required PublicKey? TransferFeeConfigAuthority { get; init; }

    /// <summary>The authority allowed to withdraw withheld fees, or <c>null</c> if none.</summary>
    public required PublicKey? WithdrawWithheldAuthority { get; init; }

    /// <summary>Withheld fees moved to the mint and awaiting withdrawal, in base units.</summary>
    public required ulong WithheldAmount { get; init; }

    /// <summary>The fee used while the current epoch is below <see cref="NewerTransferFee"/>'s epoch.</summary>
    public required TransferFee OlderTransferFee { get; init; }

    /// <summary>The fee used once the current epoch reaches its <see cref="TransferFee.Epoch"/>.</summary>
    public required TransferFee NewerTransferFee { get; init; }

    /// <summary>Returns the fee entry in effect at <paramref name="epoch"/>.</summary>
    /// <param name="epoch">The epoch to evaluate the schedule at (e.g. from <c>getEpochInfo</c>).</param>
    /// <returns><see cref="NewerTransferFee"/> once its epoch is reached; <see cref="OlderTransferFee"/> before that.</returns>
    public TransferFee GetEpochFee(ulong epoch)
        => epoch >= NewerTransferFee.Epoch ? NewerTransferFee : OlderTransferFee;

    internal static TransferFeeConfig Decode(ReadOnlySpan<byte> data) => new()
    {
        TransferFeeConfigAuthority = Token2022Layout.ReadOptionalKey(data),
        WithdrawWithheldAuthority = Token2022Layout.ReadOptionalKey(data[32..]),
        WithheldAmount = BinaryPrimitives.ReadUInt64LittleEndian(data[64..]),
        OlderTransferFee = TransferFee.Decode(data[72..]),
        NewerTransferFee = TransferFee.Decode(data[(72 + TransferFee.Length)..])
    };
}
