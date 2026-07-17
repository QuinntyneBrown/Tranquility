using System.Security.Cryptography;

namespace Tranquility.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing in PHC string format
/// (<c>$pbkdf2-sha256$i=&lt;iterations&gt;$&lt;salt-b64&gt;$&lt;hash-b64&gt;</c>).
/// Parameters travel with each record, so they can be raised without
/// invalidating stored credentials.
/// </summary>
public static class PasswordHasher
{
    private const int DefaultIterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static string Hash(string password, int iterations = DefaultIterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"$pbkdf2-sha256$i={iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string phcString)
    {
        var parts = phcString.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !parts[1].StartsWith("i=", StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1].AsSpan(2), out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
