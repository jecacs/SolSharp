using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Builds client-side feature-gate activation and pending-activation revocation instructions.</summary>
public static class FeatureGateProgram
{
    /// <summary>The runtime feature program address.</summary>
    public static readonly PublicKey ProgramId = PublicKey.Parse(SolanaProgramIds.FeatureProgram);

    /// <summary>The incinerator account that receives lamports from revoked pending activations.</summary>
    public static readonly PublicKey IncineratorId =
        PublicKey.Parse("1nc1nerator11111111111111111111111111111111");

    /// <summary>
    /// Builds the pinned three-instruction activation sequence: transfer funding, allocate nine bytes, then assign
    /// the feature account to the feature program.
    /// </summary>
    /// <param name="featureId">The new feature account; writable and required to sign all account mutations.</param>
    /// <param name="fundingAddress">The writable signer that funds the feature account.</param>
    /// <param name="lamports">The exact number of lamports to transfer.</param>
    /// <returns>The transfer, allocate, and assign instructions in canonical order.</returns>
    public static Instruction[] ActivateWithLamports(
        PublicKey featureId,
        PublicKey fundingAddress,
        ulong lamports) =>
        [
            SystemProgram.Transfer(fundingAddress, featureId, lamports),
            SystemProgram.Allocate(featureId, FeatureAccountState.DataLength),
            SystemProgram.Assign(featureId, ProgramId)
        ];

    /// <summary>Builds the feature program's pending-activation revocation instruction.</summary>
    /// <param name="featureId">The pending feature account; writable and required to sign.</param>
    /// <returns>The revocation instruction with feature, incinerator, and System Program account metas.</returns>
    public static Instruction RevokePendingActivation(PublicKey featureId) => new()
    {
        ProgramId = ProgramId,
        Accounts =
        [
            AccountMeta.WritableSigner(featureId),
            AccountMeta.Writable(IncineratorId),
            AccountMeta.Readonly(SystemProgram.ProgramId)
        ],
        Data = [0]
    };
}
