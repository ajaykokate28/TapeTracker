using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class EmployeeListPage : ContentPage
{
    private readonly EmployeeListViewModel _vm;

    public EmployeeListPage(EmployeeListViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Refresh on every navigation-back so newly-added or renamed employees
        // show up without an explicit pull-to-refresh.
        await _vm.LoadCommand.ExecuteAsync(null);
    }
}
