using System.Security.Cryptography;
using System.Text;

namespace TapeTracker.Services;

/// <summary>
/// TapeTracker backup archive format (extension <c>.ttbak</c>).
///
/// <para>Header layout (little-endian):
/// <list type="table">
/// <item><term>0..5</term><description>Magic bytes <c>"TTBAK\0"</c> (6 bytes)</description></item>
/// <item><term>6</term><description>Format version — currently <c>1</c></description></item>
/// <item><term>7</term><description>Flags — bit 0 = encrypted, bits 1-7 reserved</description></item>
/// <item><term>encrypted only</term><description>Salt (16 bytes) → Nonce (12 bytes) → Tag (16 bytes) → ciphertext</description></item>
/// <item><term>plain</term><description>Raw SQLite bytes follow immediately</description></item>
/// </list>
/// </para>
///
/// <para>Encryption: AES-256-GCM with a PBKDF2-SHA256(200_000 iter, 32-byte key) derived from
/// the user-supplied passphrase + per-file salt. GCM handles integrity via its authentication
/// tag, so verification during restore is free — a wrong password / corrupted archive throws
/// <see cref="CryptographicException"/> instead of returning garbage.</para>
///
/// <para>Design goals:
/// <list type="bullet">
/// <item>Self-describing — a picker can identify the format from the magic bytes.</item>
/// <item>Forward-compatible — the version byte lets us evolve the layout without breaking
/// existing archives.</item>
/// <item>Zero-dependency — uses only <c>System.Security.Cryptography</c>.</item>
/// </list>
/// </para>
/// </summary>
public static class BackupCrypto
{
    private static readonly byte[] Magic =
        Encoding.ASCII.GetBytes("TTBAK\0");   // 6 bytes

    public const byte CurrentVersion = 1;
    private const byte FlagEncrypted = 0x01;

    private const int SaltSize   = 16;
    private const int NonceSize  = 12;
    private const int TagSize    = 16;
    private const int KeyBytes   = 32;
    private const int Iterations = 200_000;

    /// <summary>Well-known file extension for TapeTracker archives.</summary>
    public const string FileExtension = ".ttbak";

    /// <summary>MIME type registered informally for the archive.</summary>
    public const string MimeType = "application/x-tapetracker-backup";

    /// <summary>
    /// Wraps <paramref name="plaintext"/> in the <c>.ttbak</c> envelope. When
    /// <paramref name="passphrase"/> is null/empty the file is stored unencrypted
    /// (still tagged with the magic header so restore can validate it).
    /// </summary>
    public static byte[] Pack(byte[] plaintext, string? passphrase)
    {
        if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));

        bool encrypt = !string.IsNullOrEmpty(passphrase);
        using var ms = new MemoryStream();
        ms.Write(Magic, 0, Magic.Length);
        ms.WriteByte(CurrentVersion);
        ms.WriteByte(encrypt ? FlagEncrypted : (byte)0);

        if (!encrypt)
        {
            ms.Write(plaintext, 0, plaintext.Length);
            return ms.ToArray();
        }

        // Encrypt-with-passphrase branch.
        Span<byte> salt      = stackalloc byte[SaltSize];
        Span<byte> nonce     = stackalloc byte[NonceSize];
        RandomNumberGenerator.Fill(salt);
        RandomNumberGenerator.Fill(nonce);

        var key = Rfc2898DeriveBytes.Pbkdf2(
            passphrase!, salt.ToArray(), Iterations, HashAlgorithmName.SHA256, KeyBytes);

        var ciphertext = new byte[plaintext.Length];
        var tag        = new byte[TagSize];
        using (var aes = new AesGcm(key, TagSize))
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

        ms.Write(salt);
        ms.Write(nonce);
        ms.Write(tag);
        ms.Write(ciphertext, 0, ciphertext.Length);
        return ms.ToArray();
    }

    /// <summary>
    /// Reads a <c>.ttbak</c> archive and returns the original bytes. Throws
    /// <see cref="InvalidDataException"/> on bad header, <see cref="CryptographicException"/>
    /// on wrong passphrase / tampered archive.
    /// </summary>
    public static byte[] Unpack(byte[] archive, string? passphrase)
    {
        if (archive is null || archive.Length < Magic.Length + 2)
            throw new InvalidDataException("File is too small to be a TapeTracker backup.");

        for (int i = 0; i < Magic.Length; i++)
        {
            if (archive[i] != Magic[i])
                throw new InvalidDataException("File is not a TapeTracker backup (bad magic).");
        }

        byte version = archive[Magic.Length];
        byte flags   = archive[Magic.Length + 1];
        if (version != CurrentVersion)
            throw new InvalidDataException(
                $"Unsupported backup version {version}. This app expects version {CurrentVersion}.");

        bool isEncrypted = (flags & FlagEncrypted) != 0;
        int  headerEnd   = Magic.Length + 2;

        if (!isEncrypted)
        {
            var plain = new byte[archive.Length - headerEnd];
            Array.Copy(archive, headerEnd, plain, 0, plain.Length);
            return plain;
        }

        if (string.IsNullOrEmpty(passphrase))
            throw new CryptographicException("Backup is encrypted — a passphrase is required.");

        int expectedMin = headerEnd + SaltSize + NonceSize + TagSize;
        if (archive.Length < expectedMin)
            throw new InvalidDataException("Encrypted backup is truncated.");

        var salt = new byte[SaltSize];
        Array.Copy(archive, headerEnd, salt, 0, SaltSize);
        var nonce = new byte[NonceSize];
        Array.Copy(archive, headerEnd + SaltSize, nonce, 0, NonceSize);
        var tag = new byte[TagSize];
        Array.Copy(archive, headerEnd + SaltSize + NonceSize, tag, 0, TagSize);
        int cipherStart = expectedMin;
        int cipherLen   = archive.Length - cipherStart;
        var cipher = new byte[cipherLen];
        Array.Copy(archive, cipherStart, cipher, 0, cipherLen);

        var key = Rfc2898DeriveBytes.Pbkdf2(
            passphrase, salt, Iterations, HashAlgorithmName.SHA256, KeyBytes);

        var plaintext = new byte[cipherLen];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plaintext);
        }
        catch (CryptographicException)
        {
            // Rewrap with a friendlier message — the base exception is
            // "The computed authentication tag did not match" which
            // 99% of the time means "wrong passphrase".
            throw new CryptographicException(
                "Could not decrypt backup. The passphrase may be wrong or the file may be damaged.");
        }
        return plaintext;
    }

    /// <summary>
    /// Cheap header-only probe. Returns <c>true</c> if the byte prefix looks like a
    /// TapeTracker archive. Second value indicates whether the archive is encrypted.
    /// Does NOT verify the passphrase or the payload.
    /// </summary>
    public static bool TryReadHeader(ReadOnlySpan<byte> archive, out bool isEncrypted)
    {
        isEncrypted = false;
        if (archive.Length < Magic.Length + 2) return false;
        for (int i = 0; i < Magic.Length; i++)
            if (archive[i] != Magic[i]) return false;
        isEncrypted = (archive[Magic.Length + 1] & FlagEncrypted) != 0;
        return true;
    }
}
