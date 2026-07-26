using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class OcrScanPage : ContentPage
{
    public OcrScanPage(OcrScanViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
