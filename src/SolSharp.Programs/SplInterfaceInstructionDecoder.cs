namespace SolSharp.Programs;

public static partial class Token2022Program
{
    /// <summary>Decodes an SPL token-metadata interface instruction discriminator.</summary>
    /// <param name="data">Complete instruction data.</param>
    /// <returns>The variant and raw Borsh payload, or <c>null</c> for an unknown/short discriminator.</returns>
    public static DecodedSplInterfaceInstruction? DecodeTokenMetadataInstructionData(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
            return null;
        var discriminator = data[..8];
        var name = discriminator.SequenceEqual(InitializeMetadataDiscriminator) ? "Initialize" :
            discriminator.SequenceEqual(UpdateMetadataFieldDiscriminator) ? "UpdateField" :
            discriminator.SequenceEqual(RemoveMetadataKeyDiscriminator) ? "RemoveKey" :
            discriminator.SequenceEqual(UpdateMetadataAuthorityDiscriminator) ? "UpdateAuthority" :
            discriminator.SequenceEqual(EmitMetadataDiscriminator) ? "Emit" : null;
        return name is null ? null : new DecodedSplInterfaceInstruction(name, data[8..]);
    }

    /// <summary>Decodes an SPL token-group interface instruction discriminator.</summary>
    /// <param name="data">Complete instruction data.</param>
    /// <returns>The variant and raw POD payload, or <c>null</c> for an unknown/short discriminator.</returns>
    public static DecodedSplInterfaceInstruction? DecodeTokenGroupInstructionData(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
            return null;
        var discriminator = data[..8];
        var name = discriminator.SequenceEqual(InitializeTokenGroupDiscriminator) ? "InitializeGroup" :
            discriminator.SequenceEqual(UpdateTokenGroupMaxSizeDiscriminator) ? "UpdateGroupMaxSize" :
            discriminator.SequenceEqual(UpdateTokenGroupAuthorityDiscriminator) ? "UpdateGroupAuthority" :
            discriminator.SequenceEqual(InitializeTokenGroupMemberDiscriminator) ? "InitializeMember" : null;
        return name is null ? null : new DecodedSplInterfaceInstruction(name, data[8..]);
    }
}

public static partial class TransferHookProgram
{
    /// <summary>Decodes an SPL transfer-hook interface instruction discriminator.</summary>
    /// <param name="data">Complete instruction data.</param>
    /// <returns>The variant and raw POD payload, or <c>null</c> for an unknown/short discriminator.</returns>
    public static DecodedSplInterfaceInstruction? DecodeInstructionData(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
            return null;
        var discriminator = data[..8];
        var name = discriminator.SequenceEqual(ExecuteDiscriminator) ? "Execute" :
            discriminator.SequenceEqual(InitializeExtraAccountMetasDiscriminator) ? "InitializeExtraAccountMetaList" :
            discriminator.SequenceEqual(UpdateExtraAccountMetasDiscriminator) ? "UpdateExtraAccountMetaList" : null;
        return name is null ? null : new DecodedSplInterfaceInstruction(name, data[8..]);
    }
}

/// <summary>A decoded discriminator and opaque payload from an SPL interface instruction.</summary>
public sealed class DecodedSplInterfaceInstruction
{
    private readonly byte[] _payload;

    internal DecodedSplInterfaceInstruction(string name, ReadOnlySpan<byte> payload)
    {
        Name = name;
        _payload = [.. payload];
    }

    /// <summary>The upstream instruction variant name.</summary>
    public string Name { get; }

    /// <summary>The exact bytes after the interface discriminator.</summary>
    public ReadOnlyMemory<byte> Payload => _payload;
}
