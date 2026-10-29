using CustomAlertManager.Data.Entities;
using CustomAlertManager.Scheduler.Models;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Data;

public sealed class AlertDbContext(DbContextOptions<AlertDbContext> options) : DbContext(options)
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
        modelBuilder.Entity<NotificationTarget>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Destination).HasMaxLength(2048);
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
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NewsSource>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Url).HasMaxLength(450);
            entity.HasIndex(e => e.Url).IsUnique();
        });

        modelBuilder.Entity<AlertSubscription>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_AlertSubscriptions_FrequencyMinutes",
                $"FrequencyMinutes IN ({string.Join(", ", AlertFrequency.PresetMinutes)})"));
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
    }
}
