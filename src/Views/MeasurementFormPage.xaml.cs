using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class MeasurementFormPage : ContentPage
{
    public MeasurementFormPage(MeasurementFormViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        RootScroll.Opacity = 0;
        RootScroll.TranslationY = 24;
        await Task.WhenAll(
            RootScroll.FadeTo(1, 380, Easing.CubicOut),
            RootScroll.TranslateTo(0, 0, 380, Easing.CubicOut)
        );
    }
}
