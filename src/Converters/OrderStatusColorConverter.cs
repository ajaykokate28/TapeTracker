using System.Globalization;
using TapeTracker.Models;

namespace TapeTracker.Converters;

/// <summary>Converts an OrderStatus enum value to its display color.</summary>
public class OrderStatusColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is OrderStatus s
            ? s switch
            {
                OrderStatus.Received  => Color.FromArgb("#6B7280"),
                OrderStatus.Cutting   => Color.FromArgb("#F59E0B"),
                OrderStatus.Stitching => Color.FromArgb("#3B82F6"),
                OrderStatus.Ready     => Color.FromArgb("#10B981"),
                OrderStatus.Delivered => Color.FromArgb("#8B5CF6"),
                _                     => Color.FromArgb("#6B7280")
            }
            : Color.FromArgb("#6B7280");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
