using TapeTracker.Models;

namespace TapeTracker.Services;

/// <summary>
/// Opens WhatsApp (falling back to SMS) with a pre-composed message about the
/// given order. All templates are localized and pick up the current
/// organization's shop name + currency symbol so the customer sees a branded
/// message in their own language. Zero API cost — pure deep links.
///
/// Kept static because the callers (view models) always resolve
/// <see cref="IAuthService"/> anyway; passing the org through the call site
/// avoids an extra service dependency injection everywhere the message
/// button appears.
/// </summary>
public static class WhatsAppAlertService
{
    /// <summary>
    /// "Your order is ready" template. Includes the outstanding balance so the
    /// customer knows what to bring — the single biggest source of awkward
    /// counter conversations for a small tailor.
    /// </summary>
    public static Task SendReadyAlertAsync(Customer customer, Order order, Organization? org)
    {
        var L         = LocalizationService.Current;
        var shopName  = string.IsNullOrWhiteSpace(org?.Name) ? "TapeTracker" : org!.Name;
        var currency  = string.IsNullOrWhiteSpace(org?.CurrencySymbol) ? "₹" : org!.CurrencySymbol!;

        var message = string.Format(
            L.WaReadyMessageFormat,
            customer.Name,
            order.OrderNumber,
            shopName,
            currency,
            order.BalanceDue.ToString("0.##"));

        return SendAsync(customer.Phone, message);
    }

    /// <summary>
    /// Payment-reminder template — nudges the customer to clear an outstanding
    /// balance. Only useful when the order actually has a balance due; callers
    /// should hide the button when <c>BalanceDue == 0</c>.
    /// </summary>
    public static Task SendPaymentReminderAsync(Customer customer, Order order, Organization? org)
    {
        var L         = LocalizationService.Current;
        var shopName  = string.IsNullOrWhiteSpace(org?.Name) ? "TapeTracker" : org!.Name;
        var currency  = string.IsNullOrWhiteSpace(org?.CurrencySymbol) ? "₹" : org!.CurrencySymbol!;

        var message = string.Format(
            L.WaReminderMessageFormat,
            customer.Name,
            order.OrderNumber,
            shopName,
            currency,
            order.BalanceDue.ToString("0.##"));

        return SendAsync(customer.Phone, message);
    }

    /// <summary>
    /// Shared open-a-messenger flow. WhatsApp → SMS → in-app alert with copyable text.
    /// </summary>
    private static async Task SendAsync(string phone, string message)
    {
        // Sanitize phone: keep digits and leading +.
        var digits = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());

        var waUri = new Uri($"whatsapp://send?phone={digits}&text={Uri.EscapeDataString(message)}");

        try
        {
            if (await Launcher.TryOpenAsync(waUri)) return;
        }
        catch { /* WhatsApp not installed */ }

        if (Sms.Default.IsComposeSupported)
        {
            await Sms.Default.ComposeAsync(new SmsMessage(message, digits));
        }
        else
        {
            // Last-resort fallback: show the message so the user can copy-paste.
            await Shell.Current.DisplayAlert("Alert",
                $"WhatsApp and SMS not available. Message:\n\n{message}", "OK");
        }
    }
}
