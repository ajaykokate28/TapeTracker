using System.Globalization;

namespace TapeTracker.Converters;

/// <summary>
/// Converts a bool to a <see cref="Color"/>.
/// <para>
/// The <c>ConverterParameter</c> may be either:
/// <list type="bullet">
///   <item><description><c>"#RRGGBB"</c> – returned when <c>true</c>; falls back to
///   <c>#D1D5DB</c> (Tailwind gray-300) when <c>false</c> (legacy behaviour).</description></item>
///   <item><description><c>"#RRGGBB|#RRGGBB"</c> – first color for <c>true</c>,
///   second for <c>false</c>.</description></item>
/// </list>
/// </para>
/// Example: <c>TextColor="{Binding IsSelected, Converter={StaticResource BoolToColorConverter}, ConverterParameter='#FFFFFF|#334155'}"</c>
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    private const string DefaultFalseColor = "#D1D5DB";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Same truthiness semantics as before: accept bool, string (non-null / non-empty),
        // or numeric length > 0 so ellipse/PIN indicators keep working.
        bool truthy = value switch
        {
            bool b   => b,
            string s => !string.IsNullOrEmpty(s),
            int i    => i > 0,
            long l   => l > 0,
            _        => value is not null
        };

        var raw = parameter?.ToString() ?? "#512BD4";
        string trueColor, falseColor;

        var pipeIdx = raw.IndexOf('|');
        if (pipeIdx >= 0)
        {
            trueColor  = raw[..pipeIdx];
            falseColor = raw[(pipeIdx + 1)..];
        }
        else
        {
            trueColor  = raw;
            falseColor = DefaultFalseColor;
        }

        return Color.FromArgb(truthy ? trueColor : falseColor);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
