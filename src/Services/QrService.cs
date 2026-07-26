using QRCoder;
using SkiaSharp;

namespace TapeTracker.Services;

public class QrService : IQrService
{
    public ImageSource GenerateOrderQr(
        string customerName,
        string orderNumber,
        string status,
        DateTime date)
    {
        var content = $"TapeTracker Order\nCustomer: {customerName}\nOrder: {orderNumber}\nStatus: {status}\nDate: {date:dd/MM/yyyy}";

        var bytes = GeneratePng(content, moduleSize: 10);
        return ImageSource.FromStream(() => new MemoryStream(bytes));
    }

    private static byte[] GeneratePng(string content, int moduleSize = 10)
    {
        // Generate QR bit matrix using QRCoder (pure logic, no rendering)
        var qrGenerator = new QRCodeGenerator();
        var qrData      = qrGenerator.CreateQrCode(
            content,
            QRCodeGenerator.ECCLevel.Q,
            forceUtf8: true);

        var matrix       = qrData.ModuleMatrix;
        int modules      = matrix.Count;
        int margin       = 2;    // quiet-zone in modules
        int totalModules = modules + margin * 2;
        int px           = totalModules * moduleSize;

        // Render to SkiaSharp bitmap (SkiaSharp is already in the project)
        using var bitmap = new SKBitmap(px, px);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.White);

        using var darkPaint = new SKPaint { Color = SKColors.Black, IsAntialias = false };

        for (int row = 0; row < modules; row++)
        {
            for (int col = 0; col < modules; col++)
            {
                if (matrix[row][col])
                {
                    float x = (margin + col) * moduleSize;
                    float y = (margin + row) * moduleSize;
                    canvas.DrawRect(x, y, moduleSize, moduleSize, darkPaint);
                }
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data  = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
