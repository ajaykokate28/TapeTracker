using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TapeTracker.Data;
using TapeTracker.Models;

namespace TapeTracker.Services;

/// <summary>
/// End-to-end backup/restore. Every archive is wrapped in the <c>.ttbak</c>
/// envelope defined by <see cref="BackupCrypto"/>; encryption is opt-in via a
/// user-supplied passphrase.
///
/// Every state-changing method also emits an <see cref="IAuditService"/> event
/// so the admin's activity log has a trustworthy record of who backed up or
/// restored, when, and how big the archive was.
/// </summary>
public class BackupService : IBackupService
{
    private const string AutoBackupTag = "auto_";

    private readonly TapeTrackerDbContext _db;
    private readonly IAuditService      _audit;

    public BackupService(TapeTrackerDbContext db, IAuditService audit)
    {
        _db    = db;
        _audit = audit;
    }

    // NOTE: filename intentionally kept as "tapetrack.db" — see the matching
    // note in MauiProgram.cs. This must always match the path registered with
    // the DI container or restores will target the wrong file.
    private static string DbPath =>
        Path.Combine(FileSystem.AppDataDirectory, "tapetrack.db");

    public string LocalBackupFolder =>
        Path.Combine(FileSystem.AppDataDirectory, "backups");

    // ── Create ───────────────────────────────────────────────────────────────

    public async Task<string> BackupAsync(string? passphrase = null)
    {
        var path = await CreateArchiveAsync(passphrase, autoBackup: false);

        // Also invite the user to share/save the file off-device.
        try
        {
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "TapeTracker Backup",
                File  = new ShareFile(path, BackupCrypto.MimeType)
            });
        }
        catch
        {
            // Share sheet isn't available (e.g. running as a test harness) —
            // that's fine, the file is still saved locally so nothing is lost.
        }
        return path;
    }

    public Task<string> AutoBackupAsync(string? passphrase)
        => CreateArchiveAsync(passphrase, autoBackup: true);

    private async Task<string> CreateArchiveAsync(string? passphrase, bool autoBackup)
    {
        // Flush pending changes AND checkpoint the SQLite WAL so the .db file
        // on disk is a complete, self-consistent snapshot. Without this the
        // archive can miss the latest un-checkpointed writes.
        await _db.SaveChangesAsync();
        try
        {
            await _db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);");
        }
        catch { /* checkpoint is best-effort; the copy below is still valid */ }

        Directory.CreateDirectory(LocalBackupFolder);

        var stamp    = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var prefix   = autoBackup ? AutoBackupTag : string.Empty;
        // NOTE: filename prefix intentionally kept as "TapeTrack_" (not
        // "TapeTracker_") so backups taken before the rename remain
        // discoverable and restorable via the standard file listing.
        var fileName = $"TapeTrack_{prefix}{stamp}{BackupCrypto.FileExtension}";
        var target   = Path.Combine(LocalBackupFolder, fileName);

        var raw       = await File.ReadAllBytesAsync(DbPath);
        var archive   = BackupCrypto.Pack(raw, passphrase);
        await File.WriteAllBytesAsync(target, archive);

        var encrypted = !string.IsNullOrEmpty(passphrase);
        await _audit.LogAsync(AuditAction.Backup,
            autoBackup
                ? $"Auto-backup saved ({FormatSize(archive.Length)}{(encrypted ? ", encrypted" : "")})"
                : $"Backup saved ({FormatSize(archive.Length)}{(encrypted ? ", encrypted" : "")})",
            "Backup");

        return target;
    }

    // ── Restore ──────────────────────────────────────────────────────────────

    public async Task<RestoreResult> RestoreAsync(Func<Task<string?>> passphraseProvider)
    {
        FileResult? picked;
        try
        {
            picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select TapeTracker backup",
                FileTypes   = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI,   new[] { BackupCrypto.FileExtension, ".db", ".sqlite" } },
                    { DevicePlatform.Android, new[] { "application/octet-stream", "*/*" } },
                    { DevicePlatform.iOS,     new[] { "public.data" } },
                })
            });
        }
        catch (Exception ex)
        {
            return new RestoreResult { Outcome = RestoreOutcome.UnknownError, Message = ex.Message };
        }

        if (picked is null)
            return new RestoreResult { Outcome = RestoreOutcome.Cancelled };

        return await RestoreFromPathAsync(picked.FullPath, passphraseProvider);
    }

    public async Task<RestoreResult> RestoreFromPathAsync(string path, Func<Task<string?>> passphraseProvider)
    {
        byte[] archive;
        try
        {
            archive = await File.ReadAllBytesAsync(path);
        }
        catch (Exception ex)
        {
            return new RestoreResult { Outcome = RestoreOutcome.UnknownError, Message = ex.Message };
        }

        // If the file lacks our magic header, we accept a plain SQLite file too
        // — pre-Phase-6 backups were bare .db copies and legitimate users may
        // still have them. We detect this by peeking at the SQLite magic.
        bool hasTapeMagic = BackupCrypto.TryReadHeader(archive, out bool isEncrypted);

        byte[] plaintext;
        if (hasTapeMagic)
        {
            string? passphrase = null;
            if (isEncrypted)
            {
                passphrase = await passphraseProvider();
                if (string.IsNullOrEmpty(passphrase))
                    return new RestoreResult { Outcome = RestoreOutcome.Cancelled };
            }
            try
            {
                plaintext = BackupCrypto.Unpack(archive, passphrase);
            }
            catch (CryptographicException ex)
            {
                return new RestoreResult
                {
                    Outcome = RestoreOutcome.WrongPassphrase,
                    Message = ex.Message,
                    SourcePath = path
                };
            }
            catch (InvalidDataException ex)
            {
                var outcome = ex.Message.Contains("truncated", StringComparison.OrdinalIgnoreCase)
                    ? RestoreOutcome.Truncated
                    : RestoreOutcome.BadFormat;
                return new RestoreResult { Outcome = outcome, Message = ex.Message, SourcePath = path };
            }
        }
        else if (LooksLikeSqlite(archive))
        {
            plaintext = archive;
        }
        else
        {
            return new RestoreResult
            {
                Outcome    = RestoreOutcome.BadFormat,
                Message    = "The file isn't a TapeTracker backup or a SQLite database.",
                SourcePath = path
            };
        }

        // Sanity check: the extracted payload must itself be a SQLite database.
        if (!LooksLikeSqlite(plaintext))
            return new RestoreResult
            {
                Outcome    = RestoreOutcome.BadFormat,
                Message    = "The backup did not contain a valid database.",
                SourcePath = path
            };

        // Rollback dance: keep the current DB alongside so a mid-restore crash
        // doesn't wipe the shop's live data.
        var rollback = DbPath + ".rollback";
        try
        {
            // Close all EF connections so the file lock is released — otherwise
            // the File.WriteAllBytes below fails on Windows with a sharing violation.
            await _db.Database.CloseConnectionAsync();

            if (File.Exists(DbPath))
                File.Copy(DbPath, rollback, overwrite: true);

            await File.WriteAllBytesAsync(DbPath, plaintext);
        }
        catch (Exception ex)
        {
            if (File.Exists(rollback))
            {
                try { File.Copy(rollback, DbPath, overwrite: true); }
                catch { /* rollback best-effort */ }
            }
            return new RestoreResult
            {
                Outcome = RestoreOutcome.UnknownError,
                Message = ex.Message,
                SourcePath = path
            };
        }
        finally
        {
            if (File.Exists(rollback))
            {
                try { File.Delete(rollback); } catch { /* ignore */ }
            }
        }

        await _audit.LogAsync(AuditAction.Restore,
            $"Restored backup from {Path.GetFileName(path)} ({FormatSize(plaintext.Length)})",
            "Backup");

        return new RestoreResult
        {
            Outcome       = RestoreOutcome.Success,
            SourcePath    = path,
            BytesRestored = plaintext.Length
        };
    }

    // ── Local backup listing / management ───────────────────────────────────

    public Task<IReadOnlyList<LocalBackupInfo>> ListLocalBackupsAsync()
    {
        if (!Directory.Exists(LocalBackupFolder))
            return Task.FromResult<IReadOnlyList<LocalBackupInfo>>(Array.Empty<LocalBackupInfo>());

        var infos = new List<LocalBackupInfo>();
        // Allocated once outside the loop (CA2014) — an 8-byte buffer is tiny
        // but the analyzer flags stackalloc-in-loop unconditionally.
        var headBuf = new byte[8];
        foreach (var file in Directory.EnumerateFiles(LocalBackupFolder))
        {
            try
            {
                var fi = new FileInfo(file);
                bool encrypted = false;
                if (fi.Length >= 8)
                {
                    // Read only the first few bytes — no need to slurp the entire archive.
                    using var stream = fi.OpenRead();
                    var read = stream.Read(headBuf, 0, headBuf.Length);
                    BackupCrypto.TryReadHeader(headBuf.AsSpan(0, read), out encrypted);
                }
                infos.Add(new LocalBackupInfo
                {
                    Path         = fi.FullName,
                    FileName     = fi.Name,
                    SizeBytes    = fi.Length,
                    CreatedAt    = fi.LastWriteTime,
                    IsEncrypted  = encrypted,
                    IsAutoBackup = fi.Name.Contains(AutoBackupTag, StringComparison.Ordinal)
                });
            }
            catch { /* skip unreadable files */ }
        }
        infos.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));   // newest first
        return Task.FromResult<IReadOnlyList<LocalBackupInfo>>(infos);
    }

    public Task DeleteLocalBackupAsync(string path)
    {
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
        return Task.CompletedTask;
    }

    public Task<DateTime?> GetLastBackupAtAsync()
    {
        // Cheap on-disk scan — avoids building the full LocalBackupInfo list
        // just to answer "how old is the newest file?". A user with hundreds
        // of backup archives would otherwise pay for header sniffing on
        // every home-screen load.
        if (!Directory.Exists(LocalBackupFolder))
            return Task.FromResult<DateTime?>(null);

        DateTime? newest = null;
        foreach (var file in Directory.EnumerateFiles(LocalBackupFolder))
        {
            try
            {
                var ts = File.GetLastWriteTime(file);
                if (newest is null || ts > newest) newest = ts;
            }
            catch { /* skip unreadable files */ }
        }
        return Task.FromResult(newest);
    }

    public async Task ShareLocalBackupAsync(string path)
    {
        if (!File.Exists(path)) return;
        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "TapeTracker Backup",
            File  = new ShareFile(path, BackupCrypto.MimeType)
        });
    }

    // ── Internals ────────────────────────────────────────────────────────────

    /// <summary>SQLite files start with the ASCII string "SQLite format 3" + NUL.</summary>
    private static bool LooksLikeSqlite(byte[] payload)
    {
        if (payload is null || payload.Length < 16) return false;
        // "SQLite format 3\0" = 53 51 4C 69 74 65 20 66 6F 72 6D 61 74 20 33 00
        ReadOnlySpan<byte> sig = "SQLite format 3\0"u8;
        for (int i = 0; i < sig.Length; i++)
            if (payload[i] != sig[i]) return false;
        return true;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)              return $"{bytes} B";
        if (bytes < 1024 * 1024)       return $"{bytes / 1024.0:0.#} KB";
        return                              $"{bytes / (1024.0 * 1024.0):0.##} MB";
    }
}
