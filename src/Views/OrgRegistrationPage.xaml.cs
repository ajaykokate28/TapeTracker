using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class OrgRegistrationPage : ContentPage
{
    private readonly OrgRegistrationViewModel _vm;

    public OrgRegistrationPage(OrgRegistrationViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = _vm;
    }

    /// <summary>Set by <see cref="App"/> so the page can hand control back to the shell after signup.</summary>
    public Action? OnRegistered
    {
        get => _vm.RegisteredHandler;
        set => _vm.RegisteredHandler = value;
    }
}
