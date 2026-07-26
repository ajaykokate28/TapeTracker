using TapeTracker.Models;

namespace TapeTracker.Services;

/// <summary>
/// Append-only activity trail. Every meaningful state change in the app —
/// login, customer save, order status change, employee CRUD, stage assignment
/// — funnels through here so admins have a single, authoritative "who did
/// what and when" view.
///
/// Implementations MUST swallow all exceptions internally. Logging is
/// best-effort — a failing audit call must never break a user's workflow.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Records an event for the currently signed-in user (uses
    /// <see cref="IAuthService.CurrentUser"/>). For pre-authentication events
    /// (login failure, first-run org creation) use <see cref="LogForAsync"/>.
    /// </summary>
    Task LogAsync(
        AuditAction action,
        string summary,
        string entityType = "",
        int    entityId   = 0);

    /// <summary>
    /// Records an event stamped with an explicit user + organization — used
    /// during login attempts (where <see cref="IAuthService.CurrentUser"/> is
    /// still null) and during first-run org creation.
    /// </summary>
    Task LogForAsync(
        int?   userId,
        string userDisplayName,
        int    organizationId,
        AuditAction action,
        string summary,
        string entityType = "",
        int    entityId   = 0);

    /// <summary>
    /// Returns the most-recent events for the current organization. Optional
    /// filters keep the query cheap; <paramref name="take"/> caps the page size
    /// (default 100). Ordered newest-first.
    /// </summary>
    Task<IReadOnlyList<AuditLog>> QueryAsync(
        AuditAction? action  = null,
        int?         userId  = null,
        DateTime?    since   = null,
        int          take    = 100);
}
