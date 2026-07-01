using System.Security.Cryptography;
using System.Text;

namespace SolSharp.Wallet;

/// <summary>
/// BIP-39 mnemonic-to-seed derivation: PBKDF2-HMAC-SHA512 over the NFKD-normalized phrase, with the salt
/// <c>"mnemonic" + passphrase</c> and 2048 rounds. The phrase is deliberately not validated against a
/// wordlist, so any mnemonic a wallet produced can be imported.
/// See <see href="https://github.com/bitcoin/bips/blob/master/bip-0039.mediawiki">BIP-39</see>.
/// </summary>
public static class Bip39
{
    private const int Iterations = 2048;
    private const int SeedLength = 64;

    /// <summary>Derives the 64-byte BIP-39 seed for <paramref name="mnemonic"/>.</summary>
    /// <param name="mnemonic">The mnemonic phrase (typically 12 or 24 space-separated words).</param>
    /// <param name="passphrase">The optional BIP-39 passphrase (the "25th word"); empty by default.</param>
    /// <returns>The 64-byte seed. It is key material: zero it after use.</returns>
    /// <exception cref="ArgumentException"><paramref name="mnemonic"/> is <c>null</c>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="passphrase"/> is <c>null</c>.</exception>
    public static byte[] ToSeed(string mnemonic, string passphrase = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mnemonic);
        ArgumentNullException.ThrowIfNull(passphrase);

        var password = Encoding.UTF8.GetBytes(mnemonic.Normalize(NormalizationForm.FormKD));
        var salt = Encoding.UTF8.GetBytes("mnemonic" + passphrase.Normalize(NormalizationForm.FormKD));
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, SeedLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
        }
    }
}
