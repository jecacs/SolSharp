using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Builds instructions for the deprecated non-upgradeable BPF loader interface.</summary>
public static class LegacyBpfLoaderProgram
{
    /// <summary>The BPF Loader 2 address used by the final non-upgradeable loader.</summary>
    public static readonly PublicKey ProgramId = PublicKey.Parse("BPFLoader2111111111111111111111111111111111");

    /// <summary>The original BPF loader address accepted by the generic legacy builders.</summary>
    public static readonly PublicKey OriginalProgramId = PublicKey.Parse("BPFLoader1111111111111111111111111111111111");

    private static readonly PublicKey RentSysvar = PublicKey.Parse(Sysvars.Rent);

    /// <summary>Writes program bytes into a legacy loader-owned account.</summary>
    /// <param name="programAccount">The writable program-account signer.</param>
    /// <param name="offset">The byte offset at which to write.</param>
    /// <param name="bytes">The program bytes.</param>
    /// <param name="loaderProgramId">The selected legacy loader; defaults to BPF Loader 2.</param>
    /// <returns>The legacy write instruction.</returns>
    [Obsolete("The non-upgradeable BPF loaders are deprecated; use LoaderV4Program instead.")]
    public static Instruction Write(
        PublicKey programAccount,
        uint offset,
        ReadOnlySpan<byte> bytes,
        PublicKey? loaderProgramId = null)
    {
        var payload = bytes.ToArray();
        return new()
        {
            ProgramId = loaderProgramId ?? ProgramId,
            Accounts = [AccountMeta.WritableSigner(programAccount)],
            Data = ProgramWireEncoding.Build(0, stream =>
            {
                ProgramWireEncoding.WriteUInt32(stream, offset);
                ProgramWireEncoding.WriteByteVector(stream, payload);
            })
        };
    }

    /// <summary>Finalizes a legacy loader-owned program account for execution.</summary>
    /// <param name="programAccount">The writable program-account signer.</param>
    /// <param name="loaderProgramId">The selected legacy loader; defaults to BPF Loader 2.</param>
    /// <returns>The legacy finalize instruction.</returns>
    [Obsolete("The non-upgradeable BPF loaders are deprecated; use LoaderV4Program instead.")]
    public static Instruction Finalize(PublicKey programAccount, PublicKey? loaderProgramId = null)
        => new()
        {
            ProgramId = loaderProgramId ?? ProgramId,
            Accounts = [AccountMeta.WritableSigner(programAccount), AccountMeta.Readonly(RentSysvar)],
            Data = ProgramWireEncoding.Build(1)
        };
}
