namespace TapeTracker.Models;

/// <summary>
/// A person who logs into the app. The first user of a fresh organization is
/// created as <see cref="UserRole.Admin"/> during the org-registration flow;
/// subsequent employees are added by that admin. Password is stored as a
/// PBKDF2-SHA256 hash with a per-user salt (never in plain text).
/// </summary>
public class User
{
    public int    Id             { get; set; }
    public int    OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    /// <summary>Login handle. Unique per Organization, case-insensitive.</summary>
    public string Username     { get; set; } = string.Empty;

    /// <summary>Human-friendly name shown on cards, dashboards, and assignments.</summary>
    public string DisplayName  { get; set; } = string.Empty;

    /// <summary>Base64-encoded PBKDF2 hash of the password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Base64-encoded per-user random salt used with the password hash.</summary>
    public string PasswordSalt { get; set; } = string.Empty;

    public UserRole Role       { get; set; } = UserRole.Employee;

    /// <summary>
    /// Optional per-user PIN for fast in-session unlock after the initial
    /// username/password login. Stored as a SHA-256 hash (small entropy space
    /// so PBKDF2 would just add latency without meaningful hardening).
    /// </summary>
    public string?  PinHash    { get; set; }

    /// <summary>Soft-disable an employee without deleting historic assignments.</summary>
    public bool     IsActive   { get; set; } = true;

    /// <summary>
    /// Optional plaintext recovery prompt shown on the "Forgot password?" flow —
    /// e.g. "Mother's maiden name?" or "Shop opening year?". Stored as-typed
    /// so users see back exactly what they wrote. When null/empty, the
    /// self-service reset path is disabled for this account (the user must
    /// ask an admin to reset via <see cref="Services.IAuthService.ResetEmployeePasswordAsync"/>).
    /// </summary>
    public string?  RecoveryQuestion   { get; set; }

    /// <summary>
    /// SHA-256 hash of the normalised recovery answer (trimmed + lowercased,
    /// no salt). Salting adds no meaningful hardening here because the
    /// answer space is tiny compared to a password and verification runs
    /// only against a single account at a time; keeping the shape simple
    /// lets us avoid another column pair.
    /// </summary>
    public string?  RecoveryAnswerHash { get; set; }

    public DateTime  CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
