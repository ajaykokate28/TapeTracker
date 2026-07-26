using System.Text.RegularExpressions;
using TapeTracker.Models;

namespace TapeTracker.Services;

/// <summary>
/// Parses raw OCR text from a measurement slip into a Customer + measurements.
///
/// Handles common tailor slip formats:
///   Name: Ravi Kumar        Phone: 9876543210
///   Order: 001              Date: 10/07/2026
///   Shirt: Length 28  Chest 42  Waist 36  Hip 40
///          Shoulder 16  Sleeve 24  Cuff 9  Collar 15
///   Pant:  Length 42  Waist 34  Hip 40  Thigh 24
///          Ankle 14   Knee 18   Seat 38
/// </summary>
public static class MeasurementTextParser
{
    public static ParsedSlip Parse(string ocrText)
    {
        var result = new ParsedSlip
        {
            Order = new Order
            {
                Date  = DateTime.Today,
                Shirt = new ShirtMeasurement(),
                Pant  = new PantMeasurement()
            },
            RawText = ocrText
        };

        var text = ocrText ?? string.Empty;

        // ── Customer fields ──────────────────────────────────────────
        result.CustomerName  = ExtractString(text, @"name\s*[:\-]?\s*(.+?)(?:\n|phone|order|date|$)");
        result.CustomerPhone = ExtractString(text, @"phone\s*[:\-]?\s*([+\d\s\-()]{7,20})");
        result.Order.OrderNumber = ExtractString(text, @"order(?:\s*no\.?|#)?\s*[:\-]?\s*(\S+)");

        var dateStr = ExtractString(text, @"date\s*[:\-]?\s*([\d]{1,2}[\/\-\.][\d]{1,2}[\/\-\.][\d]{2,4})");
        if (!string.IsNullOrEmpty(dateStr) &&
            DateTime.TryParseExact(dateStr,
                new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "MM/dd/yyyy", "d/M/yy" },
                null, System.Globalization.DateTimeStyles.None, out var parsedDate))
        {
            result.Order.Date = parsedDate;
        }

        // ── Shirt fields ─────────────────────────────────────────────
        var shirt = result.Order.Shirt!;
        shirt.Length   = ExtractDecimal(text, @"(?:shirt[^:]*:.*?)?length\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");
        shirt.Chest    = ExtractDecimal(text, @"chest\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");
        shirt.Waist    = ExtractDecimal(text, @"(?:shirt[^P\n]*)?waist\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");
        shirt.Hip      = ExtractDecimal(text, @"(?:shirt[^P\n]*)?hip\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");
        shirt.Shoulder = ExtractDecimal(text, @"shoulder\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");
        shirt.Sleeve   = ExtractDecimal(text, @"sleeve\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");
        shirt.Cuff     = ExtractDecimal(text, @"cuff\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");
        shirt.Collar   = ExtractDecimal(text, @"collar\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");

        // ── Pant fields ───────────────────────────────────────────────
        var pantText = ExtractSection(text, "pant");
        var pant = result.Order.Pant!;
        pant.Length = ExtractDecimal(pantText, @"length\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");
        pant.Waist  = ExtractDecimal(pantText, @"waist\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");
        pant.Hip    = ExtractDecimal(pantText, @"hip\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");
        pant.Thigh  = ExtractDecimal(pantText, @"thigh\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");
        pant.Ankle  = ExtractDecimal(pantText, @"ankle\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");
        pant.Knee   = ExtractDecimal(pantText, @"knee\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");
        pant.Seat   = ExtractDecimal(pantText, @"seat\s*[:\-]?\s*([\d]+(?:[.,][\d]+)?)");

        return result;
    }

    // Returns the substring of text starting from sectionKeyword to end (case-insensitive)
    private static string ExtractSection(string text, string sectionKeyword)
    {
        var idx = text.IndexOf(sectionKeyword, StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? text[idx..] : string.Empty;
    }

    private static string ExtractString(string text, string pattern)
    {
        var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
    }

    private static decimal ExtractDecimal(string text, string pattern)
    {
        var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success) return 0m;
        var raw = m.Groups[1].Value.Replace(',', '.');
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }
}

public class ParsedSlip
{
    public string CustomerName  { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public Order  Order   { get; set; } = new();
    public string RawText { get; set; } = string.Empty;

    public int FieldsFound
    {
        get
        {
            int count = 0;
            if (Order.Shirt is { } s)
            {
                if (s.Length   > 0) count++; if (s.Chest    > 0) count++;
                if (s.Waist    > 0) count++; if (s.Hip      > 0) count++;
                if (s.Shoulder > 0) count++; if (s.Sleeve   > 0) count++;
                if (s.Cuff     > 0) count++; if (s.Collar   > 0) count++;
            }
            if (Order.Pant is { } p)
            {
                if (p.Length > 0) count++; if (p.Waist  > 0) count++;
                if (p.Hip    > 0) count++; if (p.Thigh  > 0) count++;
                if (p.Ankle  > 0) count++; if (p.Knee   > 0) count++;
                if (p.Seat   > 0) count++;
            }
            return count;
        }
    }
}
