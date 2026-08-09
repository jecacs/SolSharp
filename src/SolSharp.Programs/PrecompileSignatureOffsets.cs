namespace SolSharp.Programs;

/// <summary>One 14-byte Ed25519 precompile offsets record.</summary>
/// <param name="SignatureOffset">Offset to the 64-byte signature.</param>
/// <param name="SignatureInstructionIndex">Instruction containing the signature.</param>
/// <param name="PublicKeyOffset">Offset to the 32-byte public key.</param>
/// <param name="PublicKeyInstructionIndex">Instruction containing the public key.</param>
/// <param name="MessageOffset">Offset to the message.</param>
/// <param name="MessageLength">Message length in bytes.</param>
/// <param name="MessageInstructionIndex">Instruction containing the message.</param>
public readonly record struct Ed25519SignatureOffsets(
    ushort SignatureOffset,
    ushort SignatureInstructionIndex,
    ushort PublicKeyOffset,
    ushort PublicKeyInstructionIndex,
    ushort MessageOffset,
    ushort MessageLength,
    ushort MessageInstructionIndex);

/// <summary>One 14-byte Secp256r1 precompile offsets record.</summary>
/// <param name="SignatureOffset">Offset to the 64-byte compact signature.</param>
/// <param name="SignatureInstructionIndex">Instruction containing the signature.</param>
/// <param name="PublicKeyOffset">Offset to the 33-byte compressed public key.</param>
/// <param name="PublicKeyInstructionIndex">Instruction containing the public key.</param>
/// <param name="MessageOffset">Offset to the message.</param>
/// <param name="MessageLength">Message length in bytes.</param>
/// <param name="MessageInstructionIndex">Instruction containing the message.</param>
public readonly record struct Secp256r1SignatureOffsets(
    ushort SignatureOffset,
    ushort SignatureInstructionIndex,
    ushort PublicKeyOffset,
    ushort PublicKeyInstructionIndex,
    ushort MessageOffset,
    ushort MessageLength,
    ushort MessageInstructionIndex);

/// <summary>One 11-byte Secp256k1 precompile offsets record.</summary>
/// <param name="SignatureOffset">Offset to the 64-byte signature followed by its recovery ID.</param>
/// <param name="SignatureInstructionIndex">Instruction containing the signature.</param>
/// <param name="EthereumAddressOffset">Offset to the 20-byte Ethereum address.</param>
/// <param name="EthereumAddressInstructionIndex">Instruction containing the address.</param>
/// <param name="MessageOffset">Offset to the message.</param>
/// <param name="MessageLength">Message length in bytes.</param>
/// <param name="MessageInstructionIndex">Instruction containing the message.</param>
public readonly record struct Secp256k1SignatureOffsets(
    ushort SignatureOffset,
    byte SignatureInstructionIndex,
    ushort EthereumAddressOffset,
    byte EthereumAddressInstructionIndex,
    ushort MessageOffset,
    ushort MessageLength,
    byte MessageInstructionIndex);
