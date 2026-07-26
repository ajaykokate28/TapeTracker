using System.ComponentModel.DataAnnotations.Schema;

namespace TapeTracker.Models;

/// <summary>
/// One priced line on an order's invoice. Multiple line items per order let
/// a tailor bill "3 shirts for Rahul + 1 kurta for Priya" as separate rows
/// on the same bill instead of collapsing the whole order into a single
/// stitching charge.
///
/// The order's <see cref="Order.Price"/> is the sum of every line item's
/// <see cref="LineTotal"/> and is recomputed at save-time; the field on
/// Order stays for pre-Phase-9 orders that never had line items.
/// </summary>
public class InvoiceLineItem
{
    public int Id       { get; set; }
    public int OrderId  { get; set; }
    public Order? Order { get; set; }

    /// <summary>Who the item is for. Blank means the primary customer.
    /// For family-scoped shops this typically holds a family member's name
    /// ("Rahul", "Priya"); the UI offers a dropdown of family members
    /// (customers sharing the same Tag) plus free-text entry.</summary>
    public string ForWhom     { get; set; } = string.Empty;

    /// <summary>Garment / service description — e.g. "Shirt", "Pant",
    /// "Kurta", "Salwar Kameez", "Alteration". Free-form because tailors
    /// name items differently across regions.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Number of units at this price. Integer because half-shirts
    /// aren't a thing; alterations bill by item count too.</summary>
    public int    Quantity    { get; set; } = 1;

    /// <summary>Per-unit price in the shop's currency.</summary>
    public decimal UnitPrice  { get; set; }

    /// <summary>Sort order within the invoice (0-based). Preserved so the
    /// user's row order on the form matches the printed bill exactly.</summary>
    public int SortIndex { get; set; }

    [NotMapped]
    public decimal LineTotal => Quantity * UnitPrice;
}
