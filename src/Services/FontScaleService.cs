using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TapeTracker.Services;

/// <summary>
/// Global text-size accessibility service. Presents four discrete scale
/// presets and applies them as a render transform on the root window page,
/// so every screen zooms in lockstep without requiring every XAML file to
/// switch to DynamicResource font sizes.
///
/// Design notes:
///   ● Kept as a singleton (mirrors <see cref="ThemeService"/>) so any page
///     can bind to it via <c>{Binding Source={x:Static services:FontScaleService.Current}, ...}</c>.
///   ● Persists the chosen index (0–3) via <see cref="Preferences"/>; the
///     scale factor is derived, never stored, so future preset changes only
///     touch this file.
///   ● Applies the transform in <see cref="Apply"/> by walking every
///     currently-open window. Called both after a preset change AND after
///     Shell navigation (subscribed from <see cref="App.SubscribeFontScale"/>)
///     because Shell can swap the visible page under us during tab changes.
/// </summary>
public partial class FontScaleService : ObservableObject
{
    public static FontScaleService Current { get; } = new();
    private FontScaleService() { }

    // ── Presets ──────────────────────────────────────────────────────────
    // Kept modest at the extremes: 0.90x still hits WCAG minimum tap-target
    // sizes on Windows, and 1.30x is the largest zoom before the fixed-height
    // headers on some pages start clipping. Adding more presets would demand
    // a rework of those headers.
    private static readonly double[] ScaleFactors  = new[] { 0.9,  1.0, 1.15, 1.30 };
    // Plain ASCII glyphs. Earlier revisions used the Unicode superscript
    // codepoints (U+207A, U+207B) for a prettier "A⁻ / A / A⁺ / A⁺⁺", but
    // OpenSansSemibold (our OpenSans-Semibold.ttf bundle) has no glyphs for
    // those codepoints on Windows and the pill rendered as an empty tofu
    // box. Switching to plain "A- / A / A+ / A++" keeps the visual meaning
    // clear and works with every font we might ship.
    private static readonly string[] ScaleLabels   = new[] { "A-", "A", "A+", "A++" };

    private const string PrefKey = "TextSizeIndex";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Scale))]
    [NotifyPropertyChangedFor(nameof(Label))]
    public partial int SizeIndex { get; set; } = 1;

    /// <summary>Actual render-transform scale factor for the current preset.</summary>
    public double Scale => ScaleFactors[Math.Clamp(SizeIndex, 0, ScaleFactors.Length - 1)];

    /// <summary>Short glyph shown on the header pill — reads as A⁻ / A / A⁺ / A⁺⁺.</summary>
    public string Label => ScaleLabels[Math.Clamp(SizeIndex, 0, ScaleLabels.Length - 1)];

    /// <summary>Restore the persisted preset. Call once from App startup.</summary>
    public void Initialize()
    {
        var saved = Preferences.Default.Get(PrefKey, 1);
        SizeIndex = Math.Clamp(saved, 0, ScaleFactors.Length - 1);
        Apply();
    }

    [RelayCommand]
    private void CycleTextSize()
    {
        SizeIndex = (SizeIndex + 1) % ScaleFactors.Length;
        Preferences.Default.Set(PrefKey, SizeIndex);
        Apply();
    }

    /// <summary>
    /// Jump directly to a specific preset — used by the Shop Settings
    /// picker where users see all four choices at once. The command
    /// parameter is bound as a string ("0"..."3") in XAML because
    /// CommandParameter can't cleanly carry an int in .NET MAUI bindings
    /// without a converter, and parsing here keeps the XAML boilerplate
    /// to a minimum.
    /// </summary>
    [RelayCommand]
    private void SelectPreset(string? index)
    {
        if (!int.TryParse(index, out var i)) return;
        i = Math.Clamp(i, 0, ScaleFactors.Length - 1);
        if (i == SizeIndex) return;
        SizeIndex = i;
        Preferences.Default.Set(PrefKey, SizeIndex);
        Apply();
    }

    /// <summary>
    /// Push the current <see cref="Scale"/> onto every visible window's root
    /// page. Anchored at the top-left so headers don't drift off the left
    /// edge when the scale grows.
    /// </summary>
    public void Apply()
    {
        var app = Application.Current;
        if (app is null) return;

        foreach (var win in app.Windows)
        {
            if (win.Page is not VisualElement page) continue;
            page.AnchorX = 0;
            page.AnchorY = 0;
            page.Scale   = Scale;
        }
    }
}
