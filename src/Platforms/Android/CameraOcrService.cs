#if ANDROID
using Android.Gms.Extensions;
using Android.Graphics;
using Google.MLKit.Vision.Common;
using Google.MLKit.Vision.Text;
using TapeTracker.Services;

namespace TapeTracker.Platforms.Android;

public class CameraOcrService : ICameraOcrService
{
    public async Task<string?> CaptureAndRecognizeAsync()
    {
        var photo = await MediaPicker.Default.CapturePhotoAsync();
        return photo is null ? null : await RecognizeAsync(photo);
    }

    public async Task<string?> PickAndRecognizeAsync()
    {
        var photo = await MediaPicker.Default.PickPhotoAsync();
        return photo is null ? null : await RecognizeAsync(photo);
    }

    private static async Task<string?> RecognizeAsync(FileResult photo)
    {
        await using var stream = await photo.OpenReadAsync();
        var bitmap = await BitmapFactory.DecodeStreamAsync(stream);
        if (bitmap is null) return null;

        var image = InputImage.FromBitmap(bitmap, 0);
        var recognizer = TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions);

        var result = await recognizer.Process(image);
        return result?.Text;
    }
}
#endif
