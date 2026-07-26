namespace TapeTracker.Services;

/// <summary>A single backup file found in the app's local backup folder.</summary>
public class LocalBackupInfo
{
    public string   Path          { get; init; } = string.Empty;
    public string   FileName      { get; init; } = string.Empty;
    public long     SizeBytes     { get; init; }
    public DateTime CreatedAt     { get; init; }
    public bool     IsEncrypted   { get; init; }
    public bool     IsAutoBackup  { get; init; }
}

/// <summary>
/// Reason an <see cref="IBackupService.RestoreAsync"/> call finished.
/// Lets the UI show a targeted alert instead of a generic "did it work?" bool.
/// </summary>
public enum RestoreOutcome
{
    Success,
    Cancelled,
    BadFormat,
    WrongPassphrase,
    Truncated,
    UnknownError
}

public class RestoreResult
{
    public RestoreOutcome Outcome { get; init; }
    public string?        Message { get; init; }
    public string?        SourcePath { get; init; }
    public long           BytesRestored { get; init; }
}

public interface IBackupService
{
    /// <summary>
    /// Creates a <c>.ttbak</c> archive, saves it to the local backups folder,
    /// AND shares it via the OS share sheet. Encrypts with the supplied
    /// <paramref name="passphrase"/> when non-empty.
    /// </summary>
    /// <returns>The absolute path of the saved local file.</returns>
    Task<string> BackupAsync(string? passphrase = null);

    /// <summary>
    /// Creates a <c>.ttbak</c> archive silently in the local backups folder
    /// without invoking the share sheet. Intended for the auto-backup
    /// scheduler. Returns the saved path.
    /// </summary>
    Task<string> AutoBackupAsync(string? passphrase);

    /// <summary>
    /// Restores a backup after prompting the user to pick a file. If the
    /// archive is encrypted, <paramref name="passphraseProvider"/> is invoked
    /// to ask the user for the passphrase (may be called multiple times if
    /// the first attempt is wrong — the UI decides how many retries to offer).
    /// </summary>
    Task<RestoreResult> RestoreAsync(Func<Task<string?>> passphraseProvider);

    /// <summary>
    /// Restores from a specific path (used by the Backup Manager to restore
    /// one of the local files). Same passphrase-callback contract as
    /// <see cref="RestoreAsync"/>.
    /// </summary>
    Task<RestoreResult> RestoreFromPathAsync(string path, Func<Task<string?>> passphraseProvider);

    /// <summary>Lists every archive in the local backups folder, newest first.</summary>
    Task<IReadOnlyList<LocalBackupInfo>> ListLocalBackupsAsync();

    /// <summary>
    /// Timestamp of the most recent backup (auto or manual). Returns
    /// <c>null</c> when no backups exist. Used by the Home screen to
    /// surface a "your data hasn't been backed up in a while" nudge
    /// without loading the full <see cref="LocalBackupInfo"/> list.
    /// </summary>
    Task<DateTime?> GetLastBackupAtAsync();

    /// <summary>Permanently deletes a local backup file.</summary>
    Task DeleteLocalBackupAsync(string path);

    /// <summary>Shares an existing local backup via the OS share sheet.</summary>
    Task ShareLocalBackupAsync(string path);

    /// <summary>Absolute path of the folder where local backups live.</summary>
    string LocalBackupFolder { get; }
}
