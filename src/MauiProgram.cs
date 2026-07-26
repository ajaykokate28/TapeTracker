using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using TapeTracker.Data;
using TapeTracker.Services;
using TapeTracker.ViewModels;
using TapeTracker.Views;

namespace TapeTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Database
        //
        // NOTE: filename intentionally kept as "tapetrack.db" (not "tapetracker.db")
        // to preserve backwards compatibility with existing user installations
        // from before the app was renamed to TapeTracker. If you ever want a
        // clean-break rename, change this string and BackupService.DbPath in
        // lock-step and add a migration that copies the old file if present.
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "tapetrack.db");
        builder.Services.AddDbContext<TapeTrackerDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Services
        builder.Services.AddSingleton<IUnitPreferenceService, UnitPreferenceService>();
        builder.Services.AddScoped<IMeasurementService, MeasurementService>();
        builder.Services.AddScoped<IExportService, ExportService>();
        builder.Services.AddScoped<IPdfService, PdfService>();
        builder.Services.AddScoped<IDashboardService, DashboardService>();
        builder.Services.AddScoped<IBackupService, BackupService>();
        builder.Services.AddSingleton<IQrService, QrService>();
        // Auth is a singleton so CurrentUser/CurrentOrganization survive
        // navigation and are the single source of truth across pages.
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IAuditService, AuditService>();
        // PinService depends on IAuthService and needs a scope factory for its
        // per-user PIN persistence, so it's a singleton (not stateful itself).
        builder.Services.AddSingleton<IPinService, PinService>();
        builder.Services.AddSingleton<IMeasurementAssistant, NullMeasurementAssistant>();
        // Auto-backup infrastructure. Settings is a singleton because it
        // wraps Preferences/SecureStorage (both process-wide) and the
        // scheduler needs a scope factory to spin up a scoped BackupService
        // per run.
        builder.Services.AddSingleton<IAutoBackupSettingsService, AutoBackupSettingsService>();
        builder.Services.AddSingleton<IAutoBackupScheduler, AutoBackupScheduler>();

        // OCR service — platform-specific implementation, zero cloud cost
#if ANDROID
        builder.Services.AddTransient<ICameraOcrService, TapeTracker.Platforms.Android.CameraOcrService>();
#elif WINDOWS
        builder.Services.AddTransient<ICameraOcrService, TapeTracker.Platforms.Windows.CameraOcrService>();
#endif

        // Voice-input (dictation) service — Windows uses the WinRT
        // SpeechRecognizer with its built-in "Listening..." dialog. Other
        // platforms fall back to the no-op stub so the DI graph stays
        // resolvable without an ifdef in every view-model that wants a mic
        // button.
#if WINDOWS
        builder.Services.AddSingleton<IVoiceInputService, TapeTracker.Platforms.Windows.VoiceInputService>();
#else
        builder.Services.AddSingleton<IVoiceInputService, NullVoiceInputService>();
#endif

        // ViewModels
        builder.Services.AddTransient<OcrScanViewModel>();
        builder.Services.AddTransient<CustomerListViewModel>();
        builder.Services.AddTransient<AllCustomersViewModel>();
        builder.Services.AddTransient<MeasurementFormViewModel>();
        builder.Services.AddTransient<CustomerDetailViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<QrViewModel>();
        builder.Services.AddTransient<PinViewModel>();
        builder.Services.AddTransient<OrgRegistrationViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<ForgotPasswordViewModel>();
        builder.Services.AddTransient<EmployeeListViewModel>();
        builder.Services.AddTransient<EmployeeEditViewModel>();
        builder.Services.AddTransient<AssignOrderViewModel>();
        builder.Services.AddTransient<ActivityLogViewModel>();
        builder.Services.AddTransient<BackupManagerViewModel>();
        builder.Services.AddTransient<DeliveryCalendarViewModel>();
        builder.Services.AddTransient<ShopSettingsViewModel>();

        // Views (Pages)
        builder.Services.AddTransient<OcrScanPage>();
        builder.Services.AddTransient<SplashPage>();
        builder.Services.AddTransient<CustomerListPage>();
        builder.Services.AddTransient<AllCustomersPage>();
        builder.Services.AddTransient<MeasurementFormPage>();
        builder.Services.AddTransient<CustomerDetailPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<QrPage>();
        builder.Services.AddTransient<PinPage>();
        builder.Services.AddTransient<OrgRegistrationPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ForgotPasswordPage>();
        builder.Services.AddTransient<EmployeeListPage>();
        builder.Services.AddTransient<EmployeeEditPage>();
        builder.Services.AddTransient<AssignOrderPage>();
        builder.Services.AddTransient<ActivityLogPage>();
        builder.Services.AddTransient<BackupManagerPage>();
        builder.Services.AddTransient<DeliveryCalendarPage>();
        builder.Services.AddTransient<ShopSettingsPage>();
        // AppShell is transient so a fresh copy is resolved after login (or
        // logout \u2192 login), avoiding stale role-based nav state.
        builder.Services.AddTransient<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        App.IPocProvider = app.Services;
        return app;
    }
}
