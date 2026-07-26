namespace TapeTracker.Services;

/// <summary>
/// Captures a photo (or picks from gallery) and returns the OCR-extracted text.
/// Android: ML Kit on-device. Windows: Windows.Media.Ocr. Both are free / offline.
/// </summary>
public interface ICameraOcrService
{
    /// <summary>
    /// Opens the camera, captures a photo, runs OCR and returns the raw text.
    /// Returns null if the user cancelled.
    /// </summary>
    Task<string?> CaptureAndRecognizeAsync();

    /// <summary>
    /// Picks an existing image from the gallery, runs OCR and returns the raw text.
    /// Returns null if the user cancelled.
    /// </summary>
    Task<string?> PickAndRecognizeAsync();
}
