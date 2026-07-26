using TapeTracker.Views;

namespace TapeTracker;

public partial class AppShell : Shell
{
    /// <summary>
    /// MAUI's <see cref="Routing"/> table is process-global. AppShell is
    /// registered as Transient in DI, so a fresh instance is resolved after
    /// every login (or logout → login). Without this guard, each new AppShell
    /// would call <see cref="Routing.RegisterRoute(string, Type)"/> a second
    /// time and produce the "Ambiguous routes matched for …" error that
    /// GoToAsync throws when the same key resolves to multiple entries.
    /// </summary>
    private static bool _routesRegistered;

    public AppShell()
    {
        InitializeComponent();
        RegisterRoutesOnce();
    }

    private static void RegisterRoutesOnce()
    {
        if (_routesRegistered) return;
        _routesRegistered = true;

        // NOTE: DashboardPage and CustomerListPage are intentionally NOT
        // registered here — they're already exposed by the <ShellContent
        // Route="..."> declarations in AppShell.xaml, and re-registering
        // them via Routing.RegisterRoute produces the
        // "Ambiguous routes matched for //IMPL_DashboardPage/DashboardPage/..."
        // error that Shell.GoToAsync throws when it finds two entries for
        // the same route key.
        Routing.RegisterRoute(nameof(MeasurementFormPage), typeof(MeasurementFormPage));
        Routing.RegisterRoute(nameof(CustomerDetailPage),  typeof(CustomerDetailPage));
        Routing.RegisterRoute(nameof(OcrScanPage),         typeof(OcrScanPage));
        Routing.RegisterRoute(nameof(AllCustomersPage),    typeof(AllCustomersPage));
        Routing.RegisterRoute(nameof(QrPage),              typeof(QrPage));
        Routing.RegisterRoute(nameof(PinPage),             typeof(PinPage));
        Routing.RegisterRoute(nameof(EmployeeListPage),    typeof(EmployeeListPage));
        Routing.RegisterRoute(nameof(EmployeeEditPage),    typeof(EmployeeEditPage));
        Routing.RegisterRoute(nameof(AssignOrderPage),     typeof(AssignOrderPage));
        Routing.RegisterRoute(nameof(ActivityLogPage),     typeof(ActivityLogPage));
        Routing.RegisterRoute(nameof(BackupManagerPage),   typeof(BackupManagerPage));
        // DeliveryCalendarPage is intentionally NOT registered here — it's a
        // top-level tab in AppShell.xaml (same reason as DashboardPage /
        // CustomerListPage). Existing GoToAsync("DeliveryCalendarPage")
        // callers (Dashboard header shortcut) resolve the ShellContent route
        // key just fine without a Routing entry.
        Routing.RegisterRoute(nameof(ShopSettingsPage),     typeof(ShopSettingsPage));
    }
}
