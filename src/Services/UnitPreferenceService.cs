namespace TapeTracker.Services;

public class UnitPreferenceService : IUnitPreferenceService
{
    private const string PreferenceKey = "measurement_unit";
    private const decimal InchToCm = 2.54m;

    public event EventHandler? UnitChanged;

    public MeasurementUnit CurrentUnit
    {
        get
        {
            var saved = Preferences.Default.Get(PreferenceKey, "inch");
            return saved == "cm" ? MeasurementUnit.Cm : MeasurementUnit.Inch;
        }
    }

    public string UnitLabel => CurrentUnit == MeasurementUnit.Cm ? "cm" : "in";

    public void SetUnit(MeasurementUnit unit)
    {
        Preferences.Default.Set(PreferenceKey, unit == MeasurementUnit.Cm ? "cm" : "inch");
        UnitChanged?.Invoke(this, EventArgs.Empty);
    }

    public decimal ToDisplay(decimal inches)
        => CurrentUnit == MeasurementUnit.Cm
            ? Math.Round(inches * InchToCm, 2)
            : Math.Round(inches, 2);

    public decimal ToInches(decimal displayValue)
        => CurrentUnit == MeasurementUnit.Cm
            ? Math.Round(displayValue / InchToCm, 4)
            : displayValue;
}
