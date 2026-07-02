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

    /// <summary>The compiled instructions, addressing accounts by their index into the message's account list.</summary>
    IReadOnlyList<CompiledInstruction> Instructions { get; }

    /// <summary>Serializes the message to the exact bytes a signer signs over.</summary>
    /// <returns>The serialized message.</returns>
    byte[] Serialize();

    /// <summary>Returns the exact length of the serialized message, in bytes.</summary>
    /// <returns>The serialized length.</returns>
    int GetSerializedLength();

    /// <summary>Serializes the message into <paramref name="destination"/> without allocating.</summary>
    /// <param name="destination">The span to write into; must be at least <see cref="GetSerializedLength"/> bytes.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is smaller than <see cref="GetSerializedLength"/> bytes.</exception>
    /// <exception cref="FormatException">The message's recent blockhash is not a 32-byte base58 value.</exception>
    int Serialize(Span<byte> destination);

    /// <summary>
    /// Resolves the compiled instructions back into <see cref="Instruction"/>s, mapping each account index to
    /// its public key and signer/writable flags. A v0 message additionally loads accounts from the supplied
    /// address lookup tables (pass every table the message references); a legacy message ignores them.
    /// </summary>
    /// <param name="lookupTables">The resolved lookup tables the message references.</param>
    /// <returns>The resolved instructions, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lookupTables"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A referenced table is not supplied, or an account index is out of range.</exception>
    IReadOnlyList<Instruction> DecompileInstructions(IReadOnlyList<AddressLookupTableAccount> lookupTables);

    /// <summary>Resolves the compiled instructions, for a message that loads no lookup-table accounts.</summary>
    /// <returns>The resolved instructions, in order.</returns>
    /// <exception cref="ArgumentException">The message references lookup tables; use the overload that supplies them.</exception>
    IReadOnlyList<Instruction> DecompileInstructions() => DecompileInstructions([]);
}
