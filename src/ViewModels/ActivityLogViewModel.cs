using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

/// <summary>
/// One entry in the activity feed. Owns its own colour, icon, and human-friendly
/// "3 min ago" label so the XAML stays simple (no per-row converters).
/// </summary>
public partial class ActivityRowViewModel : ObservableObject
{
    public AuditLog Log { get; }

    public ActivityRowViewModel(AuditLog log)
    {
        Log = log;
        RefreshLocalized();
    }

    public int Id => Log.Id;
    public string UserDisplayName => Log.UserDisplayName;
    public string Summary         => Log.Summary;
    public string EntityType      => Log.EntityType;
    public int    EntityId        => Log.EntityId;

    /// <summary>Best-effort avatar-style badge — first letter of the actor.</summary>
    public string Initial => string.IsNullOrWhiteSpace(Log.UserDisplayName)
        ? "?"
        : Log.UserDisplayName.Trim()[..1].ToUpperInvariant();

    [ObservableProperty] public partial string RelativeTime { get; set; } = string.Empty;
    [ObservableProperty] public partial string ExactTime    { get; set; } = string.Empty;
    [ObservableProperty] public partial string ActionLabel  { get; set; } = string.Empty;
    [ObservableProperty] public partial string ActionColor  { get; set; } = "#6B7280";
    [ObservableProperty] public partial string Icon         { get; set; } = "\u2022";

    /// <summary>
    /// Recomputes the localized "3 min ago" / "Today HH:mm" text, the exact
    /// tooltip timestamp, the coloured pill label, and the leading icon.
    /// Called when the parent list is loaded and whenever the app language flips.
    /// </summary>
    public void RefreshLocalized()
    {
        var local = Log.Timestamp.ToLocalTime();
        RelativeTime = HumanizeAgo(local);
        ExactTime    = local.ToString("dd MMM yyyy HH:mm");
        (ActionLabel, ActionColor, Icon) = DecorateAction(Log.Action);
    }

    private static string HumanizeAgo(DateTime local)
    {
        var l  = LocalizationService.Current;
        var d  = DateTime.Now - local;
        if (d.TotalSeconds < 60)  return l.JustNowLabel;
        if (d.TotalMinutes < 60)  return string.Format(l.MinutesAgoFmt, (int)d.TotalMinutes);
        if (d.TotalHours   < 24)  return string.Format(l.HoursAgoFmt,   (int)d.TotalHours);
        if (d.TotalDays    < 7)   return string.Format(l.DaysAgoFmt,    (int)d.TotalDays);
        return local.ToString("dd MMM yyyy");
    }

    private static (string Label, string Color, string Icon) DecorateAction(AuditAction a) => a switch
    {
        AuditAction.Login               => (LocalizationService.Current.FilterAuthLabel,        "#3B82F6", "\u2192"),
        AuditAction.LoginFailed         => ("Failed login",                                     "#DC2626", "\u2715"),
        AuditAction.Logout              => (LocalizationService.Current.FilterAuthLabel,        "#6B7280", "\u2190"),
        AuditAction.OrganizationCreated => ("Org",                                              "#8B5CF6", "\u2605"),
        AuditAction.UserCreated         => (LocalizationService.Current.FilterUsersLabel,       "#10B981", "+"),
        AuditAction.UserUpdated         => (LocalizationService.Current.FilterUsersLabel,       "#6B7280", "\u270E"),
        AuditAction.UserDeactivated     => (LocalizationService.Current.FilterUsersLabel,       "#DC2626", "\u2296"),
        AuditAction.UserActivated       => (LocalizationService.Current.FilterUsersLabel,       "#10B981", "\u2295"),
        AuditAction.UserPasswordReset   => (LocalizationService.Current.FilterUsersLabel,       "#F59E0B", "\U0001F511"),
        AuditAction.PinChanged          => (LocalizationService.Current.PinLabel,               "#6B7280", "\u2022"),
        AuditAction.CustomerCreated     => (LocalizationService.Current.FilterCustomersLabel,   "#10B981", "+"),
        AuditAction.CustomerUpdated     => (LocalizationService.Current.FilterCustomersLabel,   "#3B82F6", "\u270E"),
        AuditAction.CustomerDeleted     => (LocalizationService.Current.FilterCustomersLabel,   "#DC2626", "\u2716"),
        AuditAction.CustomerRestored    => (LocalizationService.Current.FilterCustomersLabel,   "#10B981", "\u21BA"),
        AuditAction.OrderCreated        => (LocalizationService.Current.FilterOrdersLabel,      "#10B981", "+"),
        AuditAction.OrderUpdated        => (LocalizationService.Current.FilterOrdersLabel,      "#3B82F6", "\u270E"),
        AuditAction.OrderDeleted        => (LocalizationService.Current.FilterOrdersLabel,      "#DC2626", "\u2716"),
        AuditAction.OrderRestored       => (LocalizationService.Current.FilterOrdersLabel,      "#10B981", "\u21BA"),
        AuditAction.OrderStatusChanged  => (LocalizationService.Current.FilterOrdersLabel,      "#F59E0B", "\u2192"),
        AuditAction.StageAssigned       => (LocalizationService.Current.FilterAssignmentsLabel, "#4F46E5", "\u25B6"),
        AuditAction.StageUnassigned     => (LocalizationService.Current.FilterAssignmentsLabel, "#6B7280", "\u25A1"),
        AuditAction.StageCompleted      => (LocalizationService.Current.FilterAssignmentsLabel, "#059669", "\u2714"),
        AuditAction.Exported            => (LocalizationService.Current.ExportShortLabel,       "#8B5CF6", "\u2B07"),
        AuditAction.Backup              => (LocalizationService.Current.BackupLabel,            "#8B5CF6", "\u2B07"),
        AuditAction.Restore             => (LocalizationService.Current.RestoreLabel,           "#8B5CF6", "\u2B06"),
        _                               => (a.ToString(),                                       "#6B7280", "\u2022")
    };
}

/// <summary>Category buckets exposed on the filter chip row.</summary>
public enum ActivityFilter
{
    All,
    Auth,
    Customers,
    Orders,
    Users,
    Assignments
}

/// <summary>Admin-only activity feed. Load, filter, refresh.</summary>
public partial class ActivityLogViewModel : BaseViewModel
{
    private readonly IAuditService _auditService;

    public ObservableCollection<ActivityRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>Currently active filter chip. Setter re-runs the load.</summary>
    [ObservableProperty]
    public partial ActivityFilter ActiveFilter { get; set; } = ActivityFilter.All;

    partial void OnActiveFilterChanged(ActivityFilter value) => _ = LoadAsync();

    // Individual chip-highlight flags — cleaner XAML than a value converter.
    public bool IsAll         => ActiveFilter == ActivityFilter.All;
    public bool IsAuth        => ActiveFilter == ActivityFilter.Auth;
    public bool IsCustomers   => ActiveFilter == ActivityFilter.Customers;
    public bool IsOrders      => ActiveFilter == ActivityFilter.Orders;
    public bool IsUsers       => ActiveFilter == ActivityFilter.Users;
    public bool IsAssignments => ActiveFilter == ActivityFilter.Assignments;

    partial void OnActiveFilterChanging(ActivityFilter value)
    {
        // Notify all chip flags so their visual state updates in lock-step.
        OnPropertyChanged(nameof(IsAll));
        OnPropertyChanged(nameof(IsAuth));
        OnPropertyChanged(nameof(IsCustomers));
        OnPropertyChanged(nameof(IsOrders));
        OnPropertyChanged(nameof(IsUsers));
        OnPropertyChanged(nameof(IsAssignments));
    }

    public ActivityLogViewModel(IAuditService auditService)
    {
        _auditService = auditService;
        Title = LocalizationService.Current.ActivityLogTitle;

        LocalizationService.Current.PropertyChanged += (_, _) =>
        {
            Title = LocalizationService.Current.ActivityLogTitle;
            foreach (var r in Rows) r.RefreshLocalized();
        };
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            // Ask the service for a raw feed, then apply the category filter
            // in memory. The category buckets straddle multiple enum values so
            // one SQL WHERE clause would be awkward; the row count is capped so
            // filtering in memory is fine.
            var raw = await _auditService.QueryAsync(take: 200);

            static bool IsAuthAction(AuditAction a) =>
                a is AuditAction.Login or AuditAction.LoginFailed or AuditAction.Logout;
            static bool IsAssignAction(AuditAction a) =>
                a is AuditAction.StageAssigned or AuditAction.StageUnassigned or AuditAction.StageCompleted;

            var filtered = ActiveFilter switch
            {
                ActivityFilter.Auth        => raw.Where(a => IsAuthAction(a.Action)),
                ActivityFilter.Customers   => raw.Where(a => a.EntityType == "Customer"),
                ActivityFilter.Orders      => raw.Where(a =>
                    a.EntityType == "Order" && !IsAssignAction(a.Action)),
                ActivityFilter.Users       => raw.Where(a =>
                    a.EntityType == "User" || a.Action == AuditAction.OrganizationCreated),
                ActivityFilter.Assignments => raw.Where(a => IsAssignAction(a.Action)),
                _                          => raw
            };

            Rows.Clear();
            foreach (var l in filtered)
                Rows.Add(new ActivityRowViewModel(l));
            IsEmpty = Rows.Count == 0;
        }
        finally { IsBusy = false; }
    }

    // ── Filter chip commands ──────────────────────────────────────────────

    [RelayCommand] private void SetFilterAll()         => ActiveFilter = ActivityFilter.All;
    [RelayCommand] private void SetFilterAuth()        => ActiveFilter = ActivityFilter.Auth;
    [RelayCommand] private void SetFilterCustomers()   => ActiveFilter = ActivityFilter.Customers;
    [RelayCommand] private void SetFilterOrders()      => ActiveFilter = ActivityFilter.Orders;
    [RelayCommand] private void SetFilterUsers()       => ActiveFilter = ActivityFilter.Users;
    [RelayCommand] private void SetFilterAssignments() => ActiveFilter = ActivityFilter.Assignments;
}
