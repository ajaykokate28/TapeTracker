using TapeTracker.Models;

namespace TapeTracker.Services;

public interface IPdfService
{
    /// <summary>Generates a PDF bill for the given order and opens the platform share sheet.</summary>
    Task GenerateAndShareAsync(Customer customer, Order order, IUnitPreferenceService unitService);
}
