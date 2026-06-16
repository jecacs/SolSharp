using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// A compiled transaction message — a legacy <see cref="Message"/> or a versioned <see cref="MessageV0"/> —
/// that a <see cref="Transaction"/> can sign and serialize.
/// </summary>
public interface ITransactionMessage
{
    /// <summary>The number of leading account keys that must sign the transaction.</summary>
    byte RequiredSignatures { get; }

    /// <summary>The static account keys; the first <see cref="RequiredSignatures"/> are the signers, fee payer first.</summary>
    IReadOnlyList<PublicKey> AccountKeys { get; }

    /// <summary>Serializes the message to the exact bytes a signer signs over.</summary>
    /// <returns>The serialized message.</returns>
    byte[] Serialize();
}
