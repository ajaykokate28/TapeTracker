using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TapeTracker.Data;

namespace TapeTracker.Services;

/// <summary>
/// Idempotent schema migrator for the SQLite database.
/// Called once after EnsureCreated() to add tables / columns that were
/// introduced after the initial release — without deleting existing data.
///
/// All statements use IF NOT EXISTS / ALTER TABLE … ADD COLUMN so they are
/// safe to run on any version of the database, old or new.
/// </summary>
public static class SchemaMigrator
{
    public static void Apply(TapeTrackerDbContext db)
    {
        // Use a raw connection so we can run DDL outside EF's change-tracking
        var conn = db.Database.GetDbConnection();
        conn.Open();

        try
        {
            using var cmd = conn.CreateCommand();

            // Table rebuilds below drop and recreate tables that other tables
            // reference by FK. SQLite refuses to drop a table with live FKs
            // unless enforcement is disabled first (the standard "12-step
            // ALTER TABLE" procedure). Turn it off for the whole migration
            // and back on at the end.
            cmd.CommandText = "PRAGMA foreign_keys = OFF;";
            cmd.ExecuteNonQuery();

            // ── v1.1: Orders table ───────────────────────────────────────────
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Orders (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerId  INTEGER NOT NULL REFERENCES Customers(Id) ON DELETE CASCADE,
                    OrderNumber TEXT    NOT NULL DEFAULT '',
                    Date        TEXT    NOT NULL DEFAULT (date('now')),
                    DueDate     TEXT,
                    Status      INTEGER NOT NULL DEFAULT 0,
                    Notes       TEXT    NOT NULL DEFAULT '',
                    IsDeleted   INTEGER NOT NULL DEFAULT 0
                );";
            cmd.ExecuteNonQuery();

            // ── v1.1: ShirtMeasurements (OrderId FK) ─────────────────────────
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ShirtMeasurements (
                    Id       INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderId  INTEGER NOT NULL REFERENCES Orders(Id) ON DELETE CASCADE,
                    Length   NUMERIC NOT NULL DEFAULT 0,
                    Chest    NUMERIC NOT NULL DEFAULT 0,
                    Waist    NUMERIC NOT NULL DEFAULT 0,
                    Hip      NUMERIC NOT NULL DEFAULT 0,
                    Shoulder NUMERIC NOT NULL DEFAULT 0,
                    Sleeve   NUMERIC NOT NULL DEFAULT 0,
                    Cuff     NUMERIC NOT NULL DEFAULT 0,
                    Collar   NUMERIC NOT NULL DEFAULT 0
                );";
            cmd.ExecuteNonQuery();

            // ── v1.1: PantMeasurements (OrderId FK) ──────────────────────────
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS PantMeasurements (
                    Id      INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderId INTEGER NOT NULL REFERENCES Orders(Id) ON DELETE CASCADE,
                    Length  NUMERIC NOT NULL DEFAULT 0,
                    Waist   NUMERIC NOT NULL DEFAULT 0,
                    Hip     NUMERIC NOT NULL DEFAULT 0,
                    Thigh   NUMERIC NOT NULL DEFAULT 0,
                    Ankle   NUMERIC NOT NULL DEFAULT 0,
                    Knee    NUMERIC NOT NULL DEFAULT 0,
                    Seat    NUMERIC NOT NULL DEFAULT 0
                );";
            cmd.ExecuteNonQuery();

            // ── v1.4: IsDeleted column on Customers ──────────────────────────
            AddColumnIfMissing(cmd, "Customers", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");

            // ── v1.4: IsDeleted column on Orders ─────────────────────────────
            AddColumnIfMissing(cmd, "Orders", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");

            // ── v1.6: Tag column on Customers (family/grouping label) ────────
            AddColumnIfMissing(cmd, "Customers", "Tag", "TEXT NULL");

            // ── v1.1: Ensure OrderId exists on measurement tables ─────────────
            // Old schema had CustomerId; adding OrderId here is safe (no-op if present)
            AddColumnIfMissing(cmd, "ShirtMeasurements", "OrderId", "INTEGER");
            AddColumnIfMissing(cmd, "PantMeasurements",  "OrderId", "INTEGER");

            // ── v1.1: Migrate old Customers data into Orders ─────────────────
            // If old Customers table has order columns (OrderNumber, Date, etc.)
            // and Orders table is empty, migrate each customer to a first Order.
            if (OldCustomerSchemaExists(cmd) && !HasAnyOrders(cmd))
                MigrateOldCustomersToOrders(cmd);

            // ── v1.5: Rebuild tables that still have legacy columns ──────────
            // The v1.1 migration added new FKs but left old NOT NULL columns
            // in place. `ALTER TABLE DROP COLUMN` refuses to remove a column
            // referenced by a FK/index/constraint, so we do the SQLite-standard
            // table-rebuild dance (create → copy → drop → rename) instead.
            // Each helper is idempotent — it only rebuilds if a legacy column
            // is still present.
            RebuildCustomersIfLegacy(cmd);
            RebuildMeasurementIfLegacy(cmd, "ShirtMeasurements", ShirtMeasurementCols);
            RebuildMeasurementIfLegacy(cmd, "PantMeasurements",  PantMeasurementCols);

            // ── v1.7: Normalize existing Customer.Tag values ─────────────────
            // Trim + collapse whitespace and merge case-insensitive dupes so
            // "Sharma Family" / " sharma  family " become the same tag.
            NormalizeCustomerTags(cmd);

            // ── v2.0: Multi-tenant + user accounts + stage assignments ──────
            // Create the new tables idempotently, then backfill an
            // OrganizationId column on Customers / Orders and point every
            // existing row at a single default org ("My Shop") so nothing is
            // orphaned. The admin user for that org is NOT created here — the
            // first-run OrgRegistrationPage owns credentials so the password
            // hash never touches disk in an incomplete state.
            EnsureAuthTables(cmd);
            AddColumnIfMissing(cmd, "Customers", "OrganizationId",  "INTEGER NULL");
            AddColumnIfMissing(cmd, "Orders",    "OrganizationId",  "INTEGER NULL");
            AddColumnIfMissing(cmd, "Orders",    "CreatedByUserId", "INTEGER NULL");
            BackfillDefaultOrganization(cmd);

            // ── v2.1: Audit trail (Phase 5) ──────────────────────────────
            EnsureAuditTable(cmd);
            EnsureInvoiceLineItemsTable(cmd);

            // ── v2.2: Rush flag + stage-change timestamp for SLA warnings ──
            // Both nullable-safe defaults: existing orders come in as non-rush
            // with a null StageChangedAt, which the app treats as "unknown"
            // (never reported as overdue).
            AddColumnIfMissing(cmd, "Orders", "IsRush",         "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing(cmd, "Orders", "StageChangedAt", "TEXT NULL");

            // ── v2.3: Shop-branding fields on Organizations (Phase 8) ──────
            // All nullable so existing orgs migrate cleanly; UI treats null
            // as "not configured" and falls back to sensible defaults.
            AddColumnIfMissing(cmd, "Organizations", "GstNumber",      "TEXT NULL");
            AddColumnIfMissing(cmd, "Organizations", "CurrencySymbol", "TEXT NULL");
            AddColumnIfMissing(cmd, "Organizations", "InvoicePrefix",  "TEXT NULL");
            AddColumnIfMissing(cmd, "Organizations", "LogoPath",       "TEXT NULL");
            AddColumnIfMissing(cmd, "Organizations", "TaxRatePct",     "NUMERIC NOT NULL DEFAULT 0");
            AddColumnIfMissing(cmd, "Organizations", "OwnerName",      "TEXT NULL");
            AddColumnIfMissing(cmd, "Organizations", "Email",          "TEXT NULL");

            // ── v2.4: Shop item catalog (Phase 9 follow-up) ──────────────
            // Stored as a JSON array string so the schema doesn't need a
            // second table for what is typically a short user-managed list
            // of 5-15 garment names.
            AddColumnIfMissing(cmd, "Organizations", "ItemCatalogJson", "TEXT NULL");

            // ── v2.3: Pricing & payments on Orders (Phase 8) ───────────────
            // Zero-defaults mean pre-Phase-8 rows show up as "no pricing"
            // (Price > 0 test) rather than falsely as "₹0 owed".
            AddColumnIfMissing(cmd, "Orders", "Price",          "NUMERIC NOT NULL DEFAULT 0");
            AddColumnIfMissing(cmd, "Orders", "DiscountAmount", "NUMERIC NOT NULL DEFAULT 0");
            AddColumnIfMissing(cmd, "Orders", "TaxAmount",      "NUMERIC NOT NULL DEFAULT 0");
            AddColumnIfMissing(cmd, "Orders", "AdvancePaid",    "NUMERIC NOT NULL DEFAULT 0");
            AddColumnIfMissing(cmd, "Orders", "PaymentMethod",  "INTEGER NOT NULL DEFAULT 0");

            // Self-service password recovery (Forgot password? flow on login page).
            // Both columns are optional so existing installs stay signable-in
            // without needing to configure a recovery question first — they'll
            // see a "not set up" hint in the reset flow and can either set one
            // via Shop Settings or fall back on their admin.
            AddColumnIfMissing(cmd, "Users", "RecoveryQuestion",   "TEXT NULL");
            AddColumnIfMissing(cmd, "Users", "RecoveryAnswerHash", "TEXT NULL");

            // Re-enable FK enforcement now that all rebuilds are done.
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // Persist the failure so it isn't lost — it explains any subsequent
            // "Save Failed" or blank-detail-page behaviour.
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TapeTracker", "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(
                    Path.Combine(logDir, "unhandled.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SchemaMigrator: {ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { /* logging must never crash */ }
            throw;
        }
        finally
        {
            // Leave connection management to EF
            if (conn.State == System.Data.ConnectionState.Open)
                conn.Close();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void AddColumnIfMissing(
        System.Data.Common.DbCommand cmd,
        string table,
        string column,
        string definition)
    {
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = cmd.ExecuteReader();
        bool exists = false;
        while (reader.Read())
        {
            if (string.Equals(reader["name"]?.ToString(), column,
                    StringComparison.OrdinalIgnoreCase))
            { exists = true; break; }
        }
        reader.Close();

        if (!exists)
        {
            cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Checks whether the Customers table still has legacy order columns.</summary>
    private static bool OldCustomerSchemaExists(System.Data.Common.DbCommand cmd)
    {
        cmd.CommandText = "PRAGMA table_info(Customers);";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (string.Equals(r["name"]?.ToString(), "OrderNumber",
                    StringComparison.OrdinalIgnoreCase))
            { r.Close(); return true; }
        }
        return false;
    }

    private static bool HasAnyOrders(System.Data.Common.DbCommand cmd)
    {
        cmd.CommandText = "SELECT COUNT(*) FROM Orders;";
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// One-time data migration: copies each customer's legacy measurements into
    /// a single Order record so old data is not lost.
    /// </summary>
    private static void MigrateOldCustomersToOrders(System.Data.Common.DbCommand cmd)
    {
        // Discover which columns actually exist in the old Customers table
        cmd.CommandText = "PRAGMA table_info(Customers);";
        var customerCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                customerCols.Add(r["name"]?.ToString() ?? "");

        // Check if ShirtMeasurements is the old CustomerId-based schema
        cmd.CommandText = "PRAGMA table_info(ShirtMeasurements);";
        var shirtCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                shirtCols.Add(r["name"]?.ToString() ?? "");
        bool oldSeparateTables = shirtCols.Contains("CustomerId");

        // Build a safe SELECT — only reference columns that actually exist
        string Col(string name, string fallback = "NULL")
            => customerCols.Contains(name) ? name : fallback;

        cmd.CommandText = $@"
            SELECT Id,
                   {Col("OrderNumber",  "''")},
                   {Col("Date",         $"date('now')")},
                   {Col("DueDate",      "NULL")},
                   {Col("ShirtLength",  "0")}, {Col("ShirtChest",   "0")},
                   {Col("ShirtWaist",   "0")}, {Col("ShirtHip",     "0")},
                   {Col("ShirtShoulder","0")}, {Col("ShirtSleeve",  "0")},
                   {Col("ShirtCuff",    "0")}, {Col("ShirtCollar",  "0")},
                   {Col("PantLength",   "0")}, {Col("PantWaist",    "0")},
                   {Col("PantHip",      "0")}, {Col("PantThigh",    "0")},
                   {Col("PantAnkle",    "0")}, {Col("PantKnee",     "0")},
                   {Col("PantSeat",     "0")}
            FROM Customers;";

        var rows = new List<object?[]>();
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var row = new object?[r.FieldCount];
                for (int i = 0; i < r.FieldCount; i++)
                    row[i] = r.IsDBNull(i) ? null : r.GetValue(i);
                rows.Add(row);
            }
        }

        foreach (var row in rows)
        {
            long customerId = Convert.ToInt64(row[0]);
            var orderNum    = row[1]?.ToString() ?? "ORD-001";
            var date        = row[2]?.ToString() ?? DateTime.Today.ToString("yyyy-MM-dd");
            var dueDate     = row[3]?.ToString();

            // Insert Order
            cmd.CommandText = @"
                INSERT INTO Orders (CustomerId, OrderNumber, Date, DueDate, Status, Notes, IsDeleted)
                VALUES (@cid, @on, @dt, @dd, 0, '', 0);
                SELECT last_insert_rowid();";

            var sp = ((SqliteCommand)cmd).Parameters;
            sp.Clear();
            sp.AddWithValue("@cid", customerId);
            sp.AddWithValue("@on",  orderNum);
            sp.AddWithValue("@dt",  date);
            sp.AddWithValue("@dd",  (object?)dueDate ?? DBNull.Value);

            long orderId = Convert.ToInt64(cmd.ExecuteScalar());

            if (oldSeparateTables)
            {
                // Old schema: measurements already exist in separate tables keyed by CustomerId.
                // Just set OrderId on those existing rows — data preserved as-is.
                cmd.CommandText = "UPDATE ShirtMeasurements SET OrderId = @oid WHERE CustomerId = @cid;";
                sp.Clear();
                sp.AddWithValue("@oid", orderId);
                sp.AddWithValue("@cid", customerId);
                cmd.ExecuteNonQuery();

                cmd.CommandText = "UPDATE PantMeasurements SET OrderId = @oid WHERE CustomerId = @cid;";
                sp.Clear();
                sp.AddWithValue("@oid", orderId);
                sp.AddWithValue("@cid", customerId);
                cmd.ExecuteNonQuery();
            }
            else
            {
                // Old schema: measurements were inline columns on the Customers row.
                // INSERT new rows into measurement tables from those values.
                cmd.CommandText = @"
                    INSERT INTO ShirtMeasurements
                        (OrderId, Length, Chest, Waist, Hip, Shoulder, Sleeve, Cuff, Collar)
                    VALUES
                        (@oid, @sl, @sc, @sw, @sh, @ss, @slv, @scf, @scol);";
                sp.Clear();
                sp.AddWithValue("@oid",  orderId);
                sp.AddWithValue("@sl",   D(row[4]));  sp.AddWithValue("@sc",   D(row[5]));
                sp.AddWithValue("@sw",   D(row[6]));  sp.AddWithValue("@sh",   D(row[7]));
                sp.AddWithValue("@ss",   D(row[8]));  sp.AddWithValue("@slv",  D(row[9]));
                sp.AddWithValue("@scf",  D(row[10])); sp.AddWithValue("@scol", D(row[11]));
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    INSERT INTO PantMeasurements
                        (OrderId, Length, Waist, Hip, Thigh, Ankle, Knee, Seat)
                    VALUES
                        (@oid, @pl, @pw, @ph, @pt, @pa, @pk, @ps);";
                sp.Clear();
                sp.AddWithValue("@oid", orderId);
                sp.AddWithValue("@pl",  D(row[12])); sp.AddWithValue("@pw",  D(row[13]));
                sp.AddWithValue("@ph",  D(row[14])); sp.AddWithValue("@pt",  D(row[15]));
                sp.AddWithValue("@pa",  D(row[16])); sp.AddWithValue("@pk",  D(row[17]));
                sp.AddWithValue("@ps",  D(row[18]));
                cmd.ExecuteNonQuery();
            }
        }
    }

    private static double D(object? v) => v is null ? 0.0 : Convert.ToDouble(v);

    // ── Legacy column sentinels ───────────────────────────────────────────────
    // If any of these columns are still present on the given table, the whole
    // table gets rebuilt with the current-model schema.

    private static readonly string[] LegacyCustomerSentinels =
    {
        "OrderNumber", "Date", "DueDate",
        "ShirtLength", "ShirtChest", "ShirtWaist", "ShirtHip",
        "ShirtShoulder", "ShirtSleeve", "ShirtCuff", "ShirtCollar",
        "PantLength", "PantWaist", "PantHip", "PantThigh",
        "PantAnkle", "PantKnee", "PantSeat"
    };

    // Columns the CURRENT model keeps on each table. Used to build the
    // `INSERT INTO _new (…) SELECT …` copy list during rebuild.
    private static readonly string[] CurrentCustomerCols =
        { "Id", "Name", "Phone", "Tag", "IsDeleted" };

    private static readonly string[] ShirtMeasurementCols =
        { "Id", "OrderId", "Length", "Chest", "Waist", "Hip",
          "Shoulder", "Sleeve", "Cuff", "Collar" };

    private static readonly string[] PantMeasurementCols =
        { "Id", "OrderId", "Length", "Waist", "Hip",
          "Thigh", "Ankle", "Knee", "Seat" };

    /// <summary>Returns the names of all columns currently on <paramref name="table"/>.</summary>
    private static HashSet<string> GetColumns(System.Data.Common.DbCommand cmd, string table)
    {
        cmd.CommandText = $"PRAGMA table_info({table});";
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            cols.Add(r["name"]?.ToString() ?? string.Empty);
        return cols;
    }

    /// <summary>
    /// Rebuilds the Customers table so it contains only Id / Name / Phone /
    /// IsDeleted. Runs only if any legacy inline-order column is still there.
    /// Preserves all existing customer rows.
    /// </summary>
    private static void RebuildCustomersIfLegacy(System.Data.Common.DbCommand cmd)
    {
        var present = GetColumns(cmd, "Customers");
        if (!LegacyCustomerSentinels.Any(present.Contains))
            return; // Already clean.

        // Only copy columns that both the current schema and the old table have,
        // to be defensive against databases that pre-date IsDeleted.
        var copyCols = CurrentCustomerCols.Where(present.Contains).ToArray();
        var copyList = string.Join(", ", copyCols);

        cmd.CommandText = $@"
            BEGIN TRANSACTION;
            CREATE TABLE Customers_new (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                Name      TEXT    NOT NULL DEFAULT '',
                Phone     TEXT    NULL,
                Tag       TEXT    NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );
            INSERT INTO Customers_new ({copyList})
                SELECT {copyList} FROM Customers;
            DROP TABLE Customers;
            ALTER TABLE Customers_new RENAME TO Customers;
            COMMIT;";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Rewrites every Customer.Tag value through <see cref="TagUtil.Normalize"/>
    /// and merges case-insensitive duplicates onto a single canonical spelling
    /// (whichever variant was seen first). Runs on every startup — cheap because
    /// only rows whose stored value actually differs from the normalized form
    /// are updated.
    /// </summary>
    private static void NormalizeCustomerTags(System.Data.Common.DbCommand cmd)
    {
        // Nothing to do if the Tag column isn't there yet (fresh DB flow).
        if (!GetColumns(cmd, "Customers").Contains("Tag")) return;

        cmd.CommandText = "SELECT Id, Tag FROM Customers WHERE Tag IS NOT NULL AND Tag <> '';";
        var rows = new List<(long Id, string Tag)>();
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                rows.Add((r.GetInt64(0), r.GetString(1)));

        if (rows.Count == 0) return;

        // Pick a canonical spelling (first seen wins) per case-insensitive key.
        var canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, tag) in rows)
        {
            var norm = TagUtil.Normalize(tag);
            if (norm is null) continue;
            canonical.TryAdd(norm, norm);
        }

        foreach (var (id, tag) in rows)
        {
            var norm = TagUtil.Normalize(tag);
            var target = norm is null ? null : canonical[norm];
            if (string.Equals(tag, target, StringComparison.Ordinal))
                continue; // already clean

            cmd.CommandText = "UPDATE Customers SET Tag = @tag WHERE Id = @id;";
            var sp = ((SqliteCommand)cmd).Parameters;
            sp.Clear();
            sp.AddWithValue("@tag", (object?)target ?? DBNull.Value);
            sp.AddWithValue("@id",  id);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Rebuilds a measurement table (Shirt or Pant) so it contains only the
    /// current-model columns. Runs only if the legacy <c>CustomerId</c> column
    /// is still present. Preserves all existing measurement rows.
    /// </summary>
    private static void RebuildMeasurementIfLegacy(
        System.Data.Common.DbCommand cmd,
        string table,
        string[] currentCols)
    {
        var present = GetColumns(cmd, table);
        if (!present.Contains("CustomerId"))
            return; // Already clean.

        // Only copy columns that both current schema and the old table have.
        var copyCols = currentCols.Where(present.Contains).ToArray();
        var copyList = string.Join(", ", copyCols);

        // Build the CREATE TABLE body from the known current column list.
        // First column is Id (PK autoincrement), second is OrderId (FK NOT NULL),
        // remaining are numeric measurements (NOT NULL default 0).
        var columnDefs = new List<string> {
            "Id       INTEGER PRIMARY KEY AUTOINCREMENT",
            "OrderId  INTEGER NOT NULL REFERENCES Orders(Id) ON DELETE CASCADE"
        };
        foreach (var col in currentCols.Skip(2))
            columnDefs.Add($"{col,-8} NUMERIC NOT NULL DEFAULT 0");
        var columnDefsSql = string.Join(",\n                ", columnDefs);

        // WHERE OrderId IS NOT NULL: legacy rows with unlinked OrderId (would
        // have failed anyway under the new schema) are silently discarded.
        cmd.CommandText = $@"
            BEGIN TRANSACTION;
            CREATE TABLE {table}_new (
                {columnDefsSql}
            );
            INSERT INTO {table}_new ({copyList})
                SELECT {copyList} FROM {table}
                WHERE OrderId IS NOT NULL;
            DROP TABLE {table};
            ALTER TABLE {table}_new RENAME TO {table};
            COMMIT;";
        cmd.ExecuteNonQuery();
    }

    // ── v2.0: Auth / multi-tenant scaffolding ────────────────────────────────

    /// <summary>
    /// Creates <c>Organizations</c>, <c>Users</c>, and <c>OrderStageAssignments</c>
    /// tables if missing. All statements are IF NOT EXISTS so this is safe on
    /// every schema version, old or new.
    /// </summary>
    private static void EnsureAuthTables(System.Data.Common.DbCommand cmd)
    {
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Organizations (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                Name      TEXT    NOT NULL DEFAULT '',
                Phone     TEXT    NULL,
                Address   TEXT    NULL,
                CreatedAt TEXT    NOT NULL DEFAULT (datetime('now'))
            );";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                OrganizationId     INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
                Username           TEXT    NOT NULL,
                DisplayName        TEXT    NOT NULL DEFAULT '',
                PasswordHash       TEXT    NOT NULL DEFAULT '',
                PasswordSalt       TEXT    NOT NULL DEFAULT '',
                Role               INTEGER NOT NULL DEFAULT 1,
                PinHash            TEXT    NULL,
                IsActive           INTEGER NOT NULL DEFAULT 1,
                CreatedAt          TEXT    NOT NULL DEFAULT (datetime('now')),
                LastLoginAt        TEXT    NULL,
                RecoveryQuestion   TEXT    NULL,
                RecoveryAnswerHash TEXT    NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Org_Username
                ON Users(OrganizationId, Username COLLATE NOCASE);";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS OrderStageAssignments (
                Id                INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderId           INTEGER NOT NULL REFERENCES Orders(Id)  ON DELETE CASCADE,
                Stage             INTEGER NOT NULL,
                AssignedUserId    INTEGER NOT NULL REFERENCES Users(Id)    ON DELETE RESTRICT,
                AssignedAt        TEXT    NOT NULL DEFAULT (datetime('now')),
                CompletedAt       TEXT    NULL,
                CompletedByUserId INTEGER NULL REFERENCES Users(Id)        ON DELETE SET NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_StageAssign_Order_Stage
                ON OrderStageAssignments(OrderId, Stage);";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates the append-only <c>AuditLogs</c> table if missing. Uses TEXT for
    /// the timestamp so SQLite can sort it lexicographically (ISO-8601 format).
    /// </summary>
    private static void EnsureInvoiceLineItemsTable(System.Data.Common.DbCommand cmd)
    {
        // Priced rows on an order's bill (Phase 9). Cascades from Orders so a
        // deleted order takes its line items with it — matches the EF Core
        // relationship defined in TapeTrackerDbContext.
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS InvoiceLineItems (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderId     INTEGER NOT NULL REFERENCES Orders(Id) ON DELETE CASCADE,
                ForWhom     TEXT    NOT NULL DEFAULT '',
                Description TEXT    NOT NULL DEFAULT '',
                Quantity    INTEGER NOT NULL DEFAULT 1,
                UnitPrice   NUMERIC NOT NULL DEFAULT 0,
                SortIndex   INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS IX_InvoiceLineItems_Order_Sort
                ON InvoiceLineItems(OrderId, SortIndex);";
        cmd.ExecuteNonQuery();
    }

    private static void EnsureAuditTable(System.Data.Common.DbCommand cmd)
    {
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS AuditLogs (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                OrganizationId  INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
                UserId          INTEGER NULL     REFERENCES Users(Id)         ON DELETE SET NULL,
                UserDisplayName TEXT    NOT NULL DEFAULT '',
                Action          INTEGER NOT NULL,
                Timestamp       TEXT    NOT NULL DEFAULT (datetime('now')),
                EntityType      TEXT    NOT NULL DEFAULT '',
                EntityId        INTEGER NOT NULL DEFAULT 0,
                Summary         TEXT    NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS IX_AuditLogs_Org_Time
                ON AuditLogs(OrganizationId, Timestamp DESC);";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Ensures every existing Customer and Order has a valid OrganizationId. If
    /// any orphan rows are found, a single default org "My Shop" is created
    /// (only once — subsequent runs find it and no-op) and all orphans are
    /// assigned to it. This preserves data from pre-v2.0 databases without
    /// forcing the user through an interactive migration wizard.
    /// </summary>
    private static void BackfillDefaultOrganization(System.Data.Common.DbCommand cmd)
    {
        // Fast path: nothing to backfill.
        cmd.CommandText = @"
            SELECT
              (SELECT COUNT(*) FROM Customers WHERE OrganizationId IS NULL OR OrganizationId = 0)
            + (SELECT COUNT(*) FROM Orders    WHERE OrganizationId IS NULL OR OrganizationId = 0);";
        var orphans = Convert.ToInt64(cmd.ExecuteScalar());
        if (orphans == 0) return;

        // Reuse an existing "My Shop" if one exists (multiple upgrade runs must
        // not create duplicates). Otherwise create it.
        cmd.CommandText = "SELECT Id FROM Organizations WHERE Name = 'My Shop' LIMIT 1;";
        var existing = cmd.ExecuteScalar();

        long orgId;
        if (existing is null or DBNull)
        {
            cmd.CommandText = @"
                INSERT INTO Organizations (Name, Phone, Address, CreatedAt)
                VALUES ('My Shop', NULL, NULL, datetime('now'));
                SELECT last_insert_rowid();";
            orgId = Convert.ToInt64(cmd.ExecuteScalar());
        }
        else
        {
            orgId = Convert.ToInt64(existing);
        }

        cmd.CommandText = $@"
            UPDATE Customers SET OrganizationId = {orgId}
             WHERE OrganizationId IS NULL OR OrganizationId = 0;
            UPDATE Orders    SET OrganizationId = {orgId}
             WHERE OrganizationId IS NULL OR OrganizationId = 0;";
        cmd.ExecuteNonQuery();
    }
}
