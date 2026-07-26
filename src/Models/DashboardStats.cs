namespace TapeTracker.Models;

public class DashboardStats
{
    public int TotalCustomers { get; set; }
    public int TotalOrders    { get; set; }
    public int ActiveOrders   { get; set; }   // not Delivered
    public int DueSoonOrders  { get; set; }   // due in ≤7 days, not Delivered
    public int OverdueOrders  { get; set; }   // due < today, not Delivered
    public Dictionary<OrderStatus, int> ByStatus  { get; set; } = new();
    public List<MonthlyCount>           MonthlyTrend  { get; set; } = new();
    public List<RecentOrderInfo>        RecentOrders  { get; set; } = new();

    // ── Money stats (Phase 8) ───────────────────────────────────────────────
    /// <summary>Sum of BalanceDue across all non-delivered orders with
    /// pricing. Powers the "Outstanding" dashboard card.</summary>
    public decimal OutstandingBalance  { get; set; }
    /// <summary>Sum of GrandTotal for orders dated within the current month.
    /// Powers the "Revenue this month" tile.</summary>
    public decimal RevenueThisMonth    { get; set; }
    /// <summary>Number of active orders that are still owed money — the
    /// tailor's follow-up queue.</summary>
    public int     UnpaidOrderCount    { get; set; }
}

public record MonthlyCount(string Label, int Count);

public class RecentOrderInfo
{
    public int         CustomerId   { get; set; }
    public string      CustomerName { get; set; } = string.Empty;
    public string      OrderNumber  { get; set; } = string.Empty;
    public DateTime    Date         { get; set; }
    public DateTime?   DueDate      { get; set; }
    public OrderStatus Status       { get; set; }
}
