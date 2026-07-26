using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class EmployeeEditPage : ContentPage
{
    public EmployeeEditPage(EmployeeEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
