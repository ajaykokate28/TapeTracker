using TapeTracker.Models;

namespace TapeTracker.Services;

public interface IMeasurementService
{
    // Customer CRUD
    Task<List<Customer>> GetAllCustomersAsync(string? searchTerm = null, int? limit = null, OrderStatus? statusFilter = null, string? tagFilter = null);
    Task<Customer?> GetCustomerByIdAsync(int id);
    Task<int> SaveCustomerAsync(Customer customer);   // returns customer Id
    Task DeleteCustomerAsync(int id);
    Task<List<string>> GetDistinctTagsAsync();

    /// <summary>
    /// Returns the display names of every customer whose Tag matches the
    /// given customer's Tag — used by the invoice line-item editor as a
    /// quick-pick list of family members. Includes the customer itself.
    /// Returns just <c>[customer.Name]</c> when the customer has no tag.
    /// </summary>
    Task<List<string>> GetFamilyMemberNamesAsync(int customerId);

    // Order CRUD
    Task<List<Order>> GetOrdersByCustomerAsync(int customerId);
    Task<Order?> GetOrderByIdAsync(int orderId);
    Task SaveOrderAsync(Order order);
    Task DeleteOrderAsync(int orderId);
    Task RestoreOrderAsync(int orderId);
    Task HardDeleteOrderAsync(int orderId);
    Task UpdateOrderStatusAsync(int orderId, OrderStatus status);

    /// <summary>Toggle the <see cref="Order.IsRush"/> flag. Admin-only —
    /// callers should have already checked <c>Auth.CanEditOrders</c>.</summary>
    Task SetOrderRushAsync(int orderId, bool isRush);

    /// <summary>
    /// Orders in the current org whose <see cref="Order.DueDate"/> falls
    /// within the inclusive range. Used by the delivery calendar. Excludes
    /// soft-deleted rows; includes the customer so the calendar can render
    /// the customer name inline.
    /// </summary>
    Task<List<Order>> GetOrdersByDueDateRangeAsync(DateTime fromInclusive, DateTime toInclusive);

    /// <summary>
    /// Active orders (not Delivered, not soft-deleted) whose current stage
    /// has been open for at least <paramref name="minStuckDays"/> days —
    /// powers the dashboard's overdue widget. Ordered by most-stuck first.
    /// Orders with no <see cref="Order.StageChangedAt"/> (legacy) are
    /// excluded because we can't compute their stuck-days reliably.
    /// </summary>
    Task<List<Order>> GetStuckOrdersAsync(int minStuckDays = 3);

    /// <summary>
    /// Active orders (not Delivered, not soft-deleted) whose <c>DueDate</c>
    /// is on <paramref name="today"/> or earlier. Ordered by due date
    /// ascending so the most-overdue rows surface first. Powers the Home
    /// screen's "urgent orders" widget without paying for the full
    /// dashboard aggregation.
    /// </summary>
    Task<List<Order>> GetDueTodayOrOverdueOrdersAsync(DateTime today, int limit = 8);

    // Customer restore / hard-delete (for undo)
    Task RestoreCustomerAsync(int id);
    Task HardDeleteCustomerAsync(int id);

    // Helpers
    Task<string> GenerateOrderNumberAsync();

    // ── Stage assignments (Phase 4) ─────────────────────────────────────────

    /// <summary>All assignments for the given order, ordered by stage. Each
    /// assignment is materialized with its <c>AssignedUser</c> so the UI can
    /// render the person's display name without a second round-trip.</summary>
    Task<List<OrderStageAssignment>> GetAssignmentsForOrderAsync(int orderId);

    /// <summary>
    /// Creates or updates the assignment for <paramref name="orderId"/> at
    /// <paramref name="stage"/>. Pass <c>null</c> for <paramref name="userId"/>
    /// to remove the assignment (unassign). Admin-only — callers should have
    /// already checked <c>Auth.CanManageCustomers</c>.
    /// </summary>
    Task AssignOrderStageAsync(int orderId, OrderStatus stage, int? userId);

    /// <summary>
    /// Marks the given stage assignment as completed by <paramref name="userId"/>.
    /// Also advances the parent order's overall status to the completed stage
    /// so the pipeline / status pill stay in sync.
    /// </summary>
    Task CompleteStageAsync(int assignmentId, int userId);

    /// <summary>
    /// Every active (not-yet-completed) stage assignment for the given user
    /// inside the current organization. Powers the employee's "My Work" queue
    /// on the dashboard. Each item includes the parent order and its customer
    /// so the UI can render everything without extra queries.
    /// </summary>
    Task<List<OrderStageAssignment>> GetMyWorkAsync(int userId);
}
