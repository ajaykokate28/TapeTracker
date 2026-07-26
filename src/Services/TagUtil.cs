using System.Text.RegularExpressions;

namespace TapeTracker.Services;

/// <summary>
/// Single source of truth for how customer tags (family / group labels) are
/// normalized. Applied on save, on read, and by the DB migration so a stray
/// "Sharma  Family" or " sharma family " collapses into the same bucket as
/// "Sharma Family".
/// </summary>
public static class TagUtil
{
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Returns the canonical form of a tag string: trimmed, with any run of
    /// whitespace collapsed to a single space. Returns <c>null</c> for null,
    /// empty, or whitespace-only input so callers can treat "no tag" uniformly.
    /// Casing is left as the user typed it — de-duplication happens elsewhere
    /// with a case-insensitive comparison so we can preserve the user's
    /// preferred spelling.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = WhitespaceRun.Replace(raw.Trim(), " ");
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>
    /// Case-insensitive equality on the normalized form of two tags.
    /// </summary>
    public static bool AreEqual(string? a, string? b)
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
    }
}
