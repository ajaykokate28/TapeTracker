namespace TapeTracker.Services;

/// <inheritdoc />
public class AutoBackupSettingsService : IAutoBackupSettingsService
{
    private const string KeyEnabled       = "autobackup.enabled";
    private const string KeyIntervalDays  = "autobackup.interval_days";
    private const string KeyRetentionCnt  = "autobackup.retention_count";
    private const string KeyLastRunTicks  = "autobackup.last_run_ticks_utc";
    private const string KeyPassphrase    = "autobackup.passphrase";     // stored in SecureStorage

    public bool IsEnabled
    {
        get => Preferences.Default.Get(KeyEnabled, false);
        set => Preferences.Default.Set(KeyEnabled, value);
    }

    public int IntervalDays
    {
        get
        {
            var raw = Preferences.Default.Get(KeyIntervalDays, 7);
            // Clamp defensively — hand-edited prefs shouldn't create a runaway
            // scheduler that fires every restart or one that never fires.
            return Math.Clamp(raw, 1, 30);
        }
        set => Preferences.Default.Set(KeyIntervalDays, Math.Clamp(value, 1, 30));
    }

    public int RetentionCount
    {
        get
        {
            var raw = Preferences.Default.Get(KeyRetentionCnt, 10);
            return Math.Max(1, raw);
        }
        set => Preferences.Default.Set(KeyRetentionCnt, Math.Max(1, value));
    }

    public DateTime? LastAutoBackupUtc
    {
        get
        {
            var t = Preferences.Default.Get(KeyLastRunTicks, 0L);
            return t == 0 ? null : new DateTime(t, DateTimeKind.Utc);
        }
        set => Preferences.Default.Set(KeyLastRunTicks, value?.ToUniversalTime().Ticks ?? 0L);
    }

    public async Task<bool> HasPassphraseAsync()
        => !string.IsNullOrEmpty(await GetPassphraseAsync());

    public async Task<string?> GetPassphraseAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(KeyPassphrase);
        }
        catch
        {
            // SecureStorage can throw on unsupported platforms/OS quirks —
            // "no passphrase" is a safe fallback.
            return null;
        }
    }

    public async Task SetPassphraseAsync(string? passphrase)
    {
        try
        {
            if (string.IsNullOrEmpty(passphrase))
                SecureStorage.Default.Remove(KeyPassphrase);
            else
                await SecureStorage.Default.SetAsync(KeyPassphrase, passphrase);
        }
        catch
        {
            // Best-effort — if SecureStorage isn't available the auto-backup
            // will fall back to plaintext, which we surface via HasPassphraseAsync.
        }
    }
}
