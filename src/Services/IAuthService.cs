using TapeTracker.Models;

namespace TapeTracker.Services;

/// <summary>
/// Owns the authenticated session. Every other service or view model that
/// needs "who is logged in?" or "which org am I in?" should read
/// <see cref="CurrentUser"/> / <see cref="CurrentOrganization"/> instead of
/// caching those values locally, so a logout instantly invalidates every stale
/// reference.
/// </summary>
public interface IAuthService
{
    /// <summary>Currently signed-in user; null when nobody is logged in.</summary>
    User? CurrentUser { get; }

    /// <summary>Organization the current user belongs to; null when logged out.</summary>
    Organization? CurrentOrganization { get; }

    /// <summary>Convenience alias for <c>CurrentUser?.Role == UserRole.Admin</c>.</summary>
    bool IsAdmin { get; }

    /// <summary>True for admins. Employees cannot create, edit, or delete customers.</summary>
    bool CanManageCustomers { get; }

    /// <summary>True for admins. Employees can only advance the status of an assigned order.</summary>
    bool CanEditOrders { get; }

    /// <summary>True for admins. Employees never see the customer/order delete affordances.</summary>
    bool CanDeleteOrders { get; }

    /// <summary>True for admins. Controls visibility of Backup/Restore, Export, and the whole shop dashboard.</summary>
    bool CanExportOrBackup { get; }

    /// <summary>True for admins. Employees see a lean dashboard scoped to their assigned work only.</summary>
    bool CanSeeShopWideDashboard { get; }

    /// <summary>Every authenticated user can advance status of an order they own (all today, stage-scoped in Phase 4).</summary>
    bool CanAdvanceOrderStatus { get; }

    /// <summary>True when at least one Organization exists in the database.</summary>
    Task<bool> AnyOrganizationExistsAsync();

    /// <summary>
    /// Creates a new Organization and its first user (always Admin). Fails if
    /// the org name or username is empty, or if the password is shorter than 4
    /// characters. On success, immediately signs the admin in.
    /// </summary>
    Task<Organization> RegisterOrganizationAsync(
        string orgName,
        string? phone,
        string? address,
        string adminUsername,
        string adminDisplayName,
        string adminPassword);

    /// <summary>
    /// Verifies credentials against the users table. Returns null on failure
    /// (unknown username, wrong password, or inactive user) — the caller should
    /// show a generic "invalid credentials" message so we don't leak which
    /// field was wrong.
    /// </summary>
    Task<User?> LoginAsync(string username, string password);

    /// <summary>Clears the in-memory session. Does not touch the database.</summary>
    void Logout();

    /// <summary>
    /// Fired after <see cref="CurrentUser"/> changes (login or logout).
    /// Views that swap layout by role should re-render here.
    /// </summary>
    event EventHandler? AuthStateChanged;

    // ── Employee management (admin only) ─────────────────────────────────────

    /// <summary>All users in the current organization, ordered by role then name.
    /// Returns an empty list when nobody is signed in.</summary>
    Task<IReadOnlyList<User>> GetEmployeesAsync();

    /// <summary>Fetch a single user by id, scoped to the current organization.
    /// Returns null when the user doesn't exist or belongs to another org.</summary>
    Task<User?> GetEmployeeByIdAsync(int id);

    /// <summary>
    /// Adds a new employee under the current admin's organization. Fails when
    /// the username collides with an existing user in the same org (case-insensitive)
    /// or when the caller isn't an admin.
    /// </summary>
    Task<User> CreateEmployeeAsync(
        string username,
        string displayName,
        string password,
        UserRole role);

    /// <summary>
    /// Updates the non-credential fields of an employee. Password is untouched —
    /// use <see cref="ResetEmployeePasswordAsync"/> for that.
    /// </summary>
    Task UpdateEmployeeAsync(
        int id,
        string displayName,
        UserRole role,
        bool isActive);

    /// <summary>Admin-initiated password reset. Wipes the target user's PIN too
    /// so the next login is a clean start.</summary>
    Task ResetEmployeePasswordAsync(int id, string newPassword);

    // ── Self-service password recovery (Forgot password? on login page) ─────

    /// <summary>True when <see cref="CurrentUser"/> has a recovery question
    /// configured. Bound to the Shop Settings status pill so users can see at
    /// a glance whether they're protected against forgetting their password.</summary>
    bool HasRecoveryQuestion { get; }

    /// <summary>
    /// Look up the plaintext recovery question for a given username without
    /// requiring an authenticated session (this is the login-time recovery
    /// entry point). Returns null if the user doesn't exist, is inactive, or
    /// has never configured a question. Callers should NOT distinguish
    /// between those cases in the UI, to prevent username-enumeration.
    /// </summary>
    Task<string?> GetRecoveryQuestionAsync(string username);

    /// <summary>
    /// Verifies the recovery answer for <paramref name="username"/> and, on
    /// success, replaces the password + clears the PIN. Returns false on any
    /// failure (unknown user, no recovery configured, wrong answer, invalid
    /// new password) — again with a deliberately-vague failure signature to
    /// prevent leaking which check failed.
    /// </summary>
    Task<bool> ResetPasswordWithRecoveryAsync(string username, string answer, string newPassword);

    /// <summary>
    /// Configures / updates the recovery question &amp; answer for the currently
    /// signed-in user. Passing empty strings clears the recovery data
    /// (disables the self-service flow for this account). Requires an
    /// authenticated session.
    /// </summary>
    Task SetRecoveryQuestionAsync(string? question, string? answer);

    // ── Shop settings (Phase 8, admin only) ──────────────────────────────────

    /// <summary>
    /// Persists the shop-branding + billing fields on the current organization.
    /// Also refreshes <see cref="CurrentOrganization"/> so every downstream
    /// consumer (invoice, WhatsApp templates, dashboard) sees the new values
    /// without a re-login. Admin-only.
    /// </summary>
    Task UpdateOrganizationAsync(
        string name,
        string? phone,
        string? address,
        string? gstNumber,
        string? currencySymbol,
        string? invoicePrefix,
        string? logoPath,
        decimal taxRatePct,
        string? ownerName,
        string? email,
        IEnumerable<string>? itemCatalog = null);
}
