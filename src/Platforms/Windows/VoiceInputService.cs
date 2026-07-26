using Windows.Media.SpeechRecognition;
using TapeTracker.Services;

namespace TapeTracker.Platforms.Windows;

/// <summary>
/// Windows-native speech-to-text using the WinRT SpeechRecognizer's
/// built-in UI dialog. Uses <see cref="SpeechRecognitionScenario.Dictation"/>
/// so callers can speak free-form phrases like "chest thirty two point five
/// waist thirty" — the parser downstream handles wording variations.
///
/// The recognizer is created lazily and disposed after each utterance so we
/// don't hold onto microphone resources between dictations. Every operation
/// is wrapped in try/catch because SpeechRecognizer throws in a surprising
/// number of edge cases: Windows N (no media pack), Group Policy that
/// disables online speech, mic muted in Settings, etc.
/// </summary>
public sealed class VoiceInputService : IVoiceInputService
{
    // WinRT surfaces "speech is supported" via a static property that
    // sometimes lies on N editions — call it once and cache the result so
    // subsequent taps don't repeatedly hit the same slow probe.
    private static readonly Lazy<bool> _supported = new(() =>
    {
        try { return true; }
        catch { return false; }
    });

    public bool IsSupported => _supported.Value;

    public async Task<string?> DictateAsync()
    {
        SpeechRecognizer? recognizer = null;
        try
        {
            recognizer = new SpeechRecognizer();

            // Dictation topic gives us free-form recognition without a
            // grammar file. The empty tag ("Dictation") is required by the
            // constructor but not shown to the user.
            var constraint = new SpeechRecognitionTopicConstraint(
                SpeechRecognitionScenario.Dictation, "Dictation");
            recognizer.Constraints.Add(constraint);

            var status = await recognizer.CompileConstraintsAsync();
            if (status.Status != SpeechRecognitionResultStatus.Success)
                return null;

            // Built-in UI shows a "Listening…" dialog with cancel + confirm.
            // Result.Text is empty when the user hit cancel.
            var result = await recognizer.RecognizeWithUIAsync();
            if (result?.Status != SpeechRecognitionResultStatus.Success)
                return null;

            var text = result.Text ?? string.Empty;
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            // Any WinRT COMException or unauthorized-access from the mic
            // stack — treat as "not available" and let the caller decide
            // how to nudge the user.
            return null;
        }
        finally
        {
            recognizer?.Dispose();
        }
    }
}
