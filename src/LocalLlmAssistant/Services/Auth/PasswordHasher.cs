using System.Security.Cryptography;

namespace LocalLlmAssistant.Services.Auth;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public static (string hash, string salt) HashPassword(string password)
    {
        using var rng = RandomNumberGenerator.Create();
        var saltBytes = new byte[SaltSize];
        rng.GetBytes(saltBytes);

        var hashBytes = DeriveKey(password, saltBytes);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public static bool Verify(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
        {
            return false;
        }

        var saltBytes = Convert.FromBase64String(storedSalt);
        var hashBytes = DeriveKey(password, saltBytes);
        var storedHashBytes = Convert.FromBase64String(storedHash);
        return CryptographicOperations.FixedTimeEquals(storedHashBytes, hashBytes);
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
        return Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
    }
}
