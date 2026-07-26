using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TapeTracker.Services;

/// <summary>
/// Runtime dark/light mode toggle. Persists choice across app restarts.
/// Access via ThemeService.Current. Call Initialize() once from App startup.
/// </summary>
public partial class ThemeService : ObservableObject
{
    public static ThemeService Current { get; } = new();
    private ThemeService() { }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeIcon))]
    public partial bool IsDarkMode { get; set; }

    public string ThemeIcon => IsDarkMode ? "☀️" : "🌙";

    /// <summary>Restore persisted theme. Call once from App constructor.</summary>
    public void Initialize()
    {
        var saved = Preferences.Default.Get("AppTheme", "Light");
        IsDarkMode = saved == "Dark";
        Apply();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        Apply();
        Preferences.Default.Set("AppTheme", IsDarkMode ? "Dark" : "Light");
    }

    private static void Apply()
    {
        if (Application.Current is not null)
            Application.Current.UserAppTheme = ThemeService.Current.IsDarkMode
                ? AppTheme.Dark
                : AppTheme.Light;
    }
}
