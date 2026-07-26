using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class BackupManagerPage : ContentPage
{
    private readonly BackupManagerViewModel _vm;

    public BackupManagerPage(BackupManagerViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Refresh whenever the user comes back — a manual "Backup Now" from
        // another page (Dashboard toolbar) or a recent auto-backup should
        // appear in the list without a reload.
        await _vm.LoadCommand.ExecuteAsync(null);
    }
}
