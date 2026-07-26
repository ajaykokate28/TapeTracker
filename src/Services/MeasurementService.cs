using Microsoft.EntityFrameworkCore;
using TapeTracker.Data;
using TapeTracker.Models;

namespace TapeTracker.Services;

public class MeasurementService : IMeasurementService
{
    private readonly TapeTrackerDbContext _db;
    private readonly IAuthService       _auth;
    private readonly IAuditService      _audit;

    public MeasurementService(TapeTrackerDbContext db, IAuthService auth, IAuditService audit)
    {
        _db    = db;
        _auth  = auth;
        _audit = audit;
        DbBootstrap.EnsureReady(_db);   // create tables + apply migrations (idempotent)
    }

    /// <summary>
    /// Every read must scope by the currently signed-in organization so
    /// customers/orders never leak across shops sharing a workstation. Before
    /// login (splash / registration / login screen) this returns 0, which
    /// results in an empty query — safe for the not-signed-in case.
    /// </summary>
    private int CurrentOrgId => _auth.CurrentOrganization?.Id ?? 0;

    // â”€â”€ Customer â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<List<Customer>> GetAllCustomersAsync(
        string? searchTerm = null,
        int? limit = null,
        OrderStatus? statusFilter = null,
        string? tagFilter = null)
    {
        var orgId = CurrentOrgId;
        var query = _db.Customers
            .Include(c => c.Orders.Where(o => !o.IsDeleted))
                .ThenInclude(o => o.Shirt)
            .Include(c => c.Orders.Where(o => !o.IsDeleted))
                .ThenInclude(o => o.Pant)
            .Where(c => !c.IsDeleted && c.OrganizationId == orgId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();

            // Strip non-digit characters so "987 654 3210" or "(987) 654-3210"
            // still matches a stored "9876543210". Also enables the common
            // "search by last 4 digits" workflow for walk-in lookups.
            var digitsOnly = new string(term.Where(char.IsDigit).ToArray());
            bool isDigitQuery = digitsOnly.Length > 0 && digitsOnly.Length == term.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("+", "").Length;

            if (isDigitQuery && digitsOnly.Length >= 3)
            {
                // Pure-digit query: match stored phone with any separators stripped.
                query = query.Where(c =>
                    c.Name.ToLower().Contains(term) ||
                    c.Phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("+", "").Contains(digitsOnly) ||
                    (c.Tag != null && c.Tag.ToLower().Contains(term)) ||
                    c.Orders.Any(o => o.OrderNumber.ToLower().Contains(term)));
            }
            else
            {
                query = query.Where(c =>
                    c.Name.ToLower().Contains(term) ||
                    c.Phone.ToLower().Contains(term) ||
                    (c.Tag != null && c.Tag.ToLower().Contains(term)) ||
                    c.Orders.Any(o => o.OrderNumber.ToLower().Contains(term)));
            }
        }

        if (statusFilter.HasValue)
            query = query.Where(c => c.Orders.Any(o => o.Status == statusFilter.Value));

        var normalizedTagFilter = TagUtil.Normalize(tagFilter);
        if (normalizedTagFilter is not null)
        {
            // Case-insensitive so old (pre-normalization) rows still match.
            var tagLower = normalizedTagFilter.ToLower();
            query = query.Where(c => c.Tag != null && c.Tag.ToLower() == tagLower);
        }

        var ordered = query.OrderByDescending(c =>
            c.Orders.Max(o => (DateTime?)o.Date) ?? DateTime.MinValue);

        var results = limit.HasValue
            ? await ordered.Take(limit.Value).ToListAsync()
            : await ordered.ToListAsync();

        // Fuzzy fallback: when the caller supplied a name-like search term
        // (letters only, ≥ 3 chars) AND the strict Contains-based query
        // returned nothing, sweep the whole org's customer table with a
        // Levenshtein-distance-≤-2 pass. Covers typos like "Rmesh" →
        // "Ramesh" and "Sarma" → "Sharma" without polluting the primary
        // search path with any client-side scan cost when strict matches
        // exist.
        if (results.Count == 0 && !string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            bool isNameLike = term.Length >= 3 && term.All(c => !char.IsDigit(c));
            if (isNameLike)
            {
                var allInOrg = await _db.Customers
                    .Include(c => c.Orders.Where(o => !o.IsDeleted))
                        .ThenInclude(o => o.Shirt)
                    .Include(c => c.Orders.Where(o => !o.IsDeleted))
                        .ThenInclude(o => o.Pant)
                    .Where(c => !c.IsDeleted && c.OrganizationId == orgId)
                    .ToListAsync();

                results = allInOrg
                    .Where(c => FuzzySearch.IsCloseMatch(c.Name, term))
                    .OrderByDescending(c =>
                        c.Orders.Max(o => (DateTime?)o.Date) ?? DateTime.MinValue)
                    .Take(limit ?? int.MaxValue)
                    .ToList();
            }
        }

        return results;
    }

    public async Task<List<string>> GetDistinctTagsAsync()
    {
        var orgId = CurrentOrgId;
        // Pull the raw values first, then normalize + dedupe case-insensitively
        // in memory. Keeps the SQL simple and works even if old rows still
        // contain the un-normalized "sharma  family" / " Sharma Family " form.
        var raw = await _db.Customers
            .Where(c => !c.IsDeleted && c.OrganizationId == orgId && c.Tag != null && c.Tag != "")
            .Select(c => c.Tag!)
            .ToListAsync();

        var canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in raw)
        {
            var norm = TagUtil.Normalize(t);
            if (norm is null) continue;
            // First-seen wins as the display spelling.
            canonical.TryAdd(norm, norm);
        }

        return canonical.Values.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<List<string>> GetFamilyMemberNamesAsync(int customerId)
    {
        var orgId = CurrentOrgId;
        var self = await _db.Customers
            .Where(c => c.Id == customerId && c.OrganizationId == orgId && !c.IsDeleted)
            .Select(c => new { c.Name, c.Tag })
            .FirstOrDefaultAsync();

        if (self is null) return new List<string>();

        // No tag → single-name list. Tailors can still type family names
        // manually on the invoice; this just short-circuits the picker.
        var norm = TagUtil.Normalize(self.Tag);
        if (norm is null) return new List<string> { self.Name };

        var siblings = await _db.Customers
            .Where(c => !c.IsDeleted && c.OrganizationId == orgId && c.Tag != null && c.Tag != "")
            .Select(c => new { c.Name, c.Tag })
            .ToListAsync();

        // Match tags case-insensitively via TagUtil.Normalize so
        // "sharma family" / "Sharma Family" merge into one bucket.
        return siblings
            .Where(x => string.Equals(TagUtil.Normalize(x.Tag), norm,
                        StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<Customer?> GetCustomerByIdAsync(int id)
    {
        var orgId = CurrentOrgId;
        return await _db.Customers
            .Include(c => c.Orders.Where(o => !o.IsDeleted))
                .ThenInclude(o => o.Shirt)
            .Include(c => c.Orders.Where(o => !o.IsDeleted))
                .ThenInclude(o => o.Pant)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted && c.OrganizationId == orgId);
    }

    public async Task<int> SaveCustomerAsync(Customer customer)
    {
        // Always normalize the tag (trim + collapse whitespace) so future
        // reads / filters never see dirty duplicates.
        customer.Tag = TagUtil.Normalize(customer.Tag);

        bool isNew = customer.Id == 0;
        if (isNew)
        {
            // Stamp the current org so new rows are visible to their creator
            // and invisible to sibling shops sharing the workstation.
            customer.OrganizationId = CurrentOrgId;
            _db.Customers.Add(customer);
        }
        else
        {
            var existing = await _db.Customers.FindAsync(customer.Id);
            if (existing is null) return 0;
            existing.Name  = customer.Name;
            existing.Phone = customer.Phone;
            existing.Tag   = customer.Tag;
        }
        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            isNew ? AuditAction.CustomerCreated : AuditAction.CustomerUpdated,
            isNew ? $"Added customer '{customer.Name}'"
                  : $"Updated customer '{customer.Name}'",
            "Customer", customer.Id);
        return customer.Id;
    }

    public async Task DeleteCustomerAsync(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is not null)
        {
            customer.IsDeleted = true;   // soft-delete
            await _db.SaveChangesAsync();
            await _audit.LogAsync(AuditAction.CustomerDeleted,
                $"Deleted customer '{customer.Name}'",
                "Customer", customer.Id);
        }
    }

    public async Task RestoreCustomerAsync(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is not null)
        {
            customer.IsDeleted = false;
            await _db.SaveChangesAsync();
            await _audit.LogAsync(AuditAction.CustomerRestored,
                $"Restored customer '{customer.Name}'",
                "Customer", customer.Id);
        }
    }

    public async Task HardDeleteCustomerAsync(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is not null)
        {
            _db.Customers.Remove(customer);
            await _db.SaveChangesAsync();
        }
    }

    // â”€â”€ Order â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<List<Order>> GetOrdersByCustomerAsync(int customerId)
    {
        var orgId = CurrentOrgId;
        return await _db.Orders
            .Include(o => o.Shirt)
            .Include(o => o.Pant)
            .Include(o => o.LineItems)
            .Where(o => o.CustomerId == customerId && o.OrganizationId == orgId)
            .OrderByDescending(o => o.Date)
            .ToListAsync();
    }

    public async Task<Order?> GetOrderByIdAsync(int orderId)
    {
        var orgId = CurrentOrgId;
        return await _db.Orders
            .Include(o => o.Shirt)
            .Include(o => o.Pant)
            .Include(o => o.Customer)
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.OrganizationId == orgId);
    }

    public async Task SaveOrderAsync(Order order)
    {
        // ── Derive Price from line items when present ─────────────────────
        // The measurement form no longer exposes a single "Stitching charge"
        // input — it's computed from the line-item rows. Order.Price stays
        // for backward compatibility with pre-Phase-9 orders that were saved
        // without any lines; if a caller supplies both, line items win.
        if (order.LineItems is { Count: > 0 })
        {
            order.Price = order.LineItems.Sum(li => li.Quantity * li.UnitPrice);
            // Re-index sort order in case the caller left gaps or duplicates
            // — keeps the printed invoice's row order deterministic.
            int idx = 0;
            foreach (var li in order.LineItems.OrderBy(l => l.SortIndex))
                li.SortIndex = idx++;
        }

        bool isNew = order.Id == 0;
        if (isNew)
        {
            order.OrganizationId  = CurrentOrgId;
            order.CreatedByUserId = _auth.CurrentUser?.Id;
            // Every new order enters its first stage RIGHT NOW; stamp the
            // transition so SLA / stuck-days start counting from creation.
            order.StageChangedAt  = DateTime.UtcNow;
            _db.Orders.Add(order);
        }
        else
        {
            var existing = await _db.Orders
                .Include(o => o.Shirt)
                .Include(o => o.Pant)
                .Include(o => o.LineItems)
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            if (existing is null) return;

            // Detect a status change happening via the save-form flow (not
            // just the dedicated UpdateOrderStatusAsync path) so the SLA
            // counter resets consistently regardless of which UI touched it.
            if (existing.Status != order.Status)
                existing.StageChangedAt = DateTime.UtcNow;

            // Detect a payment change so we can log it separately from the
            // generic OrderUpdated audit entry (payments matter for the shop's
            // accounting timeline).
            var advanceDelta = order.AdvancePaid - existing.AdvancePaid;

            existing.OrderNumber    = order.OrderNumber;
            existing.Date           = order.Date;
            existing.DueDate        = order.DueDate;
            existing.Status         = order.Status;
            existing.Notes          = order.Notes;
            existing.IsRush         = order.IsRush;
            existing.Price          = order.Price;
            existing.DiscountAmount = order.DiscountAmount;
            existing.TaxAmount      = order.TaxAmount;
            existing.AdvancePaid    = order.AdvancePaid;
            existing.PaymentMethod  = order.PaymentMethod;

            // Emit a dedicated PaymentRecorded audit event when the advance
            // actually grew — helps the shop trace "when did we take this
            // deposit?" without hunting through generic order edits.
            if (advanceDelta > 0)
                await _audit.LogAsync(AuditAction.PaymentRecorded,
                    $"Received {advanceDelta:0.##} for order {existing.OrderNumber} " +
                    $"({order.PaymentMethod})",
                    "Order", existing.Id);

            if (order.Shirt is not null)
            {
                if (existing.Shirt is null)
                {
                    order.Shirt.OrderId = existing.Id;
                    _db.ShirtMeasurements.Add(order.Shirt);
                }
                else
                {
                    order.Shirt.Id      = existing.Shirt.Id;
                    order.Shirt.OrderId = existing.Shirt.OrderId;
                    _db.Entry(existing.Shirt).CurrentValues.SetValues(order.Shirt);
                }
            }

            if (order.Pant is not null)
            {
                if (existing.Pant is null)
                {
                    order.Pant.OrderId = existing.Id;
                    _db.PantMeasurements.Add(order.Pant);
                }
                else
                {
                    order.Pant.Id      = existing.Pant.Id;
                    order.Pant.OrderId = existing.Pant.OrderId;
                    _db.Entry(existing.Pant).CurrentValues.SetValues(order.Pant);
                }
            }

            // Line items: replace the entire set on every save. Simpler than
            // diff-and-patch, and safe here because there are few rows per
            // order (typically 1-5). The cascade-delete on the FK removes the
            // old rows in one round-trip.
            if (existing.LineItems is { Count: > 0 })
                _db.InvoiceLineItems.RemoveRange(existing.LineItems);
            foreach (var li in order.LineItems)
            {
                li.Id      = 0;             // new row on every save
                li.OrderId = existing.Id;
                _db.InvoiceLineItems.Add(li);
            }
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            isNew ? AuditAction.OrderCreated : AuditAction.OrderUpdated,
            isNew ? $"Created order {order.OrderNumber}"
                  : $"Updated order {order.OrderNumber}",
            "Order", order.Id);
    }

    public async Task DeleteOrderAsync(int orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order is not null)
        {
            order.IsDeleted = true;   // soft-delete
            await _db.SaveChangesAsync();
            await _audit.LogAsync(AuditAction.OrderDeleted,
                $"Deleted order {order.OrderNumber}",
                "Order", order.Id);
        }
    }

    public async Task RestoreOrderAsync(int orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order is not null)
        {
            order.IsDeleted = false;
            await _db.SaveChangesAsync();
            await _audit.LogAsync(AuditAction.OrderRestored,
                $"Restored order {order.OrderNumber}",
                "Order", order.Id);
        }
    }

    public async Task HardDeleteOrderAsync(int orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order is not null)
        {
            _db.Orders.Remove(order);
            await _db.SaveChangesAsync();
        }
    }

    public async Task UpdateOrderStatusAsync(int orderId, OrderStatus status)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order is not null)
        {
            var previous = order.Status;
            order.Status = status;

            // Stamp the transition so SLA warnings ("Stuck 4d") stay accurate.
            // Only bump the timestamp when status actually changed — a save
            // that leaves status alone must not reset the counter.
            if (previous != status)
                order.StageChangedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            if (previous != status)
                await _audit.LogAsync(AuditAction.OrderStatusChanged,
                    $"Order {order.OrderNumber}: {previous} → {status}",
                    "Order", order.Id);
        }
    }

    public async Task SetOrderRushAsync(int orderId, bool isRush)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order is null || order.IsRush == isRush) return;

        order.IsRush = isRush;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            AuditAction.OrderUpdated,
            $"Order {order.OrderNumber}: {(isRush ? "marked as RUSH" : "rush flag cleared")}",
            "Order", order.Id);
    }

    public async Task<List<Order>> GetOrdersByDueDateRangeAsync(DateTime fromInclusive, DateTime toInclusive)
    {
        var orgId = CurrentOrgId;
        var fromDate = fromInclusive.Date;
        // toInclusive is a day boundary, so treat it as the end-of-day.
        var toDate = toInclusive.Date;

        return await _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.OrganizationId == orgId
                     && !o.IsDeleted
                     && o.DueDate != null
                     && o.DueDate >= fromDate
                     && o.DueDate <= toDate)
            .OrderBy(o => o.DueDate)
            .ToListAsync();
    }

    public async Task<List<Order>> GetStuckOrdersAsync(int minStuckDays = 3)
    {
        var orgId  = CurrentOrgId;
        // SQLite stores TEXT dates in ISO-8601, so this compares lexicographically.
        var cutoff = DateTime.UtcNow.AddDays(-minStuckDays);

        return await _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.OrganizationId == orgId
                     && !o.IsDeleted
                     && o.Status != OrderStatus.Delivered
                     && o.StageChangedAt != null
                     && o.StageChangedAt <= cutoff)
            .OrderBy(o => o.StageChangedAt) // most stuck first
            .ToListAsync();
    }

    public async Task<List<Order>> GetDueTodayOrOverdueOrdersAsync(DateTime today, int limit = 8)
    {
        var orgId = CurrentOrgId;
        // Normalize to midnight so "due at 3 pm today" is still classified
        // as due today rather than falling into either bucket wrongly.
        var todayOnly = today.Date;

        return await _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.OrganizationId == orgId
                     && !o.IsDeleted
                     && o.Status != OrderStatus.Delivered
                     && o.DueDate.HasValue
                     && o.DueDate.Value <= todayOnly)
            .OrderBy(o => o.DueDate) // oldest / most overdue first
            .Take(limit)
            .ToListAsync();
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // ── Stage assignments (Phase 4) ─────────────────────────────────────────

    public async Task<List<OrderStageAssignment>> GetAssignmentsForOrderAsync(int orderId)
    {
        var orgId = CurrentOrgId;
        // Guard against cross-org lookups by joining through Order.OrganizationId.
        return await _db.OrderStageAssignments
            .Include(a => a.AssignedUser)
            .Include(a => a.Order)
            .Where(a => a.OrderId == orderId &&
                        a.Order != null &&
                        a.Order.OrganizationId == orgId)
            .OrderBy(a => a.Stage)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AssignOrderStageAsync(int orderId, OrderStatus stage, int? userId)
    {
        var orgId = CurrentOrgId;

        // Bail if the order doesn't belong to this org — prevents an admin of
        // shop A from silently poking rows in shop B.
        var order = await _db.Orders.FirstOrDefaultAsync(o =>
            o.Id == orderId && o.OrganizationId == orgId);
        if (order is null) return;

        var existing = await _db.OrderStageAssignments
            .FirstOrDefaultAsync(a => a.OrderId == orderId && a.Stage == stage);

        // Unassign: passing null userId (or 0) removes the row.
        if (userId is null or 0)
        {
            if (existing is not null)
            {
                _db.OrderStageAssignments.Remove(existing);
                await _db.SaveChangesAsync();
                await _audit.LogAsync(AuditAction.StageUnassigned,
                    $"Removed {stage} assignment on order {order.OrderNumber}",
                    "Order", order.Id);
            }
            return;
        }

        // Sanity-check: assigned user must belong to the same org so we don't
        // hand work to a stranger from a different shop.
        var assignedUser = await _db.Users
            .Where(u => u.Id == userId && u.OrganizationId == orgId && u.IsActive)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync();
        if (assignedUser is null)
            throw new InvalidOperationException("Selected user does not belong to this shop or is inactive.");

        if (existing is null)
        {
            _db.OrderStageAssignments.Add(new OrderStageAssignment
            {
                OrderId        = orderId,
                Stage          = stage,
                AssignedUserId = userId.Value,
                AssignedAt     = DateTime.UtcNow
            });
        }
        else
        {
            existing.AssignedUserId    = userId.Value;
            existing.AssignedAt        = DateTime.UtcNow;
            existing.CompletedAt       = null;   // reset completion on reassign
            existing.CompletedByUserId = null;
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditAction.StageAssigned,
            $"Assigned {stage} on order {order.OrderNumber} to {assignedUser.DisplayName}",
            "Order", order.Id);
    }

    public async Task CompleteStageAsync(int assignmentId, int userId)
    {
        var orgId = CurrentOrgId;
        var assignment = await _db.OrderStageAssignments
            .Include(a => a.Order)
            .FirstOrDefaultAsync(a => a.Id == assignmentId &&
                                      a.Order != null &&
                                      a.Order.OrganizationId == orgId);
        if (assignment is null) return;

        // Only the assigned user (or an admin acting through the same flow)
        // can mark a stage complete. We enforce this at the VM layer, but if
        // the caller is wrong we simply no-op instead of throwing to keep the
        // UI happy in edge cases (stale ID after refresh, etc.).
        if (assignment.AssignedUserId != userId && !_auth.IsAdmin) return;

        assignment.CompletedAt       = DateTime.UtcNow;
        assignment.CompletedByUserId = userId;

        // Push the parent order forward to the completed stage so the pipeline
        // stays in sync with what the employee just finished. We never move
        // *backwards*: if the order is already at a later stage (e.g. admin
        // fast-forwarded manually), we leave it alone.
        if (assignment.Order is { } ord && ord.Status < assignment.Stage)
            ord.Status = assignment.Stage;

        await _db.SaveChangesAsync();

        await _audit.LogAsync(AuditAction.StageCompleted,
            $"Completed {assignment.Stage} on order {assignment.Order?.OrderNumber}",
            "Order", assignment.OrderId);
    }

    public async Task<List<OrderStageAssignment>> GetMyWorkAsync(int userId)
    {
        var orgId = CurrentOrgId;
        return await _db.OrderStageAssignments
            .Include(a => a.Order).ThenInclude(o => o!.Customer)
            .Where(a =>
                a.AssignedUserId == userId &&
                a.CompletedAt == null &&
                a.Order != null &&
                a.Order.OrganizationId == orgId &&
                !a.Order.IsDeleted)
            // Order by due-date so the most-urgent work rises to the top;
            // orders without a due date sink to the bottom via nullable ordering.
            .OrderBy(a => a.Order!.DueDate == null)
            .ThenBy(a => a.Order!.DueDate)
            .ThenBy(a => a.Stage)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<string> GenerateOrderNumberAsync()
    {
        var orgId  = CurrentOrgId;
        var now    = DateTime.Today;
        var prefix = $"ORD-{now:MMMyy}-".ToUpper();
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var endOfMonth   = startOfMonth.AddMonths(1);

        var countThisMonth = await _db.Orders
            .Where(o => o.OrganizationId == orgId && o.Date >= startOfMonth && o.Date < endOfMonth)
            .CountAsync();

        int seq = countThisMonth + 1;
        string candidate;
        do
        {
            candidate = $"{prefix}{seq:D3}";
            var exists = await _db.Orders
                .AnyAsync(o => o.OrganizationId == orgId && o.OrderNumber == candidate);
            if (!exists) break;
            seq++;
        } while (true);

        return candidate;
    }
}
