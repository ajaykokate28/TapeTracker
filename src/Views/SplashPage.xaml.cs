using TapeTracker.Services;

namespace TapeTracker.Views;

public partial class SplashPage : ContentPage
{
    public SplashPage()
    {
        InitializeComponent();
        BindingContext = new SplashBindingContext();
    }

    public sealed class SplashBindingContext
    {
        public LocalizationService L => LocalizationService.Current;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunAnimationSequenceAsync();
    }

    private async Task RunAnimationSequenceAsync()
    {
        await Task.Delay(200);

        // 1 — Logo bounces in
        await Task.WhenAll(
            LogoContainer.FadeTo(1, 380, Easing.CubicOut),
            LogoContainer.ScaleTo(1.18, 380, Easing.CubicOut)
        );
        await LogoContainer.ScaleTo(1.0, 160, Easing.BounceOut);

        // 2 — Rings expand outward (fire and forget)
        _ = PulseRingAsync(Ring1, 0,   0.70);
        _ = PulseRingAsync(Ring2, 160, 0.50);
        _ = PulseRingAsync(Ring3, 320, 0.30);

        // 3 — Title slides up and fades in
        await Task.WhenAll(
            AppTitle.FadeTo(1, 420, Easing.CubicOut),
            AppTitle.TranslateTo(0, 0, 420, Easing.CubicOut)
        );

        // 4 — Tagline fades in
        await Task.WhenAll(
            Tagline.FadeTo(1, 340, Easing.CubicOut),
            Tagline.TranslateTo(0, 0, 340, Easing.CubicOut)
        );

        // 5 — Loading dots wave
        await LoadingDots.FadeTo(1, 260);
        await AnimateDotsAsync();

        // 6 — Fade out and let App decide the next page (org register / login / PIN / shell).
        await Task.Delay(120);
        await this.FadeTo(0, 360, Easing.CubicIn);

        if (Application.Current is App app)
            await app.RouteAfterSplashAsync();
    }

    private static async Task PulseRingAsync(View ring, int delayMs, double maxOpacity)
    {
        await Task.Delay(delayMs);
        ring.Scale = 0.4;
        ring.Opacity = maxOpacity;
        await Task.WhenAll(
            ring.ScaleTo(2.2, 860, Easing.CubicOut),
            ring.FadeTo(0, 860, Easing.CubicOut)
        );
    }

    private async Task AnimateDotsAsync()
    {
        for (int cycle = 0; cycle < 2; cycle++)
        {
            _ = BounceUpAsync(Dot1);
            await Task.Delay(140);
            _ = BounceUpAsync(Dot2);
            await Task.Delay(140);
            _ = BounceUpAsync(Dot3);
            await Task.Delay(520);
        }
    }

    private static async Task BounceUpAsync(View dot)
    {
        await dot.TranslateTo(0, -11, 180, Easing.CubicOut);
        await dot.TranslateTo(0, 0, 180, Easing.BounceOut);
    }
}
