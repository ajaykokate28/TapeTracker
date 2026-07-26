using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

/// <summary>
/// Backing data for the Delivery Calendar page. Renders a month grid of the
/// current organization's orders keyed by <see cref="Order.DueDate"/>, with
/// color-coded chips so the tailor can eyeball daily load at a glance.
///
/// Layout is Sunday-start (the most familiar calendar for Indian tailors and
/// matches Windows' default), and the grid is always 6 rows × 7 columns so
/// the page height doesn't jump when navigating month-to-month.
/// </summary>
public partial class DeliveryCalendarViewModel : BaseViewModel
{
    private readonly IMeasurementService _measurementService;

    /// <summary>First day of the currently-displayed month.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthHeader))]
    public partial DateTime AnchorMonth { get; set; } = FirstOfMonth(DateTime.Today);

    /// <summary>Formatted "October 2026" header. Refreshed by the anchor-month setter.</summary>
    public string MonthHeader =>
        AnchorMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);

    /// <summary>Total deliveries in the current month (footer summary).</summary>
    [ObservableProperty]
    public partial int MonthDeliveryCount { get; set; }

    /// <summary>True when the month has zero deliveries — swaps in the empty state.</summary>
    [ObservableProperty]
    public partial bool HasNoDeliveries { get; set; }

    /// <summary>Flat, ordered cells for the calendar grid. Always 42 items
    /// (6 rows × 7 columns) so the grid never re-layouts between months.</summary>
    public ObservableCollection<CalendarDayCell> Cells { get; } = new();

    public DeliveryCalendarViewModel(IMeasurementService measurementService)
    {
        _measurementService = measurementService;
        Title = LocalizationService.Current.DeliveryCalendarLabel;

        // Refresh header text when language changes so "October" localizes.
        LocalizationService.Current.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(MonthHeader));
            foreach (var c in Cells) c.RefreshLocalized();
        };
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await BuildCellsAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        AnchorMonth = AnchorMonth.AddMonths(1);
        await BuildCellsAsync();
    }

    [RelayCommand]
    private async Task PrevMonthAsync()
    {
        AnchorMonth = AnchorMonth.AddMonths(-1);
        await BuildCellsAsync();
    }

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        AnchorMonth = FirstOfMonth(DateTime.Today);
        await BuildCellsAsync();
    }

    [RelayCommand]
    private async Task OpenOrderAsync(CalendarOrderChip? chip)
    {
        if (chip is null) return;
        await Shell.Current.GoToAsync($"CustomerDetailPage?customerId={chip.CustomerId}");
    }

    // ── Cell construction ───────────────────────────────────────────────────

    private async Task BuildCellsAsync()
    {
        // Query only the exact month window; the calendar shows leading/trailing
        // days for context but we don't render orders in those out-of-month cells
        // (they'd read as noise). The window is inclusive on both ends.
        var monthStart = AnchorMonth;
        var monthEnd   = monthStart.AddMonths(1).AddDays(-1);

        var orders = await _measurementService.GetOrdersByDueDateRangeAsync(monthStart, monthEnd);

        // Group by due-day for O(1) lookup while building cells.
        var byDay = orders
            .Where(o => o.DueDate.HasValue)
            .GroupBy(o => o.DueDate!.Value.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Rebuild the 42-cell fixed grid.
        Cells.Clear();

        // Sunday-start: the leading blank offset is DayOfWeek-of-day-1 (Sun=0).
        var leading = (int)monthStart.DayOfWeek;
        var firstCellDate = monthStart.AddDays(-leading);

        for (int i = 0; i < 42; i++)
        {
            var date = firstCellDate.AddDays(i);
            var isInMonth = date.Month == monthStart.Month && date.Year == monthStart.Year;
            var isToday = date == DateTime.Today;

            var ordersForDay = isInMonth && byDay.TryGetValue(date, out var list)
                ? list
                : new List<Order>();

            Cells.Add(new CalendarDayCell(date, isInMonth, isToday, ordersForDay));
        }

        MonthDeliveryCount = orders.Count;
        HasNoDeliveries    = orders.Count == 0;
    }

    private static DateTime FirstOfMonth(DateTime d) => new(d.Year, d.Month, 1);
}

// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// One cell of the month grid. Owns its own list of order chips so the XAML
/// template doesn't need value converters.
/// </summary>
public partial class CalendarDayCell : ObservableObject
{
    public DateTime Date       { get; }
    public bool     IsInMonth  { get; }
    public bool     IsToday    { get; }
    public string   DayNumber  => Date.Day.ToString(CultureInfo.CurrentCulture);
    public ObservableCollection<CalendarOrderChip> Chips { get; } = new();
    /// <summary>"+3" indicator when the day has more than 3 deliveries.</summary>
    public string OverflowLabel { get; }
    public bool HasOverflow => !string.IsNullOrEmpty(OverflowLabel);

    /// <summary>Muted styling for prev/next month leading/trailing days.</summary>
    public double CellOpacity => IsInMonth ? 1.0 : 0.35;

    /// <summary>Today ring color (indigo) or transparent for other days.</summary>
    public string TodayHighlightColor => IsToday ? "#4F46E5" : "Transparent";

    public CalendarDayCell(DateTime date, bool isInMonth, bool isToday, List<Order> orders)
    {
        Date      = date;
        IsInMonth = isInMonth;
        IsToday   = isToday;

        // Show at most 3 chips per cell; anything beyond shows as "+N" so the
        // grid stays scannable. Chips are sorted: rush → overdue → date.
        var sorted = orders
            .OrderByDescending(o => o.IsRush)
            .ThenByDescending(o => o.Status != OrderStatus.Delivered
                                  && o.DueDate is { } dd && dd.Date < DateTime.Today)
            .ThenBy(o => o.OrderNumber)
            .ToList();

        foreach (var o in sorted.Take(3))
            Chips.Add(new CalendarOrderChip(o));

        if (sorted.Count > 3)
            OverflowLabel = $"+{sorted.Count - 3}";
        else
            OverflowLabel = string.Empty;
    }

    /// <summary>Re-fires PropertyChanged so language-dependent labels refresh
    /// when the user toggles languages while the page is open.</summary>
    public void RefreshLocalized()
    {
        foreach (var c in Chips) c.RefreshLocalized();
    }
}

// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A single order chip inside a calendar cell. Carries pre-computed color +
/// display strings so the DataTemplate has no converters.
/// </summary>
public partial class CalendarOrderChip : ObservableObject
{
    public int    OrderId       { get; }
    public int    CustomerId    { get; }
    public string OrderNumber   { get; }
    public string CustomerName  { get; }
    public bool   IsRush        { get; }
    public bool   IsOverdue     { get; }
    public bool   IsDelivered   { get; }
    /// <summary>Background tint (rush=red, overdue=purple, delivered=gray, else stage color).</summary>
    public string TintColor     { get; }
    /// <summary>Short label shown on the chip — order number.</summary>
    public string ChipLabel     => OrderNumber;

    private readonly Order _order;

    public CalendarOrderChip(Order order)
    {
        _order       = order;
        OrderId      = order.Id;
        CustomerId   = order.CustomerId;
        OrderNumber  = order.OrderNumber;
        CustomerName = order.Customer?.Name ?? string.Empty;
        IsRush       = order.IsRush;
        IsDelivered  = order.Status == OrderStatus.Delivered;
        IsOverdue    = !IsDelivered
                     && order.DueDate is { } d
                     && d.Date < DateTime.Today;

        // Priority: rush > overdue > delivered > by-status.
        if (IsRush)        TintColor = "#DC2626";
        else if (IsOverdue) TintColor = "#8B5CF6";
        else if (IsDelivered) TintColor = "#94A3B8";
        else TintColor = order.Status switch
        {
            OrderStatus.Received  => "#6B7280",
            OrderStatus.Cutting   => "#F59E0B",
            OrderStatus.Stitching => "#3B82F6",
            OrderStatus.Ready     => "#10B981",
            _                     => "#6B7280"
        };
    }

    public void RefreshLocalized() { /* nothing language-dependent yet */ }
}
