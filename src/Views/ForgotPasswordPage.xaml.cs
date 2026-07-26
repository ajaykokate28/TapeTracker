using TapeTracker.ViewModels;

namespace TapeTracker.Views;

/// <summary>
/// Hosts the <see cref="ForgotPasswordViewModel"/> wizard. Deliberately does
/// NOT participate in Shell — the login flow runs *before* the shell is
/// created (see <c>App.RouteAfterSplashAsync</c>), so we swap the current
/// Window's Page directly the same way LoginPage does. The
/// <see cref="BackToLogin"/> callback lets the host (App) return control to
/// the LoginPage with the username pre-filled.
/// </summary>
public partial class ForgotPasswordPage : ContentPage
{
    private readonly ForgotPasswordViewModel _vm;

    public ForgotPasswordPage(ForgotPasswordViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = _vm;
    }

    /// <summary>
    /// Invoked when the user hits "Back to sign-in" or after a successful
    /// reset. The string is the username to pre-fill on the login page.
    /// </summary>
    public Action<string?>? BackToLogin { get; set; }

    /// <summary>
    /// Fired by the VM after a successful reset. Passing the callback via a
    /// setter keeps ForgotPasswordViewModel free of any Window/Shell knowledge.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Fresh start every time this page appears — protects against
        // orphaned state if the user cancels a reset mid-way and later
        // reopens the flow.
        _vm.Reset();
        _vm.ResetSucceeded = uname =>
        {
            // Small delay lets the user see the success panel before the
            // login screen flashes back. Fire-and-forget; if the page is
            // disposed before the delay elapses, BackToLogin will simply
            // be null.
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(1400), () =>
                BackToLogin?.Invoke(uname));
        };
    }

    private void OnBackToLoginTapped(object? sender, TappedEventArgs e)
        => BackToLogin?.Invoke(string.IsNullOrWhiteSpace(_vm.Username) ? null : _vm.Username);
}
