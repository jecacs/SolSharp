using System.Text.Json.Serialization;

namespace SolSharp.Wallet;

/// <summary>
/// Source-generated metadata for <see cref="Keypair.FromJsonArray"/>'s key-file format, keeping the
/// parse reflection-free (Native AOT safe).
/// </summary>
[JsonSerializable(typeof(int[]))]
internal sealed partial class WalletJsonContext : JsonSerializerContext;
