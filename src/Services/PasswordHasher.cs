using System.Security.Cryptography;

namespace TapeTracker.Services;

/// <summary>
/// Hashes user passwords with PBKDF2-SHA256 (100 000 iterations, 128-bit random
/// salt, 256-bit derived key). All values are Base64-encoded for SQLite storage.
///
/// Deliberately keeps the algorithm parameters as public constants so we can
/// bump iteration count in a future release and lazily re-hash on next login
/// without touching users who never sign in again.
/// </summary>
public static class PasswordHasher
{
    public const int SaltSizeBytes  = 16;    // 128-bit
    public const int HashSizeBytes  = 32;    // 256-bit
    public const int Iterations     = 100_000;

    /// <summary>Generates a fresh salt + hash pair for a new password.</summary>
    public static (string Hash, string Salt) Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password must not be empty.", nameof(password));

        var saltBytes = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSizeBytes);

        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    /// <summary>
    /// Constant-time verify. Returns <c>false</c> for any invalid input — never
    /// throws on malformed stored data so a corrupted row can't lock everyone out.
    /// </summary>
    public static bool Verify(string password, string storedHashBase64, string storedSaltBase64)
    {
        if (string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(storedHashBase64) ||
            string.IsNullOrEmpty(storedSaltBase64))
            return false;

        byte[] saltBytes, expectedHash;
        try
        {
            saltBytes    = Convert.FromBase64String(storedSaltBase64);
            expectedHash = Convert.FromBase64String(storedHashBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}
