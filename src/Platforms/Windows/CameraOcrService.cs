using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using TapeTracker.Services;

namespace TapeTracker.Platforms.Windows;

public class CameraOcrService : ICameraOcrService
{
    public async Task<string?> CaptureAndRecognizeAsync()
    {
        // Windows desktop has no camera MediaPicker — fall back to file picker
        return await PickAndRecognizeAsync();
    }

    public async Task<string?> PickAndRecognizeAsync()
    {
        var photo = await MediaPicker.Default.PickPhotoAsync();
        return photo is null ? null : await RecognizeAsync(photo);
    }

    private static async Task<string?> RecognizeAsync(FileResult photo)
    {
        await using var netStream = await photo.OpenReadAsync();

        // Copy to WinRT IRandomAccessStream
        var memStream = new InMemoryRandomAccessStream();
        var outputStream = memStream.GetOutputStreamAt(0);
        await RandomAccessStream.CopyAsync(netStream.AsInputStream(), outputStream);
        await outputStream.FlushAsync();
        memStream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(memStream);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
                     ?? OcrEngine.TryCreateFromLanguage(new Language("en"));

        if (engine is null) return null;

        var result = await engine.RecognizeAsync(softwareBitmap);
        return result?.Text;
    }
}
