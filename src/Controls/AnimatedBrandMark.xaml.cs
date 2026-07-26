namespace TapeTracker.Controls;

/// <summary>
/// Reusable brand emblem — displays the TapeTracker tape-mark.
///
/// Historical note: this control originally ran a continuous "breathing"
/// scale animation (1.00 → 1.05 → 1.00 on a ~2.5 s cycle) to add a touch
/// of visual life to every page's header. Because the mark lives inside
/// <c>Shell.TitleView</c>, the animation was on-screen on every page all
/// day and drove the WinUI compositor at ~60 Hz continuously — measured
/// at ~13 % of a single CPU core when the app was otherwise idle.
///
/// For a data-entry app running on tailor-shop hardware, that background
/// draw was pure cost with no functional value. The animation loop was
/// removed; the mark is now static (still centre-anchored so any future
/// re-introduction of the pulse can be dropped straight back into
/// <see cref="RunPulseLoopAsync"/>). Startup CPU drops to ~0 % at idle,
/// battery-friendlier on laptops and quieter fans on kiosk PCs.
///
/// The Loaded/Unloaded event handlers are kept as no-ops so the
/// accompanying XAML (which wires them up) still binds cleanly without a
/// second edit; XAML compiler treats missing handlers as errors.
/// </summary>
public partial class AnimatedBrandMark : ContentView
{
    public AnimatedBrandMark()
    {
        InitializeComponent();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        // Intentional no-op. See class summary for the reasoning behind
        // removing the pulse loop that used to live here.
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        // Intentional no-op. See class summary.
    }
}
