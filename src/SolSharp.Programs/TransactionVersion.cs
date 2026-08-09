namespace SolSharp.Programs;

/// <summary>The wire version of a compiled Solana transaction.</summary>
public enum TransactionVersion
{
    /// <summary>The original unversioned transaction message format.</summary>
    Legacy = -1,

    /// <summary>The version 0 message format with address lookup tables.</summary>
    V0 = 0,

    /// <summary>The SIMD-0385 version 1 format with inline transaction configuration.</summary>
    V1 = 1
}
