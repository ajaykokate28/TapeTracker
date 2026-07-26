using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class ShopSettingsPage : ContentPage
{
    public ShopSettingsPage(ShopSettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
