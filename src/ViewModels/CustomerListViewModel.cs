using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

public partial class CustomerListViewModel : BaseViewModel
{
    private readonly IMeasurementService _measurementService;
    private readonly IUnitPreferenceService _unitService;
    private readonly IBackupService _backupService;

    /// <summary>
    /// Age threshold (in days) after which the Home screen surfaces the
    /// "your data hasn't been backed up in a while" banner. Kept as a const
    /// so the same figure feeds both the visibility check and the localized
    /// message.
    /// </summary>
    private const int BackupStaleDays = 7;

    public ObservableCollection<Customer> Customers { get; } = new();

    /// <summary>
    /// Compact list of orders that are either overdue or due today. Populated
    /// by <see cref="LoadAsync"/> alongside the main customer list so the
    /// Home screen renders in a single round-trip. Empty by default so the
    /// widget row collapses on brand-new installs.
    /// </summary>
    public ObservableCollection<UrgentOrderItem> UrgentOrders { get; } = new();

    /// <summary>Drives the widget's visibility — <c>Count</c> binding on the
    /// UI would work but a bool keeps XAML simpler.</summary>
    [ObservableProperty]
    public partial bool HasUrgentOrders { get; set; }

    /// <summary>Distinct family / group tags used across the customer table.</summary>
    public ObservableCollection<string> AvailableTags { get; } = new();

    /// <summary>Items for the tag-filter dropdown. Always starts with the
    /// localized "All" label so the user can clear the filter with one tap.</summary>
    public ObservableCollection<string> TagFilterOptions { get; } = new() { LocalizationService.Current.AllLabel };

    /// <summary>The localized display label for the "no filter" option.
    /// Used both for populating the dropdown and for the "clear" tap handler.</summary>
    public static string AllTagsOption => LocalizationService.Current.AllLabel;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Currently selected item in the tag dropdown (localized "All" label or a tag name).</summary>
    [ObservableProperty]
    public partial string SelectedTagOption { get; set; } = LocalizationService.Current.AllLabel;

    // Set true while we're rebuilding TagFilterOptions so the Picker's binding
    // resetting SelectedItem to null doesn't trigger a spurious filter change.
    private bool _suppressTagOptionChange;

    partial void OnSelectedTagOptionChanged(string value)
    {
        if (_suppressTagOptionChange) return;
        // Treat both the current localized "All" label AND a plain "All" fallback
        // as "no filter" — covers stale values captured before a language switch.
        ActiveTag = string.IsNullOrEmpty(value)
                    || value == LocalizationService.Current.AllLabel
                    || value == "All"
            ? string.Empty : value;
    }

    /// <summary>Selected tag filter. Empty = show all customers.</summary>
    [ObservableProperty]
    public partial string ActiveTag { get; set; } = string.Empty;

    partial void OnActiveTagChanged(string value) => _ = LoadAsync();

    [ObservableProperty]
    public partial string UnitLabel { get; set; } = "in";

    public CustomerListViewModel(IMeasurementService measurementService,
                                 IUnitPreferenceService unitService,
                                 IBackupService backupService)
    {
        _measurementService = measurementService;
        _unitService = unitService;
        _backupService = backupService;
        Title = "TapeTracker";
        UnitLabel = _unitService.UnitLabel;
        _unitService.UnitChanged += (_, _) => UnitLabel = _unitService.UnitLabel;

        // Refresh the "All" entry in the dropdown whenever the app language
        // changes so the picker never shows a stale English label.
        LocalizationService.Current.PropertyChanged += (_, _) => RelabelAllOption();
    }

    /// <summary>Rewrites TagFilterOptions[0] with the current localized "All"
    /// label. Called on language toggle. Preserves the user's tag selection.</summary>
    private void RelabelAllOption()
    {
        if (TagFilterOptions.Count == 0) return;
        var newLabel = LocalizationService.Current.AllLabel;

        var wasAllSelected = string.IsNullOrEmpty(ActiveTag);

        _suppressTagOptionChange = true;
        try
        {
            TagFilterOptions[0] = newLabel;
        }
        finally { _suppressTagOptionChange = false; }

        if (wasAllSelected) SelectedTagOption = newLabel;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            // Only cap the recent-list to 20 when there's no search or tag filter.
            var limit = string.IsNullOrWhiteSpace(SearchText) && string.IsNullOrWhiteSpace(ActiveTag)
                ? (int?)20 : null;
            var tagFilter = string.IsNullOrWhiteSpace(ActiveTag) ? null : ActiveTag;
            var list  = await _measurementService.GetAllCustomersAsync(SearchText, limit, tagFilter: tagFilter);
            Customers.Clear();
            foreach (var c in list)
                Customers.Add(c);

            await RefreshAvailableTagsAsync();
            await RefreshBackupBannerAsync();
            await RefreshUrgentOrdersAsync();
        }
        finally { IsBusy = false; }
    }

    // ── Due today / Overdue widget ──────────────────────────────────────────
    //
    // Compact horizontally-scrolling strip surfaced above the main customer
    // list. Rebuilt on every LoadAsync so status changes made through the
    // detail page (Advance, Repeat, edit due-date) are reflected the moment
    // the user swipes back to Home. Failures are swallowed — the widget
    // should never block the primary customer list from rendering.

    private async Task RefreshUrgentOrdersAsync()
    {
        try
        {
            var today = DateTime.Today;
            var rows  = await _measurementService.GetDueTodayOrOverdueOrdersAsync(today, limit: 8);

            UrgentOrders.Clear();
            foreach (var o in rows)
            {
                UrgentOrders.Add(UrgentOrderItem.FromOrder(o, today));
            }
            HasUrgentOrders = UrgentOrders.Count > 0;
        }
        catch
        {
            HasUrgentOrders = false;
        }
    }

    [RelayCommand]
    private async Task OpenUrgentOrderAsync(UrgentOrderItem? item)
    {
        if (item is null) return;
        await Shell.Current.GoToAsync($"CustomerDetailPage?customerId={item.CustomerId}");
    }

    // ── Backup-stale banner ─────────────────────────────────────────────────
    //
    // Shown at the top of Home when either no backup exists or the newest
    // archive is more than BackupStaleDays old. Admins-only — employees
    // can't run a backup anyway, so nagging them is noise. The dismiss
    // command hides the banner for the rest of the session (in-memory
    // only, so the reminder comes back next launch).

    [ObservableProperty]
    public partial bool ShowBackupStaleBanner { get; set; }

    [ObservableProperty]
    public partial string BackupStaleMessage { get; set; } = string.Empty;

    private bool _bannerDismissedThisSession;

    private async Task RefreshBackupBannerAsync()
    {
        if (_bannerDismissedThisSession || !Auth.CanExportOrBackup)
        {
            ShowBackupStaleBanner = false;
            return;
        }

        try
        {
            var last = await _backupService.GetLastBackupAtAsync();
            var (show, msg) = BuildBannerText(last);
            ShowBackupStaleBanner = show;
            BackupStaleMessage    = msg;
        }
        catch
        {
            // Never let a banner failure block the customer list from loading.
            ShowBackupStaleBanner = false;
        }
    }

    private static (bool show, string msg) BuildBannerText(DateTime? last)
    {
        var L = LocalizationService.Current;
        if (last is null)
            return (true, L.BackupNeverMsg);

        var days = (int)(DateTime.Now - last.Value).TotalDays;
        return days >= BackupStaleDays
            ? (true,  string.Format(L.BackupStaleDaysMsgFormat, days))
            : (false, string.Empty);
    }

    [RelayCommand]
    private void DismissBackupBanner()
    {
        _bannerDismissedThisSession = true;
        ShowBackupStaleBanner       = false;
    }

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        // Route into the full Backup Manager rather than firing a silent
        // backup here — the manager surfaces encryption + share options
        // and matches what the toolbar's "Backup" secondary action does.
        if (!await RequirePermissionAsync(Auth.CanExportOrBackup)) return;
        await Shell.Current.GoToAsync("BackupManagerPage");
    }

    private async Task RefreshAvailableTagsAsync()
    {
        try
        {
            var tags = await _measurementService.GetDistinctTagsAsync();

            // Bail out when nothing changed — otherwise clearing the Picker's
            // ItemsSource nulls the SelectedItem, which racing the two-way binding
            // wipes out the active tag on every reload.
            if (AvailableTags.SequenceEqual(tags)) return;

            AvailableTags.Clear();
            foreach (var t in tags) AvailableTags.Add(t);

            var previouslySelected = SelectedTagOption;
            var allLabel = LocalizationService.Current.AllLabel;
            _suppressTagOptionChange = true;
            try
            {
                TagFilterOptions.Clear();
                TagFilterOptions.Add(allLabel);
                foreach (var t in tags) TagFilterOptions.Add(t);
            }
            finally
            {
                _suppressTagOptionChange = false;
            }

            // Restore the previous selection if it still exists in the new list.
            SelectedTagOption = TagFilterOptions.Contains(previouslySelected)
                ? previouslySelected
                : allLabel;
        }
        catch { /* best-effort */ }
    }

    [ObservableProperty]
    public partial bool ShowUndoBanner { get; set; }
    [ObservableProperty]
    public partial string UndoMessage { get; set; } = string.Empty;
    private Customer? _pendingDeleteCustomer;
    private CancellationTokenSource? _undoCts;

    [RelayCommand]
    private async Task SearchAsync() => await LoadAsync();

    [RelayCommand]
    private async Task DeleteAsync(Customer customer)
    {
        // Only admins may soft-delete customers. Employees hitting this via a
        // stale UI element get the standard "Not allowed" alert.
        if (!await RequirePermissionAsync(Auth.CanDeleteOrders)) return;

        // Soft-delete immediately (no confirm dialog — undo available)
        await _measurementService.DeleteCustomerAsync(customer.Id);
        Customers.Remove(customer);

        // Show undo banner for 5 seconds
        _undoCts?.Cancel();
        _pendingDeleteCustomer = customer;
        UndoMessage   = $"Deleted \"{customer.Name}\"";
        ShowUndoBanner = true;

        _undoCts = new CancellationTokenSource();
        var token = _undoCts.Token;
        try
        {
            await Task.Delay(5000, token);
            // Timer expired — hard-delete
            await _measurementService.HardDeleteCustomerAsync(customer.Id);
        }
        catch (TaskCanceledException) { /* Undo was tapped — do nothing */ }
        finally
        {
            ShowUndoBanner         = false;
            _pendingDeleteCustomer = null;
        }
    }

    [RelayCommand]
    private async Task UndoDeleteAsync()
    {
        _undoCts?.Cancel();
        if (_pendingDeleteCustomer is not null)
        {
            await _measurementService.RestoreCustomerAsync(_pendingDeleteCustomer.Id);
            Customers.Insert(0, _pendingDeleteCustomer);
        }
        ShowUndoBanner = false;
    }

    [RelayCommand]
    private async Task AddNewAsync()
    {
        if (!await RequirePermissionAsync(Auth.CanManageCustomers)) return;
        await Shell.Current.GoToAsync("MeasurementFormPage");
    }

    [RelayCommand]
    private async Task ViewDetailAsync(Customer? customer)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TapeTracker", "logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "clicks.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] ViewDetail fired. customer={(customer is null ? "NULL" : $"Id={customer.Id} Name='{customer.Name}'")}{Environment.NewLine}");
        }
        catch { }

        if (customer is null)
        {
            await Shell.Current.DisplayAlert("Debug", "ViewDetail command fired but customer is null.", "OK");
            return;
        }

        try
        {
            await Shell.Current.GoToAsync($"CustomerDetailPage?customerId={customer.Id}");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Navigation Failed", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private void ToggleUnit()
    {
        var next = _unitService.CurrentUnit == MeasurementUnit.Inch
            ? MeasurementUnit.Cm
            : MeasurementUnit.Inch;
        _unitService.SetUnit(next);
    }

    [RelayCommand]
    private async Task ShowAllAsync()
    {
        await Shell.Current.GoToAsync("AllCustomersPage");
    }

    [RelayCommand]
    private async Task ScanSlipAsync()
    {
        if (!await RequirePermissionAsync(Auth.CanManageCustomers)) return;
        await Shell.Current.GoToAsync("OcrScanPage");
    }

    /// <summary>
    /// Legacy toolbar shortcut kept for backwards compatibility. The full
    /// Backup Manager (with encryption, restore-from-file, auto-backup
    /// settings, and per-backup share/delete) now lives at
    /// <c>BackupManagerPage</c> and is the preferred entry point for admins.
    /// </summary>
    [RelayCommand]
    private async Task BackupAsync()
    {
        if (!await RequirePermissionAsync(Auth.CanExportOrBackup)) return;
        await Shell.Current.GoToAsync("BackupManagerPage");
    }

    /// <summary>
    /// Legacy toolbar shortcut. Same story as <see cref="BackupAsync"/> —
    /// forwards to the Backup Manager where restore-from-file with a
    /// passphrase prompt is a single tap away.
    /// </summary>
    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (!await RequirePermissionAsync(Auth.CanExportOrBackup)) return;
        await Shell.Current.GoToAsync("BackupManagerPage");
    }
}

/// <summary>
/// Flat, immutable display model backing the Home screen's "Due today /
/// Overdue" widget. Doesn't derive from ObservableObject because the row
/// is rebuilt from scratch on every load — mutation isn't needed.
/// </summary>
public class UrgentOrderItem
{
    public int      CustomerId   { get; init; }
    public string   CustomerName { get; init; } = string.Empty;
    public string   OrderNumber  { get; init; } = string.Empty;
    public DateTime DueDate      { get; init; }

    /// <summary>Days overdue — 0 means "due today", positive means "N days
    /// late". Never negative because callers filter to due &lt;= today.</summary>
    public int DaysOverdue { get; init; }

    public bool IsDueToday => DaysOverdue == 0;

    /// <summary>Chip color: amber for due-today, red for overdue.</summary>
    public string ChipColor => IsDueToday ? "#F59E0B" : "#DC2626";

    /// <summary>Short label rendered inside the chip. Localized on read so
    /// language toggles refresh it without needing a Refresh call.</summary>
    public string ChipLabel => IsDueToday
        ? LocalizationService.Current.DueTodayLabel
        : string.Format(LocalizationService.Current.DaysOverdueFormat, DaysOverdue);

    public static UrgentOrderItem FromOrder(TapeTracker.Models.Order order, DateTime today)
    {
        var due  = order.DueDate ?? today;
        var days = (int)(today.Date - due.Date).TotalDays;
        return new UrgentOrderItem
        {
            CustomerId   = order.CustomerId,
            CustomerName = order.Customer?.Name ?? string.Empty,
            OrderNumber  = order.OrderNumber,
            DueDate      = due,
            DaysOverdue  = Math.Max(0, days)
        };
    }
}
