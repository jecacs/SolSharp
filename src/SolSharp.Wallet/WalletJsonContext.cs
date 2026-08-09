using System.Text.Json.Serialization;

namespace SolSharp.Wallet;

/// <summary>
/// Source-generated metadata for Ed25519 and BLS keypair JSON import and export,
/// keeping key-file processing reflection-free (Native AOT safe).
/// </summary>
[JsonSerializable(typeof(int[]))]
internal sealed partial class WalletJsonContext : JsonSerializerContext;
