using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class DeliveryCalendarPage : ContentPage
{
    private readonly DeliveryCalendarViewModel _vm;

    public DeliveryCalendarPage(DeliveryCalendarViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Kick a fresh load on every appearance so returning from an order
        // reflects any status changes the tailor just made.
        await _vm.LoadCommand.ExecuteAsync(null);
    }
}
