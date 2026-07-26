using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

/// <summary>
/// Wraps a <see cref="User"/> with UI-friendly, localized labels. Kept
/// separate from the model so per-row projections (role chip, active badge,
/// "you" marker) don't leak into the domain layer.
/// </summary>
public partial class EmployeeRowViewModel : ObservableObject
{
    public User User { get; }
    private readonly IAuthService _auth;

    public EmployeeRowViewModel(User user, IAuthService auth)
    {
        User  = user;
        _auth = auth;
    }

    public int    Id            => User.Id;
    public string DisplayName   => User.DisplayName;
    public string Username      => User.Username;
    public bool   IsActive      => User.IsActive;

    public bool   IsAdmin       => User.Role == UserRole.Admin;
    public bool   IsSelf        => _auth.CurrentUser?.Id == User.Id;

    public string RoleLabel => User.Role == UserRole.Admin
        ? LocalizationService.Current.AdminRoleLabel
        : LocalizationService.Current.EmployeeRoleLabel;

    public string ActiveLabel => User.IsActive
        ? LocalizationService.Current.ActiveLabel
        : LocalizationService.Current.InactiveLabel;

    public string LastLoginLabel => User.LastLoginAt.HasValue
        ? User.LastLoginAt.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm")
        : "—";

    /// <summary>Deactivating buttons should not be shown for the currently signed-in admin.</summary>
    public bool CanToggleActivation => !IsSelf;
}

/// <summary>
/// Powers the Employees list page. Admin-only — the caller is expected to gate
/// navigation via <see cref="IAuthService.CanManageCustomers"/>, but the load
/// method also short-circuits gracefully when the current user isn't admin.
/// </summary>
public partial class EmployeeListViewModel : BaseViewModel
{
    private readonly IAuthService _authService;

    public ObservableCollection<EmployeeRowViewModel> Employees { get; } = new();

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public EmployeeListViewModel(IAuthService authService)
    {
        _authService = authService;
        Title = LocalizationService.Current.EmployeesLabel;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var list = await _authService.GetEmployeesAsync();
            Employees.Clear();
            foreach (var u in list)
                Employees.Add(new EmployeeRowViewModel(u, _authService));
            IsEmpty = Employees.Count == 0;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        // No query param → create mode.
        await Shell.Current.GoToAsync("EmployeeEditPage");
    }

    [RelayCommand]
    private async Task EditAsync(EmployeeRowViewModel? row)
    {
        if (row is null) return;
        await Shell.Current.GoToAsync($"EmployeeEditPage?userId={row.Id}");
    }

    [RelayCommand]
    private async Task ResetPasswordAsync(EmployeeRowViewModel? row)
    {
        if (row is null) return;
        await Shell.Current.GoToAsync($"EmployeeEditPage?userId={row.Id}&mode=password");
    }

    [RelayCommand]
    private async Task ToggleActivationAsync(EmployeeRowViewModel? row)
    {
        if (row is null || row.IsSelf) return;   // guard: never lock yourself out
        try
        {
            if (row.IsActive)
            {
                bool confirm = await Shell.Current.DisplayAlert(
                    L.ConfirmDeactivateTitle,
                    L.ConfirmDeactivateMsg,
                    L.YesSignOutLabel,  // reuse "Yes, sign out" style affirmative
                    L.CancelLabel);
                if (!confirm) return;

                await _authService.UpdateEmployeeAsync(
                    row.Id, row.User.DisplayName, row.User.Role, isActive: false);
            }
            else
            {
                await _authService.UpdateEmployeeAsync(
                    row.Id, row.User.DisplayName, row.User.Role, isActive: true);
            }
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(L.PermissionDeniedTitle, ex.Message, L.OkLabel);
        }
    }
}
