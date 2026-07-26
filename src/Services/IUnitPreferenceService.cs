namespace TapeTracker.Services;

public enum MeasurementUnit { Inch, Cm }

public interface IUnitPreferenceService
{
    MeasurementUnit CurrentUnit { get; }
    void SetUnit(MeasurementUnit unit);
    decimal ToDisplay(decimal inches);
    decimal ToInches(decimal displayValue);
    string UnitLabel { get; }
    event EventHandler UnitChanged;
}
