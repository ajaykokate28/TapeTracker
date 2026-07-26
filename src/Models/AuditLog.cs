namespace TapeTracker.Models;

/// <summary>
/// A single event in the shop's activity feed. Rows are append-only — nothing
/// ever mutates or deletes an <see cref="AuditLog"/>, so the trail is trustworthy.
///
/// The design keeps the schema minimal on purpose: the <see cref="EntityType"/>
/// / <see cref="EntityId"/> pair is a loose reference (no FK) so we can point at
/// any table without paying migration cost every time a new logged action shows up.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }

    /// <summary>Owning organization. Every query filters by this so activity
    /// never bleeds across shops sharing a workstation.</summary>
    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    /// <summary>User who performed the action. Nullable so failed-login and
    /// system-triggered events (schema migrations, backfills) can still log.</summary>
    public int?  UserId { get; set; }
    public User? User   { get; set; }

    /// <summary>Snapshot of the actor's display name at the time of the event.
    /// Persisted so the feed keeps rendering correctly even if the user is later
    /// renamed or deactivated.</summary>
    public string UserDisplayName { get; set; } = string.Empty;

    public AuditAction Action    { get; set; }
    public DateTime    Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Kind of thing the event is about ("Customer", "Order", "User", …).
    /// Empty for events that don't reference a specific row (e.g. logout).</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Row id of the referenced entity. Zero when not applicable.</summary>
    public int EntityId { get; set; }

    /// <summary>Free-form English sentence explaining what happened. Rendered
    /// as-is in the feed and pre-composed by the caller so the UI doesn't need
    /// to reflect internal enum values into human text.</summary>
    public string Summary { get; set; } = string.Empty;
}
