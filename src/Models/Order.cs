namespace TapeTracker.Models;

public class Order
{
    public int    Id           { get; set; }
    public int    CustomerId   { get; set; }
    public Customer? Customer  { get; set; }

    /// <summary>Owning organization (denormalized from Customer.OrganizationId for
    /// query efficiency and to preserve the linkage if the customer row is ever moved).</summary>
    public int    OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    /// <summary>Which user created the order. Nullable for pre-v2.0 rows migrated
    /// before the users table existed.</summary>
    public int?  CreatedByUserId { get; set; }
    public User? CreatedBy       { get; set; }

    public string      OrderNumber { get; set; } = string.Empty;
    public DateTime    Date        { get; set; } = DateTime.Today;
    public DateTime?   DueDate     { get; set; }
    public OrderStatus Status      { get; set; } = OrderStatus.Received;
    public string      Notes       { get; set; } = string.Empty;

    /// <summary>Soft-delete flag — excluded from normal queries, restorable within the undo window.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Marks the order as high-priority. Rush orders bubble to the top of
    /// customer / calendar / dashboard queues and render with a red flag
    /// badge. Toggle from the measurement form or the order card.
    /// </summary>
    public bool IsRush { get; set; }

    /// <summary>
    /// UTC timestamp of the last <see cref="Status"/> transition. Used to
    /// compute "stuck N days" SLA warnings on the order card + dashboard.
    /// Nullable so legacy rows (pre-SLA tracking) don't misreport as
    /// overdue — the UI treats null as "unknown, not overdue".
    /// </summary>
    public DateTime? StageChangedAt { get; set; }

    // ── Pricing & payments (Phase 8) ────────────────────────────────────────
    // All amounts are stored in the organization's currency, before conversion.
    // Zero is treated as "not entered" everywhere so pre-Phase-8 orders don't
    // show up as ₹0 owed.

    /// <summary>Subtotal / stitching charge before tax and discount.</summary>
    public decimal Price          { get; set; }

    /// <summary>Flat discount amount subtracted from <see cref="Price"/>.
    /// Percentage-based discounts convert to a flat amount at save time so
    /// the invoice math is always concrete.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Tax amount added on top of (Price − Discount). Frozen at
    /// order-save time from the organization's TaxRatePct so future changes
    /// to the shop's rate don't retroactively re-price historical orders.</summary>
    public decimal TaxAmount      { get; set; }

    /// <summary>Amount the customer has already paid (advance / deposit).
    /// Cumulative — the form always shows current running total.</summary>
    public decimal AdvancePaid    { get; set; }

    /// <summary>How the customer paid. <see cref="PaymentMethod.None"/> when
    /// no payment has been recorded yet.</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.None;

    // ── Computed helpers (not persisted) ─────────────────────────────────────

    /// <summary>Subtotal after discount, before tax.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal NetPrice => Math.Max(0m, Price - DiscountAmount);

    /// <summary>Final billed total (Price − Discount + Tax).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal GrandTotal => NetPrice + TaxAmount;

    /// <summary>Amount still owed. Zero means paid in full.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal BalanceDue => Math.Max(0m, GrandTotal - AdvancePaid);

    /// <summary>True when the order has any pricing recorded.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool HasPricing => Price > 0m;

    /// <summary>True when the customer has paid everything owed.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsPaidInFull => HasPricing && BalanceDue == 0m;

    public ShirtMeasurement? Shirt { get; set; }
    public PantMeasurement?  Pant  { get; set; }

    /// <summary>
    /// Priced line items on the invoice. Empty for pre-Phase-9 orders — the
    /// PDF service falls back to a single "Stitching charge" row using
    /// <see cref="Price"/> in that case, so historical invoices still render.
    /// </summary>
    public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();

    /// <summary>Per-stage employee assignments (0..N; unique on Stage).</summary>
    public ICollection<OrderStageAssignment> StageAssignments { get; set; }
        = new List<OrderStageAssignment>();
}
