using SkiaSharp;
using TapeTracker.Models;

namespace TapeTracker.Services;

/// <summary>
/// Generates a branded, localized invoice PDF from the current organization's
/// shop settings (name, address, GST, logo, currency). Falls back to sensible
/// defaults when a shop hasn't yet visited the Shop Settings page.
///
/// Layout is a single A4 page with:
///   • Header band: shop name, owner, address, GST, invoice #, date
///   • Bill-to block: customer name, phone
///   • Measurement tables (only sections with any non-zero value are drawn)
///   • Pricing summary: subtotal, discount, tax, grand total, advance, balance
///   • Footer: thank-you note + generated-by stamp
///
/// SkiaSharp rasterizes to PDF directly — no external dependencies.
/// </summary>
public class PdfService : IPdfService
{
    private readonly IAuthService _auth;

    public PdfService(IAuthService auth) => _auth = auth;

    public async Task GenerateAndShareAsync(Customer customer, Order order, IUnitPreferenceService unitService)
    {
        var pdfBytes = GeneratePdf(customer, order, unitService, _auth.CurrentOrganization);

        var safeName   = string.Concat(customer.Name.Split(Path.GetInvalidFileNameChars()));
        var fileName   = $"Invoice_{safeName}_{order.OrderNumber}.pdf";
        var dir        = Path.Combine(FileSystem.AppDataDirectory, "bills");
        Directory.CreateDirectory(dir);
        var filePath   = Path.Combine(dir, fileName);

        await File.WriteAllBytesAsync(filePath, pdfBytes);

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = $"{LocalizationService.Current.InvoiceLabel} — {customer.Name} / {order.OrderNumber}",
            File  = new ShareFile(filePath)
        });
    }

    // ── PDF generation ───────────────────────────────────────────────────────

    private static byte[] GeneratePdf(
        Customer customer,
        Order order,
        IUnitPreferenceService unitService,
        Organization? org)
    {
        var L        = LocalizationService.Current;
        var currency = string.IsNullOrWhiteSpace(org?.CurrencySymbol) ? "₹" : org!.CurrencySymbol!;
        var shopName = string.IsNullOrWhiteSpace(org?.Name) ? "TapeTracker" : org!.Name;

        const float pageW  = 595f;   // A4 width  pt
        const float pageH  = 842f;   // A4 height pt
        const float margin = 40f;

        using var stream    = new MemoryStream();
        using var document  = SKDocument.CreatePdf(stream);
        using var canvas    = document.BeginPage(pageW, pageH);

        // ── Fonts & paints ───────────────────────────────────────────────────
        using var fontShop     = new SKFont { Size = 22, Embolden = true };
        using var fontInvoice  = new SKFont { Size = 18, Embolden = true };
        using var fontH2       = new SKFont { Size = 12, Embolden = true };
        using var fontLabel    = new SKFont { Size = 10 };
        using var fontValue    = new SKFont { Size = 11 };
        using var fontValueBold= new SKFont { Size = 11, Embolden = true };
        using var fontTotal    = new SKFont { Size = 14, Embolden = true };
        using var fontFooter   = new SKFont { Size = 9  };
        using var fontPaid     = new SKFont { Size = 28, Embolden = true };

        var indigo   = new SKColor(0x4F, 0x46, 0xE5);
        var slate    = new SKColor(0x64, 0x74, 0x8B);
        var gray400  = new SKColor(0x9C, 0xA3, 0xAF);
        var green    = new SKColor(0x10, 0xB9, 0x81);
        var red      = new SKColor(0xDC, 0x26, 0x26);
        var lightBg  = new SKColor(0xF3, 0xF4, 0xF6);

        using var paintShop     = new SKPaint { Color = indigo,        IsAntialias = true };
        using var paintInvoice  = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var paintH2       = new SKPaint { Color = slate,         IsAntialias = true };
        using var paintLabel    = new SKPaint { Color = gray400,       IsAntialias = true };
        using var paintValue    = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var paintTotal    = new SKPaint { Color = indigo,        IsAntialias = true };
        using var paintFooter   = new SKPaint { Color = gray400,       IsAntialias = true };
        using var paintLine     = new SKPaint { Color = new SKColor(0xE5, 0xE7, 0xEB), StrokeWidth = 0.5f };
        using var paintHeaderBg = new SKPaint { Color = indigo,        IsAntialias = true };
        using var paintBoxBg    = new SKPaint { Color = lightBg,       IsAntialias = true };
        using var paintPaid     = new SKPaint { Color = green,         IsAntialias = true };
        using var paintUnpaid   = new SKPaint { Color = red,           IsAntialias = true };

        // ── Header band: indigo strip with shop name + INVOICE label ─────────
        canvas.DrawRect(0, 0, pageW, 90, paintHeaderBg);

        // Logo (if configured) — 60x60 top-left, rest of the header slides right.
        float shopX = margin;
        if (!string.IsNullOrEmpty(org?.LogoPath) && File.Exists(org!.LogoPath))
        {
            try
            {
                using var logo = SKBitmap.Decode(org.LogoPath);
                if (logo is not null)
                {
                    var destRect = new SKRect(margin, 15, margin + 60, 75);
                    canvas.DrawBitmap(logo, destRect);
                    shopX = margin + 72;
                }
            }
            catch { /* corrupt logo file — silently skip */ }
        }

        // Shop name (top-left) + owner beneath.
        canvas.DrawText(shopName, shopX, 40, SKTextAlign.Left, fontShop, paintInvoice);
        if (!string.IsNullOrWhiteSpace(org?.OwnerName))
            canvas.DrawText(org!.OwnerName, shopX, 60, SKTextAlign.Left, fontH2,
                new SKPaint { Color = new SKColor(0xE0, 0xE7, 0xFF), IsAntialias = true });

        // INVOICE label (top-right) + invoice number + date beneath.
        canvas.DrawText(L.InvoiceLabel, pageW - margin, 40, SKTextAlign.Right, fontInvoice, paintInvoice);
        var invoicePrefix = string.IsNullOrWhiteSpace(org?.InvoicePrefix) ? "INV" : org!.InvoicePrefix!;
        var invoiceNo     = $"{invoicePrefix}-{order.Id:D5}";
        canvas.DrawText($"{L.InvoiceNumberLabel} {invoiceNo}", pageW - margin, 60, SKTextAlign.Right, fontLabel,
            new SKPaint { Color = new SKColor(0xE0, 0xE7, 0xFF), IsAntialias = true });
        canvas.DrawText(order.Date.ToString("dd MMM yyyy"), pageW - margin, 76, SKTextAlign.Right, fontLabel,
            new SKPaint { Color = new SKColor(0xE0, 0xE7, 0xFF), IsAntialias = true });

        float y = 110;

        // ── Shop contact strip ───────────────────────────────────────────────
        var contactParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(org?.Address))   contactParts.Add(org!.Address!.Replace('\n', ' '));
        if (!string.IsNullOrWhiteSpace(org?.Phone))     contactParts.Add(org!.Phone!);
        if (!string.IsNullOrWhiteSpace(org?.Email))     contactParts.Add(org!.Email!);
        if (contactParts.Count > 0)
        {
            canvas.DrawText(string.Join("  •  ", contactParts), margin, y, SKTextAlign.Left, fontLabel,
                new SKPaint { Color = slate, IsAntialias = true });
            y += 14;
        }
        if (!string.IsNullOrWhiteSpace(org?.GstNumber))
        {
            canvas.DrawText($"{L.GstNumberLabel}: {org!.GstNumber}", margin, y, SKTextAlign.Left, fontLabel,
                new SKPaint { Color = slate, IsAntialias = true });
            y += 14;
        }
        y += 4;
        canvas.DrawLine(margin, y, pageW - margin, y, paintLine);
        y += 16;

        // ── Bill-to block ────────────────────────────────────────────────────
        canvas.DrawText(L.BillToLabel, margin, y, SKTextAlign.Left, fontH2, paintH2);
        y += 15;
        canvas.DrawText(customer.Name, margin, y, SKTextAlign.Left, fontValueBold, paintValue);
        if (!string.IsNullOrWhiteSpace(customer.Phone))
            canvas.DrawText(customer.Phone, pageW - margin, y, SKTextAlign.Right, fontValue, paintValue);
        y += 16;

        // Order metadata on the same row as bill-to for compactness.
        var meta = $"Order: {order.OrderNumber}";
        if (order.DueDate.HasValue)
            meta += $"   •   Due: {order.DueDate.Value:dd MMM yyyy}";
        canvas.DrawText(meta, margin, y, SKTextAlign.Left, fontLabel, paintLabel);
        y += 12;
        canvas.DrawLine(margin, y, pageW - margin, y, paintLine);
        y += 14;

        // ── Itemized line-items table (Phase 9) ─────────────────────────────
        // Measurements are no longer printed on the customer invoice — they
        // belong to the tailor's job card, not the bill. Instead we show a
        // proper 4-column table: For / Item / Qty × Rate / Amount.
        //
        // Column layout (page width = 595):
        //   For       : margin  → forX + forW    (100 pt wide)
        //   Item      : itemX   → qtyX           (~ 260 pt wide)
        //   Qty × Rate: qtyX    → amtX           (~ 100 pt wide, centred)
        //   Amount    : amtX    → pageW - margin (right-aligned)
        // Amount column right-aligns to (pageW − margin), so no explicit X.
        const float forX  = 40f;
        const float itemX = 140f;
        const float qtyX  = 340f;

        canvas.DrawText(L.LineItemsSection, margin, y, SKTextAlign.Left, fontH2, paintH2);
        y += 14;

        // Header row (grey background band).
        var headerTop = y - 12;
        canvas.DrawRect(margin, headerTop, pageW - 2 * margin, 20, paintBoxBg);
        y = headerTop + 14;
        canvas.DrawText(L.LineItemForLabel,    forX,  y, SKTextAlign.Left,   fontLabel, new SKPaint { Color = slate, IsAntialias = true });
        canvas.DrawText(L.LineItemDescLabel,   itemX, y, SKTextAlign.Left,   fontLabel, new SKPaint { Color = slate, IsAntialias = true });
        canvas.DrawText($"{L.LineItemQtyLabel} × {L.LineItemRateLabel}",
                                               qtyX,  y, SKTextAlign.Left,   fontLabel, new SKPaint { Color = slate, IsAntialias = true });
        canvas.DrawText(L.LineItemAmountLabel, pageW - margin, y, SKTextAlign.Right, fontLabel, new SKPaint { Color = slate, IsAntialias = true });
        y = headerTop + 22;
        canvas.DrawLine(margin, y, pageW - margin, y, paintLine);
        y += 12;

        // Body rows — sorted by SortIndex so hand-arranged order is preserved.
        var lines = order.LineItems.OrderBy(li => li.SortIndex).ToList();
        if (lines.Count == 0 && order.HasPricing)
        {
            // Pre-Phase-9 fallback: synthesize a single "Stitching charge" row
            // so historical orders still print a coherent invoice instead of
            // an empty table + a total.
            lines.Add(new InvoiceLineItem
            {
                Description = L.StitchingChargeLabel,
                Quantity    = 1,
                UnitPrice   = order.Price
            });
        }

        foreach (var li in lines)
        {
            var forText  = string.IsNullOrWhiteSpace(li.ForWhom) ? "-" : li.ForWhom;
            var qtyRate  = $"{li.Quantity} × {currency}{li.UnitPrice:0.##}";
            var amount   = $"{currency}{li.LineTotal:0.##}";

            canvas.DrawText(TrimTo(forText, 14),           forX,  y, SKTextAlign.Left,  fontValue, paintValue);
            canvas.DrawText(TrimTo(li.Description, 32),    itemX, y, SKTextAlign.Left,  fontValue, paintValue);
            canvas.DrawText(qtyRate,                       qtyX,  y, SKTextAlign.Left,  fontValue, paintValue);
            canvas.DrawText(amount,                        pageW - margin, y, SKTextAlign.Right, fontValueBold, paintValue);
            y += 16;
        }

        y += 4;
        canvas.DrawLine(margin, y, pageW - margin, y, paintLine);
        y += 14;

        // ── Pricing summary (only when the order has any pricing) ────────────
        if (order.HasPricing)
        {
            var boxTop = y;
            const float boxH = 130;
            canvas.DrawRoundRect(margin, boxTop, pageW - 2 * margin, boxH, 8, 8, paintBoxBg);
            y = boxTop + 16;

            void PayRow(string lbl, string val, SKFont? valFont = null, SKPaint? valPaint = null)
            {
                canvas.DrawText(lbl, margin + 14, y, SKTextAlign.Left, fontLabel,
                    new SKPaint { Color = slate, IsAntialias = true });
                canvas.DrawText(val, pageW - margin - 14, y, SKTextAlign.Right,
                    valFont ?? fontValue, valPaint ?? paintValue);
                y += 16;
            }

            PayRow(L.StitchingChargeLabel, $"{currency} {order.Price:0.##}");
            if (order.DiscountAmount > 0)
                PayRow($"{L.DiscountLabel} (−)", $"{currency} {order.DiscountAmount:0.##}");
            PayRow(L.SubtotalLabel, $"{currency} {order.NetPrice:0.##}");
            if (order.TaxAmount > 0)
                PayRow(L.TaxLabel, $"{currency} {order.TaxAmount:0.##}");
            // Divider inside the box
            canvas.DrawLine(margin + 14, y - 4, pageW - margin - 14, y - 4, paintLine);
            y += 4;
            PayRow(L.GrandTotalLabel, $"{currency} {order.GrandTotal:0.##}", fontTotal, paintTotal);

            y = boxTop + boxH + 12;

            // Advance + balance strip
            var stripTop = y;
            canvas.DrawText(L.AdvancePaidLabel, margin, y, SKTextAlign.Left, fontLabel, paintLabel);
            canvas.DrawText($"{currency} {order.AdvancePaid:0.##}",
                margin + 130, y, SKTextAlign.Left, fontValueBold, paintValue);

            var balPaint = order.IsPaidInFull ? paintPaid : paintUnpaid;
            canvas.DrawText(L.BalanceDueLabel, pageW - margin - 140, y, SKTextAlign.Left, fontLabel, paintLabel);
            canvas.DrawText($"{currency} {order.BalanceDue:0.##}",
                pageW - margin, y, SKTextAlign.Right, fontValueBold, balPaint);
            y += 20;

            if (order.PaymentMethod != PaymentMethod.None)
            {
                canvas.DrawText($"{L.PaymentMethodLabel}: {order.PaymentMethod.Localize()}",
                    margin, y, SKTextAlign.Left, fontLabel, paintLabel);
                y += 18;
            }

            // Big "PAID" stamp for fully-paid invoices — a nice touch of polish
            // that stops the customer from worrying whether they owe anything.
            if (order.IsPaidInFull)
            {
                canvas.Save();
                canvas.RotateDegrees(-14, pageW - 130, boxTop + 60);
                canvas.DrawText(L.PaidLabel, pageW - 130, boxTop + 60, SKTextAlign.Center, fontPaid,
                    new SKPaint { Color = new SKColor(0x10, 0xB9, 0x81, 0x80), IsAntialias = true });
                canvas.Restore();
            }
        }

        // ── Footer ───────────────────────────────────────────────────────────
        y = pageH - 46;
        canvas.DrawLine(margin, y, pageW - margin, y, paintLine);
        y += 14;
        canvas.DrawText(L.ThankYouLabel, margin, y, SKTextAlign.Left, fontLabel,
            new SKPaint { Color = slate, IsAntialias = true });
        canvas.DrawText($"{L.GeneratedByLabel}  •  {DateTime.Now:dd MMM yyyy HH:mm}",
            pageW - margin, y, SKTextAlign.Right, fontFooter, paintFooter);

        document.EndPage();
        document.Close();

        return stream.ToArray();
    }

    /// <summary>Truncates <paramref name="s"/> to <paramref name="max"/> chars
    /// with a single ellipsis so long "For" / "Item" cells don't overflow
    /// the neighbouring column. SkiaSharp text has no built-in clipping so
    /// this is the pragmatic guardrail.</summary>
    private static string TrimTo(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s.Substring(0, max - 1) + "…";
    }
}
