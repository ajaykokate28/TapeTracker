using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

/// <summary>
/// Backs the "Forgot password?" flow reachable from the login page. Modelled
/// as a single view-model with a "step" enum instead of three separate pages
/// because:
///   ● The whole recovery journey is a single conceptual task — splitting it
///     across pages would force awkward query-parameter marshalling through
///     Shell navigation.
///   ● Users need to be able to correct earlier answers (wrong username?
///     go back and retype) without losing the password they just typed.
///
/// State machine:
///     EnterUsername → (question lookup) → AnswerAndReset → (verify+set) → Done
///
/// The <see cref="ResetSucceeded"/> event lets the hosting page hand
/// control back to the login screen with the username pre-filled.
/// </summary>
public partial class ForgotPasswordViewModel : BaseViewModel
{
    private readonly IAuthService _auth;

    public enum WizardStep
    {
        EnterUsername   = 0,
        AnswerAndReset  = 1,
        Done            = 2
    }

    // ── Progress ────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStepUsername))]
    [NotifyPropertyChangedFor(nameof(IsStepAnswer))]
    [NotifyPropertyChangedFor(nameof(IsStepDone))]
    public partial WizardStep Step { get; set; } = WizardStep.EnterUsername;

    public bool IsStepUsername => Step == WizardStep.EnterUsername;
    public bool IsStepAnswer   => Step == WizardStep.AnswerAndReset;
    public bool IsStepDone     => Step == WizardStep.Done;

    // ── Step 1 fields ───────────────────────────────────────────────────────
    [ObservableProperty] public partial string Username { get; set; } = string.Empty;

    // ── Step 2 fields ───────────────────────────────────────────────────────
    [ObservableProperty] public partial string RecoveryQuestion { get; set; } = string.Empty;
    [ObservableProperty] public partial string Answer           { get; set; } = string.Empty;
    [ObservableProperty] public partial string NewPassword      { get; set; } = string.Empty;
    [ObservableProperty] public partial string ConfirmPassword  { get; set; } = string.Empty;

    // ── Error / info banner (shared across steps) ───────────────────────────
    [ObservableProperty] public partial string BannerMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool   HasBanner     { get; set; }
    /// <summary>Used to colour the banner red on failures, blue on info hints
    /// (e.g. "your account has no recovery question — ask your admin").</summary>
    [ObservableProperty] public partial bool   BannerIsError { get; set; }

    /// <summary>
    /// Fires after a successful reset so the host page can pop back to the
    /// login screen with the username pre-filled. Deliberately kept as an
    /// <see cref="Action{T}"/> rather than a Messenger to avoid pulling
    /// CommunityToolkit's MVVM messenger dependency into a single flow.
    /// </summary>
    public Action<string>? ResetSucceeded { get; set; }

    public ForgotPasswordViewModel(IAuthService auth)
    {
        _auth = auth;
        Title = LocalizationService.Current.ForgotPasswordTitle;
    }

    /// <summary>Called by the host page when Shell navigates back to it —
    /// clears any partial state so the second visit starts fresh.</summary>
    public void Reset()
    {
        Step             = WizardStep.EnterUsername;
        Username         = string.Empty;
        RecoveryQuestion = string.Empty;
        Answer           = string.Empty;
        NewPassword      = string.Empty;
        ConfirmPassword  = string.Empty;
        HasBanner        = false;
        BannerMessage    = string.Empty;
        BannerIsError    = false;
    }

    private void ShowError(string message)
    {
        BannerMessage = message;
        BannerIsError = true;
        HasBanner     = true;
    }

    private void ShowInfo(string message)
    {
        BannerMessage = message;
        BannerIsError = false;
        HasBanner     = true;
    }

    private void ClearBanner()
    {
        HasBanner     = false;
        BannerMessage = string.Empty;
    }

    // ── Step 1 → Step 2 ─────────────────────────────────────────────────────
    [RelayCommand]
    private async Task ContinueAsync()
    {
        if (IsBusy) return;
        ClearBanner();

        var L = LocalizationService.Current;
        if (string.IsNullOrWhiteSpace(Username))
        {
            ShowError(L.ForgotEnterUsernameMsg);
            return;
        }

        IsBusy = true;
        try
        {
            var question = await _auth.GetRecoveryQuestionAsync(Username);
            if (string.IsNullOrEmpty(question))
            {
                // Deliberately vague. "No recovery question configured" also
                // covers "user doesn't exist" — see IAuthService comment.
                ShowInfo(L.ForgotNoQuestionMsg);
                return;
            }

            RecoveryQuestion = question;
            Step             = WizardStep.AnswerAndReset;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Step 2 → Done ────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task ResetPasswordAsync()
    {
        if (IsBusy) return;
        ClearBanner();

        var L = LocalizationService.Current;

        if (string.IsNullOrWhiteSpace(Answer))
        {
            ShowError(L.ForgotEnterAnswerMsg);
            return;
        }
        if (NewPassword is null || NewPassword.Length < 4)
        {
            ShowError(L.ForgotPasswordTooShortMsg);
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            ShowError(L.ForgotPasswordMismatchMsg);
            return;
        }

        IsBusy = true;
        try
        {
            var ok = await _auth.ResetPasswordWithRecoveryAsync(
                Username, Answer, NewPassword);
            if (!ok)
            {
                ShowError(L.ForgotWrongAnswerMsg);
                return;
            }

            Step = WizardStep.Done;
            // Fire the callback after the UI has flipped to the "Done" panel —
            // the host page will pop the modal back to LoginPage after a
            // moment of "success" affirmation, which is easier to trust
            // than an instant transition.
            ResetSucceeded?.Invoke(Username);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            // Never keep the new password in memory beyond the moment we
            // hand it off to the auth service.
            NewPassword     = string.Empty;
            ConfirmPassword = string.Empty;
        }
    }

    /// <summary>Back button on step 2 — returns to username entry so the user
    /// can correct a typo without losing the modal.</summary>
    [RelayCommand]
    private void BackToUsername()
    {
        Step             = WizardStep.EnterUsername;
        RecoveryQuestion = string.Empty;
        Answer           = string.Empty;
        NewPassword      = string.Empty;
        ConfirmPassword  = string.Empty;
        ClearBanner();
    }
}
