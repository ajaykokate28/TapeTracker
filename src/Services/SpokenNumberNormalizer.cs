using System.Text;
using System.Text.RegularExpressions;

namespace TapeTracker.Services;

/// <summary>
/// Rewrites English spoken numbers ("thirty two point five") into their
/// digit form ("32.5") so <see cref="MeasurementTextParser"/> — which
/// expects "chest 32.5" style input — can handle voice transcripts without
/// its own vocabulary table. Focused on the range 0–199 with an optional
/// "point [digit-word]+" tail; anything outside that band leaves the token
/// untouched because measurements past 199 in / cm are vanishingly rare.
///
/// Kept intentionally simple:
///   ● No support for "and" ("one hundred AND twenty") because Windows
///     dictation already omits it in results.
///   ● No support for "half" / "quarter" — tailors would say "point five"
///     or "point two five" when reading from a slip.
///   ● No support for Hindi / Marathi number words yet — Windows Speech
///     Recognition on those locales usually returns digits directly, so
///     this normalizer only runs after the English path has clearly won.
/// </summary>
public static class SpokenNumberNormalizer
{
    private static readonly Dictionary<string, int> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0, ["oh"] = 0,
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
        ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9,
        ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13,
        ["fourteen"] = 14, ["fifteen"] = 15, ["sixteen"] = 16,
        ["seventeen"] = 17, ["eighteen"] = 18, ["nineteen"] = 19
    };

    private static readonly Dictionary<string, int> Tens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["twenty"] = 20, ["thirty"] = 30, ["forty"] = 40, ["fifty"] = 50,
        ["sixty"]  = 60, ["seventy"] = 70, ["eighty"] = 80, ["ninety"] = 90
    };

    /// <summary>
    /// Walks the input token-by-token and coalesces number words into their
    /// digit form. Non-numeric tokens ("chest", "waist", "hip") pass through
    /// unchanged so the surrounding measurement parser can still keyword-match.
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Split preserving whitespace so we can rebuild the sentence roughly
        // as it came in. Uses a regex to keep punctuation attached to the
        // following word — makes "chest, thirty two" behave like "chest thirty two".
        var tokens = Regex.Split(input, @"(\s+)");
        var sb = new StringBuilder(input.Length);

        int i = 0;
        while (i < tokens.Length)
        {
            var tok = tokens[i];
            if (string.IsNullOrWhiteSpace(tok))
            {
                sb.Append(tok);
                i++;
                continue;
            }

            // "point" starts the fractional tail if there's a number just before.
            // Handled inside CollectNumber below so "point five" alone (rare)
            // also renders as "0.5".
            if (TryCollectNumber(tokens, ref i, out var digitForm))
            {
                sb.Append(digitForm);
                continue;
            }

            sb.Append(tok);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Reads one number phrase from the token stream. Returns false when the
    /// token at the current index isn't the start of a recognised number so
    /// the outer loop can pass it through as plain text.
    /// </summary>
    private static bool TryCollectNumber(string[] tokens, ref int i, out string digitForm)
    {
        digitForm = string.Empty;

        // Skip leading punctuation on the first token — "thirty," should still parse.
        var head = StripPunctuation(tokens[i]);
        if (string.IsNullOrEmpty(head)) return false;

        int? whole = null;

        // Numeric-first path: dictation frequently already returns "32", in which
        // case we just want to pick up an optional " point five" tail without
        // rewriting the leading digits.
        if (int.TryParse(head, out var direct))
        {
            whole = direct;
            i++;
        }
        else if (Units.TryGetValue(head, out var u))
        {
            whole = u;
            i++;
        }
        else if (Tens.TryGetValue(head, out var t))
        {
            whole = t;
            i++;
            // Look one more (non-whitespace) token ahead for a units word.
            var next = PeekNonWhitespace(tokens, i);
            if (next.tokenIndex >= 0)
            {
                var nextWord = StripPunctuation(next.value);
                if (Units.TryGetValue(nextWord, out var uu) && uu >= 1 && uu <= 9)
                {
                    whole = t + uu;
                    i = next.tokenIndex + 1;
                }
            }
        }
        else if (string.Equals(head, "hundred", StringComparison.OrdinalIgnoreCase))
        {
            // "hundred" on its own → 100.
            whole = 100;
            i++;
        }
        else if (string.Equals(head, "point", StringComparison.OrdinalIgnoreCase))
        {
            // "point five" alone → 0.5
            whole = 0;
            // Do NOT advance i — leave "point" for the fractional block below.
        }
        else
        {
            return false;
        }

        // ── Optional "hundred [tens] [units]" pattern for 100–199 ────────────
        var afterHundred = PeekNonWhitespace(tokens, i);
        if (afterHundred.tokenIndex >= 0 && whole is >= 1 and <= 9)
        {
            var word = StripPunctuation(afterHundred.value);
            if (string.Equals(word, "hundred", StringComparison.OrdinalIgnoreCase))
            {
                var base100 = whole.Value * 100;
                i = afterHundred.tokenIndex + 1;

                var next = PeekNonWhitespace(tokens, i);
                if (next.tokenIndex >= 0)
                {
                    var nextWord = StripPunctuation(next.value);
                    if (int.TryParse(nextWord, out var d)) { base100 += d; i = next.tokenIndex + 1; }
                    else if (Units.TryGetValue(nextWord, out var uu)) { base100 += uu; i = next.tokenIndex + 1; }
                    else if (Tens.TryGetValue(nextWord, out var tt))
                    {
                        base100 += tt;
                        i = next.tokenIndex + 1;
                        var next2 = PeekNonWhitespace(tokens, i);
                        if (next2.tokenIndex >= 0)
                        {
                            var word2 = StripPunctuation(next2.value);
                            if (Units.TryGetValue(word2, out var uuu) && uuu >= 1 && uuu <= 9)
                            {
                                base100 += uuu;
                                i = next2.tokenIndex + 1;
                            }
                        }
                    }
                }
                whole = base100;
            }
        }

        // ── Optional "point <digit>+" fractional tail ─────────────────────────
        var fractional = new StringBuilder();
        var pointPeek = PeekNonWhitespace(tokens, i);
        if (pointPeek.tokenIndex >= 0 &&
            string.Equals(StripPunctuation(pointPeek.value), "point", StringComparison.OrdinalIgnoreCase))
        {
            i = pointPeek.tokenIndex + 1;
            // Collect one or more digit words / numerics.
            while (true)
            {
                var peek = PeekNonWhitespace(tokens, i);
                if (peek.tokenIndex < 0) break;

                var word = StripPunctuation(peek.value);
                if (Units.TryGetValue(word, out var u) && u <= 9)
                {
                    fractional.Append(u);
                    i = peek.tokenIndex + 1;
                }
                else if (word.Length == 1 && char.IsDigit(word[0]))
                {
                    fractional.Append(word);
                    i = peek.tokenIndex + 1;
                }
                else break;
            }
        }

        digitForm = fractional.Length > 0
            ? $"{whole ?? 0}.{fractional}"
            : (whole ?? 0).ToString();
        return true;
    }

    private static (int tokenIndex, string value) PeekNonWhitespace(string[] tokens, int startIdx)
    {
        for (int k = startIdx; k < tokens.Length; k++)
        {
            if (!string.IsNullOrWhiteSpace(tokens[k]))
                return (k, tokens[k]);
        }
        return (-1, string.Empty);
    }

    private static string StripPunctuation(string token)
    {
        // Trim trailing/leading ,.;:!?—light touch so "thirty-two" (hyphenated)
        // still needs its own handling, but comma-separated lists work.
        return token.Trim(',', '.', ';', ':', '!', '?', '"', '\'', '(', ')');
    }
}
