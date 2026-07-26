using TapeTracker.Models;

namespace TapeTracker.Services;

public interface IExportService
{
    Task ExportAsync(Customer customer, Order order, IUnitPreferenceService unitService);
    Task ExportAllToExcelAsync(IEnumerable<Customer> customers, IUnitPreferenceService unitService);
}
