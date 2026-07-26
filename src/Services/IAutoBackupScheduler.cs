namespace TapeTracker.Services;

/// <summary>
/// Runs the auto-backup on demand. There's no timer/thread in the app — MAUI
/// desktop apps don't have a guaranteed background service, so we take the
/// pragmatic route: fire whenever the user opens the dashboard (or logs in),
/// and skip if the configured interval hasn't elapsed since the last run.
/// </summary>
public interface IAutoBackupScheduler
{
    /// <summary>
    /// Runs an auto-backup if all conditions are met:
    /// <list type="bullet">
    /// <item>a user is signed in (so <see cref="IAuditService"/> has org context),</item>
    /// <item>auto-backup is enabled,</item>
    /// <item>the configured interval has passed since <see cref="IAutoBackupSettingsService.LastAutoBackupUtc"/>.</item>
    /// </list>
    /// Silent — never surfaces UI, and errors are logged to the audit trail
    /// rather than thrown at the caller.
    /// </summary>
    Task RunIfDueAsync();
}
