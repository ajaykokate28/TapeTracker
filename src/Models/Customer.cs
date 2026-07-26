using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace TapeTracker.Models;

public class Customer
{
    public int    Id    { get; set; }

    /// <summary>Owning organization. Every customer belongs to exactly one org so
    /// multi-tenant queries never leak data across shops sharing a workstation.</summary>
    public int    OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public string Name  { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Optional grouping label. Multiple customers can share the same tag so a single
    /// paying customer's family members (each with their own Name and measurements)
    /// can be grouped and filtered together (e.g. "Sharma Family").
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>Soft-delete flag — excluded from normal queries, restorable within the undo window.</summary>
    public bool IsDeleted { get; set; }

    // All orders for this customer (1:N)
    public ICollection<Order> Orders { get; set; } = new List<Order>();

    // ── Display helpers for the customer list card (bound to latest order) ────
    // Not mapped to the database — computed from the eagerly-loaded Orders collection.
    [NotMapped]
    public Order? LatestOrder => Orders?.OrderByDescending(o => o.Date).FirstOrDefault();

    [NotMapped]
    public string OrderNumber => LatestOrder?.OrderNumber ?? string.Empty;

    [NotMapped]
    public DateTime Date => LatestOrder?.Date ?? DateTime.MinValue;

    [NotMapped]
    public DateTime? DeliveryDate => LatestOrder?.DueDate;
}

