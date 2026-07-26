using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

[QueryProperty(nameof(CustomerId), "customerId")]
public partial class CustomerDetailViewModel : BaseViewModel
{
    private readonly IMeasurementService _measurementService;
    private readonly IUnitPreferenceService _unitService;
    private readonly IExportService _exportService;
    private readonly IPdfService _pdfService;

    private int _customerId;
    public int CustomerId
    {
        get => _customerId;
        set
        {
            _customerId = value;
            // Shell calls the setter synchronously with the query-string value.
            // We fire-and-forget the load but swallow to an alert so exceptions
            // don't vanish and leave the page silently blank.
            _ = SafeLoadAsync(value);
        }
    }

    private Customer? _customer;

    [ObservableProperty]
    public partial string CustomerName { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string Phone { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string UnitLabel { get; set; } = "in";

    /// <summary>Family / group tag of the current customer (may be empty).</summary>
    [ObservableProperty]
    public partial string Tag { get; set; } = string.Empty;

    /// <summary>True when the customer has a Tag and there's at least one other member
    /// sharing it. Drives visibility of the family panel.</summary>
    [ObservableProperty]
    public partial bool HasFamily { get; set; }

    /// <summary>Other customers who share this customer's Tag (excludes the current one).</summary>
    public ObservableCollection<Customer> FamilyMembers { get; } = new();

    // Order history list
    public ObservableCollection<OrderSummaryItem> Orders { get; } = new();

    // Selected order for detail / actions
    [ObservableProperty]
    public partial OrderSummaryItem? SelectedOrder { get; set; }

    private readonly IAuthService _authService;

    public CustomerDetailViewModel(
        IMeasurementService measurementService,
        IUnitPreferenceService unitService,
        IExportService exportService,
        IPdfService pdfService,
        IAuthService authService)
    {
        _measurementService = measurementService;
        _unitService        = unitService;
        _exportService      = exportService;
        _pdfService         = pdfService;
        _authService        = authService;
        Title      = LocalizationService.Current.CustomerFieldLabel;
        UnitLabel  = _unitService.UnitLabel;
        _unitService.UnitChanged += (_, _) => { UnitLabel = _unitService.UnitLabel; BuildOrders(); };

        // When the user toggles the app language the status pill on every order
        // card needs to re-emit its localized text. Each OrderSummaryItem is a
        // plain ObservableObject with a cached StatusLabel, so we re-run its
        // RefreshStatus after every language change to push the new strings out.
        LocalizationService.Current.PropertyChanged += (_, _) =>
        {
            foreach (var item in Orders)
                item.RefreshStatus();
            Title = LocalizationService.Current.CustomerFieldLabel;
        };
    }

    private async Task SafeLoadAsync(int id)
    {
        try
        {
            await LoadAsync(id);
        }
        catch (Exception ex)
        {
            // Surface DB / migration errors instead of leaving the page blank
            // — matches the behaviour of the crash-hardened SaveAsync flow.
            var shell = Shell.Current;
            if (shell is not null)
                await shell.DisplayAlert("Load Failed", ex.InnerException?.Message ?? ex.Message, "OK");
        }
    }

    private async Task LoadAsync(int id)
    {
        _customer = await _measurementService.GetCustomerByIdAsync(id);
        if (_customer is null) return;
        Title        = _customer.Name;
        CustomerName = _customer.Name;
        Phone        = _customer.Phone;
        Tag          = _customer.Tag ?? string.Empty;
        BuildOrders();
        await LoadFamilyMembersAsync();
    }

    private async Task LoadFamilyMembersAsync()
    {
        FamilyMembers.Clear();
        HasFamily = false;
        if (string.IsNullOrWhiteSpace(Tag)) return;

        try
        {
            var members = await _measurementService.GetAllCustomersAsync(tagFilter: Tag);
            foreach (var m in members)
            {
                if (m.Id == _customerId) continue; // exclude the current customer
                FamilyMembers.Add(m);
            }
            HasFamily = FamilyMembers.Count > 0;
        }
        catch { /* best-effort — panel just stays hidden if it fails */ }
    }

    /// <summary>Navigate to another family member's detail page.</summary>
    [RelayCommand]
    private async Task ViewFamilyMemberAsync(Customer? member)
    {
        if (member is null) return;
        await Shell.Current.GoToAsync($"CustomerDetailPage?customerId={member.Id}");
    }

    private void BuildOrders()
    {
        if (_customer is null) return;
        Orders.Clear();

        // Priority sort so the tailor sees urgent work first:
        //   1) Rush flag → red-flagged orders on top
        //   2) Active (not-delivered) before delivered
        //   3) Overdue (past due-date & not delivered) next
        //   4) Then most recent order date
        var sorted = _customer.Orders
            .OrderByDescending(o => o.IsRush)
            .ThenByDescending(o => o.Status != OrderStatus.Delivered)
            .ThenByDescending(o =>
                o.Status != OrderStatus.Delivered
                && o.DueDate.HasValue
                && o.DueDate.Value.Date < DateTime.Today)
            .ThenByDescending(o => o.Date);

        foreach (var o in sorted)
            Orders.Add(new OrderSummaryItem(o, _unitService));
        SelectedOrder = Orders.FirstOrDefault();
    }

    // â”€â”€ Add new order for this customer â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [RelayCommand]
    private async Task AddOrderAsync()
    {
        if (!await RequirePermissionAsync(Auth.CanManageCustomers)) return;
        await Shell.Current.GoToAsync($"MeasurementFormPage?customerId={_customerId}");
    }

    // â”€â”€ Edit selected order â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [RelayCommand]
    private async Task EditOrderAsync(OrderSummaryItem? item)
    {
        if (!await RequirePermissionAsync(Auth.CanEditOrders)) return;
        var target = item ?? SelectedOrder;
        if (target is null) return;
        await Shell.Current.GoToAsync($"MeasurementFormPage?orderId={target.Order.Id}");
    }

    // â”€â”€ Advance status (Status Tracker â€” Feature #2) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [RelayCommand]
    private async Task AdvanceStatusAsync(OrderSummaryItem? item)
    {
        var target = item ?? SelectedOrder;
        if (target is null) return;
        if (target.Order.Status == OrderStatus.Delivered) return;

        var next = target.Order.Status + 1;
        await _measurementService.UpdateOrderStatusAsync(target.Order.Id, next);
        target.Order.Status = next;
        target.RefreshStatus();
    }

    // â”€â”€ WhatsApp / SMS alert when Ready (Feature #3) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [RelayCommand]
    private async Task SendReadyAlertAsync(OrderSummaryItem? item)
    {
        var target = item ?? SelectedOrder;
        if (target is null || _customer is null) return;
        await WhatsAppAlertService.SendReadyAlertAsync(
            _customer, target.Order, _authService.CurrentOrganization);
    }

    /// <summary>Sends a payment-reminder WhatsApp/SMS. Hidden on paid-in-full orders.</summary>
    [RelayCommand]
    private async Task SendPaymentReminderAsync(OrderSummaryItem? item)
    {
        var target = item ?? SelectedOrder;
        if (target is null || _customer is null) return;
        if (!target.Order.HasPricing || target.Order.BalanceDue <= 0m) return;
        await WhatsAppAlertService.SendPaymentReminderAsync(
            _customer, target.Order, _authService.CurrentOrganization);
    }

    // â”€â”€ Repeat / clone last order (Feature #4) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [RelayCommand]
    private async Task RepeatOrderAsync(OrderSummaryItem? item)
    {
        if (!await RequirePermissionAsync(Auth.CanManageCustomers)) return;
        var source = item ?? SelectedOrder;
        if (source is null) return;

        var newOrderNo = await _measurementService.GenerateOrderNumberAsync();
        var clone = new Order
        {
            CustomerId  = _customerId,
            OrderNumber = newOrderNo,
            Date        = DateTime.Today,
            DueDate     = DateTime.Today.AddDays(7),
            Status      = OrderStatus.Received,
            Shirt = source.Order.Shirt is { } s ? new ShirtMeasurement
            {
                Length = s.Length, Chest = s.Chest, Waist = s.Waist, Hip = s.Hip,
                Shoulder = s.Shoulder, Sleeve = s.Sleeve, Cuff = s.Cuff, Collar = s.Collar
            } : null,
            Pant = source.Order.Pant is { } p ? new PantMeasurement
            {
                Length = p.Length, Waist = p.Waist, Hip = p.Hip, Thigh = p.Thigh,
                Ankle = p.Ankle, Knee = p.Knee, Seat = p.Seat
            } : null
        };

        await _measurementService.SaveOrderAsync(clone);
        await LoadAsync(_customerId);   // refresh list
    }

    // â”€â”€ PDF Bill (Feature #1) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [RelayCommand]
    private async Task PrintPdfAsync(OrderSummaryItem? item)
    {
        var target = item ?? SelectedOrder;
        if (target is null || _customer is null) return;

        // Server-side re-check of the Phase-9 gating rules. Even though the
        // XAML hides the chip, a stale binding or keyboard shortcut could
        // still fire the command — we refuse and explain why instead.
        var L = LocalizationService.Current;
        if (!_authService.IsAdmin)
        {
            await Shell.Current.DisplayAlert(
                L.InvoiceLockedTitle, L.InvoiceAdminOnlyMsg, "OK");
            return;
        }
        if (target.Order.Status != OrderStatus.Ready &&
            target.Order.Status != OrderStatus.Delivered)
        {
            await Shell.Current.DisplayAlert(
                L.InvoiceLockedTitle, L.InvoiceNotReadyMsg, "OK");
            return;
        }

        try
        {
            await _pdfService.GenerateAndShareAsync(_customer, target.Order, _unitService);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("PDF Error", ex.Message, "OK");
        }
    }

    // â”€â”€ JSON Export â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [RelayCommand]
    private async Task ExportJsonAsync(OrderSummaryItem? item)
    {
        var target = item ?? SelectedOrder;
        if (target is null || _customer is null) return;
        try
        {
            await _exportService.ExportAsync(_customer, target.Order, _unitService);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Export Failed", ex.Message, "OK");
        }
    }

    // ── Delete order ──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteOrderAsync(OrderSummaryItem? item)
    {
        if (!await RequirePermissionAsync(Auth.CanDeleteOrders)) return;
        var target = item ?? SelectedOrder;
        if (target is null) return;
        bool ok = await Shell.Current.DisplayAlert("Delete Order",
            $"Delete order {target.Order.OrderNumber}?", "Yes", "No");
        if (!ok) return;
        await _measurementService.DeleteOrderAsync(target.Order.Id);
        Orders.Remove(target);
        SelectedOrder = Orders.FirstOrDefault();
    }

    // ── QR Code (Feature #11) ─────────────────────────────────────────────

    [RelayCommand]
    private async Task ShowQrAsync(OrderSummaryItem? item)
    {
        var target = item ?? SelectedOrder;
        if (target is null) return;
        await Shell.Current.GoToAsync($"QrPage?orderId={target.Order.Id}");
    }

    // ── Assign employees per stage (Phase 4) ─────────────────────────────

    [RelayCommand]
    private async Task AssignOrderAsync(OrderSummaryItem? item)
    {
        if (!await RequirePermissionAsync(Auth.CanManageCustomers)) return;
        var target = item ?? SelectedOrder;
        if (target is null) return;
        await Shell.Current.GoToAsync($"AssignOrderPage?orderId={target.Order.Id}");
    }

    // ── Quick-contact shortcuts (Phone header pills) ─────────────────────
    //
    // These fire off native intents built on the phone number in the
    // customer's profile — a `tel:` URI for calls and a `whatsapp://`
    // deep link for chat. Both fall back gracefully so the shopkeeper
    // never hits a dead end: if WhatsApp isn't installed we try SMS, and
    // if the dialer isn't available we surface the number in an alert so
    // it can be copied.

    /// <summary>Digits + optional leading `+` — used for both tel: and wa.me.</summary>
    private string SanitizedPhone
        => new string((Phone ?? string.Empty).Where(c => char.IsDigit(c) || c == '+').ToArray());

    [RelayCommand]
    private async Task CallCustomerAsync()
    {
        var digits = SanitizedPhone;
        if (string.IsNullOrWhiteSpace(digits))
        {
            await Shell.Current.DisplayAlert(L.NoPhoneTitle, L.NoPhoneMsg, L.OkLabel);
            return;
        }

        try
        {
            if (PhoneDialer.Default.IsSupported)
            {
                PhoneDialer.Default.Open(digits);
                return;
            }
            await Launcher.OpenAsync(new Uri($"tel:{digits}"));
        }
        catch
        {
            await Shell.Current.DisplayAlert(L.NoPhoneTitle, digits, L.OkLabel);
        }
    }

    [RelayCommand]
    private async Task OpenWhatsAppAsync()
    {
        var digits = SanitizedPhone;
        if (string.IsNullOrWhiteSpace(digits))
        {
            await Shell.Current.DisplayAlert(L.NoPhoneTitle, L.NoPhoneMsg, L.OkLabel);
            return;
        }

        try
        {
            var waUri = new Uri($"whatsapp://send?phone={digits}");
            if (await Launcher.TryOpenAsync(waUri)) return;

            // WhatsApp not installed — fall back to the web deep-link which
            // opens WhatsApp Web / prompts the browser to hand off.
            await Launcher.OpenAsync(new Uri($"https://wa.me/{digits.TrimStart('+')}"));
        }
        catch
        {
            await Shell.Current.DisplayAlert(L.NoPhoneTitle, digits, L.OkLabel);
        }
    }
}

/// <summary>Flat display wrapper for an Order row in the history list.</summary>
public partial class OrderSummaryItem : ObservableObject
{
    public Order Order { get; }
    private readonly IUnitPreferenceService _unit;

    /// <summary>
    /// Direct-on-template access to the localization singleton so the
    /// DataTemplate's compiled bindings can resolve <c>{Binding L.XXX}</c>
    /// against the item's own <c>x:DataType</c>. The alternative —
    /// <c>{Binding Source={RelativeSource AncestorType=ContentPage},
    /// Path=BindingContext.L.XXX}</c> — silently returns empty at runtime
    /// under compiled bindings on WinUI, which is why field labels and
    /// action-button text were rendering blank.
    /// </summary>
    public LocalizationService L => LocalizationService.Current;

    /// <summary>Same rationale as <see cref="L"/> — exposes <see cref="IAuthService"/>
    /// on the template so admin-only chips can bind <c>Auth.Can*</c> directly.</summary>
    public IAuthService Auth =>
        _authFallback ??= App.IPocProvider!.GetRequiredService<IAuthService>();
    private static IAuthService? _authFallback;

    [ObservableProperty]
    public partial string StatusLabel { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string StatusColor { get; set; } = "#6B7280";
    [ObservableProperty]
    public partial bool CanAdvance { get; set; }
    [ObservableProperty]
    public partial bool IsReady { get; set; }

    // Step indicators
    [ObservableProperty]
    public partial bool Step0Done { get; set; }
    [ObservableProperty]
    public partial bool Step1Done { get; set; }
    [ObservableProperty]
    public partial bool Step2Done { get; set; }
    [ObservableProperty]
    public partial bool Step3Done { get; set; }
    [ObservableProperty]
    public partial bool Step4Done { get; set; }

    public OrderSummaryItem(Order order, IUnitPreferenceService unit)
    {
        Order  = order;
        _unit  = unit;
        RefreshStatus();
    }

    public void RefreshStatus()
    {
        StatusLabel = LocalizationService.Current.LocalizeStatus(Order.Status);
        StatusColor = Order.Status switch
        {
            OrderStatus.Received  => "#6B7280",
            OrderStatus.Cutting   => "#F59E0B",
            OrderStatus.Stitching => "#3B82F6",
            OrderStatus.Ready     => "#10B981",
            OrderStatus.Delivered => "#8B5CF6",
            _                     => "#6B7280"
        };
        CanAdvance = Order.Status < OrderStatus.Delivered;
        IsReady    = Order.Status == OrderStatus.Ready;

        var s = (int)Order.Status;
        Step0Done = s >= 0; Step1Done = s >= 1; Step2Done = s >= 2;
        Step3Done = s >= 3; Step4Done = s >= 4;

        // CanGenerateInvoice depends on Status — nudge XAML to re-evaluate
        // its IsVisible binding after an in-place status advance.
        OnPropertyChanged(nameof(CanGenerateInvoice));
    }

    public string DateLabel        => Order.Date.ToString("dd MMM yyyy");
    public string DueDateLabel     => Order.DueDate.HasValue
        ? Order.DueDate.Value.ToString("dd MMM yyyy") : "-";
    public string OrderNumber      => Order.OrderNumber;
    public string Notes            => Order.Notes ?? string.Empty;

    /// <summary>Notes section is only rendered when the order actually has a note.</summary>
    public bool   HasNotes         => !string.IsNullOrWhiteSpace(Order.Notes);

    // ── Rush / SLA display (Phase 7) ───────────────────────────────────────
    /// <summary>Bind visibility on this to show a red flag chip.</summary>
    public bool   IsRush           => Order.IsRush;

    /// <summary>
    /// Number of whole days the order has spent in its current stage.
    /// Null when either the order has no <see cref="Order.StageChangedAt"/>
    /// (legacy) or is already delivered — in both cases the SLA chip is
    /// hidden. Zero and small positives are OK; the chip only surfaces at
    /// <see cref="StuckDaysWarnThreshold"/> to avoid noise.
    /// </summary>
    public int? StuckDays
    {
        get
        {
            if (Order.Status == OrderStatus.Delivered) return null;
            if (Order.StageChangedAt is not { } t) return null;
            var span = DateTime.UtcNow - t;
            return span.TotalDays < 0 ? 0 : (int)span.TotalDays;
        }
    }
    public const int StuckDaysWarnThreshold = 3;

    /// <summary>True when the stuck-days chip should be shown.</summary>
    public bool ShowStuckChip => StuckDays is int d && d >= StuckDaysWarnThreshold;
    public string StuckDaysDisplay =>
        StuckDays is int d
            ? string.Format(LocalizationService.Current.StuckDaysFormat, d)
            : string.Empty;

    /// <summary>True when the due date is set AND has passed AND order isn't delivered.</summary>
    public bool IsOverdue =>
        Order.Status != OrderStatus.Delivered
        && Order.DueDate is { } d
        && d.Date < DateTime.Today;

    // ── Payment display (Phase 8) ──────────────────────────────────────────
    /// <summary>Currency symbol from the current org, defaulting to ₹.</summary>
    private string Currency =>
        (App.IPocProvider?.GetService(typeof(IAuthService)) as IAuthService)
            ?.CurrentOrganization?.CurrencySymbol ?? "₹";

    public bool   HasPricing         => Order.HasPricing;
    public string GrandTotalDisplay  => $"{Currency} {Order.GrandTotal:0.##}";
    public string BalanceDueDisplay  => $"{Currency} {Order.BalanceDue:0.##}";
    public string AdvancePaidDisplay => $"{Currency} {Order.AdvancePaid:0.##}";
    /// <summary>Shows the payment-reminder WhatsApp button only when there's
    /// pricing and money is actually owed.</summary>
    public bool   CanSendPaymentReminder => HasPricing && Order.BalanceDue > 0m;

    /// <summary>
    /// Invoice PDF is a Phase-9 admin tool that only makes sense once the
    /// clothes are actually finished. Two gates:
    ///   1) Only administrators can generate invoices (accounting concern),
    ///   2) Only orders in Ready or Delivered state qualify — anything
    ///      earlier is still work-in-progress with no final line items.
    /// The button is hidden entirely when either gate fails, keeping the
    /// action toolbar uncluttered for employees.
    /// </summary>
    public bool CanGenerateInvoice =>
        Auth.IsAdmin &&
        (Order.Status == OrderStatus.Ready || Order.Status == OrderStatus.Delivered);

    /// <summary>PAID / PARTIAL / UNPAID pill label — localized.</summary>
    public string PaymentStatusLabel
    {
        get
        {
            if (!HasPricing) return LocalizationService.Current.NoPricingLabel;
            if (Order.IsPaidInFull) return LocalizationService.Current.PaidInFullLabel;
            if (Order.AdvancePaid > 0m) return LocalizationService.Current.PartialPaidLabel;
            return LocalizationService.Current.UnpaidLabel;
        }
    }

    /// <summary>Pill background: green if paid, amber if partial, red if unpaid, gray if no pricing.</summary>
    public string PaymentStatusColor
    {
        get
        {
            if (!HasPricing) return "#94A3B8";
            if (Order.IsPaidInFull) return "#10B981";
            if (Order.AdvancePaid > 0m) return "#F59E0B";
            return "#DC2626";
        }
    }

    /// <summary>True when at least one shirt field has a non-zero value —
    /// controls whether the shirt measurement block is shown at all.</summary>
    public bool HasShirtMeasurements =>
        Order.Shirt is not null && (
            Order.Shirt.Length   > 0 || Order.Shirt.Chest    > 0 ||
            Order.Shirt.Waist    > 0 || Order.Shirt.Hip      > 0 ||
            Order.Shirt.Shoulder > 0 || Order.Shirt.Sleeve   > 0 ||
            Order.Shirt.Cuff     > 0 || Order.Shirt.Collar   > 0);

    /// <summary>True when at least one pant field has a non-zero value.</summary>
    public bool HasPantMeasurements =>
        Order.Pant is not null && (
            Order.Pant.Length > 0 || Order.Pant.Waist > 0 ||
            Order.Pant.Hip    > 0 || Order.Pant.Thigh > 0 ||
            Order.Pant.Knee   > 0 || Order.Pant.Ankle > 0 ||
            Order.Pant.Seat   > 0);

    // ── Shirt display values (full set) ────────────────────────────────────
    public string ShirtLengthDisplay   => D(Order.Shirt?.Length   ?? 0);
    public string ShirtChestDisplay    => D(Order.Shirt?.Chest    ?? 0);
    public string ShirtWaistDisplay    => D(Order.Shirt?.Waist    ?? 0);
    public string ShirtHipDisplay      => D(Order.Shirt?.Hip      ?? 0);
    public string ShirtShoulderDisplay => D(Order.Shirt?.Shoulder ?? 0);
    public string ShirtSleeveDisplay   => D(Order.Shirt?.Sleeve   ?? 0);
    public string ShirtCuffDisplay     => D(Order.Shirt?.Cuff     ?? 0);
    public string ShirtCollarDisplay   => D(Order.Shirt?.Collar   ?? 0);

    // ── Pant display values (full set) ─────────────────────────────────────
    public string PantLengthDisplay    => D(Order.Pant?.Length ?? 0);
    public string PantWaistDisplay     => D(Order.Pant?.Waist  ?? 0);
    public string PantHipDisplay       => D(Order.Pant?.Hip    ?? 0);
    public string PantThighDisplay     => D(Order.Pant?.Thigh  ?? 0);
    public string PantKneeDisplay      => D(Order.Pant?.Knee   ?? 0);
    public string PantAnkleDisplay     => D(Order.Pant?.Ankle  ?? 0);
    public string PantSeatDisplay      => D(Order.Pant?.Seat   ?? 0);

    // ── Legacy compact fields (kept so existing XAML references compile) ───
    public string ChestDisplay  => ShirtChestDisplay;
    public string WaistDisplay  => ShirtWaistDisplay;
    public string PantWDisplay  => PantWaistDisplay;

    private string D(decimal inches)
    {
        var v = _unit.ToDisplay(inches);
        return v == 0 ? "-" : $"{v:0.##} {_unit.UnitLabel}";
    }
}

