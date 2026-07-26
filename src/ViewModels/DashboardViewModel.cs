using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly IDashboardService      _dashboardService;
    private readonly IExportService         _exportService;
    private readonly IUnitPreferenceService _unitService;
    private readonly IMeasurementService    _measurementService;
    private readonly IAuditService          _auditService;
    private readonly IAutoBackupScheduler   _autoBackup;
    private readonly IAuthService           _authService;

    // ── Stat cards ────────────────────────────────────────────────────────────
    [ObservableProperty]
    public partial int TotalCustomers { get; set; }
    [ObservableProperty]
    public partial int TotalOrders { get; set; }
    [ObservableProperty]
    public partial int ActiveOrders { get; set; }
    [ObservableProperty]
    public partial int DueSoonOrders { get; set; }
    [ObservableProperty]
    public partial int OverdueOrders { get; set; }

    // ── Money stats (Phase 8) ────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutstandingBalanceDisplay), nameof(HasOutstandingBalance))]
    public partial decimal OutstandingBalance { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RevenueThisMonthDisplay))]
    public partial decimal RevenueThisMonth { get; set; }

    [ObservableProperty]
    public partial int UnpaidOrderCount { get; set; }

    public string OutstandingBalanceDisplay =>
        $"{Currency}{OutstandingBalance:0.##}";
    public string RevenueThisMonthDisplay =>
        $"{Currency}{RevenueThisMonth:0.##}";
    public bool HasOutstandingBalance => OutstandingBalance > 0m;

    /// <summary>Currency symbol from the current shop settings, defaulting to ₹.</summary>
    private string Currency =>
        _authService.CurrentOrganization?.CurrencySymbol ?? "₹";

    // ── Per-status counts ─────────────────────────────────────────────────────
    [ObservableProperty]
    public partial int ReceivedCount { get; set; }
    [ObservableProperty]
    public partial int CuttingCount { get; set; }
    [ObservableProperty]
    public partial int StitchingCount { get; set; }
    [ObservableProperty]
    public partial int ReadyCount { get; set; }
    [ObservableProperty]
    public partial int DeliveredCount { get; set; }

    // ── Progress-bar values (0..1) ────────────────────────────────────────────
    [ObservableProperty]
    public partial double ReceivedPct { get; set; }
    [ObservableProperty]
    public partial double CuttingPct { get; set; }
    [ObservableProperty]
    public partial double StitchingPct { get; set; }
    [ObservableProperty]
    public partial double ReadyPct { get; set; }
    [ObservableProperty]
    public partial double DeliveredPct { get; set; }

    // ── Chart + recent orders ─────────────────────────────────────────────────
    public List<MonthlyCount>                   MonthlyTrend  { get; private set; } = new();
    public ObservableCollection<RecentOrderInfo> RecentOrders  { get; } = new();

    // ── My Work (employee dashboard, Phase 4) ─────────────────────────────
    public ObservableCollection<MyWorkItem> MyWork { get; } = new();

    /// <summary>True when the signed-in employee has zero active assignments —
    /// drives the friendly "Nothing assigned yet" placeholder on the dashboard.</summary>
    [ObservableProperty]
    public partial bool HasNoAssignedWork { get; set; } = true;

    // ── Recent activity (admin dashboard, Phase 5) ────────────────────────
    public ObservableCollection<ActivityRowViewModel> RecentActivity { get; } = new();

    [ObservableProperty]
    public partial bool HasRecentActivity { get; set; }

    // ── Attention widget: rush + stuck orders (Phase 7) ────────────────────
    /// <summary>Union of "rush" and "stuck ≥ 3 days" orders for the admin
    /// dashboard. Capped at 6 rows to keep the card compact — the calendar
    /// page is the full drill-down.</summary>
    public ObservableCollection<AttentionOrderItem> AttentionOrders { get; } = new();

    [ObservableProperty]
    public partial bool HasAttentionItems { get; set; }

    public DashboardViewModel(
        IDashboardService      dashboardService,
        IExportService         exportService,
        IUnitPreferenceService unitService,
        IMeasurementService    measurementService,
        IAuditService          auditService,
        IAutoBackupScheduler   autoBackup,
        IAuthService           authService)
    {
        _dashboardService   = dashboardService;
        _exportService      = exportService;
        _unitService        = unitService;
        _measurementService = measurementService;
        _auditService       = auditService;
        _autoBackup         = autoBackup;
        _authService        = authService;
        Title = "Dashboard";

        // Re-emit MyWork + activity rows' localized labels when the language
        // changes so stage pills and "3 min ago" strings refresh in place.
        LocalizationService.Current.PropertyChanged += (_, _) =>
        {
            foreach (var item in MyWork)         item.RefreshLocalized();
            foreach (var item in RecentActivity) item.RefreshLocalized();
        };
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var stats = await _dashboardService.GetStatsAsync();

            TotalCustomers = stats.TotalCustomers;
            TotalOrders    = stats.TotalOrders;
            ActiveOrders   = stats.ActiveOrders;
            DueSoonOrders  = stats.DueSoonOrders;
            OverdueOrders  = stats.OverdueOrders;

            OutstandingBalance = stats.OutstandingBalance;
            RevenueThisMonth   = stats.RevenueThisMonth;
            UnpaidOrderCount   = stats.UnpaidOrderCount;

            double max = stats.TotalOrders > 0 ? stats.TotalOrders : 1;
            ReceivedCount  = stats.ByStatus.GetValueOrDefault(OrderStatus.Received);
            CuttingCount   = stats.ByStatus.GetValueOrDefault(OrderStatus.Cutting);
            StitchingCount = stats.ByStatus.GetValueOrDefault(OrderStatus.Stitching);
            ReadyCount     = stats.ByStatus.GetValueOrDefault(OrderStatus.Ready);
            DeliveredCount = stats.ByStatus.GetValueOrDefault(OrderStatus.Delivered);

            ReceivedPct  = ReceivedCount  / max;
            CuttingPct   = CuttingCount   / max;
            StitchingPct = StitchingCount / max;
            ReadyPct     = ReadyCount     / max;
            DeliveredPct = DeliveredCount / max;

            MonthlyTrend = stats.MonthlyTrend;
            OnPropertyChanged(nameof(MonthlyTrend));

            RecentOrders.Clear();
            foreach (var r in stats.RecentOrders)
                RecentOrders.Add(r);

            await LoadMyWorkAsync();
            await LoadRecentActivityAsync();
            await LoadAttentionItemsAsync();
        }
        finally { IsBusy = false; }

        // Fire-and-forget the scheduled backup check. This is deliberately
        // OUTSIDE the IsBusy block so a slow backup (encrypting a large DB)
        // never freezes the dashboard UI. The scheduler is a no-op when the
        // interval hasn't elapsed, so it's safe to call on every refresh.
        _ = Task.Run(() => _autoBackup.RunIfDueAsync());
    }

    /// <summary>
    /// Loads the top 6 most-recent activity events for the current org — used
    /// by the admin dashboard widget. Employees don't render this section so
    /// we still fetch (cheap) but the panel is hidden by role in XAML.
    /// </summary>
    private async Task LoadRecentActivityAsync()
    {
        RecentActivity.Clear();
        var logs = await _auditService.QueryAsync(take: 6);
        foreach (var l in logs)
            RecentActivity.Add(new ActivityRowViewModel(l));
        HasRecentActivity = RecentActivity.Count > 0;
    }

    /// <summary>
    /// Populates the employee "My Work" queue for the currently signed-in user.
    /// Admins load it too — it's still useful when an admin has personally been
    /// assigned a stage — but the panel only renders when they *don't* have the
    /// shop-wide dashboard view enabled (i.e. on smaller screens or if the
    /// admin has explicitly hidden management widgets in a future setting).
    /// </summary>
    private async Task LoadMyWorkAsync()
    {
        MyWork.Clear();
        var userId = Auth.CurrentUser?.Id ?? 0;
        if (userId == 0)
        {
            HasNoAssignedWork = true;
            return;
        }
        var assignments = await _measurementService.GetMyWorkAsync(userId);
        foreach (var a in assignments)
            MyWork.Add(new MyWorkItem(a));
        HasNoAssignedWork = MyWork.Count == 0;
    }

    [RelayCommand]
    private async Task ExportAllAsync()
    {
        if (!await RequirePermissionAsync(Auth.CanExportOrBackup)) return;
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var customers = await _measurementService.GetAllCustomersAsync();
            if (customers.Count == 0)
            {
                await Shell.Current.DisplayAlert("No Data", "No customers to export.", "OK");
                return;
            }
            await _exportService.ExportAllToExcelAsync(customers, _unitService);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Export Failed", ex.Message, "OK");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GoToCustomerAsync(RecentOrderInfo? item)
    {
        if (item is null) return;
        await Shell.Current.GoToAsync($"CustomerDetailPage?customerId={item.CustomerId}");
    }

    [RelayCommand]
    private async Task OpenPinSettingsAsync()
        => await Shell.Current.GoToAsync("PinPage?isSettings=true");

    [RelayCommand]
    private async Task OpenEmployeesAsync()
    {
        // Belt-and-suspenders — the toolbar item is already gated by Auth.CanManageCustomers
        // in XAML, but a defense-in-depth check here keeps a stale UI from doing anything scary.
        if (!await RequirePermissionAsync(Auth.CanManageCustomers)) return;
        await Shell.Current.GoToAsync("EmployeeListPage");
    }

    [RelayCommand]
    private async Task OpenActivityLogAsync()
    {
        // Admins own audit visibility — employees don't need (or want) to see
        // other people's actions on their queue.
        if (!await RequirePermissionAsync(Auth.CanSeeShopWideDashboard)) return;
        await Shell.Current.GoToAsync("ActivityLogPage");
    }

    [RelayCommand]
    private async Task OpenBackupAsync()
    {
        // Backup/restore is admin-only — permission mirrors Export because
        // both operations expose full customer data off-device.
        if (!await RequirePermissionAsync(Auth.CanExportOrBackup)) return;
        await Shell.Current.GoToAsync("BackupManagerPage");
    }

    // ── My Work commands (Phase 4) ─────────────────────────────────────────

    [RelayCommand]
    private async Task MarkCompleteAsync(MyWorkItem? item)
    {
        if (item is null) return;
        bool confirm = await Shell.Current.DisplayAlert(
            item.StageLabel,
            L.ConfirmMarkCompleteMsg,
            L.MarkCompleteLabel,
            L.CancelLabel);
        if (!confirm) return;

        var userId = Auth.CurrentUser?.Id ?? 0;
        if (userId == 0) return;

        await _measurementService.CompleteStageAsync(item.AssignmentId, userId);

        // Reload the whole dashboard so counts, pipeline pcts, and My Work all
        // reflect the newly-completed stage. Cheaper than surgical updates and
        // guarantees consistency.
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ViewMyWorkOrderAsync(MyWorkItem? item)
    {
        if (item is null) return;
        await Shell.Current.GoToAsync($"CustomerDetailPage?customerId={item.CustomerId}");
    }

    /// <summary>
    /// Loads up to 6 orders that need immediate attention — rush flag OR
    /// stuck ≥ 3 days in the current stage. Union-de-duped so a rush order
    /// that is also stuck only appears once (rush wins for the badge tint).
    /// </summary>
    private async Task LoadAttentionItemsAsync()
    {
        AttentionOrders.Clear();

        // Rush orders (all active) — a small set in practice; fetch first so
        // they always take precedence when we hit the 6-row cap.
        var today = DateTime.Today;
        var rushList  = await _measurementService.GetOrdersByDueDateRangeAsync(
            today.AddYears(-1), today.AddYears(1));
        var rushOnly  = rushList.Where(o => o.IsRush && o.Status != OrderStatus.Delivered)
                                 .OrderBy(o => o.DueDate ?? DateTime.MaxValue);

        var stuckOnly = await _measurementService.GetStuckOrdersAsync(3);

        var seen = new HashSet<int>();
        foreach (var o in rushOnly.Concat(stuckOnly))
        {
            if (!seen.Add(o.Id)) continue;
            if (AttentionOrders.Count >= 6) break;
            AttentionOrders.Add(new AttentionOrderItem(o));
        }
        HasAttentionItems = AttentionOrders.Count > 0;
    }

    [RelayCommand]
    private async Task OpenAttentionOrderAsync(AttentionOrderItem? item)
    {
        if (item is null) return;
        await Shell.Current.GoToAsync($"CustomerDetailPage?customerId={item.CustomerId}");
    }

    [RelayCommand]
    private async Task OpenDeliveryCalendarAsync()
        => await Shell.Current.GoToAsync("DeliveryCalendarPage");

    [RelayCommand]
    private async Task OpenShopSettingsAsync()
        => await Shell.Current.GoToAsync("ShopSettingsPage");
}

/// <summary>
/// Flat projection of an <see cref="Order"/> for the dashboard's attention
/// widget — carries the display strings and a tint color so the XAML
/// doesn't need converters.
/// </summary>
public class AttentionOrderItem
{
    public int    OrderId       { get; }
    public int    CustomerId    { get; }
    public string OrderNumber   { get; }
    public string CustomerName  { get; }
    public string SubtitleLabel { get; }
    /// <summary>Red for rush, amber for stuck, purple for overdue.</summary>
    public string TintColor     { get; }
    public bool   IsRush        { get; }

    public AttentionOrderItem(Order o)
    {
        OrderId      = o.Id;
        CustomerId   = o.CustomerId;
        OrderNumber  = o.OrderNumber;
        CustomerName = o.Customer?.Name ?? string.Empty;
        IsRush       = o.IsRush;

        // Priority for the pill color: rush > overdue > stuck.
        if (o.IsRush)
            TintColor = "#DC2626";
        else if (o.DueDate is { } d && d.Date < DateTime.Today)
            TintColor = "#8B5CF6";
        else
            TintColor = "#F59E0B";

        // Subtitle: whichever urgency signal fires loudest.
        var L = LocalizationService.Current;
        if (o.StageChangedAt is { } t)
        {
            var days = Math.Max(0, (int)(DateTime.UtcNow - t).TotalDays);
            SubtitleLabel = string.Format(L.StuckDaysFormat, days);
        }
        else if (o.DueDate is { } dd && dd.Date < DateTime.Today)
        {
            SubtitleLabel = L.OverdueLabel;
        }
        else
        {
            SubtitleLabel = L.RushLabel;
        }
    }
}

/// <summary>
/// Flat, UI-friendly projection of an <see cref="Models.OrderStageAssignment"/>
/// for the employee's My Work list. Owns its own localized labels so the
/// dashboard doesn't need extra converters.
/// </summary>
public partial class MyWorkItem : ObservableObject
{
    public int      AssignmentId { get; }
    public int      OrderId      { get; }
    public int      CustomerId   { get; }
    public string   OrderNumber  { get; }
    public string   CustomerName { get; }
    public string   Phone        { get; }
    public DateTime? DueDate     { get; }
    public OrderStatus Stage     { get; }

    [ObservableProperty] public partial string StageLabel  { get; set; } = string.Empty;
    [ObservableProperty] public partial string StageColor  { get; set; } = "#6B7280";
    [ObservableProperty] public partial string DueLabel    { get; set; } = string.Empty;
    [ObservableProperty] public partial bool   IsOverdue   { get; set; }

    public MyWorkItem(Models.OrderStageAssignment a)
    {
        AssignmentId = a.Id;
        OrderId      = a.OrderId;
        CustomerId   = a.Order?.CustomerId ?? 0;
        OrderNumber  = a.Order?.OrderNumber ?? string.Empty;
        CustomerName = a.Order?.Customer?.Name ?? string.Empty;
        Phone        = a.Order?.Customer?.Phone ?? string.Empty;
        DueDate      = a.Order?.DueDate;
        Stage        = a.Stage;
        RefreshLocalized();
    }

    /// <summary>Recomputes labels that depend on the active language.
    /// Called from the dashboard whenever <see cref="LocalizationService"/> fires.</summary>
    public void RefreshLocalized()
    {
        StageLabel = LocalizationService.Current.LocalizeStatus(Stage);
        StageColor = Stage switch
        {
            OrderStatus.Cutting   => "#F59E0B",
            OrderStatus.Stitching => "#3B82F6",
            OrderStatus.Ready     => "#10B981",
            _                     => "#6B7280"
        };
        DueLabel  = DueDate.HasValue
            ? DueDate.Value.ToString("dd MMM yyyy")
            : LocalizationService.Current.NoDueDateLabel;
        IsOverdue = DueDate.HasValue && DueDate.Value.Date < DateTime.Today;
    }
}
