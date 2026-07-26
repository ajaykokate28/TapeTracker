using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

public partial class OcrScanViewModel : BaseViewModel
{
    private readonly ICameraOcrService _ocr;
    private readonly IUnitPreferenceService _unitService;
    private readonly IMeasurementService _measurementService;

    // ── Status ────────────────────────────────────────────────────────
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Tap 📷 Camera or 🖼 Gallery to scan a measurement slip.";
    [ObservableProperty]
    public partial bool HasResult { get; set; }
    [ObservableProperty]
    public partial string RawOcrText { get; set; } = string.Empty;
    [ObservableProperty]
    public partial int FieldsFound { get; set; }
    [ObservableProperty]
    public partial string UnitLabel { get; set; } = "in";
    [ObservableProperty]
    public partial bool ShowRawText { get; set; }

    // ── Customer fields ───────────────────────────────────────────────
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string Phone { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string OrderNumber { get; set; } = string.Empty;
    [ObservableProperty]
    public partial DateTime Date { get; set; } = DateTime.Today;

    // ── Shirt fields ──────────────────────────────────────────────────
    [ObservableProperty]
    public partial string ShirtLength { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string ShirtChest { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string ShirtWaist { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string ShirtHip { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string ShirtShoulder { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string ShirtSleeve { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string ShirtCuff { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string ShirtCollar { get; set; } = string.Empty;

    // ── Pant fields ───────────────────────────────────────────────────
    [ObservableProperty]
    public partial string PantLength { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string PantWaist { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string PantHip { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string PantThigh { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string PantAnkle { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string PantKnee { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string PantSeat { get; set; } = string.Empty;

    public OcrScanViewModel(
        ICameraOcrService ocr,
        IUnitPreferenceService unitService,
        IMeasurementService measurementService)
    {
        _ocr = ocr;
        _unitService = unitService;
        _measurementService = measurementService;
        Title = LocalizationService.Current.ScanMeasurementSlipLabel;
        UnitLabel = _unitService.UnitLabel;
        _unitService.UnitChanged += (_, _) => UnitLabel = _unitService.UnitLabel;
    }

    [RelayCommand]
    private async Task CaptureAsync() => await RunOcrAsync(() => _ocr.CaptureAndRecognizeAsync());

    [RelayCommand]
    private async Task PickFromGalleryAsync() => await RunOcrAsync(() => _ocr.PickAndRecognizeAsync());

    private async Task RunOcrAsync(Func<Task<string?>> ocrAction)
    {
        if (IsBusy) return;
        IsBusy = true;
        HasResult = false;
        var l = LocalizationService.Current;
        StatusMessage = l.ProcessingImageMsg;

        try
        {
            var text = await ocrAction();
            if (text is null)
            {
                StatusMessage = l.CancelledMsg;
                return;
            }

            RawOcrText = text;
            var parsed = MeasurementTextParser.Parse(text);
            FieldsFound = parsed.FieldsFound;
            PopulateFields(parsed);
            HasResult = true;
            StatusMessage = FieldsFound == 0
                ? l.NoMeasurementsMsg
                : string.Format(l.FieldsExtractedFmt, FieldsFound);
        }
        catch (Exception ex)
        {
            StatusMessage = l.ErrorPrefix + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PopulateFields(ParsedSlip parsed)
    {
        Name        = parsed.CustomerName;
        Phone       = parsed.CustomerPhone;
        OrderNumber = parsed.Order.OrderNumber;
        Date        = parsed.Order.Date;

        if (parsed.Order.Shirt is { } s)
        {
            ShirtLength   = Fmt(_unitService.ToDisplay(s.Length));
            ShirtChest    = Fmt(_unitService.ToDisplay(s.Chest));
            ShirtWaist    = Fmt(_unitService.ToDisplay(s.Waist));
            ShirtHip      = Fmt(_unitService.ToDisplay(s.Hip));
            ShirtShoulder = Fmt(_unitService.ToDisplay(s.Shoulder));
            ShirtSleeve   = Fmt(_unitService.ToDisplay(s.Sleeve));
            ShirtCuff     = Fmt(_unitService.ToDisplay(s.Cuff));
            ShirtCollar   = Fmt(_unitService.ToDisplay(s.Collar));
        }

        if (parsed.Order.Pant is { } p)
        {
            PantLength = Fmt(_unitService.ToDisplay(p.Length));
            PantWaist  = Fmt(_unitService.ToDisplay(p.Waist));
            PantHip    = Fmt(_unitService.ToDisplay(p.Hip));
            PantThigh  = Fmt(_unitService.ToDisplay(p.Thigh));
            PantAnkle  = Fmt(_unitService.ToDisplay(p.Ankle));
            PantKnee   = Fmt(_unitService.ToDisplay(p.Knee));
            PantSeat   = Fmt(_unitService.ToDisplay(p.Seat));
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!await RequirePermissionAsync(Auth.CanManageCustomers)) return;

        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlert("Validation", "Name is required.", "OK");
            return;
        }

        var order = new Order
        {
            OrderNumber = OrderNumber.Trim(),
            Date        = Date,
            Status      = OrderStatus.Received,
            Shirt = new ShirtMeasurement
            {
                Length   = ToInches(ShirtLength),
                Chest    = ToInches(ShirtChest),
                Waist    = ToInches(ShirtWaist),
                Hip      = ToInches(ShirtHip),
                Shoulder = ToInches(ShirtShoulder),
                Sleeve   = ToInches(ShirtSleeve),
                Cuff     = ToInches(ShirtCuff),
                Collar   = ToInches(ShirtCollar)
            },
            Pant = new PantMeasurement
            {
                Length = ToInches(PantLength),
                Waist  = ToInches(PantWaist),
                Hip    = ToInches(PantHip),
                Thigh  = ToInches(PantThigh),
                Ankle  = ToInches(PantAnkle),
                Knee   = ToInches(PantKnee),
                Seat   = ToInches(PantSeat)
            }
        };

        // Save customer first (new), then save the order linked to it
        var customer   = new Customer { Name = Name.Trim(), Phone = Phone.Trim() };
        var customerId = await _measurementService.SaveCustomerAsync(customer);
        order.CustomerId = customerId;
        await _measurementService.SaveOrderAsync(order);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private void ToggleRawText() => ShowRawText = !ShowRawText;

    [RelayCommand]
    private void ToggleUnit()
    {
        var next = _unitService.CurrentUnit == MeasurementUnit.Inch
            ? MeasurementUnit.Cm : MeasurementUnit.Inch;
        _unitService.SetUnit(next);
    }

    private decimal ToInches(string s)
        => decimal.TryParse(s, out var v) ? _unitService.ToInches(v) : 0m;

    private static string Fmt(decimal v) => v == 0 ? string.Empty : v.ToString("0.##");
}
