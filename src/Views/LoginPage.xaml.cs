using Microsoft.Extensions.DependencyInjection;
using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _vm;

    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = _vm;

        // The VM stays UI-framework agnostic — the "Forgot password?" tap
        // fires this callback, and the page (which owns the NavigationPage
        // stack) resolves the transient ForgotPasswordPage from DI and
        // pushes it. Doing the push here (instead of via Shell routes)
        // matches how the login flow lives *outside* the AppShell entirely.
        _vm.ForgotPasswordHandler = OpenForgotPasswordAsync;
    }

    /// <summary>Set by <see cref="App"/> so the page can hand control back to the shell after login.</summary>
    public Action? OnLoggedIn
    {
        get => _vm.LoggedInHandler;
        set => _vm.LoggedInHandler = value;
    }

    private async void OpenForgotPasswordAsync()
    {
        try
        {
            var services = App.IPocProvider
                ?? throw new InvalidOperationException("Service provider not available.");
            var page = services.GetRequiredService<ForgotPasswordPage>();

            // On completion (either explicit "Back to sign-in" or an
            // automatic success dispatch), pop back to this LoginPage and
            // pre-fill the username so the freshly-reset user isn't stuck
            // typing it again.
            page.BackToLogin = uname =>
            {
                _vm.PrefillAfterReset(uname);
                _ = Navigation.PopAsync();
            };

            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
