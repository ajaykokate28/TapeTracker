using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

public partial class AllCustomersViewModel : BaseViewModel
{
    private readonly IMeasurementService _measurementService;
    private readonly IExportService _exportService;
    private readonly IUnitPreferenceService _unitService;

    public ObservableCollection<Customer> Customers { get; } = new();

    /// <summary>Distinct family / group tags used across the customer table.</summary>
    public ObservableCollection<string> AvailableTags { get; } = new();

    /// <summary>Items for the tag-filter dropdown. Always starts with the localized "All".</summary>
    public ObservableCollection<string> TagFilterOptions { get; } = new() { LocalizationService.Current.AllLabel };

    /// <summary>Localized "no filter" label — kept as a static ref for the clear handler.</summary>
    public static string AllTagsOption => LocalizationService.Current.AllLabel;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Currently selected item in the tag dropdown.</summary>
    [ObservableProperty]
    public partial string SelectedTagOption { get; set; } = LocalizationService.Current.AllLabel;

    // Set true while rebuilding TagFilterOptions so the Picker's binding
    // resetting SelectedItem to null doesn't trigger a spurious filter change.
    private bool _suppressTagOptionChange;

    partial void OnSelectedTagOptionChanged(string value)
    {
        if (_suppressTagOptionChange) return;
        ActiveTag = string.IsNullOrEmpty(value)
                    || value == LocalizationService.Current.AllLabel
                    || value == "All"
            ? string.Empty : value;
    }

    /// <summary>Selected tag filter. Empty = show all customers.</summary>
    [ObservableProperty]
    public partial string ActiveTag { get; set; } = string.Empty;

    partial void OnActiveTagChanged(string value) => _ = LoadAsync();

    [ObservableProperty]
    public partial OrderStatus? StatusFilter { get; set; }

    partial void OnStatusFilterChanged(OrderStatus? value)
    {
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(IsReceivedSelected));
        OnPropertyChanged(nameof(IsCuttingSelected));
        OnPropertyChanged(nameof(IsStitchingSelected));
        OnPropertyChanged(nameof(IsReadySelected));
        OnPropertyChanged(nameof(IsDeliveredSelected));
        _ = LoadAsync();
    }

    public bool IsAllSelected       => StatusFilter == null;
    public bool IsReceivedSelected  => StatusFilter == OrderStatus.Received;
    public bool IsCuttingSelected   => StatusFilter == OrderStatus.Cutting;
    public bool IsStitchingSelected => StatusFilter == OrderStatus.Stitching;
    public bool IsReadySelected     => StatusFilter == OrderStatus.Ready;
    public bool IsDeliveredSelected => StatusFilter == OrderStatus.Delivered;

    public AllCustomersViewModel(IMeasurementService measurementService, IExportService exportService, IUnitPreferenceService unitService)
    {
        _measurementService = measurementService;
        _exportService = exportService;
        _unitService = unitService;
        Title = "All Customers";

        LocalizationService.Current.PropertyChanged += (_, _) => RelabelAllOption();
    }

    private void RelabelAllOption()
    {
        if (TagFilterOptions.Count == 0) return;
        var newLabel = LocalizationService.Current.AllLabel;
        var wasAllSelected = string.IsNullOrEmpty(ActiveTag);

        _suppressTagOptionChange = true;
        try { TagFilterOptions[0] = newLabel; }
        finally { _suppressTagOptionChange = false; }

        if (wasAllSelected) SelectedTagOption = newLabel;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var tagFilter = string.IsNullOrWhiteSpace(ActiveTag) ? null : ActiveTag;
            var list = await _measurementService.GetAllCustomersAsync(
                SearchText, statusFilter: StatusFilter, tagFilter: tagFilter);
            Customers.Clear();
            foreach (var c in list)
                Customers.Add(c);

            await RefreshAvailableTagsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshAvailableTagsAsync()
    {
        try
        {
            var tags = await _measurementService.GetDistinctTagsAsync();

            // Bail out when nothing changed — otherwise clearing the Picker's
            // ItemsSource nulls SelectedItem, which racing the two-way binding
            // wipes out the active tag on every reload.
            if (AvailableTags.SequenceEqual(tags)) return;

            AvailableTags.Clear();
            foreach (var t in tags) AvailableTags.Add(t);

            var previouslySelected = SelectedTagOption;
            var allLabel = LocalizationService.Current.AllLabel;
            _suppressTagOptionChange = true;
            try
            {
                TagFilterOptions.Clear();
                TagFilterOptions.Add(allLabel);
                foreach (var t in tags) TagFilterOptions.Add(t);
            }
            finally
            {
                _suppressTagOptionChange = false;
            }

            SelectedTagOption = TagFilterOptions.Contains(previouslySelected)
                ? previouslySelected
                : allLabel;
        }
        catch { /* best-effort */ }
    }

    [RelayCommand]
    private async Task SearchAsync() => await LoadAsync();

    [RelayCommand]
    private void FilterByStatus(string? statusStr)
    {
        if (string.IsNullOrEmpty(statusStr) || !Enum.TryParse<OrderStatus>(statusStr, out var s))
            StatusFilter = null;
        else
            StatusFilter = s;
    }


    [RelayCommand]
    private async Task DeleteAsync(Customer customer)
    {
        if (!await RequirePermissionAsync(Auth.CanDeleteOrders)) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Delete", $"Delete {customer.Name} and all their orders?", "Yes", "No");
        if (!confirm) return;

        await _measurementService.DeleteCustomerAsync(customer.Id);
        Customers.Remove(customer);
    }

    [RelayCommand]
    private async Task ViewDetailAsync(Customer customer)
    {
        await Shell.Current.GoToAsync($"CustomerDetailPage?customerId={customer.Id}");
    }

    [RelayCommand]
    private async Task ExportAllToExcelAsync()
    {
        if (!await RequirePermissionAsync(Auth.CanExportOrBackup)) return;
        if (Customers.Count == 0)
        {
            await Shell.Current.DisplayAlert("No Data", "Load customers first before exporting.", "OK");
            return;
        }
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _exportService.ExportAllToExcelAsync(Customers, _unitService);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Export Failed", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
