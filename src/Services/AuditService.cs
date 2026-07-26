using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TapeTracker.Data;
using TapeTracker.Models;

namespace TapeTracker.Services;

/// <inheritdoc />
public class AuditService : IAuditService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuthService         _auth;

    public AuditService(IServiceScopeFactory scopeFactory, IAuthService auth)
    {
        _scopeFactory = scopeFactory;
        _auth         = auth;
    }

    public Task LogAsync(
        AuditAction action,
        string summary,
        string entityType = "",
        int    entityId   = 0)
    {
        var user  = _auth.CurrentUser;
        var org   = _auth.CurrentOrganization;
        if (org is null) return Task.CompletedTask;   // no org context → nothing to log against

        return LogForAsync(
            userId:          user?.Id,
            userDisplayName: user?.DisplayName ?? "system",
            organizationId:  org.Id,
            action:          action,
            summary:         summary,
            entityType:      entityType,
            entityId:        entityId);
    }

    public async Task LogForAsync(
        int?   userId,
        string userDisplayName,
        int    organizationId,
        AuditAction action,
        string summary,
        string entityType = "",
        int    entityId   = 0)
    {
        // Logging is strictly best-effort. If the DB is momentarily busy or
        // the schema hasn't been bootstrapped yet on this thread, we swallow
        // the exception and keep the user's flow moving. The failure is
        // persisted to the app's crash-log file so it isn't invisible.
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();
            DbBootstrap.EnsureReady(db);

            db.AuditLogs.Add(new AuditLog
            {
                OrganizationId  = organizationId,
                UserId          = userId,
                UserDisplayName = string.IsNullOrWhiteSpace(userDisplayName) ? "system" : userDisplayName,
                Action          = action,
                Timestamp       = DateTime.UtcNow,
                EntityType      = entityType ?? string.Empty,
                EntityId        = entityId,
                // 1000-char cap on the column; truncate defensively so a
                // caller with a long dynamic summary can't blow up the insert.
                Summary         = (summary ?? string.Empty).Length > 1000
                                    ? (summary ?? string.Empty)[..1000]
                                    : (summary ?? string.Empty)
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            SafeLogFailure(ex);
        }
    }

    public async Task<IReadOnlyList<AuditLog>> QueryAsync(
        AuditAction? action  = null,
        int?         userId  = null,
        DateTime?    since   = null,
        int          take    = 100)
    {
        var orgId = _auth.CurrentOrganization?.Id ?? 0;
        if (orgId == 0) return Array.Empty<AuditLog>();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TapeTrackerDbContext>();
        DbBootstrap.EnsureReady(db);

        var q = db.AuditLogs
            .Where(a => a.OrganizationId == orgId)
            .AsQueryable();

        if (action.HasValue) q = q.Where(a => a.Action == action.Value);
        if (userId.HasValue) q = q.Where(a => a.UserId == userId.Value);
        if (since.HasValue)  q = q.Where(a => a.Timestamp >= since.Value.ToUniversalTime());

        return await q
            .OrderByDescending(a => a.Timestamp)
            .Take(take)
            .AsNoTracking()
            .ToListAsync();
    }

    private static void SafeLogFailure(Exception ex)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TapeTracker", "logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(
                Path.Combine(logDir, "audit.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AuditService: {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* logging must never crash */ }
    }
}
