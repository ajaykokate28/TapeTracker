using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class PinPage : ContentPage
{
    private readonly PinViewModel _vm;

    public PinPage(PinViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }
}
