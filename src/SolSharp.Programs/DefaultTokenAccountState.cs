namespace SolSharp.Programs;

/// <summary>The default state assigned to new accounts of a Token-2022 mint.</summary>
public enum DefaultTokenAccountState : byte
{
    /// <summary>The account has not been initialized.</summary>
    Uninitialized = 0,

    /// <summary>The account is initialized and usable.</summary>
    Initialized = 1,

    /// <summary>The account starts frozen and requires its mint's freeze authority to thaw it.</summary>
    Frozen = 2
}
