using TapeTracker.Models;
using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class AllCustomersPage : ContentPage
{
    private readonly AllCustomersViewModel _vm;

    public AllCustomersPage(AllCustomersViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        RootGrid.Opacity = 0;
        RootGrid.TranslationY = 20;
        _vm.LoadCommand.Execute(null);
        await Task.WhenAll(
            RootGrid.FadeTo(1, 320, Easing.CubicOut),
            RootGrid.TranslateTo(0, 0, 320, Easing.CubicOut)
        );
    }

    // Code-behind tap handlers to avoid the RelativeSource/x:Reference pitfalls
    // inside virtualised CollectionView DataTemplates on WinUI 3.

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
        _vm.SelectedTagOption = AllCustomersViewModel.AllTagsOption;
    }
}
