using TapeTracker.ViewModels;

namespace TapeTracker.Views;

public partial class CustomerDetailPage : ContentPage
{
    private readonly CustomerDetailViewModel _vm;

    public CustomerDetailPage(CustomerDetailViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    // Code-behind handlers for the per-order action buttons.
    // Bindings with RelativeSource/x:Reference inside a virtualised CollectionView
    // DataTemplate are unreliable on WinUI 3, so we wire commands directly.

    private void OnAdvanceStatus(object sender, TappedEventArgs e) => Fire(sender, _vm.AdvanceStatusCommand);
    private void OnEditOrder(object sender, TappedEventArgs e)      => Fire(sender, _vm.EditOrderCommand);
    private void OnRepeatOrder(object sender, TappedEventArgs e)    => Fire(sender, _vm.RepeatOrderCommand);

    // Swipe gestures on the order card — reuse the same Fire() plumbing as
    // the tap handlers because a SwipeItemView inherits BindingContext from
    // its enclosing SwipeView, which in turn sits inside the DataTemplate
    // that binds each row to an OrderSummaryItem.
    private void OnSwipeAdvance(object sender, System.EventArgs e) => Fire(sender, _vm.AdvanceStatusCommand);
    private void OnSwipeRepeat(object sender, System.EventArgs e)  => Fire(sender, _vm.RepeatOrderCommand);
    private void OnPrintPdf(object sender, TappedEventArgs e)       => Fire(sender, _vm.PrintPdfCommand);
    private void OnShowQr(object sender, TappedEventArgs e)         => Fire(sender, _vm.ShowQrCommand);
    private void OnExportJson(object sender, TappedEventArgs e)     => Fire(sender, _vm.ExportJsonCommand);
    private void OnSendReadyAlert(object sender, TappedEventArgs e) => Fire(sender, _vm.SendReadyAlertCommand);
    private void OnSendPaymentReminder(object sender, TappedEventArgs e) => Fire(sender, _vm.SendPaymentReminderCommand);
    private void OnDeleteOrder(object sender, TappedEventArgs e)    => Fire(sender, _vm.DeleteOrderCommand);
    private void OnAssignOrder(object sender, TappedEventArgs e)    => Fire(sender, _vm.AssignOrderCommand);

    private static void Fire(object sender, System.Windows.Input.ICommand command)
    {
        if (sender is BindableObject el && el.BindingContext is OrderSummaryItem item &&
            command.CanExecute(item))
        {
            command.Execute(item);
        }
    }
}

