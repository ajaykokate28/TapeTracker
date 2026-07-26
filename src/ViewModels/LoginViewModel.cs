using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

/// <summary>
/// Backs the login page. On success calls <see cref="LoggedInHandler"/> so the
/// host page can hand control to the main shell (or to the PIN quick-unlock).
/// </summary>
public partial class LoginViewModel : BaseViewModel
{
    private const string LastUsernameKey = "auth.last_username";

    private readonly IAuthService _auth;

    [ObservableProperty] public partial string Username     { get; set; } = string.Empty;
    [ObservableProperty] public partial string Password     { get; set; } = string.Empty;
    [ObservableProperty] public partial bool   RememberMe   { get; set; } = true;
    [ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool   HasError     { get; set; }

    /// <summary>One-shot green banner shown after a successful password
    /// recovery so the user knows why they're back on the login screen. Kept
    /// separate from <see cref="HasError"/> so we don't have to tint a red
    /// container green (which the styles don't support).</summary>
    [ObservableProperty] public partial string InfoMessage  { get; set; } = string.Empty;
    [ObservableProperty] public partial bool   HasInfo      { get; set; }

    public Action? LoggedInHandler { get; set; }

    /// <summary>Invoked when the user taps "Forgot password?". The host page
    /// pushes <c>ForgotPasswordPage</c> onto its navigation stack — done via
    /// callback rather than direct Navigation calls here so the view-model
    /// stays UI-framework agnostic and unit-testable.</summary>
    public Action? ForgotPasswordHandler { get; set; }

    [RelayCommand]
    private void OpenForgotPassword() => ForgotPasswordHandler?.Invoke();

    /// <summary>
    /// Pre-populates the username entry and shows a one-shot "password reset"
    /// info banner. Called from the LoginPage host after popping back from
    /// ForgotPasswordPage so the user knows to type their fresh password
    /// straight into the same screen they came from.
    /// </summary>
    public void PrefillAfterReset(string? username)
    {
        if (!string.IsNullOrWhiteSpace(username))
            Username = username.Trim();
        Password    = string.Empty;
        HasError    = false;
        ErrorMessage = string.Empty;
        HasInfo     = true;
        InfoMessage = LocalizationService.Current.PasswordResetSuccessBanner;
    }

    public LoginViewModel(IAuthService auth)
    {
        _auth = auth;
        Title = LocalizationService.Current.LoginTitle;

        // Pre-fill the last username so returning admins can just type their password.
        var last = Preferences.Default.Get(LastUsernameKey, string.Empty);
        if (!string.IsNullOrEmpty(last))
        {
            Username   = last;
            RememberMe = true;
        }
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy) return;
        HasError     = false;
        ErrorMessage = string.Empty;
        HasInfo      = false;
        InfoMessage  = string.Empty;
        IsBusy       = true;
        try
        {
            var user = await _auth.LoginAsync(Username, Password);
            if (user is null)
            {
                HasError     = true;
                ErrorMessage = LocalizationService.Current.InvalidCredentialsMsg;
                return;
            }

            if (RememberMe)
                Preferences.Default.Set(LastUsernameKey, user.Username);
            else
                Preferences.Default.Remove(LastUsernameKey);

            LoggedInHandler?.Invoke();
        }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            // Never keep the password around in memory longer than needed.
            Password = string.Empty;
        }
    }
}
