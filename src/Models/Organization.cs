namespace TapeTracker.Models;

/// <summary>
/// A tailor shop / business unit. Every customer, order, and user is scoped to
/// exactly one Organization. Introduced in v2.0 to enable multi-user, role-based
/// operation on a shared workstation. On upgrade, all pre-existing data is
/// migrated into a single default organization named "My Shop".
/// </summary>
public class Organization
{
    public int      Id        { get; set; }
    public string   Name      { get; set; } = string.Empty;
    public string?  Phone     { get; set; }
    public string?  Address   { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Shop branding & billing (Phase 8) ────────────────────────────────────
    // Every field is nullable so pre-Phase-8 rows don't need backfill; the UI
    // treats null as "not configured" and falls back to sensible defaults
    // (e.g. currency = "₹", tax = 0, invoice prefix = "INV").

    /// <summary>Government tax identifier printed on invoices (India: GSTIN,
    /// elsewhere VAT no. etc.). Free-form so any regime works.</summary>
    public string? GstNumber        { get; set; }

    /// <summary>Currency symbol used on the bill and in-app money labels.
    /// Defaults to "₹" when null. Kept as a symbol not an ISO code so tailors
    /// don't need to learn currency codes.</summary>
    public string? CurrencySymbol   { get; set; }

    /// <summary>Prefix for auto-generated invoice numbers, e.g. "INV" →
    /// "INV-2026-0042". Distinct from <c>OrderNumber</c> so the shop can
    /// keep its existing order-numbering scheme while still emitting proper
    /// sequential invoices.</summary>
    public string? InvoicePrefix    { get; set; }

    /// <summary>Absolute path to the shop logo (PNG/JPG) inside the app's
    /// data folder. Rendered top-left on the invoice. Null → text-only header.</summary>
    public string? LogoPath         { get; set; }

    /// <summary>Default tax rate applied to every order's subtotal. Stored as
    /// a percentage (18 = 18%). Individual orders can override by leaving the
    /// order's own tax amount blank — the invoice picks up this default.</summary>
    public decimal TaxRatePct       { get; set; }

    /// <summary>Optional owner / proprietor name printed under the shop name
    /// on the invoice. Distinct from <see cref="Name"/> so a "Sharma Tailors"
    /// shop can also credit "Rajesh Sharma, Master Tailor" on the bill.</summary>
    public string? OwnerName        { get; set; }

    /// <summary>Optional email address printed on invoices.</summary>
    public string? Email            { get; set; }

    /// <summary>
    /// The shop's item catalog, persisted as a JSON array of strings
    /// (e.g. <c>["Shirt","Pant","Kurta","Salwar Kameez"]</c>). Populates the
    /// quick-pick chip on the line-item editor so tailors don't need to retype
    /// the same 5-10 garment names on every bill. Empty ⇒ the app falls back
    /// to a sensible default seed the first time the picker opens.
    /// </summary>
    public string? ItemCatalogJson { get; set; }

    /// <summary>
    /// Parsed view of <see cref="ItemCatalogJson"/>. Read-only wrapper —
    /// mutations should go through <see cref="SetItemCatalog"/> so the
    /// JSON string and the in-memory list stay in sync.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<string> ItemCatalog
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ItemCatalogJson)) return Array.Empty<string>();
            try
            {
                var list = System.Text.Json.JsonSerializer
                    .Deserialize<List<string>>(ItemCatalogJson);
                return list ?? new List<string>();
            }
            catch { return Array.Empty<string>(); }
        }
    }

    /// <summary>Serialize a fresh list into <see cref="ItemCatalogJson"/>.
    /// Empty values are dropped and whitespace is trimmed so a user typing
    /// "  " into the editor doesn't create a ghost entry.</summary>
    public void SetItemCatalog(IEnumerable<string> items)
    {
        var clean = items
            .Select(s => (s ?? string.Empty).Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        ItemCatalogJson = clean.Count == 0
            ? null
            : System.Text.Json.JsonSerializer.Serialize(clean);
    }

    public ICollection<User>     Users     { get; set; } = new List<User>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Order>    Orders    { get; set; } = new List<Order>();
}
