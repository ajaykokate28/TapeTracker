namespace TapeTracker.Models;

/// <summary>
/// Access level of a <see cref="User"/> inside an <see cref="Organization"/>.
/// Persisted as an int so new roles can be added without a data migration.
/// </summary>
public enum UserRole
{
    /// <summary>Full CRUD on customers, orders, and other users. Assigns work to employees.</summary>
    Admin = 0,

    /// <summary>Can only mark their own assigned order stages as complete.</summary>
    Employee = 1
}
