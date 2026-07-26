namespace TapeTracker.Services;

/// <summary>
/// Manages an optional 4-digit PIN that locks the app on startup.
/// PIN is stored as a SHA-256 hash in Preferences (no plain-text storage).
/// </summary>
public interface IPinService
{
    bool IsPinEnabled { get; }
    bool VerifyPin(string pin);
    void SetPin(string pin);
    void ClearPin();
}
