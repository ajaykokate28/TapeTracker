using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

/// <summary>
/// Backs the one-time org-registration page. Successful registration signs the
/// new admin in immediately and fires <see cref="RegisteredHandler"/> so the
/// host page can swap the shell into the main app UI.
/// </summary>
public partial class OrgRegistrationViewModel : BaseViewModel
{
    private readonly IAuthService _auth;

    [ObservableProperty] public partial string  ShopName        { get; set; } = string.Empty;
    [ObservableProperty] public partial string  ShopPhone       { get; set; } = string.Empty;
    [ObservableProperty] public partial string  ShopAddress     { get; set; } = string.Empty;

    [ObservableProperty] public partial string  Username        { get; set; } = string.Empty;
    [ObservableProperty] public partial string  DisplayName     { get; set; } = string.Empty;
    [ObservableProperty] public partial string  Password        { get; set; } = string.Empty;
    [ObservableProperty] public partial string  ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty] public partial string  ErrorMessage    { get; set; } = string.Empty;
    [ObservableProperty] public partial bool    HasError        { get; set; }

    /// <summary>Fired by the ViewModel after a successful registration + auto-login.</summary>
    public Action? RegisteredHandler { get; set; }

    public OrgRegistrationViewModel(IAuthService auth)
    {
        _auth = auth;
        Title = LocalizationService.Current.OrgRegisterTitle;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy) return;
        HasError     = false;
        ErrorMessage = string.Empty;

        if (Password != ConfirmPassword)
        {
            HasError     = true;
            ErrorMessage = LocalizationService.Current.PasswordMismatch;
            return;
        }

        IsBusy = true;
        try
        {
            await _auth.RegisterOrganizationAsync(
                ShopName,
                ShopPhone,
                ShopAddress,
                Username,
                DisplayName,
                Password);
            RegisteredHandler?.Invoke();
        }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
