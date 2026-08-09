namespace SolSharp.Programs;

/// <summary>A required field in the SPL token-metadata interface.</summary>
public enum TokenMetadataField : byte
{
    /// <summary>The token's display name.</summary>
    Name = 0,

    /// <summary>The token's short symbol.</summary>
    Symbol = 1,

    /// <summary>The URI of the token's off-chain metadata.</summary>
    Uri = 2
}
