namespace TapeTracker.Services;

/// <summary>
/// Persisted preferences for scheduled auto-backups. Non-sensitive fields live
/// in <c>Preferences</c>; the passphrase (when set) lives in
/// <c>SecureStorage</c> so it's platform-encrypted at rest.
/// </summary>
public interface IAutoBackupSettingsService
{
    /// <summary>When true, the scheduler runs a silent local backup if the
    /// interval has elapsed since the last one.</summary>
    bool IsEnabled { get; set; }

    /// <summary>Interval between auto-backups, in days. Minimum 1, maximum 30.</summary>
    int IntervalDays { get; set; }

    /// <summary>How many auto-backup files to retain locally. Older files are
    /// pruned by <see cref="IAutoBackupScheduler"/> after each run. Minimum 1.</summary>
    int RetentionCount { get; set; }

    /// <summary>Timestamp of the most recent successful auto-backup. Used by
    /// the scheduler to decide whether to fire on app start.</summary>
    DateTime? LastAutoBackupUtc { get; set; }

    /// <summary>True when the user has stored an auto-backup passphrase in
    /// <see cref="Microsoft.Maui.Storage.SecureStorage"/>.</summary>
    Task<bool> HasPassphraseAsync();

    /// <summary>Returns the stored passphrase, or null when none is set.</summary>
    Task<string?> GetPassphraseAsync();

    /// <summary>Persists a passphrase for future auto-backups. Pass null/empty
    /// to remove the passphrase (auto-backups become unencrypted).</summary>
    Task SetPassphraseAsync(string? passphrase);
}
