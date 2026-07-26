using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class ActivityLogPage : ContentPage
{
    private readonly ActivityLogViewModel _vm;

    public ActivityLogPage(ActivityLogViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }
}
