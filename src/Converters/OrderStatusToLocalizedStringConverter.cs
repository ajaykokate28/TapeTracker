using System.Globalization;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.Converters;

/// <summary>
/// Converts an <see cref="OrderStatus"/> enum value to its localized display
/// string via <see cref="LocalizationService.LocalizeStatus"/>. Used by
/// XAML bindings on plain-model objects (e.g. <c>RecentOrderInfo</c>) that
/// don't have an <c>INotifyPropertyChanged</c> hook to re-emit the status
/// label when the language changes.
/// </summary>
public sealed class OrderStatusToLocalizedStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            OrderStatus s => LocalizationService.Current.LocalizeStatus(s),
            null          => string.Empty,
            _             => value.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
