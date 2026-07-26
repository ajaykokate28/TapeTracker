using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

/// <summary>
/// One view model, three modes:
///   ● Create             (no query params)
///   ● Edit               (?userId=N)
///   ● Reset password     (?userId=N&amp;mode=password) — same edit fields hidden,
///                        only the new-password entry is shown.
///
/// Uses <see cref="QueryProperty"/> so Shell navigation drives the mode
/// switch without extra wiring in the page.
/// </summary>
[QueryProperty(nameof(UserId), "userId")]
[QueryProperty(nameof(Mode),   "mode")]
public partial class EmployeeEditViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private User? _target;

    // ── Route parameters ────────────────────────────────────────────────────

    private int _userId;
    public int UserId
    {
        get => _userId;
        set
        {
            if (_userId == value) return;
            _userId = value;
            _ = LoadAsync();
        }
    }

    /// <summary>"password" → password-reset mode. Any other value → create/edit.</summary>
    [ObservableProperty]
    public partial string Mode { get; set; } = string.Empty;

    partial void OnModeChanged(string value) => RefreshMode();

    // ── Editable fields ─────────────────────────────────────────────────────

    [ObservableProperty] public partial string  Username        { get; set; } = string.Empty;
    [ObservableProperty] public partial string  DisplayName     { get; set; } = string.Empty;
    [ObservableProperty] public partial string  Password        { get; set; } = string.Empty;
    [ObservableProperty] public partial string  ConfirmPassword { get; set; } = string.Empty;
    [ObservableProperty] public partial bool    IsAdminRole     { get; set; }
    [ObservableProperty] public partial bool    IsActive        { get; set; } = true;

    // ── Derived state ───────────────────────────────────────────────────────

    /// <summary>True while creating a brand-new user (no id).</summary>
    [ObservableProperty] public partial bool IsCreateMode        { get; set; } = true;

    /// <summary>True while editing an existing user's profile.</summary>
    [ObservableProperty] public partial bool IsEditMode          { get; set; }

    /// <summary>True while resetting an existing user's password.</summary>
    [ObservableProperty] public partial bool IsPasswordResetMode { get; set; }

    /// <summary>Username is only editable on creation — after that it becomes
    /// a stable login identifier and shouldn't change out from under people.</summary>
    public bool IsUsernameEditable => IsCreateMode;

    /// <summary>The profile fields (name, role, active) don't apply to a pure
    /// password reset — hide them for less clutter.</summary>
    public bool AreProfileFieldsVisible => !IsPasswordResetMode;

    /// <summary>Set on both create and password-reset — never on plain edit
    /// (password stays untouched unless the admin taps "reset").</summary>
    public bool ArePasswordFieldsVisible => IsCreateMode || IsPasswordResetMode;

    [ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool   HasError     { get; set; }

    public EmployeeEditViewModel(IAuthService authService)
    {
        _authService = authService;
        Title = LocalizationService.Current.AddEmployeeLabel;
    }

    // ── Loading ─────────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        if (_userId <= 0)
        {
            _target = null;
            IsCreateMode        = true;
            IsEditMode          = false;
            IsPasswordResetMode = false;
            RefreshDerivedFlags();
            Title = LocalizationService.Current.AddEmployeeLabel;
            return;
        }

        _target = await _authService.GetEmployeeByIdAsync(_userId);
        if (_target is null) return;

        Username    = _target.Username;
        DisplayName = _target.DisplayName;
        IsAdminRole = _target.Role == UserRole.Admin;
        IsActive    = _target.IsActive;
        RefreshMode();
    }

    private void RefreshMode()
    {
        bool passwordMode = string.Equals(Mode, "password", StringComparison.OrdinalIgnoreCase);
        IsCreateMode        = _target is null;
        IsPasswordResetMode = !IsCreateMode && passwordMode;
        IsEditMode          = !IsCreateMode && !IsPasswordResetMode;

        Title = IsPasswordResetMode
            ? LocalizationService.Current.ResetPasswordLabel
            : IsEditMode
                ? LocalizationService.Current.EditEmployeeLabel
                : LocalizationService.Current.AddEmployeeLabel;

        RefreshDerivedFlags();
    }

    private void RefreshDerivedFlags()
    {
        OnPropertyChanged(nameof(IsUsernameEditable));
        OnPropertyChanged(nameof(AreProfileFieldsVisible));
        OnPropertyChanged(nameof(ArePasswordFieldsVisible));
    }

    // ── Save ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        HasError     = false;
        ErrorMessage = string.Empty;

        // Validate password confirmation only when the field is actually shown.
        if (ArePasswordFieldsVisible && Password != ConfirmPassword)
        {
            HasError     = true;
            ErrorMessage = L.PasswordMismatch;
            return;
        }

        IsBusy = true;
        try
        {
            var role = IsAdminRole ? UserRole.Admin : UserRole.Employee;

            if (IsCreateMode)
            {
                await _authService.CreateEmployeeAsync(Username, DisplayName, Password, role);
            }
            else if (IsPasswordResetMode)
            {
                await _authService.ResetEmployeePasswordAsync(_userId, Password);
            }
            else
            {
                await _authService.UpdateEmployeeAsync(_userId, DisplayName, role, IsActive);
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy   = false;
            Password = ConfirmPassword = string.Empty;   // never linger in memory
        }
    }

    [RelayCommand]
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");

    // ── Toggle commands for the custom pill switches ────────────────────────
    // The native Switch on WinUI has almost no contrast in its OFF state, so
    // the XAML uses a custom Border+Ellipse pill wired to these commands
    // instead of a two-way Switch binding.

    [RelayCommand]
    private void ToggleRole()   => IsAdminRole = !IsAdminRole;

    [RelayCommand]
    private void ToggleActive() => IsActive = !IsActive;
}
