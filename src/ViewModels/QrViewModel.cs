using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

[QueryProperty(nameof(OrderId), "orderId")]
public partial class QrViewModel : BaseViewModel
{
    private readonly IMeasurementService _measurementService;
    private readonly IQrService          _qrService;

    [ObservableProperty]
    public partial int OrderId { get; set; }
    [ObservableProperty]
    public partial string CustomerName { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string OrderNumber { get; set; }  = string.Empty;
    [ObservableProperty]
    public partial string StatusLabel { get; set; }  = string.Empty;
    [ObservableProperty]
    public partial string DateLabel { get; set; }    = string.Empty;
    [ObservableProperty]
    public partial ImageSource? QrImage { get; set; }

    private Order? _order;

    public QrViewModel(IMeasurementService measurementService, IQrService qrService)
    {
        _measurementService = measurementService;
        _qrService          = qrService;
        Title = LocalizationService.Current.OrderQrCodeLabel;
    }

    partial void OnOrderIdChanged(int value)
    {
        if (value > 0) _ = LoadAsync(value);
    }

    private async Task LoadAsync(int orderId)
    {
        IsBusy = true;
        try
        {
            _order = await _measurementService.GetOrderByIdAsync(orderId);
            if (_order is null) return;

            CustomerName = _order.Customer?.Name ?? string.Empty;
            OrderNumber  = _order.OrderNumber;
            StatusLabel  = LocalizationService.Current.LocalizeStatus(_order.Status);
            DateLabel    = _order.Date.ToString("dd MMM yyyy");

            QrImage = _qrService.GenerateOrderQr(
                CustomerName,
                OrderNumber,
                StatusLabel,
                _order.Date);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ShareAsync()
    {
        if (QrImage is null || _order is null) return;

        // Regenerate PNG bytes for sharing
        var bytes = await Task.Run(() =>
        {
            var svc = new QrService();
            // Re-generate image source to get bytes via stream
            var src = svc.GenerateOrderQr(CustomerName, OrderNumber, StatusLabel, _order.Date);
            // Get bytes from the StreamImageSource
            if (src is StreamImageSource sis)
            {
                using var stream = sis.Stream(CancellationToken.None).GetAwaiter().GetResult()!;
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }
            return Array.Empty<byte>();
        });

        if (bytes.Length == 0) return;

        var dir  = Path.Combine(FileSystem.CacheDirectory, "qr");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"QR_{OrderNumber}.png");
        await File.WriteAllBytesAsync(path, bytes);

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = $"QR Code – {OrderNumber}",
            File  = new ShareFile(path)
        });
    }
}
