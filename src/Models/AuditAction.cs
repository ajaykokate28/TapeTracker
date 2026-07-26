namespace TapeTracker.Models;

/// <summary>
/// High-level classification of an event captured in the audit trail.
/// Kept intentionally coarse — the specific "what" lives in
/// <see cref="AuditLog.Summary"/> as a human-readable sentence. This enum
/// exists purely for filtering and for colour-coding pills in the UI.
/// </summary>
public enum AuditAction
{
    // ── Authentication ───────────────────────────────────────────────
    Login             = 0,
    LoginFailed       = 1,
    Logout            = 2,

    // ── Organization / Users ─────────────────────────────────────────
    OrganizationCreated = 10,
    UserCreated         = 11,
    UserUpdated         = 12,
    UserDeactivated     = 13,
    UserActivated       = 14,
    UserPasswordReset   = 15,
    PinChanged          = 16,

    // ── Customers ────────────────────────────────────────────────────
    CustomerCreated   = 20,
    CustomerUpdated   = 21,
    CustomerDeleted   = 22,
    CustomerRestored  = 23,

    // ── Orders ───────────────────────────────────────────────────────
    OrderCreated      = 30,
    OrderUpdated      = 31,
    OrderDeleted      = 32,
    OrderRestored     = 33,
    OrderStatusChanged = 34,

    // ── Stage assignments (Phase 4) ──────────────────────────────────
    StageAssigned     = 40,
    StageUnassigned   = 41,
    StageCompleted    = 42,

    // ── Data operations ──────────────────────────────────────────────
    Exported          = 50,
    Backup            = 51,
    Restore           = 52,

    // ── Shop settings & payments (Phase 8) ───────────────────────────
    OrganizationUpdated = 60,
    PaymentRecorded     = 61
}
