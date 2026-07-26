using SkiaSharp;
using SkiaSharp.Views.Maui;
using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _vm;

    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.MonthlyTrend))
                MonthlyChart.InvalidateSurface();
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshCommand.Execute(null);
    }

    private void OnMonthlyChartPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        var data = _vm.MonthlyTrend;
        if (data.Count == 0) return;

        int maxVal = data.Max(m => m.Count);
        if (maxVal == 0) maxVal = 1;

        var   info   = e.Info;
        float padL   = 8f,  padR = 8f, padT = 16f, padB = 28f;
        float chartW = info.Width  - padL - padR;
        float chartH = info.Height - padT - padB;
        int   n      = data.Count;
        float slotW  = chartW / n;
        float barW   = slotW * 0.55f;
        float barOff = slotW * 0.225f;

#pragma warning disable CS0618  // using deprecated-but-functional SkiaSharp text API
        using var barPaint = new SKPaint { Color = SKColor.Parse("#512BD4"), IsAntialias = true };
        using var lblPaint = new SKPaint
        {
            Color       = SKColors.Gray,
            TextSize    = 22f,
            TextAlign   = SKTextAlign.Center,
            IsAntialias = true
        };
        using var cntPaint = new SKPaint
        {
            Color       = SKColor.Parse("#512BD4"),
            TextSize    = 20f,
            TextAlign   = SKTextAlign.Center,
            IsAntialias = true
        };

        for (int i = 0; i < n; i++)
        {
            float barH = (float)data[i].Count / maxVal * chartH;
            float cx   = padL + i * slotW + barOff + barW / 2f;
            float top  = padT + chartH - barH;
            float bot  = padT + chartH;

            var rect = new SKRect(cx - barW / 2f, top, cx + barW / 2f, bot);
            canvas.DrawRoundRect(rect, 4, 4, barPaint);

            // Month label below
            canvas.DrawText(data[i].Label, cx, info.Height - 4f, lblPaint);

            // Count above bar
            if (data[i].Count > 0)
                canvas.DrawText(data[i].Count.ToString(), cx, top - 3f, cntPaint);
        }
#pragma warning restore CS0618
    }
}
