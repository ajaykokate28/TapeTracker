using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

/// <summary>
/// Handles both the lock screen (unlock mode) and the PIN settings page (set/clear mode).
/// Mode is controlled by the <see cref="IsSettingsMode"/> flag.
/// </summary>
[QueryProperty(nameof(IsSettings), "isSettings")]
public partial class PinViewModel : BaseViewModel
{
    private readonly IPinService _pinService;

    [ObservableProperty]
    public partial bool IsSettings { get; set; }

    partial void OnIsSettingsChanged(bool value)
    {
        IsSettingsMode = value;
        var l = LocalizationService.Current;
        Title = value ? (IsPinEnabled ? l.ChangeOrRemovePinTitle : l.SetPinTitle) : l.EnterPinTitle;
    }

    // ── mode ──────────────────────────────────────────────────────────────
    /// <summary>True = settings page (set/change/clear PIN). False = unlock screen.</summary>
    [ObservableProperty]
    public partial bool IsSettingsMode { get; set; }

    // ── state ─────────────────────────────────────────────────────────────
    [ObservableProperty]
    public partial string PinInput { get; set; }    = string.Empty;
    [ObservableProperty]
    public partial string ConfirmPin { get; set; }  = string.Empty;
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool HasError { get; set; }
    [ObservableProperty]
    public partial bool IsPinEnabled { get; set; }
    [ObservableProperty]
    public partial bool ShowConfirm { get; set; }   // second entry for set-PIN flow

    public PinViewModel(IPinService pinService)
    {
        _pinService = pinService;
        IsPinEnabled = _pinService.IsPinEnabled;
        Title = LocalizationService.Current.EnterPinTitle;
    }

    // ── numpad ───────────────────────────────────────────────────────────
    [RelayCommand]
    private void Digit(string d)
    {
        var target = ShowConfirm ? ConfirmPin : PinInput;
        if (target.Length >= 4) return;

        if (ShowConfirm)
            ConfirmPin += d;
        else
            PinInput += d;

        if (!ShowConfirm && PinInput.Length == 4)
            _ = OnPinCompleteAsync();
        else if (ShowConfirm && ConfirmPin.Length == 4)
            _ = OnConfirmCompleteAsync();
    }

    [RelayCommand]
    private void Backspace()
    {
        if (ShowConfirm && ConfirmPin.Length > 0)
            ConfirmPin = ConfirmPin[..^1];
        else if (!ShowConfirm && PinInput.Length > 0)
            PinInput = PinInput[..^1];
    }

    // ── unlock flow ──────────────────────────────────────────────────────
    private async Task OnPinCompleteAsync()
    {
        if (IsSettingsMode)
        {
            StatusMessage = LocalizationService.Current.EnterPinAgainMsg;
            ShowConfirm   = true;
            HasError      = false;
            return;
        }

        // Unlock mode
        if (_pinService.VerifyPin(PinInput))
        {
            HasError      = false;
            StatusMessage = string.Empty;
            OnUnlocked?.Invoke();
        }
        else
        {
            HasError      = true;
            StatusMessage = LocalizationService.Current.IncorrectPinMsg;
            PinInput      = string.Empty;
        }
        await Task.CompletedTask;
    }

    private async Task OnConfirmCompleteAsync()
    {
        if (PinInput != ConfirmPin)
        {
            HasError      = true;
            StatusMessage = LocalizationService.Current.PinsDontMatchMsg;
            PinInput      = string.Empty;
            ConfirmPin    = string.Empty;
            ShowConfirm   = false;
            return;
        }

        _pinService.SetPin(PinInput);
        IsPinEnabled  = true;
        HasError      = false;
        StatusMessage = LocalizationService.Current.PinSetSuccessMsg;
        PinInput      = string.Empty;
        ConfirmPin    = string.Empty;
        ShowConfirm   = false;
        await Shell.Current.GoToAsync("..");   // back to settings
    }

    [RelayCommand]
    private void ClearPin()
    {
        _pinService.ClearPin();
        IsPinEnabled  = false;
        StatusMessage = LocalizationService.Current.PinRemovedMsg;
        HasError      = false;
    }

    /// <summary>Fired when unlock succeeds (in lock-screen mode).</summary>
    public Action? OnUnlocked { get; set; }
}
