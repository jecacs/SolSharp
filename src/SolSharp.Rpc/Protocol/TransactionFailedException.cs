using System.Text.Json;

namespace SolSharp.Rpc.Protocol;

/// <summary>
/// Thrown by <see cref="SolanaRpcClient.SendAndConfirmTransactionAsync"/> when a transaction is confirmed
/// but executed with an on-chain error, so the caller never mistakes a landed-but-failed transaction for success.
/// </summary>
/// <param name="signature">The transaction signature (base58).</param>
/// <param name="error">The on-chain error reported by the cluster.</param>
public sealed class TransactionFailedException(string signature, JsonElement? error)
    : Exception($"Transaction {signature} was confirmed but failed on-chain: {error}")
{
    /// <summary>The signature of the transaction that failed.</summary>
    public string Signature { get; } = signature;

    /// <summary>The on-chain error reported by the cluster.</summary>
    public JsonElement? Error { get; } = error;
}
