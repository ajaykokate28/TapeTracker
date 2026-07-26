using Microsoft.Extensions.DependencyInjection;

namespace TapeTracker.Services;

/// <inheritdoc />
public class AutoBackupScheduler : IAutoBackupScheduler
{
    private readonly IServiceScopeFactory        _scopeFactory;
    private readonly IAutoBackupSettingsService  _settings;
    private readonly IAuthService                _auth;

    private static readonly SemaphoreSlim _gate = new(1, 1);

    public AutoBackupScheduler(
        IServiceScopeFactory scopeFactory,
        IAutoBackupSettingsService settings,
        IAuthService auth)
    {
        _scopeFactory = scopeFactory;
        _settings     = settings;
        _auth         = auth;
    }

    public async Task RunIfDueAsync()
    {
        if (_auth.CurrentUser is null)      return;
        if (!_settings.IsEnabled)           return;

        // Serialize concurrent callers — the dashboard, splash, and any future
        // trigger could all invoke this within milliseconds on cold start.
        if (!await _gate.WaitAsync(0)) return;
        try
        {
            var last = _settings.LastAutoBackupUtc;
            var due  = last is null ||
                       DateTime.UtcNow - last.Value >= TimeSpan.FromDays(_settings.IntervalDays);
            if (!due) return;

            var passphrase = await _settings.GetPassphraseAsync();

            // BackupService is Scoped — spin up a scope for the operation so
            // we get a fresh DbContext instead of reusing one that may belong
            // to a different UI thread.
            using var scope = _scopeFactory.CreateScope();
            var backup = scope.ServiceProvider.GetRequiredService<IBackupService>();

            try
            {
                await backup.AutoBackupAsync(passphrase);
                _settings.LastAutoBackupUtc = DateTime.UtcNow;
                await PruneOldAutoBackupsAsync(backup);
            }
            catch
            {
                // Silent failure — a scheduled backup should never nag the user
                // mid-workflow. The audit service already logged the successful
                // path; if we crashed before that, the failure lives in the
                // app's crash-log file via BackupService.
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Deletes the oldest auto-backup files beyond
    /// <see cref="IAutoBackupSettingsService.RetentionCount"/> so the local
    /// folder doesn't grow without bound. Manual backups are never pruned.
    /// </summary>
    private async Task PruneOldAutoBackupsAsync(IBackupService backup)
    {
        var list = await backup.ListLocalBackupsAsync();
        var autos = list.Where(b => b.IsAutoBackup)
                        .OrderByDescending(b => b.CreatedAt)
                        .ToList();
        var keep = _settings.RetentionCount;
        if (autos.Count <= keep) return;

        foreach (var stale in autos.Skip(keep))
        {
            try { await backup.DeleteLocalBackupAsync(stale.Path); }
            catch { /* best-effort */ }
        }
    }
}
