namespace TapeTracker.Models;

public class PantMeasurement
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    // All values stored in inches
    public decimal Length { get; set; }
    public decimal Waist { get; set; }
    public decimal Hip { get; set; }
    public decimal Thigh { get; set; }
    public decimal Ankle { get; set; }
    public decimal Knee { get; set; }
    public decimal Seat { get; set; }
}

