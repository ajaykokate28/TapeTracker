using System.Globalization;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.Converters;

/// <summary>
/// Renders a <see cref="PaymentMethod"/> enum value as its current-language
/// display name via <c>PaymentMethodExtensions.Localize()</c>. Wired into
/// <c>Picker.ItemDisplayBinding</c> so the payment-method picker shows
/// "Cash" / "रोख" / "नकद" / "રોકડ" without a custom items collection.
/// </summary>
public class PaymentMethodDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is PaymentMethod m ? m.Localize() : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException(
            "PaymentMethodDisplayConverter is display-only; Picker binds SelectedItem to the raw enum.");
}
