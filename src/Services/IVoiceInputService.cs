namespace TapeTracker.Services;

/// <summary>
/// Cross-platform contract for one-shot speech-to-text dictation. Kept
/// deliberately small — the caller supplies no grammar, so the implementation
/// is free to use whichever OS-native dictation API (Windows.Media.SpeechRecognition,
/// Android SpeechRecognizer, iOS SFSpeechRecognizer) fits the platform.
///
/// The single method returns:
///   ● The recognized text on success
///   ● <c>null</c> when the user cancelled, denied microphone permission,
///     or the platform doesn't ship a usable STT engine (unsupported Windows
///     N-editions, offline-language-pack missing, etc.). Callers should
///     interpret null as "silent no-op, tell the user why" and NOT retry.
///
/// Non-Windows platforms currently fall back to <see cref="NullVoiceInputService"/>
/// which always returns null — keeps the DI graph resolvable without an
/// ifdef in every view-model.
/// </summary>
public interface IVoiceInputService
{
    /// <summary>
    /// Show a system dictation prompt, capture a single utterance, and
    /// return the transcript. Always resolves — errors surface as null
    /// so the caller can display a soft "voice not available" hint.
    /// </summary>
    Task<string?> DictateAsync();

    /// <summary>Cheap capability probe used to hide the mic button on
    /// platforms where dictation isn't wired up.</summary>
    bool IsSupported { get; }
}

/// <summary>
/// No-op fallback registered on platforms without a native STT service.
/// Prevents <c>MeasurementFormViewModel</c> from needing a nullable
/// <see cref="IVoiceInputService"/> or a conditional constructor.
/// </summary>
public sealed class NullVoiceInputService : IVoiceInputService
{
    public bool IsSupported => false;
    public Task<string?> DictateAsync() => Task.FromResult<string?>(null);
}
