namespace TapeTracker.Models;

public class ShirtMeasurement
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    // All values stored in inches
    public decimal Length { get; set; }
    public decimal Chest { get; set; }
    public decimal Waist { get; set; }
    public decimal Hip { get; set; }
    public decimal Shoulder { get; set; }
    public decimal Sleeve { get; set; }
    public decimal Cuff { get; set; }
    public decimal Collar { get; set; }
}

