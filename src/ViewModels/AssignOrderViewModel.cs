using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Models;
using TapeTracker.Services;

namespace TapeTracker.ViewModels;

/// <summary>
/// One row per stage (Cutting / Stitching / Ready) on the assignment page.
/// Owns its own selected user so the picker binds cleanly and the parent
/// view model can flush all changes in a single save.
/// </summary>
public partial class StageAssignmentRow : ObservableObject
{
    public OrderStatus Stage { get; }
    public string      StageLabel { get; }

    /// <summary>All active employees (+ an "Unassigned" sentinel) shown in the picker.</summary>
    public IReadOnlyList<EmployeeChoice> Employees { get; }

    /// <summary>Currently picked employee. Never null — "Unassigned" is a first-class choice.</summary>
    [ObservableProperty]
    public partial EmployeeChoice SelectedEmployee { get; set; }

    /// <summary>True when this stage already has a completed assignment.
    /// Renders a small "Done" badge on the row so admins can see progress.</summary>
    [ObservableProperty]
    public partial bool IsCompleted { get; set; }

    public StageAssignmentRow(
        OrderStatus stage,
        IReadOnlyList<EmployeeChoice> employees,
        EmployeeChoice initialSelection,
        bool isCompleted)
    {
        Stage            = stage;
        StageLabel       = LocalizationService.Current.LocalizeStatus(stage);
        Employees        = employees;
        SelectedEmployee = initialSelection;
        IsCompleted      = isCompleted;
    }
}

/// <summary>Picker item — either a real user or the "Unassigned" sentinel (Id = 0).</summary>
public class EmployeeChoice
{
    public int    Id          { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public bool   IsUnassigned => Id == 0;
    public override string ToString() => DisplayName;
}

[QueryProperty(nameof(OrderId), "orderId")]
public partial class AssignOrderViewModel : BaseViewModel
{
    private readonly IMeasurementService _measurementService;
    private readonly IAuthService        _authService;

    private int _orderId;
    public int OrderId
    {
        get => _orderId;
        set
        {
            _orderId = value;
            _ = LoadAsync();
        }
    }

    [ObservableProperty] public partial string OrderNumber  { get; set; } = string.Empty;
    [ObservableProperty] public partial string CustomerName { get; set; } = string.Empty;
    [ObservableProperty] public partial string DueDateLabel { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusLabel  { get; set; } = string.Empty;

    [ObservableProperty] public partial bool HasEmployees   { get; set; }
    [ObservableProperty] public partial bool HasError       { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>One row per assignable stage. Order matches the natural
    /// pipeline flow so the UI reads top-to-bottom.</summary>
    public ObservableCollection<StageAssignmentRow> Stages { get; } = new();

    public AssignOrderViewModel(
        IMeasurementService measurementService,
        IAuthService        authService)
    {
        _measurementService = measurementService;
        _authService        = authService;
        Title = LocalizationService.Current.AssignOrderTitle;
    }

    private async Task LoadAsync()
    {
        if (_orderId <= 0 || IsBusy) return;
        IsBusy = true;
        try
        {
            HasError     = false;
            ErrorMessage = string.Empty;
            Stages.Clear();

            var order = await _measurementService.GetOrderByIdAsync(_orderId);
            if (order is null) return;

            OrderNumber  = order.OrderNumber;
            CustomerName = order.Customer?.Name ?? string.Empty;
            DueDateLabel = order.DueDate.HasValue
                ? order.DueDate.Value.ToString("dd MMM yyyy")
                : LocalizationService.Current.NoDueDateLabel;
            StatusLabel  = LocalizationService.Current.LocalizeStatus(order.Status);

            var employees = await _authService.GetEmployeesAsync();
            var pickerList = new List<EmployeeChoice>
            {
                new() { Id = 0, DisplayName = LocalizationService.Current.UnassignedLabel }
            };
            pickerList.AddRange(employees
                .Where(u => u.IsActive)
                .Select(u => new EmployeeChoice { Id = u.Id, DisplayName = u.DisplayName }));

            // Show the empty-state warning if the shop has zero non-admin
            // employees available — nothing to pick from.
            HasEmployees = pickerList.Count > 1;

            var existing = await _measurementService.GetAssignmentsForOrderAsync(_orderId);

            // Assignable stages = every stage after Received and before Delivered.
            // Received is the initial "just came in" state, Delivered is the
            // hand-off — neither has an owning tailor.
            var assignable = new[]
            {
                OrderStatus.Cutting,
                OrderStatus.Stitching,
                OrderStatus.Ready
            };

            foreach (var stage in assignable)
            {
                var current = existing.FirstOrDefault(a => a.Stage == stage);
                var selected = current?.AssignedUserId is int uid
                    ? pickerList.FirstOrDefault(p => p.Id == uid) ?? pickerList[0]
                    : pickerList[0];
                Stages.Add(new StageAssignmentRow(
                    stage,
                    pickerList,
                    selected,
                    isCompleted: current?.CompletedAt is not null));
            }
        }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!await RequirePermissionAsync(Auth.CanManageCustomers)) return;
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            HasError     = false;
            ErrorMessage = string.Empty;
            foreach (var row in Stages)
            {
                int? userId = row.SelectedEmployee.IsUnassigned
                    ? (int?)null
                    : row.SelectedEmployee.Id;
                await _measurementService.AssignOrderStageAsync(_orderId, row.Stage, userId);
            }
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}
