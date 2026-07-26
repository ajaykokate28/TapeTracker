namespace TapeTracker.Models;

/// <summary>
/// Assigns exactly one <see cref="User"/> to exactly one <see cref="OrderStatus"/>
/// stage of a specific <see cref="Order"/>. An order can have up to one assignment
/// per stage (unique index on (OrderId, Stage)). When the assigned employee marks
/// their stage complete, the parent Order.Status is auto-advanced.
/// </summary>
public class OrderStageAssignment
{
    public int Id { get; set; }

    public int    OrderId { get; set; }
    public Order? Order   { get; set; }

    /// <summary>Which pipeline stage this assignment covers.</summary>
    public OrderStatus Stage { get; set; }

    /// <summary>Employee currently responsible for the stage.</summary>
    public int   AssignedUserId { get; set; }
    public User? AssignedUser   { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Non-null once the stage is completed. Also drives the Order.Status advance.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Who actually clicked "mark complete" (usually the assigned user, but an admin can too).</summary>
    public int?  CompletedByUserId { get; set; }
    public User? CompletedBy       { get; set; }
}
