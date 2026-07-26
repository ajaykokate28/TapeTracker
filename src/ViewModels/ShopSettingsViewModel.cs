using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

/// <summary>
/// Admin-only shop settings editor. Persists shop identity + billing defaults
/// on the current <see cref="TapeTracker.Models.Organization"/> so every invoice,
/// WhatsApp template, and dashboard header reflects the tailor's real brand.
///
/// Logo handling copies the picked image into
/// <c>%AppData%\TapeTracker\logos\shop-{orgId}.{ext}</c> so the PDF service can
/// find it later without dealing with the platform-specific picker cache path.
/// </summary>
public partial class ShopSettingsViewModel : BaseViewModel
{
    private readonly IAuthService _auth;
    private readonly IAutoBackupSettingsService _autoBackup;

    // ── Shop identity ────────────────────────────────────────────────────────
    [ObservableProperty] public partial string ShopName    { get; set; } = string.Empty;
    [ObservableProperty] public partial string OwnerName   { get; set; } = string.Empty;
    [ObservableProperty] public partial string Phone       { get; set; } = string.Empty;
    [ObservableProperty] public partial string Address     { get; set; } = string.Empty;
    [ObservableProperty] public partial string Email       { get; set; } = string.Empty;

    // ── Billing ──────────────────────────────────────────────────────────────
    [ObservableProperty] public partial string GstNumber      { get; set; } = string.Empty;
    /// <summary>Free-form symbol so tailors can pick ₹ / $ / € / £ etc.</summary>
    [ObservableProperty] public partial string CurrencySymbol { get; set; } = "₹";
    [ObservableProperty] public partial string InvoicePrefix  { get; set; } = "INV";
    /// <summary>Bound as string so the entry field can be left blank without
    /// forcing a 0. Parsed at save-time; invalid text is treated as 0.</summary>
    [ObservableProperty] public partial string TaxRatePctText { get; set; } = "0";

    // ── Logo ────────────────────────────────────────────────────────────────
    /// <summary>Absolute path to the currently-selected logo, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogo))]
    public partial string? LogoPath { get; set; }

    public bool HasLogo => !string.IsNullOrEmpty(LogoPath) && File.Exists(LogoPath);

    // ── Item catalog (Phase 9.1) ────────────────────────────────────────────
    /// <summary>Editable garment names — populates the quick-pick chip strip
    /// on the invoice line-item editor. Order-preserving so the tailor's
    /// muscle memory ("Shirt is chip #1") stays intact.</summary>
    public ObservableCollection<string> CatalogItems { get; } = new();

    /// <summary>Bound to the "add" entry at the top of the catalog card.
    /// Cleared automatically after a successful add.</summary>
    [ObservableProperty]
    public partial string NewCatalogItem { get; set; } = string.Empty;

    /// <summary>Drives the empty-state hint's visibility.</summary>
    public bool HasCatalogItems => CatalogItems.Count > 0;

    public ShopSettingsViewModel(IAuthService auth, IAutoBackupSettingsService autoBackup)
    {
        _auth       = auth;
        _autoBackup = autoBackup;
        Title = LocalizationService.Current.ShopSettingsLabel;
        LoadFromCurrentOrg();
        RefreshAutoBackupSummary();
        RefreshRecoveryStatus();

        // Empty-state hint tracks the collection.
        CatalogItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasCatalogItems));
    }

    // ── Password-recovery question (self-service reset from login page) ─────
    //
    // Read-only view of the current user's recovery configuration plus a
    // command to open the "Set / update recovery question" prompt. The
    // question text is safe to display (users chose it); the answer is a
    // one-way hash so we can only ever *test* a candidate against it.

    [ObservableProperty]
    public partial bool   HasRecoveryQuestion   { get; set; }
    [ObservableProperty]
    public partial string RecoveryStatusLine    { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string CurrentRecoveryQuestion { get; set; } = string.Empty;

    private void RefreshRecoveryStatus()
    {
        var L = LocalizationService.Current;
        HasRecoveryQuestion     = _auth.HasRecoveryQuestion;
        CurrentRecoveryQuestion = _auth.CurrentUser?.RecoveryQuestion ?? string.Empty;
        RecoveryStatusLine      = HasRecoveryQuestion
            ? string.Format(L.RecoveryOnStatusFormat, CurrentRecoveryQuestion)
            : L.RecoveryOffStatus;
    }

    [RelayCommand]
    private async Task ConfigureRecoveryQuestionAsync()
    {
        var L = LocalizationService.Current;

        // Two prompts in sequence — question first (with the current one as
        // the initial value if any), then answer. DisplayPromptAsync is used
        // instead of a modal page because this is a low-frequency, high-
        // signal action and the OS-provided input dialog gets us focus
        // handling + IME behaviour for free.
        var question = await Shell.Current.DisplayPromptAsync(
            L.RecoveryConfigureTitle,
            L.RecoveryConfigureQuestionPrompt,
            L.SaveShortLabel, L.CancelLabel,
            L.RecoveryQuestionSuggestionPh,
            maxLength: 120,
            initialValue: CurrentRecoveryQuestion);

        // A null result = "Cancel". An empty string = "user wants to clear the
        // question" — pass through to the service and let it wipe the fields.
        if (question is null) return;

        string? answer = null;
        if (!string.IsNullOrWhiteSpace(question))
        {
            answer = await Shell.Current.DisplayPromptAsync(
                L.RecoveryConfigureTitle,
                string.Format(L.RecoveryConfigureAnswerPromptFormat, question.Trim()),
                L.SaveShortLabel, L.CancelLabel,
                L.RecoveryAnswerPh,
                maxLength: 120);
            if (answer is null) return;
        }

        try
        {
            await _auth.SetRecoveryQuestionAsync(question, answer);
            RefreshRecoveryStatus();
            await Shell.Current.DisplayAlert(
                L.RecoveryConfigureTitle,
                string.IsNullOrWhiteSpace(question)
                    ? L.RecoveryClearedMsg
                    : L.RecoverySavedMsg,
                L.OkLabel);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(L.RecoveryConfigureTitle, ex.Message, L.OkLabel);
        }
    }

    // ── Auto-backup summary (Phase 6 discoverability) ───────────────────────
    //
    // The full auto-backup UI lives inside BackupManagerPage, but new users
    // don't naturally hunt for it there — this compact info tile makes the
    // current status visible from Shop Settings and taps through to the
    // manager for changes. Kept read-only here so we don't split the
    // source-of-truth for interval/retention across two screens.

    [ObservableProperty]
    public partial string AutoBackupStatusLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool AutoBackupIsOn { get; set; }

    private void RefreshAutoBackupSummary()
    {
        var L = LocalizationService.Current;
        AutoBackupIsOn = _autoBackup.IsEnabled;
        if (!AutoBackupIsOn)
        {
            AutoBackupStatusLine = L.AutoBackupOffSummary;
            return;
        }

        var days = _autoBackup.IntervalDays;
        var last = _autoBackup.LastAutoBackupUtc;
        var when = last is null
            ? L.AutoBackupNeverLabel
            : (DateTime.Now - last.Value.ToLocalTime()).TotalHours < 24
                ? L.TodayLabel
                : last.Value.ToLocalTime().ToString("dd MMM yyyy");

        AutoBackupStatusLine = string.Format(L.AutoBackupOnSummaryFormat, days, when);
    }

    [RelayCommand]
    private async Task OpenAutoBackupManagerAsync()
        => await Shell.Current.GoToAsync("BackupManagerPage");

    private void LoadFromCurrentOrg()
    {
        var org = _auth.CurrentOrganization;
        if (org is null) return;

        ShopName        = org.Name;
        OwnerName       = org.OwnerName      ?? string.Empty;
        Phone           = org.Phone          ?? string.Empty;
        Address         = org.Address        ?? string.Empty;
        Email           = org.Email          ?? string.Empty;
        GstNumber       = org.GstNumber      ?? string.Empty;
        // Default the symbol to ₹ when the org has never set one — matches the
        // dominant use-case (India) without forcing every user to configure it.
        CurrencySymbol  = string.IsNullOrWhiteSpace(org.CurrencySymbol) ? "₹" : org.CurrencySymbol!;
        InvoicePrefix   = string.IsNullOrWhiteSpace(org.InvoicePrefix)  ? "INV" : org.InvoicePrefix!;
        TaxRatePctText  = org.TaxRatePct.ToString("0.##");
        LogoPath        = org.LogoPath;

        // Hydrate the catalog. Seed with a sensible starter set the first
        // time a shop opens Settings so they see the concept before typing
        // anything themselves.
        CatalogItems.Clear();
        var stored = org.ItemCatalog;
        if (stored.Count == 0)
        {
            foreach (var s in DefaultCatalog) CatalogItems.Add(s);
        }
        else
        {
            foreach (var s in stored) CatalogItems.Add(s);
        }
    }

    /// <summary>Reasonable starter catalog for a general tailor shop. Users
    /// can edit or wipe entirely.</summary>
    private static readonly string[] DefaultCatalog =
    {
        "Shirt", "Pant", "Kurta", "Sherwani", "Salwar Kameez",
        "Blouse", "Lehenga", "Alteration"
    };

    [RelayCommand]
    private void AddCatalogItem()
    {
        var name = (NewCatalogItem ?? string.Empty).Trim();
        if (name.Length == 0) return;
        // Skip duplicates (case-insensitive) instead of silently accepting.
        if (!CatalogItems.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
            CatalogItems.Add(name);
        NewCatalogItem = string.Empty;
    }

    [RelayCommand]
    private void RemoveCatalogItem(string? name)
    {
        if (name is null) return;
        CatalogItems.Remove(name);
    }

    [RelayCommand]
    private async Task ChooseLogoAsync()
    {
        try
        {
            var picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = LocalizationService.Current.ChooseLogoLabel,
                FileTypes   = FilePickerFileType.Images
            });
            if (picked is null) return;

            // Copy the picked file into the app's data folder so it survives
            // beyond the picker's temp cache and moves with backups.
            var orgId  = _auth.CurrentOrganization?.Id ?? 0;
            var dir    = Path.Combine(FileSystem.AppDataDirectory, "logos");
            Directory.CreateDirectory(dir);

            var ext    = Path.GetExtension(picked.FileName);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            var dest   = Path.Combine(dir, $"shop-{orgId}{ext}");

            using (var src = await picked.OpenReadAsync())
            using (var dst = File.Create(dest))
                await src.CopyToAsync(dst);

            LogoPath = dest;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Logo", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private void RemoveLogo()
    {
        // Only clears the reference — the file stays on disk in case the user
        // wants to re-add it later. Deleted permanently by the app's uninstall.
        LogoPath = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            if (string.IsNullOrWhiteSpace(ShopName))
            {
                await Shell.Current.DisplayAlert(
                    LocalizationService.Current.ShopNameLabel,
                    "Shop name cannot be empty.", "OK");
                return;
            }

            // Parse tax rate leniently: empty/garbage becomes 0.
            decimal.TryParse(TaxRatePctText, out var taxPct);
            taxPct = Math.Clamp(taxPct, 0m, 100m);

            await _auth.UpdateOrganizationAsync(
                name:           ShopName,
                phone:          Phone,
                address:        Address,
                gstNumber:      GstNumber,
                currencySymbol: CurrencySymbol,
                invoicePrefix:  InvoicePrefix,
                logoPath:       LogoPath,
                taxRatePct:     taxPct,
                ownerName:      OwnerName,
                email:          Email,
                itemCatalog:    CatalogItems.ToList());

            await Shell.Current.DisplayAlert(
                LocalizationService.Current.SettingsSavedTitle,
                LocalizationService.Current.SettingsSavedMsg,
                "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Save Failed",
                ex.InnerException?.Message ?? ex.Message, "OK");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}
