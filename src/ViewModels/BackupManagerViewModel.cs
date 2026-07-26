using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

/// <summary>UI-friendly projection of a single on-disk backup file.</summary>
public partial class BackupRowViewModel : ObservableObject
{
    public LocalBackupInfo Info { get; }

    public BackupRowViewModel(LocalBackupInfo info)
    {
        Info = info;
    }

    public string FileName    => Info.FileName;
    public string Path        => Info.Path;
    public bool   IsEncrypted => Info.IsEncrypted;
    public bool   IsAuto      => Info.IsAutoBackup;

    public string CreatedLabel => Info.CreatedAt.ToString("dd MMM yyyy · HH:mm");

    public string SizeLabel
    {
        get
        {
            var b = Info.SizeBytes;
            if (b < 1024)              return $"{b} B";
            if (b < 1024 * 1024)       return $"{b / 1024.0:0.#} KB";
            return                          $"{b / (1024.0 * 1024.0):0.##} MB";
        }
    }

    public string BadgeLabel => IsEncrypted
        ? LocalizationService.Current.EncryptedBadge
        : LocalizationService.Current.PlainBadge;
}

/// <summary>
/// Admin-only backup manager. Shows the local backup archive list, lets the
/// admin create a manual backup (optionally with a passphrase), restore from
/// file/list, share, or delete. Auto-backup settings live on the settings
/// panel embedded at the top of the same page.
/// </summary>
public partial class BackupManagerViewModel : BaseViewModel
{
    private readonly IBackupService              _backup;
    private readonly IAutoBackupSettingsService  _settings;

    public ObservableCollection<BackupRowViewModel> Backups { get; } = new();

    [ObservableProperty] public partial bool IsEmpty { get; set; }

    /// <summary>Optional passphrase typed into the "Backup Now" field.</summary>
    [ObservableProperty] public partial string BackupPassphrase { get; set; } = string.Empty;

    // ── Auto-backup settings bindings ───────────────────────────────────────
    [ObservableProperty] public partial bool AutoBackupEnabled { get; set; }
    [ObservableProperty] public partial int  IntervalDays      { get; set; } = 7;
    [ObservableProperty] public partial int  RetentionCount    { get; set; } = 10;
    [ObservableProperty] public partial bool HasStoredPassphrase { get; set; }
    [ObservableProperty] public partial string AutoPassphraseInput { get; set; } = string.Empty;
    [ObservableProperty] public partial string LastAutoLabel { get; set; } = string.Empty;

    /// <summary>True while we're loading settings for the first time — used to
    /// suppress the OnChanged handlers so they don't stampede-write to Preferences.</summary>
    private bool _loadingSettings;

    public BackupManagerViewModel(IBackupService backup, IAutoBackupSettingsService settings)
    {
        _backup   = backup;
        _settings = settings;
        Title     = LocalizationService.Current.BackupManagerTitle;
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            LoadSettingsSnapshot();
            HasStoredPassphrase = await _settings.HasPassphraseAsync();

            var list = await _backup.ListLocalBackupsAsync();
            Backups.Clear();
            foreach (var b in list)
                Backups.Add(new BackupRowViewModel(b));
            IsEmpty = Backups.Count == 0;
        }
        finally { IsBusy = false; }
    }

    private void LoadSettingsSnapshot()
    {
        _loadingSettings = true;
        try
        {
            AutoBackupEnabled = _settings.IsEnabled;
            IntervalDays      = _settings.IntervalDays;
            RetentionCount    = _settings.RetentionCount;

            var last = _settings.LastAutoBackupUtc;
            LastAutoLabel = last.HasValue
                ? last.Value.ToLocalTime().ToString("dd MMM yyyy · HH:mm")
                : L.NeverLabel;
        }
        finally { _loadingSettings = false; }
    }

    // Persist prefs on user edits (skipped during initial load).
    partial void OnAutoBackupEnabledChanged(bool value)
    {
        if (_loadingSettings) return;
        _settings.IsEnabled = value;
    }
    partial void OnIntervalDaysChanged(int value)
    {
        if (_loadingSettings) return;
        _settings.IntervalDays = value;
    }
    partial void OnRetentionCountChanged(int value)
    {
        if (_loadingSettings) return;
        _settings.RetentionCount = value;
    }

    // ── Backup ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        if (!await RequirePermissionAsync(Auth.CanExportOrBackup)) return;
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var pass = string.IsNullOrWhiteSpace(BackupPassphrase) ? null : BackupPassphrase;
            var path = await _backup.BackupAsync(pass);
            BackupPassphrase = string.Empty;
            await Shell.Current.DisplayAlert(
                L.BackupManagerTitle,
                string.Format(L.BackupCreatedFmt, System.IO.Path.GetFileName(path)),
                L.OkLabel);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(L.BackupManagerTitle, ex.Message, L.OkLabel);
        }
        finally { IsBusy = false; }
    }

    // ── Restore ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RestoreFromFileAsync()
    {
        if (!await RequirePermissionAsync(Auth.CanExportOrBackup)) return;

        var confirm = await Shell.Current.DisplayAlert(
            L.RestoreConfirmTitle, L.RestoreConfirmMsg,
            L.YesRestoreLabel, L.CancelLabel);
        if (!confirm) return;

        IsBusy = true;
        try
        {
            var result = await _backup.RestoreAsync(PromptPassphraseAsync);
            await HandleRestoreResultAsync(result);
        }
        finally
        {
            IsBusy = false;
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task RestoreThisAsync(BackupRowViewModel? row)
    {
        if (row is null) return;
        if (!await RequirePermissionAsync(Auth.CanExportOrBackup)) return;

        var confirm = await Shell.Current.DisplayAlert(
            L.RestoreConfirmTitle, L.RestoreConfirmMsg,
            L.YesRestoreLabel, L.CancelLabel);
        if (!confirm) return;

        IsBusy = true;
        try
        {
            var result = await _backup.RestoreFromPathAsync(row.Path, PromptPassphraseAsync);
            await HandleRestoreResultAsync(result);
        }
        finally
        {
            IsBusy = false;
            await LoadAsync();
        }
    }

    private async Task<string?> PromptPassphraseAsync()
    {
        var input = await Shell.Current.DisplayPromptAsync(
            L.PassphraseNeededTitle,
            L.PassphraseNeededMsg,
            L.OkLabel,
            L.CancelLabel,
            placeholder: L.PassphraseLabel,
            keyboard: Keyboard.Text);
        return input;
    }

    private async Task HandleRestoreResultAsync(RestoreResult result)
    {
        switch (result.Outcome)
        {
            case RestoreOutcome.Success:
                await Shell.Current.DisplayAlert(
                    L.RestoredSuccessTitle, L.RestoredSuccessMsg, L.OkLabel);
                break;
            case RestoreOutcome.Cancelled:
                // User dismissed — nothing to say.
                break;
            case RestoreOutcome.WrongPassphrase:
                await Shell.Current.DisplayAlert(
                    L.BackupManagerTitle, L.WrongPassphraseMsg, L.OkLabel);
                break;
            case RestoreOutcome.BadFormat:
            case RestoreOutcome.Truncated:
                await Shell.Current.DisplayAlert(
                    L.BackupManagerTitle, L.BadFormatMsg, L.OkLabel);
                break;
            default:
                await Shell.Current.DisplayAlert(
                    L.BackupManagerTitle,
                    result.Message ?? "Unexpected error.",
                    L.OkLabel);
                break;
        }
    }

    // ── Manage local files ──────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteAsync(BackupRowViewModel? row)
    {
        if (row is null) return;
        if (!await RequirePermissionAsync(Auth.CanExportOrBackup)) return;
        var confirm = await Shell.Current.DisplayAlert(
            L.DeleteLabel, L.ConfirmDeleteBackupMsg,
            L.DeleteLabel, L.CancelLabel);
        if (!confirm) return;
        await _backup.DeleteLocalBackupAsync(row.Path);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ShareAsync(BackupRowViewModel? row)
    {
        if (row is null) return;
        try { await _backup.ShareLocalBackupAsync(row.Path); }
        catch { /* share sheet unavailable — silently ignore */ }
    }

    // ── Auto-backup passphrase actions ──────────────────────────────────────

    [RelayCommand]
    private async Task SaveAutoPassphraseAsync()
    {
        if (!await RequirePermissionAsync(Auth.CanExportOrBackup)) return;
        var pass = AutoPassphraseInput?.Trim() ?? string.Empty;
        await _settings.SetPassphraseAsync(string.IsNullOrEmpty(pass) ? null : pass);
        AutoPassphraseInput = string.Empty;
        HasStoredPassphrase = await _settings.HasPassphraseAsync();
        await Shell.Current.DisplayAlert(L.BackupManagerTitle, L.SavedLabel, L.OkLabel);
    }

    [RelayCommand]
    private async Task ClearAutoPassphraseAsync()
    {
        if (!await RequirePermissionAsync(Auth.CanExportOrBackup)) return;
        await _settings.SetPassphraseAsync(null);
        HasStoredPassphrase = false;
        await Shell.Current.DisplayAlert(L.BackupManagerTitle, L.SavedLabel, L.OkLabel);
    }
}
