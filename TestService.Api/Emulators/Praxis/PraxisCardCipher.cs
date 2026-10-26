using System.Security.Cryptography;
using System.Text;

namespace TestService.Api.Emulators.Praxis;

/// <summary>
/// Praxis card-data cipher: AES-256-CBC with PKCS7 padding, key = secret left-padded with <c>0</c>
/// to 32 bytes, IV = request timestamp left-padded with <c>0</c> to 16 bytes, Base64 output,
/// UTF-8 plaintext (as BillingServer encrypts it).
/// </summary>
public static class PraxisCardCipher
{
    public static string Encrypt(string plainText, string secret, string timestamp)
    {
        using var aes = Aes.Create();
        aes.Key = Key(secret);
        return Convert.ToBase64String(aes.EncryptCbc(Encoding.UTF8.GetBytes(plainText), Iv(timestamp), PaddingMode.PKCS7));
    }

    /// <summary>Decrypts <paramref name="cipherText"/>; false when it is not valid Base64 or does not decrypt with this key and IV.</summary>
    public static bool TryDecrypt(string? cipherText, string secret, string timestamp, out string plainText)
    {
        plainText = string.Empty;
        if (string.IsNullOrEmpty(cipherText))
        {
            return false;
        }

        try
        {
            using var aes = Aes.Create();
            aes.Key = Key(secret);
            var bytes = aes.DecryptCbc(Convert.FromBase64String(cipherText), Iv(timestamp), PaddingMode.PKCS7);
            plainText = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private static byte[] Key(string secret) => Encoding.ASCII.GetBytes(secret.PadLeft(32, '0'));

    private static byte[] Iv(string timestamp) => Encoding.ASCII.GetBytes(timestamp.PadLeft(16, '0'));
}
