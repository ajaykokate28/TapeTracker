namespace TapeTracker.Services;

/// <summary>
/// Small utility class that provides typo-tolerant string matching for the
/// customer name search box. Uses classic Levenshtein edit distance rather
/// than fancy trigram / phonetic algorithms because:
///
///   ● Names in this app are short (≤ 40 chars typically) so the
///     O(n·m) worst-case is tiny in absolute terms.
///   ● SQLite has no built-in fuzzy operator — a pure C# implementation
///     avoids adding <c>Microsoft.Data.Sqlite</c> extensions or a
///     custom function registration path.
///   ● Two-character edit distance covers the overwhelming majority
///     of shopkeeper typos ("Rmesh" → "Ramesh", "Sharma" ↔ "Sarma")
///     without producing noisy false positives on unrelated names.
///
/// All comparisons run against a lower-cased, trimmed form. Callers are
/// responsible for pre-filtering with a substring match when possible —
/// fuzzy is meant as a fallback, not the primary path.
/// </summary>
public static class FuzzySearch
{
    /// <summary>
    /// Classic Levenshtein distance. Returns <see cref="int.MaxValue"/> when
    /// either input is null so callers can short-circuit without an extra
    /// null-check.
    /// </summary>
    public static int Distance(string? a, string? b)
    {
        if (a is null || b is null) return int.MaxValue;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        // Two-row rolling buffer keeps memory at O(min(a,b)) instead of
        // O(a·b). For 40-char names this is barely worth mentioning, but
        // it also makes GC pressure irrelevant when the caller loops over
        // hundreds of customers.
        var previous = new int[b.Length + 1];
        var current  = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++) previous[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    /// <summary>
    /// Case-insensitive, word-tolerant fuzzy match. The candidate matches
    /// when the query is within <paramref name="maxDistance"/> edits of
    /// either the full candidate OR any single space-delimited token
    /// inside it. Token-level matching lets "Rmesh" find "Rmesh Sharma"
    /// AND "Sharma Rmesh" without paying for full-string comparison.
    /// </summary>
    public static bool IsCloseMatch(string candidate, string query, int maxDistance = 2)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(query))
            return false;

        var q = query.Trim().ToLowerInvariant();
        var c = candidate.Trim().ToLowerInvariant();

        // Short queries make Levenshtein overly permissive (2 edits into
        // a 3-letter query erases the query entirely), so require a
        // slightly longer minimum before fuzzy kicks in.
        if (q.Length < 3) return false;

        if (Distance(c, q) <= maxDistance) return true;

        foreach (var token in c.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Distance(token, q) <= maxDistance) return true;
        }
        return false;
    }
}
