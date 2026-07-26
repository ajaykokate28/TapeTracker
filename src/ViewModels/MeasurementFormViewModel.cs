using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

/// <summary>
/// Handles Add / Edit for an Order.
/// Query params:
///   customerId â€“ required for new orders (add mode)
///   orderId    â€“ required for editing an existing order
/// </summary>
[QueryProperty(nameof(CustomerId), "customerId")]
[QueryProperty(nameof(OrderId),    "orderId")]
public partial class MeasurementFormViewModel : BaseViewModel
{
    private readonly IMeasurementService _measurementService;
    private readonly IUnitPreferenceService _unitService;
    private readonly IAuthService _authService;
    private readonly IVoiceInputService _voice;

    // Set by Shell navigation
    [ObservableProperty]
    public partial int CustomerId { get; set; }
    [ObservableProperty]
    public partial int OrderId { get; set; }

    partial void OnCustomerIdChanged(int value)  { if (value > 0 && OrderId == 0) _ = InitNewOrderAsync(value); }
    partial void OnOrderIdChanged(int value)      { if (value > 0) _ = LoadOrderAsync(value); }

    // â”€â”€ Customer header (display only in edit; editable in add) â”€â”€â”€â”€â”€â”€
    [ObservableProperty]
    public partial string CustomerName { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Optional grouping label (e.g. "Sharma Family"). Family members share the tag
    /// so they can be filtered together on the customer list.
    /// </summary>
    [ObservableProperty]
    public partial string Tag { get; set; } = string.Empty;

    /// <summary>Full list of existing tags in the database (used as the source
    /// for the autocomplete popup — never bound directly to the UI).</summary>
    public ObservableCollection<string> TagSuggestions { get; } = new();

    /// <summary>Subset of <see cref="TagSuggestions"/> that matches the current
    /// text in the Tag entry. Bound to the autocomplete popup, so it shows only
    /// while the user is typing something that partially matches.</summary>
    public ObservableCollection<string> FilteredTagSuggestions { get; } = new();

    /// <summary>True when the autocomplete popup should be visible: there is
    /// text in the entry, the text isn't already an exact match, and at least
    /// one suggestion matches.</summary>
    [ObservableProperty]
    public partial bool ShowTagSuggestions { get; set; }

    partial void OnTagChanged(string value) => RefreshFilteredTagSuggestions();

    /// <summary>Recomputes <see cref="FilteredTagSuggestions"/> from the current
    /// <see cref="Tag"/> text. Called whenever the entry or the source list
    /// changes.</summary>
    private void RefreshFilteredTagSuggestions()
    {
        FilteredTagSuggestions.Clear();

        var q = TagUtil.Normalize(Tag);
        if (q is null)
        {
            ShowTagSuggestions = false;
            return;
        }

        foreach (var s in TagSuggestions)
        {
            // Prefix match on the full tag, or on any individual word inside it,
            // so "s" -> "Shinde family" (starts with s) but not "Test family"
            // (no word starts with s), while "fam" -> both "Shinde family" and
            // "Test family" (second word starts with "fam").
            if (StartsWithAnyWord(s, q))
                FilteredTagSuggestions.Add(s);
        }

        // Hide the popup when the only remaining match is an exact case-
        // insensitive equal to what the user already typed — nothing to pick.
        ShowTagSuggestions =
            FilteredTagSuggestions.Count > 0 &&
            !(FilteredTagSuggestions.Count == 1 &&
              string.Equals(FilteredTagSuggestions[0], q, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns true when <paramref name="tag"/> begins with <paramref name="query"/>
    /// or any of its whitespace-separated words does. Case-insensitive.
    /// </summary>
    private static bool StartsWithAnyWord(string tag, string query)
    {
        if (tag.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var word in tag.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // â”€â”€ Order fields â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty]
    public partial string OrderNumber { get; set; } = string.Empty;
    [ObservableProperty]
    public partial DateTime Date { get; set; } = DateTime.Today;
    [ObservableProperty]
    public partial DateTime DueDate { get; set; } = DateTime.Today.AddDays(7);
    [ObservableProperty]
    public partial bool HasDueDate { get; set; }
    /// <summary>
    /// Rush flag — when true the order card renders with a red flag badge
    /// and bubbles to the top of customer/calendar/dashboard queues.
    /// </summary>
    [ObservableProperty]
    public partial bool IsRush { get; set; }
    /// <summary>
    /// Persists the loaded order's status through the form round-trip so we
    /// don't accidentally reset a Cutting/Stitching/Ready order back to
    /// Received when the user edits measurements. New orders default to
    /// Received (constructor default of OrderStatus).
    /// </summary>
    public OrderStatus LoadedStatus { get; set; } = OrderStatus.Received;

    // ── Pricing & payments (Phase 9 — line-item edition) ────────────────────
    /// <summary>
    /// Priced rows on the invoice. The Grand Total is computed live as the
    /// user types quantities and rates, so the tailor always sees the running
    /// total without a Save round-trip. Each row is a <see cref="LineItemRow"/>
    /// that fires PropertyChanged → parent viewmodel recomputes totals.
    /// </summary>
    public ObservableCollection<LineItemRow> LineItems { get; } = new();

    /// <summary>
    /// Names displayed in the "For" quick-pick sheet on each line item.
    /// Populated at load-time from the customer's family (customers sharing
    /// the same Tag). Empty for un-tagged customers; the picker button
    /// stays hidden in that case.
    /// </summary>
    public List<string> FamilyMemberSuggestions { get; private set; } = new();
    public bool         HasFamilySuggestions => FamilyMemberSuggestions.Count > 1;

    /// <summary>
    /// Garment names from the shop's <see cref="Organization.ItemCatalog"/>.
    /// Displayed in the "Item" quick-pick sheet so the tailor picks instead
    /// of typing "Shirt" for the hundredth time. Empty when the shop hasn't
    /// configured any items — the picker button hides in that case.
    /// </summary>
    public List<string> ItemCatalog { get; private set; } = new();
    public bool         HasItemCatalog => ItemCatalog.Count > 0;

    /// <summary>Discount is a flat amount in the shop's currency. String-bound
    /// so blank input doesn't force a spurious "0" into the field.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtotalDisplay), nameof(TaxDisplay),
        nameof(GrandTotalDisplay), nameof(BalanceDueDisplay))]
    public partial string DiscountText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BalanceDueDisplay))]
    public partial string AdvancePaidText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PaymentMethod SelectedPaymentMethod { get; set; } = PaymentMethod.None;

    /// <summary>Available payment methods for the picker. Localized via
    /// <c>PaymentMethodExtensions.Localize()</c> at bind time.</summary>
    public IReadOnlyList<PaymentMethod> PaymentMethods { get; } = new[]
    {
        PaymentMethod.None,
        PaymentMethod.Cash,
        PaymentMethod.Upi,
        PaymentMethod.Card,
        PaymentMethod.BankTransfer,
        PaymentMethod.Other
    };

    // ── Live-computed money displays ─────────────────────────────────────────
    // All read the current org's currency symbol + tax rate from the auth
    // service so the form updates in real time as the user types.

    private decimal LineItemsTotal => LineItems.Sum(li => li.LineTotal);
    private decimal ParsedDiscount => decimal.TryParse(DiscountText,    out var v) ? v : 0m;
    private decimal ParsedAdvance  => decimal.TryParse(AdvancePaidText, out var v) ? v : 0m;
    private decimal CurrentTaxRate =>
        _authService.CurrentOrganization?.TaxRatePct ?? 0m;
    private string  Currency =>
        _authService.CurrentOrganization?.CurrencySymbol ?? "₹";

    private decimal ComputedSubtotal =>
        Math.Max(0m, LineItemsTotal - ParsedDiscount);
    private decimal ComputedTax =>
        Math.Round(ComputedSubtotal * (CurrentTaxRate / 100m), 2, MidpointRounding.AwayFromZero);
    private decimal ComputedGrandTotal =>
        ComputedSubtotal + ComputedTax;
    private decimal ComputedBalanceDue =>
        Math.Max(0m, ComputedGrandTotal - ParsedAdvance);

    public string ItemsTotalDisplay => $"{Currency} {LineItemsTotal:0.##}";
    public string SubtotalDisplay   => $"{Currency} {ComputedSubtotal:0.##}";
    public string TaxDisplay        => $"{Currency} {ComputedTax:0.##}";
    public string GrandTotalDisplay => $"{Currency} {ComputedGrandTotal:0.##}";
    public string BalanceDueDisplay => $"{Currency} {ComputedBalanceDue:0.##}";
    public bool   HasLineItems      => LineItems.Count > 0;

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    // UI step collapse
    [ObservableProperty]
    public partial bool IsStep1Expanded { get; set; } = true;
    // Shirt / Pant / Notes collapsed by default so the form opens compact — a
    // repeat customer often only needs "Copy from last order" + Save.
    [ObservableProperty]
    public partial bool IsStep2Expanded { get; set; }
    [ObservableProperty]
    public partial bool IsStep3Expanded { get; set; }
    [ObservableProperty]
    public partial bool IsStep4Expanded { get; set; }
    /// <summary>Step 5 = Pricing & payment. Collapsed by default so the form
    /// still opens compact for tailors who haven't yet enabled billing.</summary>
    [ObservableProperty]
    public partial bool IsStep5Expanded { get; set; }

    /// <summary>
    /// When false the form hides rarely-measured fields (Cuff, Collar, Ankle,
    /// Knee, Seat) so only the primary 4-6 measurements per garment are shown.
    /// Toggle from the "Show more fields" link at the bottom of each step.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowAdvancedFields { get; set; }

    /// <summary>True when the current customer has at least one prior order —
    /// drives the visibility of the "Copy from last order" pill.</summary>
    [ObservableProperty]
    public partial bool HasPriorOrder { get; set; }

    // The most recent prior order for the current customer (excluding the one
    // being edited). Cached so "Copy from last order" is a single-tap fill.
    private Order? _lastOrder;

    // ── Customer name autocomplete (add-mode only) ──────────────────────────
    /// <summary>
    /// Customers whose name partially matches the current text in the Name
    /// entry. Shown only when adding a brand-new record (CustomerId == 0), to
    /// steer the user toward an existing customer instead of creating a
    /// duplicate.
    /// </summary>
    public ObservableCollection<Customer> CustomerNameSuggestions { get; } = new();

    [ObservableProperty]
    public partial bool ShowCustomerNameSuggestions { get; set; }

    partial void OnCustomerNameChanged(string value) => _ = RefreshCustomerNameSuggestionsAsync();

    // Unit
    [ObservableProperty]
    public partial string UnitLabel { get; set; } = "in";

    // Shirt
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

    // Pant
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

    public MeasurementFormViewModel(
        IMeasurementService measurementService,
        IUnitPreferenceService unitService,
        IAuthService authService,
        IVoiceInputService voice)
    {
        _measurementService = measurementService;
        _unitService        = unitService;
        _authService        = authService;
        _voice              = voice;
        Title = "New Order";
        UnitLabel = _unitService.UnitLabel;
        _unitService.UnitChanged += OnUnitChanged;
        _ = LoadTagSuggestionsAsync();

        // Bubble any per-row change up so the "Grand total" strip updates
        // live as the user types quantities and rates. Also fires on
        // add/remove because CollectionChanged runs first.
        LineItems.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (LineItemRow r in e.NewItems)
                    r.PropertyChanged += OnLineItemChanged;
            if (e.OldItems is not null)
                foreach (LineItemRow r in e.OldItems)
                    r.PropertyChanged -= OnLineItemChanged;
            RaiseTotalsChanged();
        };
    }

    private void OnLineItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LineItemRow.Quantity) ||
            e.PropertyName == nameof(LineItemRow.UnitPriceText))
            RaiseTotalsChanged();
    }

    // ── Voice dictation (Phase: hands-free entry) ──────────────────────────
    //
    // Two entry points — one per garment — because tailors read entire
    // sections of a slip out loud at once. Both funnel through the same
    // DictateAsync + MeasurementTextParser pipeline, differing only in
    // which set of properties they mutate afterwards. Field-count feedback
    // matches the OCR flow so users get consistent "we heard N values" UX.

    /// <summary>Bound to the mic pill's <c>IsVisible</c> so it disappears on
    /// platforms without STT rather than showing a button that always fails.</summary>
    public bool IsVoiceSupported => _voice.IsSupported;

    [RelayCommand]
    private async Task DictateShirtAsync() => await DictateSectionAsync(isShirt: true);

    [RelayCommand]
    private async Task DictatePantAsync()  => await DictateSectionAsync(isShirt: false);

    private async Task DictateSectionAsync(bool isShirt)
    {
        var L = LocalizationService.Current;
        if (!_voice.IsSupported)
        {
            await Shell.Current.DisplayAlert(L.VoiceUnavailableTitle, L.VoiceUnavailableMsg, L.OkLabel);
            return;
        }

        string? raw;
        try
        {
            raw = await _voice.DictateAsync();
        }
        catch
        {
            raw = null;
        }
        if (string.IsNullOrWhiteSpace(raw)) return;   // user cancelled or nothing heard

        // Windows Dictation returns "chest thirty two waist thirty" — flatten
        // number-words to digits so the shared MeasurementTextParser can
        // pick them up with its existing regex.
        var normalized = SpokenNumberNormalizer.Normalize(raw);

        // Prefix the section keyword so the parser's "shirt Waist" /
        // "pant Waist" disambiguation lands in the right bucket even when
        // the user forgot to say the garment name.
        var slipLike = (isShirt ? "shirt " : "pant ") + normalized;
        var parsed   = MeasurementTextParser.Parse(slipLike);

        int filled = 0;
        if (isShirt && parsed.Order.Shirt is { } s)
        {
            filled += MaybeSet(v => ShirtLength   = v, s.Length);
            filled += MaybeSet(v => ShirtChest    = v, s.Chest);
            filled += MaybeSet(v => ShirtWaist    = v, s.Waist);
            filled += MaybeSet(v => ShirtHip      = v, s.Hip);
            filled += MaybeSet(v => ShirtShoulder = v, s.Shoulder);
            filled += MaybeSet(v => ShirtSleeve   = v, s.Sleeve);
            filled += MaybeSet(v => ShirtCuff     = v, s.Cuff);
            filled += MaybeSet(v => ShirtCollar   = v, s.Collar);
        }
        else if (!isShirt && parsed.Order.Pant is { } p)
        {
            filled += MaybeSet(v => PantLength = v, p.Length);
            filled += MaybeSet(v => PantWaist  = v, p.Waist);
            filled += MaybeSet(v => PantHip    = v, p.Hip);
            filled += MaybeSet(v => PantThigh  = v, p.Thigh);
            filled += MaybeSet(v => PantAnkle  = v, p.Ankle);
            filled += MaybeSet(v => PantKnee   = v, p.Knee);
            filled += MaybeSet(v => PantSeat   = v, p.Seat);
        }

        if (filled == 0)
        {
            // Nothing recognisable — show the raw transcript so the user can
            // see whether the mic caught the wrong garment keyword or missed
            // the numbers entirely.
            await Shell.Current.DisplayAlert(
                L.VoiceNothingFoundTitle,
                string.Format(L.VoiceNothingFoundMsgFormat, raw),
                L.OkLabel);
        }
    }

    /// <summary>
    /// Assigns a positive decimal to a target property. Zero is treated
    /// as "not spoken" — the parser leaves untouched fields at 0m and we
    /// don't want a partial dictation to blank out previously-typed
    /// values. Returns 1 on success so callers can count filled fields.
    /// </summary>
    private static int MaybeSet(Action<string> setter, decimal value)
    {
        if (value <= 0m) return 0;
        setter(value.ToString("0.##"));
        return 1;
    }

    private void RaiseTotalsChanged()
    {
        OnPropertyChanged(nameof(ItemsTotalDisplay));
        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(TaxDisplay));
        OnPropertyChanged(nameof(GrandTotalDisplay));
        OnPropertyChanged(nameof(BalanceDueDisplay));
        OnPropertyChanged(nameof(HasLineItems));
    }

    [RelayCommand]
    private void AddLineItem()
    {
        // Prefill "For" with the primary customer's name so single-customer
        // orders don't need any typing in that column.
        LineItems.Add(new LineItemRow
        {
            ForWhom       = CustomerName ?? string.Empty,
            Description   = string.Empty,
            Quantity      = 1,
            UnitPriceText = string.Empty
        });
        IsStep5Expanded = true;
    }

    [RelayCommand]
    private void RemoveLineItem(LineItemRow? row)
    {
        if (row is null) return;
        LineItems.Remove(row);
    }

    /// <summary>Shows a native action sheet with the family-member names so
    /// the tailor can tap a name instead of typing. The Entry stays fully
    /// editable — the sheet is a shortcut, not a straitjacket.</summary>
    [RelayCommand]
    private async Task PickForWhomAsync(LineItemRow? row)
    {
        if (row is null || FamilyMemberSuggestions.Count == 0) return;
        var picked = await Shell.Current.DisplayActionSheet(
            LocalizationService.Current.PickFamilyMemberTitle,
            LocalizationService.Current.CancelLabel,
            null,
            FamilyMemberSuggestions.ToArray());
        if (!string.IsNullOrEmpty(picked) &&
            picked != LocalizationService.Current.CancelLabel)
            row.ForWhom = picked;
    }

    /// <summary>Shows a native action sheet with the shop's item catalog.
    /// If the catalog is empty (shop hasn't configured Settings yet), the
    /// PickItem chip stays hidden and this method is a no-op.</summary>
    [RelayCommand]
    private async Task PickDescriptionAsync(LineItemRow? row)
    {
        if (row is null || ItemCatalog.Count == 0) return;
        var picked = await Shell.Current.DisplayActionSheet(
            LocalizationService.Current.PickItemTitle,
            LocalizationService.Current.CancelLabel,
            null,
            ItemCatalog.ToArray());
        if (!string.IsNullOrEmpty(picked) &&
            picked != LocalizationService.Current.CancelLabel)
            row.Description = picked;
    }

    private async Task LoadTagSuggestionsAsync()
    {
        try
        {
            var tags = await _measurementService.GetDistinctTagsAsync();
            TagSuggestions.Clear();
            foreach (var t in tags) TagSuggestions.Add(t);
            RefreshFilteredTagSuggestions();
        }
        catch { /* suggestions are best-effort */ }
    }

    /// <summary>Called when the user taps a suggestion in the autocomplete popup.</summary>
    [RelayCommand]
    private void ApplyTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        Tag = tag;                         // triggers OnTagChanged
        ShowTagSuggestions = false;        // dismiss the popup immediately
    }

    private async Task InitNewOrderAsync(int customerId)
    {
        Title = "New Order";
        var customer = await _measurementService.GetCustomerByIdAsync(customerId);
        if (customer is null) return;
        CustomerName = customer.Name;
        Phone        = customer.Phone;
        Tag          = customer.Tag ?? string.Empty;
        OrderNumber  = await _measurementService.GenerateOrderNumberAsync();
        await LoadTagSuggestionsAsync();
        await LoadInvoiceQuickPicksAsync(customerId);
        await LoadLastOrderAsync(customerId, excludingOrderId: 0);
    }

    /// <summary>Fetches family names + shop catalog and pushes them into the
    /// two picker properties. Called from both add-mode (InitNewOrder) and
    /// edit-mode (LoadOrderAsync) so the pickers are always populated.</summary>
    private async Task LoadInvoiceQuickPicksAsync(int customerId)
    {
        try
        {
            FamilyMemberSuggestions = await _measurementService
                .GetFamilyMemberNamesAsync(customerId);
        }
        catch { FamilyMemberSuggestions = new List<string>(); }

        ItemCatalog = _authService.CurrentOrganization?.ItemCatalog.ToList()
            ?? new List<string>();

        OnPropertyChanged(nameof(FamilyMemberSuggestions));
        OnPropertyChanged(nameof(HasFamilySuggestions));
        OnPropertyChanged(nameof(ItemCatalog));
        OnPropertyChanged(nameof(HasItemCatalog));
    }

    private async Task LoadOrderAsync(int orderId)
    {
        Title = "Edit Order";
        var order = await _measurementService.GetOrderByIdAsync(orderId);
        if (order is null) return;

        CustomerId   = order.CustomerId;
        CustomerName = order.Customer?.Name ?? string.Empty;
        Phone        = order.Customer?.Phone ?? string.Empty;
        Tag          = order.Customer?.Tag ?? string.Empty;
        await LoadInvoiceQuickPicksAsync(order.CustomerId);
        OrderNumber  = order.OrderNumber;
        Date         = order.Date;
        Notes        = order.Notes;
        IsRush       = order.IsRush;
        LoadedStatus = order.Status;

        // Pricing / payment fields — empty string when zero so the entries
        // stay blank instead of showing a distracting "0".
        DiscountText           = order.DiscountAmount > 0 ? order.DiscountAmount.ToString("0.##") : string.Empty;
        AdvancePaidText        = order.AdvancePaid    > 0 ? order.AdvancePaid.ToString("0.##")    : string.Empty;
        SelectedPaymentMethod  = order.PaymentMethod;

        // Rehydrate line items in their persisted order. Pre-Phase-9 orders
        // with a legacy Price but no line items get a single synthetic row
        // so the tailor can edit and re-save without losing that charge.
        LineItems.Clear();
        var rows = order.LineItems.OrderBy(li => li.SortIndex).ToList();
        if (rows.Count == 0 && order.Price > 0)
        {
            LineItems.Add(new LineItemRow
            {
                ForWhom       = string.Empty,
                Description   = LocalizationService.Current.StitchingChargeLabel,
                Quantity      = 1,
                UnitPriceText = order.Price.ToString("0.##")
            });
        }
        else
        {
            foreach (var li in rows)
            {
                LineItems.Add(new LineItemRow
                {
                    ForWhom       = li.ForWhom,
                    Description   = li.Description,
                    Quantity      = li.Quantity <= 0 ? 1 : li.Quantity,
                    UnitPriceText = li.UnitPrice > 0 ? li.UnitPrice.ToString("0.##") : string.Empty
                });
            }
        }
        RaiseTotalsChanged();

        // Auto-expand the pricing step on edit when there's money on the order,
        // so the user immediately sees the balance owed rather than a
        // collapsed card. New orders keep it collapsed.
        if (LineItems.Count > 0 || order.AdvancePaid > 0)
            IsStep5Expanded = true;

        if (order.DueDate.HasValue) { DueDate = order.DueDate.Value; HasDueDate = true; }
        await LoadTagSuggestionsAsync();
        await LoadLastOrderAsync(order.CustomerId, excludingOrderId: order.Id);

        if (order.Shirt is { } s)
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

        if (order.Pant is { } p)
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

    [RelayCommand] private void ToggleStep1() => IsStep1Expanded = !IsStep1Expanded;
    [RelayCommand] private void ToggleStep2() => IsStep2Expanded = !IsStep2Expanded;
    [RelayCommand] private void ToggleStep3() => IsStep3Expanded = !IsStep3Expanded;
    [RelayCommand] private void ToggleStep4() => IsStep4Expanded = !IsStep4Expanded;
    [RelayCommand] private void ToggleStep5() => IsStep5Expanded = !IsStep5Expanded;
    [RelayCommand] private void ToggleAdvancedFields() => ShowAdvancedFields = !ShowAdvancedFields;

    /// <summary>
    /// Fills every shirt + pant measurement from the customer's most recent
    /// prior order — the one-tap "same again" flow for repeat customers.
    /// Auto-expands both measurement steps so the user can review before Save.
    /// </summary>
    [RelayCommand]
    private void CopyFromLastOrder()
    {
        if (_lastOrder is null) return;

        if (_lastOrder.Shirt is { } s)
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
        if (_lastOrder.Pant is { } p)
        {
            PantLength = Fmt(_unitService.ToDisplay(p.Length));
            PantWaist  = Fmt(_unitService.ToDisplay(p.Waist));
            PantHip    = Fmt(_unitService.ToDisplay(p.Hip));
            PantThigh  = Fmt(_unitService.ToDisplay(p.Thigh));
            PantAnkle  = Fmt(_unitService.ToDisplay(p.Ankle));
            PantKnee   = Fmt(_unitService.ToDisplay(p.Knee));
            PantSeat   = Fmt(_unitService.ToDisplay(p.Seat));
        }

        // Expand both steps so the user can see the copied values.
        IsStep2Expanded = true;
        IsStep3Expanded = true;
    }

    /// <summary>Loads the most recent prior order for the given customer so
    /// "Copy from last order" is a single tap. <paramref name="excludingOrderId"/>
    /// skips the order currently being edited (irrelevant in add-mode where it's 0).</summary>
    private async Task LoadLastOrderAsync(int customerId, int excludingOrderId)
    {
        try
        {
            var orders = await _measurementService.GetOrdersByCustomerAsync(customerId);
            _lastOrder = orders
                .Where(o => !o.IsDeleted && o.Id != excludingOrderId)
                .OrderByDescending(o => o.Date)
                .FirstOrDefault();
            HasPriorOrder = _lastOrder is not null;
        }
        catch
        {
            _lastOrder    = null;
            HasPriorOrder = false;
        }
    }

    /// <summary>
    /// When adding a brand-new customer (CustomerId == 0), matches the current
    /// Name text against existing customers so the user can pick an existing
    /// one instead of accidentally creating a duplicate. Silently no-ops in
    /// edit-mode.
    /// </summary>
    private async Task RefreshCustomerNameSuggestionsAsync()
    {
        // Only helpful when creating a new customer.
        if (CustomerId != 0)
        {
            ShowCustomerNameSuggestions = false;
            CustomerNameSuggestions.Clear();
            return;
        }

        var q = CustomerName?.Trim() ?? string.Empty;
        if (q.Length < 2)
        {
            ShowCustomerNameSuggestions = false;
            CustomerNameSuggestions.Clear();
            return;
        }

        try
        {
            var matches = await _measurementService.GetAllCustomersAsync(searchTerm: q, limit: 5);
            CustomerNameSuggestions.Clear();
            foreach (var c in matches) CustomerNameSuggestions.Add(c);

            // Hide the popup if the only match is already exactly what the user
            // typed (they've committed to this name) — same rule as tag suggestions.
            ShowCustomerNameSuggestions =
                CustomerNameSuggestions.Count > 0 &&
                !(CustomerNameSuggestions.Count == 1 &&
                  string.Equals(CustomerNameSuggestions[0].Name, q, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            ShowCustomerNameSuggestions = false;
        }
    }

    /// <summary>Called when the user picks an existing customer from the
    /// Name autocomplete popup. Loads that customer into the form so the
    /// new order is attached to them instead of creating a duplicate.</summary>
    [RelayCommand]
    private void PickExistingCustomer(Customer? c)
    {
        if (c is null) return;
        ShowCustomerNameSuggestions = false;
        CustomerNameSuggestions.Clear();
        // Setting CustomerId triggers InitNewOrderAsync via the partial method
        // which reloads Name/Phone/Tag from the DB and pulls in the last order.
        CustomerId = c.Id;
    }

    [RelayCommand]
    private async Task GenerateOrderNumberAsync()
        => OrderNumber = await _measurementService.GenerateOrderNumberAsync();

    [RelayCommand]
    private void ToggleUnit()
    {
        var next = _unitService.CurrentUnit == MeasurementUnit.Inch
            ? MeasurementUnit.Cm : MeasurementUnit.Inch;
        _unitService.SetUnit(next);
    }

    private void OnUnitChanged(object? sender, EventArgs e)
    {
        UnitLabel = _unitService.UnitLabel;
        decimal Factor(decimal v) => _unitService.CurrentUnit == MeasurementUnit.Cm
            ? Math.Round(v * 2.54m, 2)
            : Math.Round(v / 2.54m, 2);

        ShirtLength   = CF(ShirtLength, Factor);   ShirtChest  = CF(ShirtChest, Factor);
        ShirtWaist    = CF(ShirtWaist, Factor);    ShirtHip    = CF(ShirtHip, Factor);
        ShirtShoulder = CF(ShirtShoulder, Factor); ShirtSleeve = CF(ShirtSleeve, Factor);
        ShirtCuff     = CF(ShirtCuff, Factor);     ShirtCollar = CF(ShirtCollar, Factor);
        PantLength    = CF(PantLength, Factor);    PantWaist   = CF(PantWaist, Factor);
        PantHip       = CF(PantHip, Factor);       PantThigh   = CF(PantThigh, Factor);
        PantAnkle     = CF(PantAnkle, Factor);     PantKnee    = CF(PantKnee, Factor);
        PantSeat      = CF(PantSeat, Factor);
    }

    private static string CF(string f, Func<decimal, decimal> fn)
        => decimal.TryParse(f, out var v) && v > 0 ? Fmt(fn(v)) : f;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        // Defense-in-depth: employees should never reach this page, but if any
        // navigation slipped through we hard-block the save with the standard
        // permission alert.
        if (!await RequirePermissionAsync(Auth.CanManageCustomers)) return;

        if (string.IsNullOrWhiteSpace(CustomerName) && CustomerId == 0)
        {
            await Shell.Current.DisplayAlert("Validation", "Customer name is required.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            // Normalize (trim + collapse whitespace) and, if the user typed a
            // case-variant of an existing tag, silently adopt the existing
            // canonical spelling so we don't create "Sharma Family" alongside
            // "sharma family". TagSuggestions is already the deduped canonical
            // list returned by GetDistinctTagsAsync().
            var normalizedTag = TagUtil.Normalize(Tag);
            if (normalizedTag is not null)
            {
                var match = TagSuggestions.FirstOrDefault(
                    s => string.Equals(s, normalizedTag, StringComparison.OrdinalIgnoreCase));
                if (match is not null && !string.Equals(match, normalizedTag, StringComparison.Ordinal))
                    normalizedTag = match;
            }

            if (CustomerId == 0)
            {
                // New customer created directly from the quick form.
                var c = new Customer
                {
                    Name  = CustomerName.Trim(),
                    Phone = Phone.Trim(),
                    Tag   = normalizedTag
                };
                CustomerId = await _measurementService.SaveCustomerAsync(c);
            }
            else
            {
                // Existing customer: persist Name / Phone / Tag edits before saving the order.
                var existing = await _measurementService.GetCustomerByIdAsync(CustomerId);
                if (existing is not null)
                {
                    existing.Name  = CustomerName.Trim();
                    existing.Phone = Phone.Trim();
                    existing.Tag   = normalizedTag;
                    await _measurementService.SaveCustomerAsync(existing);
                }
            }

            var order = new Order
            {
                Id          = OrderId,
                CustomerId  = CustomerId,
                OrderNumber = OrderNumber.Trim(),
                Date        = Date,
                DueDate     = HasDueDate ? DueDate : null,
                Notes       = Notes.Trim(),
                IsRush      = IsRush,
                // Preserve the loaded status on edit; new orders default to
                // Received via LoadedStatus's initializer.
                Status      = LoadedStatus,
                // Pricing — Price is now derived from line items server-side
                // (see MeasurementService.SaveOrderAsync). Setting it here
                // for symmetry / audit-log preview only; the service
                // overwrites it from the LineItems collection anyway.
                Price          = LineItemsTotal,
                DiscountAmount = ParsedDiscount,
                TaxAmount      = ComputedTax,
                AdvancePaid    = ParsedAdvance,
                PaymentMethod  = SelectedPaymentMethod,
                LineItems      = LineItems
                    .Where(r => !string.IsNullOrWhiteSpace(r.Description) || r.LineTotal > 0)
                    .Select((r, i) => new InvoiceLineItem
                    {
                        ForWhom     = (r.ForWhom     ?? string.Empty).Trim(),
                        Description = (r.Description ?? string.Empty).Trim(),
                        Quantity    = r.Quantity <= 0 ? 1 : r.Quantity,
                        UnitPrice   = r.UnitPrice,
                        SortIndex   = i
                    }).ToList(),
                Shirt = new ShirtMeasurement
                {
                    Length   = ToIn(ShirtLength), Chest    = ToIn(ShirtChest),
                    Waist    = ToIn(ShirtWaist),  Hip      = ToIn(ShirtHip),
                    Shoulder = ToIn(ShirtShoulder), Sleeve = ToIn(ShirtSleeve),
                    Cuff     = ToIn(ShirtCuff),   Collar   = ToIn(ShirtCollar)
                },
                Pant = new PantMeasurement
                {
                    Length = ToIn(PantLength), Waist  = ToIn(PantWaist),
                    Hip    = ToIn(PantHip),   Thigh  = ToIn(PantThigh),
                    Ankle  = ToIn(PantAnkle), Knee   = ToIn(PantKnee),
                    Seat   = ToIn(PantSeat)
                }
            };

            await _measurementService.SaveOrderAsync(order);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            // Surface the DB / validation failure as a friendly alert instead of
            // letting it bubble up and crash the app. The original stack trace
            // is still written to LocalAppData\TapeTracker\logs\unhandled.log by
            // the global handler when DEBUG builds attach a debugger.
            await Shell.Current.DisplayAlert(
                "Save Failed",
                ex.InnerException?.Message ?? ex.Message,
                "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");

    private decimal ToIn(string s) => decimal.TryParse(s, out var v) ? _unitService.ToInches(v) : 0m;
    private static string Fmt(decimal v) => v == 0 ? string.Empty : v.ToString("0.##");
}

/// <summary>
/// A single editable row inside <see cref="MeasurementFormViewModel.LineItems"/>.
/// Kept as its own observable class so XAML two-way bindings on Qty / Rate /
/// Description work per-row inside a BindableLayout without requiring a custom
/// converter or a MessageBus hop.
/// </summary>
public partial class LineItemRow : ObservableObject
{
    /// <summary>Optional — who the item is for. Blank ⇒ primary customer.</summary>
    [ObservableProperty] public partial string ForWhom     { get; set; } = string.Empty;

    /// <summary>Garment / service description — e.g. "Shirt", "Pant", "Kurta".</summary>
    [ObservableProperty] public partial string Description { get; set; } = string.Empty;

    /// <summary>Integer count. Bound directly to an Entry with Keyboard=Numeric;
    /// invalid input leaves the underlying value at 1 (minimum meaningful qty).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotalDisplay))]
    public partial int Quantity { get; set; } = 1;

    /// <summary>String-bound rate so blank entry doesn't force "0". Parsed
    /// lazily inside <see cref="LineTotal"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotalDisplay))]
    public partial string UnitPriceText { get; set; } = string.Empty;

    /// <summary>Parsed unit price. 0 when the entry is blank or garbage.</summary>
    public decimal UnitPrice =>
        decimal.TryParse(UnitPriceText, out var v) ? v : 0m;

    /// <summary>Qty × Rate, live-computed. Bound as decimal so the parent VM
    /// can sum this directly without re-parsing.</summary>
    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>Pre-formatted string for the row's right-aligned amount cell.
    /// Currency prefix is intentionally omitted — the column header already
    /// says "Amount" and the org's currency is displayed once in the totals
    /// strip below.</summary>
    public string  LineTotalDisplay => LineTotal.ToString("0.##");
}
