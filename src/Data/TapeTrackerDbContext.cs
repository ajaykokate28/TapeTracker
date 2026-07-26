using Microsoft.EntityFrameworkCore;
using TapeTracker.Models;

namespace TapeTracker.Data;

public class TapeTrackerDbContext : DbContext
{
    public DbSet<Organization>         Organizations         => Set<Organization>();
    public DbSet<User>                 Users                 => Set<User>();
    public DbSet<Customer>             Customers             => Set<Customer>();
    public DbSet<Order>                Orders                => Set<Order>();
    public DbSet<ShirtMeasurement>     ShirtMeasurements     => Set<ShirtMeasurement>();
    public DbSet<PantMeasurement>      PantMeasurements      => Set<PantMeasurement>();
    public DbSet<OrderStageAssignment> OrderStageAssignments => Set<OrderStageAssignment>();
    public DbSet<AuditLog>             AuditLogs             => Set<AuditLog>();
    public DbSet<InvoiceLineItem>      InvoiceLineItems      => Set<InvoiceLineItem>();

    public TapeTrackerDbContext(DbContextOptions<TapeTrackerDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Organization
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Name).IsRequired().HasMaxLength(200);
            entity.Property(o => o.Phone).HasMaxLength(50);
            entity.Property(o => o.Address).HasMaxLength(500);

            entity.HasMany(o => o.Users)
                  .WithOne(u => u.Organization)
                  .HasForeignKey(u => u.OrganizationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(o => o.Customers)
                  .WithOne(c => c.Organization)
                  .HasForeignKey(c => c.OrganizationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(o => o.Orders)
                  .WithOne(ord => ord.Organization)
                  .HasForeignKey(ord => ord.OrganizationId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Username).IsRequired().HasMaxLength(100);
            entity.Property(u => u.DisplayName).HasMaxLength(200);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(u => u.PasswordSalt).IsRequired().HasMaxLength(200);
            entity.Property(u => u.PinHash).HasMaxLength(200);
            entity.Property(u => u.Role).HasConversion<int>();

            // Username is unique WITHIN an organization (case-insensitive at the
            // service layer). A composite index makes look-ups by (org, username)
            // trivial during login.
            entity.HasIndex(u => new { u.OrganizationId, u.Username }).IsUnique();
        });

        // Customer → Orders (1:N)
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(200);
            entity.Property(c => c.Phone).HasMaxLength(50);
            entity.Property(c => c.Tag).HasMaxLength(100);
            entity.HasIndex(c => c.OrganizationId);

            entity.HasMany(c => c.Orders)
                  .WithOne(o => o.Customer)
                  .HasForeignKey(o => o.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Order fields
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.OrderNumber).HasMaxLength(100);
            entity.Property(o => o.Notes).HasMaxLength(500);
            entity.Property(o => o.DueDate).IsRequired(false);
            entity.Property(o => o.Status).HasConversion<int>();
            entity.HasIndex(o => o.OrganizationId);

            // Order → ShirtMeasurement (1:1)
            entity.HasOne(o => o.Shirt)
                  .WithOne(s => s.Order)
                  .HasForeignKey<ShirtMeasurement>(s => s.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Order → PantMeasurement (1:1)
            entity.HasOne(o => o.Pant)
                  .WithOne(p => p.Order)
                  .HasForeignKey<PantMeasurement>(p => p.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Order → StageAssignments (1:N)
            entity.HasMany(o => o.StageAssignments)
                  .WithOne(a => a.Order)
                  .HasForeignKey(a => a.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Order → InvoiceLineItems (1:N) — deleting an order wipes its lines.
            entity.HasMany(o => o.LineItems)
                  .WithOne(li => li.Order)
                  .HasForeignKey(li => li.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Order → CreatedByUser (many:1, SetNull on delete so deleting an
            // employee doesn't wipe out order history).
            entity.HasOne(o => o.CreatedBy)
                  .WithMany()
                  .HasForeignKey(o => o.CreatedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // OrderStageAssignment
        modelBuilder.Entity<OrderStageAssignment>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Stage).HasConversion<int>();

            // Exactly one assignment per (Order, Stage). Attempting to add a
            // second assignment for the same stage is a bug in the caller.
            entity.HasIndex(a => new { a.OrderId, a.Stage }).IsUnique();

            entity.HasOne(a => a.AssignedUser)
                  .WithMany()
                  .HasForeignKey(a => a.AssignedUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.CompletedBy)
                  .WithMany()
                  .HasForeignKey(a => a.CompletedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ShirtMeasurement>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Length).HasPrecision(6, 2);
            entity.Property(s => s.Chest).HasPrecision(6, 2);
            entity.Property(s => s.Waist).HasPrecision(6, 2);
            entity.Property(s => s.Hip).HasPrecision(6, 2);
            entity.Property(s => s.Shoulder).HasPrecision(6, 2);
            entity.Property(s => s.Sleeve).HasPrecision(6, 2);
            entity.Property(s => s.Cuff).HasPrecision(6, 2);
            entity.Property(s => s.Collar).HasPrecision(6, 2);
        });

        modelBuilder.Entity<PantMeasurement>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Length).HasPrecision(6, 2);
            entity.Property(p => p.Waist).HasPrecision(6, 2);
            entity.Property(p => p.Hip).HasPrecision(6, 2);
            entity.Property(p => p.Thigh).HasPrecision(6, 2);
            entity.Property(p => p.Ankle).HasPrecision(6, 2);
            entity.Property(p => p.Knee).HasPrecision(6, 2);
            entity.Property(p => p.Seat).HasPrecision(6, 2);
        });

        // InvoiceLineItem — priced rows on an order's bill (Phase 9)
        modelBuilder.Entity<InvoiceLineItem>(entity =>
        {
            entity.HasKey(li => li.Id);
            entity.Property(li => li.ForWhom).HasMaxLength(100);
            entity.Property(li => li.Description).IsRequired().HasMaxLength(200);
            entity.Property(li => li.UnitPrice).HasPrecision(12, 2);
            // Sort within order first, then by id so hand-arranged rows
            // print in the exact order the tailor typed them.
            entity.HasIndex(li => new { li.OrderId, li.SortIndex });
        });

        // AuditLog — append-only activity trail (Phase 5)
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Action).HasConversion<int>();
            entity.Property(a => a.EntityType).HasMaxLength(50);
            entity.Property(a => a.UserDisplayName).HasMaxLength(200);
            entity.Property(a => a.Summary).HasMaxLength(1000);
            // (org, timestamp) is the hot read path — feed queries always
            // filter by org and sort by time-descending.
            entity.HasIndex(a => new { a.OrganizationId, a.Timestamp });

            entity.HasOne(a => a.Organization)
                  .WithMany()
                  .HasForeignKey(a => a.OrganizationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.User)
                  .WithMany()
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
