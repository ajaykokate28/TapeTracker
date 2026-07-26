using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class AssignOrderPage : ContentPage
{
    public AssignOrderPage(AssignOrderViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
