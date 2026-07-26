using System.Globalization;

namespace TapeTracker.Converters;

/// <summary>Returns red when delivery date is in the past, green otherwise.</summary>
public class DeliveryDateColorConverter : IValueConverter
{
    private static readonly Color Green = Color.FromArgb("#27AE60");
    private static readonly Color Red   = Color.FromArgb("#E74C3C");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
            return dt.Date < DateTime.Today ? Red : Green;
        return Green;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
