using TapeTracker.Models;
using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class CustomerListPage : ContentPage
{
    private readonly CustomerListViewModel _vm;

    public CustomerListPage(CustomerListViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        RootGrid.Opacity = 0;
        RootGrid.TranslationY = 24;
        _vm.LoadCommand.Execute(null);
        await Task.WhenAll(
            RootGrid.FadeTo(1, 380, Easing.CubicOut),
            RootGrid.TranslateTo(0, 0, 380, Easing.CubicOut)
        );
    }

    // Code-behind tap handlers used instead of RelativeSource/x:Reference bindings inside
    // the CollectionView DataTemplate. Both patterns are unreliable in MAUI/WinUI 3
    // DataTemplates (separate namescope + fragile ancestor lookup on virtualised items).
    // The item's BindingContext is the Customer, which we forward to the VM commands.

    private void OnCustomerCardTapped(object sender, TappedEventArgs e)
    {
        if (sender is BindableObject el && el.BindingContext is Customer customer)
        {
            if (_vm.ViewDetailCommand.CanExecute(customer))
                _vm.ViewDetailCommand.Execute(customer);
        }
    }

    private void OnCustomerDeleteTapped(object sender, TappedEventArgs e)
    {
        if (sender is BindableObject el && el.BindingContext is Customer customer)
        {
            if (_vm.DeleteCommand.CanExecute(customer))
                _vm.DeleteCommand.Execute(customer);
        }
    }

    private void OnClearTagFilterTapped(object sender, TappedEventArgs e)
    {
        _vm.SelectedTagOption = CustomerListViewModel.AllTagsOption;
    }
}
