using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Scheduler.Models;
using CustomAlertManager.Tests.Data;
using CustomAlertManager.Web.Models;
using CustomAlertManager.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace CustomAlertManager.Tests.Web;

[Collection("SqlDatabase")]
public class SubscriptionServiceTests(SqlDatabaseFixture fixture)
{
    [Fact]
    public async Task CreateAsync_ThenListForOwner_ReturnsOnlyThatOwnersSubscriptions()
    {
        await using var db = NewContext();
        var owner = await CreateUserAsync(db);
        var other = await CreateUserAsync(db);
        var source = await CreateSourceAsync(db);
        var service = new SubscriptionService(db);

        await service.CreateAsync(owner, Input("Mine", source), CancellationToken.None);

        await using var readDb = NewContext();
        var readService = new SubscriptionService(readDb);
        Assert.Single(await readService.ListForOwnerAsync(owner, CancellationToken.None), s => s.Name == "Mine");
        Assert.Empty(await readService.ListForOwnerAsync(other, CancellationToken.None));
    }

    [Fact]
    public async Task FindForOwnerAsync_OtherOwner_ReturnsNull()
    {
        await using var db = NewContext();
        var owner = await CreateUserAsync(db);
        var other = await CreateUserAsync(db);
        var source = await CreateSourceAsync(db);
        var created = await new SubscriptionService(db).CreateAsync(owner, Input("Private", source), CancellationToken.None);

        await using var readDb = NewContext();
        var service = new SubscriptionService(readDb);
        Assert.Null(await service.FindForOwnerAsync(created.Id, other, CancellationToken.None));
        Assert.NotNull(await service.FindForOwnerAsync(created.Id, owner, CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAsync_NoSources_ReturnsError()
    {
        await using var db = NewContext();
        var owner = await CreateUserAsync(db);
        var service = new SubscriptionService(db);

        var errors = await service.ValidateAsync(Input("No sources"), owner, CancellationToken.None);

        Assert.Contains(errors, error => error.Contains("at least one news source"));
    }

    [Fact]
    public async Task ValidateAsync_FrequencyNotAPreset_ReturnsError()
    {
        await using var db = NewContext();
        var owner = await CreateUserAsync(db);
        var source = await CreateSourceAsync(db);
        var service = new SubscriptionService(db);

        var errors = await service.ValidateAsync(Input("Odd", source) with { FrequencyMinutes = 7 }, owner, CancellationToken.None);

        Assert.Contains(errors, error => error.Contains("frequencies"));
    }

    [Fact]
    public async Task ValidateAsync_UnconfirmedEmailTarget_IsRejected()
    {
        await using var db = NewContext();
        var owner = await CreateUserAsync(db);
        var source = await CreateSourceAsync(db);
        var target = await CreateTargetAsync(db, owner, verified: false);
        var service = new SubscriptionService(db);

        var errors = await service.ValidateAsync(
            Input("Unconfirmed", source) with { TargetIds = [target] }, owner, CancellationToken.None);

        Assert.Contains(errors, error => error.Contains("chosen targets"));
    }

    [Fact]
    public async Task ValidateAsync_ValidInput_ReturnsNoErrors()
    {
        await using var db = NewContext();
        var owner = await CreateUserAsync(db);
        var source = await CreateSourceAsync(db);
        var target = await CreateTargetAsync(db, owner, verified: true);
        var service = new SubscriptionService(db);

        var errors = await service.ValidateAsync(
            Input("Valid", source) with { TargetIds = [target], IncludeKeywords = "election" },
            owner,
            CancellationToken.None);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesLinksIncludingReAddedSource_KeepsLastAlertAt()
    {
        await using var db = NewContext();
        var owner = await CreateUserAsync(db);
        var first = await CreateSourceAsync(db);
        var second = await CreateSourceAsync(db);
        var target = await CreateTargetAsync(db, owner, verified: true);
        var service = new SubscriptionService(db);
        var created = await service.CreateAsync(owner, Input("Edited", first) with { TargetIds = [target], IncludeKeywords = "old" }, CancellationToken.None);

        // The first source is kept, so its link row is removed and re-added in the same update.
        await service.UpdateAsync(
            created.Id,
            owner,
            Input("Edited", first, second) with { IncludeKeywords = "new", ExcludeKeywords = "sport" },
            CancellationToken.None);

        await using var readDb = NewContext();
        var saved = await readDb.AlertSubscriptions
            .Include(s => s.Sources)
            .Include(s => s.Keywords)
            .Include(s => s.Targets)
            .AsSplitQuery()
            .SingleAsync(s => s.Id == created.Id);
        Assert.Equal(new[] { first, second }.OrderBy(id => id), saved.Sources.Select(link => link.NewsSourceId).OrderBy(id => id));
        Assert.Equal(["new"], saved.Keywords.Where(k => k.Kind == KeywordKind.Include).Select(k => k.Term));
        Assert.Equal(["sport"], saved.Keywords.Where(k => k.Kind == KeywordKind.Exclude).Select(k => k.Term));
        Assert.Empty(saved.Targets);
        Assert.Equal(created.LastAlertAt, saved.LastAlertAt);
    }

    [Fact]
    public async Task DeleteAsync_RemovesSubscriptionAndItsLinks()
    {
        await using var db = NewContext();
        var owner = await CreateUserAsync(db);
        var source = await CreateSourceAsync(db);
        var service = new SubscriptionService(db);
        var created = await service.CreateAsync(owner, Input("Doomed", source) with { IncludeKeywords = "x" }, CancellationToken.None);
        var subscription = await service.FindForOwnerAsync(created.Id, owner, CancellationToken.None);

        await service.DeleteAsync(subscription!, CancellationToken.None);

        await using var readDb = NewContext();
        Assert.False(await readDb.AlertSubscriptions.AnyAsync(s => s.Id == created.Id));
        Assert.False(await readDb.SubscriptionSources.AnyAsync(l => l.AlertSubscriptionId == created.Id));
        Assert.False(await readDb.SubscriptionKeywords.AnyAsync(k => k.AlertSubscriptionId == created.Id));
    }

    private static SubscriptionInput Input(string name, params int[] sourceIds) => new()
    {
        Name = name,
        FrequencyMinutes = AlertFrequency.PresetMinutes[2],
        SourceIds = sourceIds.ToList(),
    };

    private AlertDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AlertDbContext>().UseSqlServer(SqlDatabaseFixture.ConnectionString).Options);

    private static async Task<int> CreateUserAsync(AlertDbContext db)
    {
        var user = new ApplicationUser
        {
            UserName = $"{Guid.NewGuid():N}@example.com",
            Email = $"{Guid.NewGuid():N}@example.com",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<int> CreateSourceAsync(AlertDbContext db)
    {
        var source = new NewsSource { Name = "Test source", Url = $"https://{Guid.NewGuid():N}.example/rss" };
        db.NewsSources.Add(source);
        await db.SaveChangesAsync();
        return source.Id;
    }

    private static async Task<int> CreateTargetAsync(AlertDbContext db, int ownerId, bool verified)
    {
        var target = new NotificationTarget
        {
            OwnerId = ownerId,
            Name = "Target",
            Kind = ChannelKind.Email,
            Destination = $"{Guid.NewGuid():N}@example.com",
            IsVerified = verified,
        };
        db.NotificationTargets.Add(target);
        await db.SaveChangesAsync();
        return target.Id;
    }
}
