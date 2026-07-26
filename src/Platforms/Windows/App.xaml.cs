using Microsoft.UI.Xaml;
using System.IO;

namespace TapeTracker.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();

        // Capture managed exceptions that would otherwise vanish silently on Windows.
        // Native access violations in Microsoft.UI.Xaml.dll still fall through to WER,
        // but this catches anything the .NET runtime can observe first.
        this.UnhandledException += (_, e) =>
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TapeTracker", "logs");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, "unhandled.log");
                File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Message}{Environment.NewLine}{e.Exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { /* logging must never crash the app */ }
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TapeTracker", "logs");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, "unhandled.log");
                File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AppDomain: {e.ExceptionObject}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { }
        };
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
