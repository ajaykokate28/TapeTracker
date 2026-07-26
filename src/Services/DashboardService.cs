using Microsoft.EntityFrameworkCore;
using TapeTracker.Data;
using TapeTracker.Models;

namespace TapeTracker.Services;

public class DashboardService : IDashboardService
{
    private readonly TapeTrackerDbContext _db;
    private readonly IAuthService       _auth;

    public DashboardService(TapeTrackerDbContext db, IAuthService auth)
    {
        _db   = db;
        _auth = auth;
        DbBootstrap.EnsureReady(_db);   // create tables + apply migrations (idempotent)
    }

    public async Task<DashboardStats> GetStatsAsync()
    {
        var today = DateTime.Today;
        var soon  = today.AddDays(7);
        var orgId = _auth.CurrentOrganization?.Id ?? 0;

        // All queries below are scoped by OrganizationId so multi-tenant
        // installs never leak counts across shops on the same workstation.
        var customersScope = _db.Customers.Where(c => c.OrganizationId == orgId);
        var ordersScope    = _db.Orders.Where(o => o.OrganizationId == orgId);

        var totalCustomers = await customersScope.CountAsync();
        var totalOrders    = await ordersScope.CountAsync();
        var activeOrders   = await ordersScope.CountAsync(o => o.Status != OrderStatus.Delivered);

        var dueSoon = await ordersScope.CountAsync(o =>
            o.DueDate.HasValue &&
            o.DueDate.Value >= today &&
            o.DueDate.Value <= soon &&
            o.Status != OrderStatus.Delivered);

        var overdue = await ordersScope.CountAsync(o =>
            o.DueDate.HasValue &&
            o.DueDate.Value < today &&
            o.Status != OrderStatus.Delivered);

        var byStatusRaw = await ordersScope
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        // Monthly trend — last 6 months (client-side grouping avoids SQLite EF date issues)
        var firstDay = new DateTime(today.AddMonths(-5).Year, today.AddMonths(-5).Month, 1);
        var rawDates = await ordersScope
            .Where(o => o.Date >= firstDay)
            .Select(o => o.Date)
            .ToListAsync();

        var monthly = Enumerable.Range(0, 6)
            .Select(i =>
            {
                var d     = today.AddMonths(-(5 - i));
                var count = rawDates.Count(dt => dt.Year == d.Year && dt.Month == d.Month);
                return new MonthlyCount(d.ToString("MMM"), count);
            })
            .ToList();

        var recent = await ordersScope
            .Include(o => o.Customer)
            .OrderByDescending(o => o.Date)
            .Take(8)
            .Select(o => new RecentOrderInfo
            {
                CustomerId   = o.CustomerId,
                CustomerName = o.Customer!.Name,
                OrderNumber  = o.OrderNumber,
                Date         = o.Date,
                DueDate      = o.DueDate,
                Status       = o.Status
            })
            .ToListAsync();

        // ── Money stats (Phase 8) ────────────────────────────────────────────
        // Compute client-side for simplicity + SQLite decimal-arithmetic quirks.
        // Only unpaid non-delivered orders count toward outstanding — delivered
        // orders that were never paid are still owed but reporting them here
        // would double-count the tailor's follow-up queue.
        var unpaidActive = await ordersScope
            .Where(o => o.Status != OrderStatus.Delivered && o.Price > 0)
            .Select(o => new { o.Price, o.DiscountAmount, o.TaxAmount, o.AdvancePaid })
            .ToListAsync();

        decimal outstandingBalance = 0m;
        int     unpaidOrderCount   = 0;
        foreach (var o in unpaidActive)
        {
            var net   = Math.Max(0m, o.Price - o.DiscountAmount);
            var total = net + o.TaxAmount;
            var due   = Math.Max(0m, total - o.AdvancePaid);
            if (due > 0)
            {
                outstandingBalance += due;
                unpaidOrderCount++;
            }
        }

        // Revenue this month = sum of GrandTotal for orders dated in current month.
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthNext  = monthStart.AddMonths(1);
        var monthRows  = await ordersScope
            .Where(o => o.Date >= monthStart && o.Date < monthNext && o.Price > 0)
            .Select(o => new { o.Price, o.DiscountAmount, o.TaxAmount })
            .ToListAsync();

        decimal revenueThisMonth = monthRows.Sum(r =>
            Math.Max(0m, r.Price - r.DiscountAmount) + r.TaxAmount);

        return new DashboardStats
        {
            TotalCustomers      = totalCustomers,
            TotalOrders         = totalOrders,
            ActiveOrders        = activeOrders,
            DueSoonOrders       = dueSoon,
            OverdueOrders       = overdue,
            ByStatus            = byStatusRaw.ToDictionary(x => x.Status, x => x.Count),
            MonthlyTrend        = monthly,
            RecentOrders        = recent,
            OutstandingBalance  = outstandingBalance,
            RevenueThisMonth    = revenueThisMonth,
            UnpaidOrderCount    = unpaidOrderCount
        };
    }
}
