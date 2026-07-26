using Microsoft.Extensions.DependencyInjection;
using TapeTracker.Services;
using TapeTracker.ViewModels;
using TapeTracker.Views;

namespace TapeTracker;

public partial class App : Application
{
    private readonly SplashPage   _splash;
    private readonly IPinService  _pinService;
    private readonly IAuthService _authService;

    public App(SplashPage splash, IPinService pinService, IAuthService authService)
    {
        InitializeComponent();
        _splash      = splash;
        _pinService  = pinService;
        _authService = authService;
        ThemeService.Current.Initialize();
        FontScaleService.Current.Initialize();
        LocalizationService.Current.Initialize();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var win = new Window(_splash);
        // Re-apply the persisted text-size scale once the window's Page tree
        // has been created. Doing it here (instead of in the App ctor) means
        // FontScaleService.Apply() actually finds the window in
        // Application.Current.Windows on first launch.
        win.Created += (_, _) => FontScaleService.Current.Apply();
        return win;
    }

    /// <summary>
    /// Called by <see cref="SplashPage"/> once the intro animation completes.
    /// Decides where to send the user next:
    ///   ● No organization yet   → OrgRegistrationPage
    ///   ● Not logged in         → LoginPage
    ///   ● Logged in + PIN set   → PinPage (quick unlock) → shell
    ///   ● Logged in + no PIN    → shell directly
    /// </summary>
    public async Task RouteAfterSplashAsync()
    {
        var window = Windows.Count > 0 ? Windows[0] : null;
        if (window is null) return;

        // No org yet → first-run wizard
        if (!await _authService.AnyOrganizationExistsAsync())
        {
            var regPage = IPocProvider!.GetRequiredService<OrgRegistrationPage>();
            regPage.OnRegistered = () => SwitchToShellOrPin(window);
            SetWindowPage(window, new NavigationPage(regPage) { BarBackgroundColor = Colors.Transparent });
            return;
        }

        // Org exists but nobody signed in → login screen
        if (_authService.CurrentUser is null)
        {
            var loginPage = IPocProvider!.GetRequiredService<LoginPage>();
            loginPage.OnLoggedIn = () => SwitchToShellOrPin(window);
            SetWindowPage(window, new NavigationPage(loginPage) { BarBackgroundColor = Colors.Transparent });
            return;
        }

        // Already signed in (edge case — session reused across restarts)
        SwitchToShellOrPin(window);
    }

    /// <summary>Once auth is complete, gate on the per-user PIN (if set) before showing the shell.</summary>
    private void SwitchToShellOrPin(Window window)
    {
        var services = IPocProvider
            ?? throw new InvalidOperationException("IPocProvider was not initialized during MauiProgram.CreateMauiApp().");

        var shell = services.GetRequiredService<AppShell>();

        if (!_pinService.IsPinEnabled)
        {
            SetWindowPage(window, shell);
            return;
        }

        var pinVm = services.GetRequiredService<PinViewModel>();
        pinVm.IsSettingsMode = false;
        pinVm.OnUnlocked     = () => SetWindowPage(window, shell);

        var pinPage = new PinPage(pinVm);
        SetWindowPage(window, new NavigationPage(pinPage) { BarBackgroundColor = Colors.Transparent });
    }

    /// <summary>
    /// Clears the session and returns to the login screen. Confirmation is
    /// the caller's responsibility (typically the toolbar Logout handler
    /// prompts before invoking this).
    /// </summary>
    public void SignOut()
    {
        var window = Windows.Count > 0 ? Windows[0] : null;
        if (window is null) return;

        _authService.Logout();

        // Resolve a fresh login page (Transient) so the previous username is
        // still pre-filled but state from the last session is discarded.
        var loginPage = IPocProvider!.GetRequiredService<LoginPage>();
        loginPage.OnLoggedIn = () => SwitchToShellOrPin(window);
        SetWindowPage(window, new NavigationPage(loginPage) { BarBackgroundColor = Colors.Transparent });
    }

    /// <summary>
    /// Swaps the window's root page and immediately re-applies the persisted
    /// text-size scale. Centralized so every navigation entry-point (splash
    /// route, sign-out, PIN unlock, etc.) keeps zoom preferences intact
    /// instead of snapping back to 1.0x whenever the page tree is rebuilt.
    /// </summary>
    private static void SetWindowPage(Window window, Page page)
    {
        window.Page = page;
        FontScaleService.Current.Apply();
    }

    public static IServiceProvider? IPocProvider { get; set; }
}
