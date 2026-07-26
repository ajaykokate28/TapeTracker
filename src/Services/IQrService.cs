namespace TapeTracker.Services;

public interface IQrService
{
    /// <summary>Generates a QR code PNG and returns it as an ImageSource.</summary>
    ImageSource GenerateOrderQr(string customerName, string orderNumber, string status, DateTime date);
}
