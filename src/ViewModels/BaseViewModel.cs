using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    public bool IsNotBusy => !IsBusy;

    /// <summary>Exposes the global localization service to every ViewModel's XAML bindings via {Binding L.Xxx}.</summary>
    public LocalizationService L => LocalizationService.Current;

    /// <summary>Exposes the global theme service to every ViewModel's XAML bindings via {Binding T.Xxx}.</summary>
    public ThemeService T => ThemeService.Current;

    /// <summary>
    /// Exposes <see cref="IAuthService"/> for XAML binding — e.g.
    /// <c>IsVisible="{Binding Auth.CanEditOrders}"</c> — so every page can
    /// hide admin-only affordances without adding an <c>IAuthService</c>
    /// dependency to its own view-model.
    /// </summary>
    public IAuthService Auth =>
        _authFallback ??= App.IPocProvider!.GetRequiredService<IAuthService>();
    private static IAuthService? _authFallback;

    /// <summary>
    /// Shows the standard "Only admins can do that" alert. Returns true when
    /// the current user has the requested permission and the caller should
    /// proceed; returns false (and displays the alert) when denied.
    /// </summary>
    protected async Task<bool> RequirePermissionAsync(bool granted)
    {
        if (granted) return true;
        await Shell.Current.DisplayAlert(
            L.PermissionDeniedTitle,
            L.PermissionDeniedMsg,
            L.OkLabel);
        return false;
    }

    /// <summary>
    /// Confirms the user really wants to sign out, then hands control back to
    /// <see cref="App.SignOut"/> which swaps the window's page to a fresh login
    /// screen. Available on every page via <c>Command="{Binding LogoutCommand}"</c>.
    /// </summary>
    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirm = await Shell.Current.DisplayAlert(
            L.ConfirmLogoutTitle,
            L.ConfirmLogoutMsg,
            L.YesSignOutLabel,
            L.CancelLabel);
        if (!confirm) return;
        if (Application.Current is App app)
            app.SignOut();
    }
}
