using CustomAlertManager.Data.Entities;
using CustomAlertManager.Scheduler.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Data;

public sealed class AlertDbContext(DbContextOptions<AlertDbContext> options) : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    public const string DefaultConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=CustomAlertManager;Trusted_Connection=True;TrustServerCertificate=True";

    public DbSet<NewsSource> NewsSources => Set<NewsSource>();

    public DbSet<AlertSubscription> AlertSubscriptions => Set<AlertSubscription>();

    public DbSet<SubscriptionSource> SubscriptionSources => Set<SubscriptionSource>();

    public DbSet<SubscriptionKeyword> SubscriptionKeywords => Set<SubscriptionKeyword>();

    public DbSet<StoredFeedItem> FeedItems => Set<StoredFeedItem>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<AlertItem> AlertItems => Set<AlertItem>();

    public DbSet<NotificationTarget> NotificationTargets => Set<NotificationTarget>();

    public DbSet<SubscriptionTarget> SubscriptionTargets => Set<SubscriptionTarget>();

    public DbSet<AlertDelivery> AlertDeliveries => Set<AlertDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Identity tables use plain names rather than the AspNet prefix.
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            // Same index Identity creates, made unique so two accounts can never share an email address.
            entity.HasIndex(user => user.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
        });
        modelBuilder.Entity<IdentityRole<int>>(entity => entity.ToTable("Roles"));
        modelBuilder.Entity<IdentityUserRole<int>>(entity => entity.ToTable("UserRoles"));
        modelBuilder.Entity<IdentityUserClaim<int>>(entity => entity.ToTable("UserClaims"));
        modelBuilder.Entity<IdentityUserLogin<int>>(entity => entity.ToTable("UserLogins"));
        modelBuilder.Entity<IdentityRoleClaim<int>>(entity => entity.ToTable("RoleClaims"));
        modelBuilder.Entity<IdentityUserToken<int>>(entity => entity.ToTable("UserTokens"));

        // Every table gets a creation timestamp, filled in by the database when the row is inserted.
        modelBuilder.Entity<ApplicationUser>(entity =>
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()"));
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(TimestampedEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(TimestampedEntity.CreatedAt))
                    .HasDefaultValueSql("SYSDATETIMEOFFSET()");
            }
        }

        modelBuilder.Entity<NotificationTarget>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Destination).HasMaxLength(2048);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AlertSubscription>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_AlertSubscriptions_FrequencyMinutes",
                $"FrequencyMinutes IN ({string.Join(", ", AlertFrequency.PresetMinutes)})"));
        });

        modelBuilder.Entity<NewsSource>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Url).HasMaxLength(450);
            entity.HasIndex(e => e.Url).IsUnique();
        });

        modelBuilder.Entity<SubscriptionSource>(entity =>
        {
            entity.HasKey(e => new { e.AlertSubscriptionId, e.NewsSourceId });
            entity.HasOne(e => e.AlertSubscription)
                .WithMany(s => s.Sources)
                .HasForeignKey(e => e.AlertSubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.NewsSource)
                .WithMany()
                .HasForeignKey(e => e.NewsSourceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SubscriptionKeyword>(entity =>
        {
            entity.Property(e => e.Term).HasMaxLength(200);
            entity.HasIndex(e => new { e.AlertSubscriptionId, e.Kind, e.Term }).IsUnique();
            entity.HasOne<AlertSubscription>()
                .WithMany(s => s.Keywords)
                .HasForeignKey(e => e.AlertSubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StoredFeedItem>(entity =>
        {
            entity.Property(e => e.ItemKey).HasColumnType("char(64)");
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Link).HasMaxLength(2048);
            entity.HasIndex(e => new { e.NewsSourceId, e.ItemKey }).IsUnique();
            entity.HasOne(e => e.NewsSource)
                .WithMany()
                .HasForeignKey(e => e.NewsSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasOne(e => e.AlertSubscription)
                .WithMany(s => s.Alerts)
                .HasForeignKey(e => e.AlertSubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AlertItem>(entity =>
        {
            entity.HasIndex(e => new { e.AlertId, e.FeedItemId }).IsUnique();
            entity.HasOne(e => e.Alert)
                .WithMany(a => a.Items)
                .HasForeignKey(e => e.AlertId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FeedItem)
                .WithMany()
                .HasForeignKey(e => e.FeedItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SubscriptionTarget>(entity =>
        {
            entity.HasKey(e => new { e.AlertSubscriptionId, e.NotificationTargetId });
            entity.HasOne(e => e.AlertSubscription)
                .WithMany(s => s.Targets)
                .HasForeignKey(e => e.AlertSubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.NotificationTarget)
                .WithMany()
                .HasForeignKey(e => e.NotificationTargetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AlertDelivery>(entity =>
        {
            entity.Property(e => e.LastError).HasMaxLength(1000);
            entity.HasIndex(e => new { e.AlertId, e.NotificationTargetId }).IsUnique();
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt });
            entity.HasOne(e => e.Alert)
                .WithMany(a => a.Deliveries)
                .HasForeignKey(e => e.AlertId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.NotificationTarget)
                .WithMany()
                .HasForeignKey(e => e.NotificationTargetId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
