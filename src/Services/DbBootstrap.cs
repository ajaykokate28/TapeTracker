using TapeTracker.Data;

namespace TapeTracker.Services;

/// <summary>
/// Ensures <see cref="TapeTrackerDbContext.Database"/> is created and every
/// schema migration is applied exactly once per process, regardless of which
/// service happens to touch the database first (Auth, Measurement, or
/// Dashboard). Prior to v2.0 the migrator was only invoked from
/// MeasurementService, which meant the pre-login AuthService could hit a
/// missing Organizations table on the very first launch of a fresh install.
/// </summary>
public static class DbBootstrap
{
    private static bool            _initialized;
    private static readonly object _lock = new();

    public static void EnsureReady(TapeTrackerDbContext db)
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            db.Database.EnsureCreated();
            SchemaMigrator.Apply(db);
            _initialized = true;
        }
    }
}
