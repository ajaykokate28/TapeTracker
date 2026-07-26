using System.Globalization;

namespace TapeTracker.Converters;

/// <summary>
/// Converts an integer value to a <see cref="Color"/> based on whether it
/// equals an expected number. The <c>ConverterParameter</c> is a pipe-
/// delimited triple: <c>"&lt;expected&gt;|&lt;matchColor&gt;|&lt;otherColor&gt;"</c>.
///
/// Example (highlights the picker cell for FontScaleService.SizeIndex == 2):
///
///   BackgroundColor="{Binding Source={x:Static services:FontScaleService.Current},
///                             Path=SizeIndex,
///                             Converter={StaticResource EqualsIntToColorConverter},
///                             ConverterParameter='2|#EEF2FF|#F8FAFC'}"
///
/// Kept separate from <c>BoolToColorConverter</c> because that one hard-codes
/// the bool → two-color mapping, whereas this one lets the caller pick the
/// "expected" integer at bind-time. Consolidating the two into a single
/// converter class would blur their meanings and defeat the pipe-parsing
/// contract each one relies on.
/// </summary>
public class EqualsIntToColorConverter : IValueConverter
{
    private static readonly Color DefaultMatch = Color.FromArgb("#EEF2FF");
    private static readonly Color DefaultOther = Color.FromArgb("#F8FAFC");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var raw = parameter?.ToString() ?? string.Empty;
        var parts = raw.Split('|');
        if (parts.Length < 3) return DefaultOther;

        if (!int.TryParse(parts[0], out var expected)) return DefaultOther;

        // Same truthiness-ish handling as BoolToColorConverter: accept int,
        // long, or anything ToString()-convertible to an int. Keeps the
        // converter usable with bindings that materialize as boxed longs
        // on Windows XAML.
        int actual = value switch
        {
            int i  => i,
            long l => (int)l,
            _      => int.TryParse(value?.ToString(), out var parsed) ? parsed : int.MinValue
        };

        var pick = actual == expected ? parts[1] : parts[2];
        return Color.FromArgb(pick);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
