using TapeTracker.Models;

namespace TapeTracker.Services;

/// <summary>
/// Plug-in interface for AI measurement suggestions.
/// Register NullMeasurementAssistant for v1; swap in a local or cloud LLM later.
/// </summary>
public interface IMeasurementAssistant
{
    Task<ShirtMeasurement> SuggestShirtAsync(string customerNotes);
    Task<PantMeasurement> SuggestPantAsync(string customerNotes);
}

public class NullMeasurementAssistant : IMeasurementAssistant
{
    public Task<ShirtMeasurement> SuggestShirtAsync(string customerNotes)
        => Task.FromResult(new ShirtMeasurement());

    public Task<PantMeasurement> SuggestPantAsync(string customerNotes)
        => Task.FromResult(new PantMeasurement());
}
