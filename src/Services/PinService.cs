using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TapeTracker.Data;
using TapeTracker.Models;

namespace TapeTracker.Services;

/// <summary>
/// Per-user 4-digit PIN store. The PIN never lives outside the authenticated
/// session — <see cref="IAuthService.CurrentUser"/> owns the identity, and
/// the hash is persisted on the User row so it works even after the app is
/// closed and reopened.
///
/// Legacy pre-v2.0 databases stored a single global PIN in Preferences; the
/// public API keeps working during the transition (when no user is signed in,
/// we simply return "no PIN") so the app doesn't crash while the login screen
/// is showing.
/// </summary>
public class PinService : IPinService
{
    private readonly IAuthService         _auth;
    private readonly IServiceScopeFactory _scopeFactory;

    public PinService(IAuthService auth, IServiceScopeFactory scopeFactory)
    {
        _auth         = auth;
        _scopeFactory = scopeFactory;
    }

    public bool IsPinEnabled
        => !string.IsNullOrEmpty(_auth.CurrentUser?.PinHash);

    public bool VerifyPin(string pin)
    {
        var user = _auth.CurrentUser;
        if (user is null || string.IsNullOrEmpty(user.PinHash))
            return true;   // no PIN set → treat as unlocked
        return Hash(pin) == user.PinHash;
    }

    public void SetPin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin) || pin.Length != 4 || !pin.All(char.IsDigit))
            throw new ArgumentException("PIN must be exactly 4 digits.");

        var user = _auth.CurrentUser
            ?? throw new InvalidOperationException("Cannot set PIN without an authenticated user.");

        using var scope = _scopeFactory.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();
        var row = db.Users.First(u => u.Id == user.Id);
        row.PinHash = Hash(pin);
        db.SaveChanges();

        // Keep the in-memory session copy in sync so IsPinEnabled reflects
        // the change without waiting for a re-login.
        user.PinHash = row.PinHash;

        _ = LogPinChangeAsync("Set unlock PIN", user.Id);
    }

    public void ClearPin()
    {
        var user = _auth.CurrentUser;
        if (user is null) return;

        using var scope = _scopeFactory.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();
        var row = db.Users.First(u => u.Id == user.Id);
        row.PinHash = null;
        db.SaveChanges();

        user.PinHash = null;

        _ = LogPinChangeAsync("Removed unlock PIN", user.Id);
    }

    /// <summary>
    /// Fire-and-forget audit call. We resolve <see cref="IAuditService"/>
    /// lazily via <see cref="App.IPocProvider"/> because both PinService and
    /// AuditService are singletons and neither should depend on the other at
    /// construction — a null-safe locator here dodges the DI cycle entirely.
    /// </summary>
    private static async Task LogPinChangeAsync(string summary, int userId)
    {
        try
        {
            var audit = App.IPocProvider?.GetService(typeof(IAuditService)) as IAuditService;
            if (audit is null) return;
            await audit.LogAsync(AuditAction.PinChanged, summary, "User", userId);
        }
        catch { /* logging is best-effort */ }
    }

    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
