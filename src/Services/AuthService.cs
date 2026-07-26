using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TapeTracker.Data;
using TapeTracker.Models;

namespace TapeTracker.Services;

/// <summary>
/// Singleton session holder. Uses an <see cref="IServiceScopeFactory"/> to spin
/// up a fresh <see cref="TapeTrackerDbContext"/> per operation so we never share
/// a scoped DbContext across threads.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AuthService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Lazily-resolved audit service. We can't take it in the constructor
    /// because <see cref="AuditService"/> depends on <see cref="IAuthService"/>
    /// (circular singleton graph). Resolving on demand via the app service
    /// provider keeps the two singletons decoupled at construction time.
    /// Returns null if <see cref="App.IPocProvider"/> isn't set yet (e.g. during
    /// bootstrap), in which case the audit call becomes a no-op.
    /// </summary>
    private IAuditService? Audit
    {
        get
        {
            var provider = App.IPocProvider;
            return provider?.GetService(typeof(IAuditService)) as IAuditService;
        }
    }

    private Task LogAsync(AuditAction action, string summary,
        string entityType = "", int entityId = 0)
        => Audit?.LogAsync(action, summary, entityType, entityId) ?? Task.CompletedTask;

    private Task LogForAsync(int? userId, string userDisplayName, int orgId,
        AuditAction action, string summary, string entityType = "", int entityId = 0)
        => Audit?.LogForAsync(userId, userDisplayName, orgId, action, summary, entityType, entityId)
           ?? Task.CompletedTask;

    public User?         CurrentUser         { get; private set; }
    public Organization? CurrentOrganization { get; private set; }

    public bool IsAdmin => CurrentUser?.Role == UserRole.Admin;

    // ── Permission matrix ────────────────────────────────────────────────────
    // Kept as pure functions on the session so every view model reads the same
    // policy. If the rules ever change, we edit one place — nothing else.
    public bool CanManageCustomers      => IsAdmin;
    public bool CanEditOrders           => IsAdmin;
    public bool CanDeleteOrders         => IsAdmin;
    public bool CanExportOrBackup       => IsAdmin;
    public bool CanSeeShopWideDashboard => IsAdmin;
    public bool CanAdvanceOrderStatus   => CurrentUser is not null;

    public bool HasRecoveryQuestion =>
        !string.IsNullOrEmpty(CurrentUser?.RecoveryQuestion) &&
        !string.IsNullOrEmpty(CurrentUser?.RecoveryAnswerHash);

    public event EventHandler? AuthStateChanged;

    public async Task<bool> AnyOrganizationExistsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();
        // AuthService may be the very first service to touch the DB (before
        // MeasurementService/DashboardService are ever resolved), so bootstrap
        // is required here or the Organizations table won't exist yet.
        DbBootstrap.EnsureReady(db);

        // We only consider the app "already registered" when there's an
        // organization that ALSO has at least one user. The pre-v2.0 schema
        // migrator can leave behind a placeholder "My Shop" org with no users
        // (created just to hold orphan customer/order rows). Treating that
        // placeholder as a real registration would strand the user at a login
        // screen they can never satisfy — so we route them to the registration
        // wizard, and RegisterOrganizationAsync's AdoptDefaultShopDataAsync
        // step re-parents the legacy rows to the shop they actually create.
        return await db.Users.AnyAsync();
    }

    public async Task<Organization> RegisterOrganizationAsync(
        string orgName,
        string? phone,
        string? address,
        string adminUsername,
        string adminDisplayName,
        string adminPassword)
    {
        if (string.IsNullOrWhiteSpace(orgName))
            throw new ArgumentException("Organization name is required.", nameof(orgName));
        if (string.IsNullOrWhiteSpace(adminUsername))
            throw new ArgumentException("Admin username is required.", nameof(adminUsername));
        if (adminPassword is null || adminPassword.Length < 4)
            throw new ArgumentException("Password must be at least 4 characters.", nameof(adminPassword));

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();
        DbBootstrap.EnsureReady(db);

        var org = new Organization
        {
            Name      = orgName.Trim(),
            Phone     = string.IsNullOrWhiteSpace(phone)   ? null : phone.Trim(),
            Address   = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();   // need Id for the FK below

        var (hash, salt) = PasswordHasher.Hash(adminPassword);
        var admin = new User
        {
            OrganizationId = org.Id,
            Username       = adminUsername.Trim(),
            DisplayName    = string.IsNullOrWhiteSpace(adminDisplayName)
                              ? adminUsername.Trim()
                              : adminDisplayName.Trim(),
            PasswordHash   = hash,
            PasswordSalt   = salt,
            Role           = UserRole.Admin,
            IsActive       = true,
            CreatedAt      = DateTime.UtcNow,
            LastLoginAt    = DateTime.UtcNow
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        // Auto-adopt any orphan pre-v2.0 rows that the schema migrator parked
        // in the placeholder "My Shop" default org — the very first registered
        // organization becomes the effective owner of that legacy data.
        await AdoptDefaultShopDataAsync(db, org.Id);

        SetSession(admin, org);

        // Explicit LogForAsync (not LogAsync) — the session was set milliseconds
        // ago but we want the org-creation event to always attribute to this admin.
        await LogForAsync(admin.Id, admin.DisplayName, org.Id,
            AuditAction.OrganizationCreated,
            $"Registered shop '{org.Name}' with admin '{admin.DisplayName}'",
            "Organization", org.Id);
        return org;
    }

    public async Task<User?> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return null;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();
        DbBootstrap.EnsureReady(db);

        var uname = username.Trim();
        var user = await db.Users
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u =>
                u.IsActive &&
                EF.Functions.Like(u.Username, uname));   // NOCASE via SQLite collation

        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            // Log the failed attempt (org-scoped only if we know the org — for a
            // completely unknown username we simply skip the audit row to avoid
            // writing to a random org context).
            if (user?.Organization is { } orgFail)
                await LogForAsync(null, uname, orgFail.Id,
                    AuditAction.LoginFailed,
                    $"Failed sign-in attempt for '{uname}'",
                    "User", user.Id);
            return null;
        }

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        SetSession(user, user.Organization!);
        await LogAsync(AuditAction.Login, $"{user.DisplayName} signed in", "User", user.Id);
        return user;
    }

    public void Logout()
    {
        if (CurrentUser is null && CurrentOrganization is null) return;
        // Snapshot the user before we drop the session so the audit row
        // still has an accurate actor.
        var actingUserId  = CurrentUser?.Id;
        var actingName    = CurrentUser?.DisplayName ?? string.Empty;
        var actingOrgId   = CurrentOrganization?.Id ?? 0;

        CurrentUser         = null;
        CurrentOrganization = null;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);

        if (actingOrgId != 0)
            _ = LogForAsync(actingUserId, actingName, actingOrgId,
                AuditAction.Logout,
                $"{actingName} signed out",
                "User", actingUserId ?? 0);
    }

    // ── Employee management ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<User>> GetEmployeesAsync()
    {
        var orgId = CurrentOrganization?.Id ?? 0;
        if (orgId == 0) return Array.Empty<User>();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();
        return await db.Users
            .Where(u => u.OrganizationId == orgId)
            .OrderBy(u => u.Role)          // admins first
            .ThenBy(u => u.DisplayName)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<User?> GetEmployeeByIdAsync(int id)
    {
        var orgId = CurrentOrganization?.Id ?? 0;
        if (orgId == 0) return null;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();
        return await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == orgId);
    }

    public async Task<User> CreateEmployeeAsync(
        string username, string displayName, string password, UserRole role)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));
        if (password is null || password.Length < 4)
            throw new ArgumentException("Password must be at least 4 characters.", nameof(password));

        var orgId = CurrentOrganization!.Id;
        var uname = username.Trim();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();
        DbBootstrap.EnsureReady(db);

        // Case-insensitive uniqueness inside the org — mirrors the DB index and
        // gives the caller a friendly error before the SqliteException fires.
        var exists = await db.Users.AnyAsync(u =>
            u.OrganizationId == orgId && EF.Functions.Like(u.Username, uname));
        if (exists)
            throw new InvalidOperationException($"A user named '{uname}' already exists in this shop.");

        var (hash, salt) = PasswordHasher.Hash(password);
        var user = new User
        {
            OrganizationId = orgId,
            Username       = uname,
            DisplayName    = string.IsNullOrWhiteSpace(displayName) ? uname : displayName.Trim(),
            PasswordHash   = hash,
            PasswordSalt   = salt,
            Role           = role,
            IsActive       = true,
            CreatedAt      = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await LogAsync(AuditAction.UserCreated,
            $"Added {(role == UserRole.Admin ? "admin" : "employee")} '{user.DisplayName}' ({user.Username})",
            "User", user.Id);
        return user;
    }

    public async Task UpdateEmployeeAsync(int id, string displayName, UserRole role, bool isActive)
    {
        RequireAdmin();
        var orgId = CurrentOrganization!.Id;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.Id == id && u.OrganizationId == orgId)
            ?? throw new InvalidOperationException("Employee not found.");

        // Guardrail: never let the last active admin be demoted or deactivated,
        // otherwise the shop locks itself out of admin operations on next login.
        if (user.Role == UserRole.Admin && (role != UserRole.Admin || !isActive))
        {
            var otherActiveAdmins = await db.Users.CountAsync(u =>
                u.OrganizationId == orgId &&
                u.Id != id &&
                u.Role == UserRole.Admin &&
                u.IsActive);
            if (otherActiveAdmins == 0)
                throw new InvalidOperationException(
                    "You can't remove or demote the last active admin. Promote another user first.");
        }

        bool wasActive        = user.IsActive;
        UserRole wasRole      = user.Role;

        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? user.Username : displayName.Trim();
        user.Role        = role;
        user.IsActive    = isActive;
        await db.SaveChangesAsync();

        // Keep the in-memory session copy fresh if the admin edited themselves.
        if (CurrentUser?.Id == user.Id)
        {
            CurrentUser.DisplayName = user.DisplayName;
            CurrentUser.Role        = user.Role;
            CurrentUser.IsActive    = user.IsActive;
        }

        // Emit a distinct event when activation flips so the feed reads more
        // naturally ("Deactivated user X" vs. a generic "Updated user X").
        if (wasActive && !isActive)
            await LogAsync(AuditAction.UserDeactivated,
                $"Deactivated user '{user.DisplayName}'",
                "User", user.Id);
        else if (!wasActive && isActive)
            await LogAsync(AuditAction.UserActivated,
                $"Reactivated user '{user.DisplayName}'",
                "User", user.Id);
        else
            await LogAsync(AuditAction.UserUpdated,
                wasRole != role
                    ? $"Updated user '{user.DisplayName}' ({wasRole} → {role})"
                    : $"Updated user '{user.DisplayName}'",
                "User", user.Id);
    }

    public async Task ResetEmployeePasswordAsync(int id, string newPassword)
    {
        RequireAdmin();
        if (newPassword is null || newPassword.Length < 4)
            throw new ArgumentException("Password must be at least 4 characters.", nameof(newPassword));

        var orgId = CurrentOrganization!.Id;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.Id == id && u.OrganizationId == orgId)
            ?? throw new InvalidOperationException("Employee not found.");

        var (hash, salt) = PasswordHasher.Hash(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        // Clear the PIN too — the old device-side unlock shortcut no longer
        // reflects who owns the account after a password reset.
        user.PinHash      = null;
        await db.SaveChangesAsync();

        // If the admin reset their own password, keep the in-memory copy in sync.
        if (CurrentUser?.Id == user.Id)
        {
            CurrentUser.PasswordHash = user.PasswordHash;
            CurrentUser.PasswordSalt = user.PasswordSalt;
            CurrentUser.PinHash      = null;
        }

        await LogAsync(AuditAction.UserPasswordReset,
            $"Reset password for '{user.DisplayName}'",
            "User", user.Id);
    }

    // ── Self-service password recovery ───────────────────────────────────────
    //
    // These three methods intentionally live outside the RequireAdmin() gate:
    //   • GetRecoveryQuestionAsync and ResetPasswordWithRecoveryAsync run for
    //     users who *can't* sign in — that's the whole point of the flow.
    //   • SetRecoveryQuestionAsync mutates the *current* user's own row, so
    //     only requires an authenticated session, not the admin role.
    //
    // Failure modes deliberately share the same "false" return / "null" hint
    // so the login page can't be used to enumerate valid usernames.

    public async Task<string?> GetRecoveryQuestionAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();
        DbBootstrap.EnsureReady(db);

        var uname = username.Trim();
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && EF.Functions.Like(u.Username, uname))
            .Select(u => new { u.RecoveryQuestion, u.RecoveryAnswerHash })
            .FirstOrDefaultAsync();

        if (user is null) return null;
        if (string.IsNullOrEmpty(user.RecoveryQuestion) ||
            string.IsNullOrEmpty(user.RecoveryAnswerHash))
            return null;
        return user.RecoveryQuestion;
    }

    public async Task<bool> ResetPasswordWithRecoveryAsync(
        string username, string answer, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(answer) ||
            newPassword is null || newPassword.Length < 4)
            return false;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();
        DbBootstrap.EnsureReady(db);

        var uname = username.Trim();
        var user = await db.Users
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u =>
                u.IsActive && EF.Functions.Like(u.Username, uname));

        if (user is null ||
            string.IsNullOrEmpty(user.RecoveryQuestion) ||
            string.IsNullOrEmpty(user.RecoveryAnswerHash))
        {
            // Also log the miss for orgs that we can identify — helps admins
            // spot targeted brute-force attempts against the recovery flow.
            if (user?.Organization is { } orgFail)
                await LogForAsync(null, uname, orgFail.Id,
                    AuditAction.LoginFailed,
                    $"Recovery attempt for '{uname}' rejected (no question configured)",
                    "User", user.Id);
            return false;
        }

        var normalized = NormalizeAnswer(answer);
        if (!ConstantTimeEquals(HashAnswer(normalized), user.RecoveryAnswerHash!))
        {
            await LogForAsync(null, uname, user.OrganizationId,
                AuditAction.LoginFailed,
                $"Recovery attempt for '{uname}' rejected (wrong answer)",
                "User", user.Id);
            return false;
        }

        var (hash, salt) = PasswordHasher.Hash(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        // Clear the PIN too — anybody who forgot the password shouldn't be
        // able to sneak past the reset via a still-valid PIN.
        user.PinHash      = null;
        await db.SaveChangesAsync();

        await LogForAsync(user.Id, user.DisplayName, user.OrganizationId,
            AuditAction.UserPasswordReset,
            $"Self-service password reset for '{user.DisplayName}'",
            "User", user.Id);
        return true;
    }

    public async Task SetRecoveryQuestionAsync(string? question, string? answer)
    {
        if (CurrentUser is null)
            throw new InvalidOperationException("Not signed in.");

        var userId = CurrentUser.Id;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException("User not found.");

        // Empty inputs clear the recovery data (opt-out). We treat "one of the
        // two is empty" as an intent to clear too, because a question without
        // a matching answer would silently break the reset flow.
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
        {
            user.RecoveryQuestion   = null;
            user.RecoveryAnswerHash = null;
        }
        else
        {
            user.RecoveryQuestion   = question.Trim();
            user.RecoveryAnswerHash = HashAnswer(NormalizeAnswer(answer));
        }
        await db.SaveChangesAsync();

        // Keep the in-memory session in sync so HasRecoveryQuestion flips
        // immediately for the Settings status pill.
        CurrentUser.RecoveryQuestion   = user.RecoveryQuestion;
        CurrentUser.RecoveryAnswerHash = user.RecoveryAnswerHash;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Normalises the answer so casing/whitespace variants ("  Bombay ",
    /// "bombay", "BOMBAY") all hash to the same value. Kept private so the
    /// exact rule can evolve without breaking already-stored hashes — since
    /// this app is offline and re-hashing on the next successful reset is
    /// zero effort, we can afford to be conservative.
    /// </summary>
    private static string NormalizeAnswer(string answer)
        => answer.Trim().ToLowerInvariant();

    /// <summary>SHA-256 → Base64. Deliberately unsalted (see model comment).</summary>
    private static string HashAnswer(string normalized)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(normalized);
        var hash  = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }

    public async Task UpdateOrganizationAsync(
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
        IEnumerable<string>? itemCatalog = null)
    {
        RequireAdmin();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Shop name cannot be empty.", nameof(name));
        if (taxRatePct < 0 || taxRatePct > 100)
            throw new ArgumentException("Tax rate must be between 0 and 100.", nameof(taxRatePct));

        var orgId = CurrentOrganization!.Id;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId)
            ?? throw new InvalidOperationException("Organization not found.");

        org.Name           = name.Trim();
        org.Phone          = string.IsNullOrWhiteSpace(phone)          ? null : phone.Trim();
        org.Address        = string.IsNullOrWhiteSpace(address)        ? null : address.Trim();
        org.GstNumber      = string.IsNullOrWhiteSpace(gstNumber)      ? null : gstNumber.Trim();
        org.CurrencySymbol = string.IsNullOrWhiteSpace(currencySymbol) ? null : currencySymbol.Trim();
        org.InvoicePrefix  = string.IsNullOrWhiteSpace(invoicePrefix)  ? null : invoicePrefix.Trim();
        org.LogoPath       = string.IsNullOrWhiteSpace(logoPath)       ? null : logoPath.Trim();
        org.TaxRatePct     = taxRatePct;
        org.OwnerName      = string.IsNullOrWhiteSpace(ownerName)      ? null : ownerName.Trim();
        org.Email          = string.IsNullOrWhiteSpace(email)          ? null : email.Trim();

        // Item catalog: null means "caller doesn't want to touch it" (e.g. a
        // partial update from an older screen). An empty list means "clear
        // the catalog"; SetItemCatalog handles the null-serialization for us.
        if (itemCatalog is not null)
            org.SetItemCatalog(itemCatalog);

        await db.SaveChangesAsync();

        // Refresh the in-memory session copy so invoices / WhatsApp / dashboards
        // see the new values immediately, without waiting for the next login.
        if (CurrentOrganization is not null)
        {
            CurrentOrganization.Name           = org.Name;
            CurrentOrganization.Phone          = org.Phone;
            CurrentOrganization.Address        = org.Address;
            CurrentOrganization.GstNumber      = org.GstNumber;
            CurrentOrganization.CurrencySymbol = org.CurrencySymbol;
            CurrentOrganization.InvoicePrefix  = org.InvoicePrefix;
            CurrentOrganization.LogoPath       = org.LogoPath;
            CurrentOrganization.TaxRatePct     = org.TaxRatePct;
            CurrentOrganization.OwnerName      = org.OwnerName;
            CurrentOrganization.Email          = org.Email;
            CurrentOrganization.ItemCatalogJson= org.ItemCatalogJson;
        }

        await LogAsync(AuditAction.OrganizationUpdated,
            $"Updated shop settings for '{org.Name}'",
            "Organization", org.Id);
    }

    private void RequireAdmin()
    {
        if (!IsAdmin)
            throw new UnauthorizedAccessException(
                "Only administrators can manage employees.");
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private void SetSession(User user, Organization org)
    {
        CurrentUser         = user;
        CurrentOrganization = org;
        AuthStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Re-parents any customers/orders that were parked in the placeholder
    /// "My Shop" org by the schema migrator into the newly-registered org so
    /// the shop owner sees their existing data on first login.
    /// </summary>
    private static async Task AdoptDefaultShopDataAsync(TapeTrackerDbContext db, int newOrgId)
    {
        var placeholder = await db.Organizations
            .FirstOrDefaultAsync(o => o.Name == "My Shop" && o.Id != newOrgId);
        if (placeholder is null) return;

        var customersMoved = await db.Customers
            .Where(c => c.OrganizationId == placeholder.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.OrganizationId, newOrgId));
        var ordersMoved = await db.Orders
            .Where(o => o.OrganizationId == placeholder.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.OrganizationId, newOrgId));

        // Only drop the placeholder if we actually moved everything AND it has
        // no users of its own — otherwise leave it alone as a safety net.
        var placeholderHasUsers = await db.Users.AnyAsync(u => u.OrganizationId == placeholder.Id);
        if (!placeholderHasUsers)
            db.Organizations.Remove(placeholder);

        _ = customersMoved; _ = ordersMoved;
        await db.SaveChangesAsync();
    }
}
