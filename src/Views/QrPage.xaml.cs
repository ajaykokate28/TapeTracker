using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class QrPage : ContentPage
{
    public QrPage(QrViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
