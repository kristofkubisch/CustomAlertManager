using CustomAlertManager.Data.Services;

namespace CustomAlertManager.Tests.Data;

[Collection("SqlDatabase")]
public class SqlRunLockTests
{
    [Fact]
    public async Task TryAcquireAsync_WhileHeld_ReturnsNullUntilReleased()
    {
        var first = new SqlRunLock(SqlDatabaseFixture.ConnectionString);
        var second = new SqlRunLock(SqlDatabaseFixture.ConnectionString);

        var lease = await first.TryAcquireAsync(CancellationToken.None);
        Assert.NotNull(lease);
        Assert.Null(await second.TryAcquireAsync(CancellationToken.None));

        await lease.DisposeAsync();

        var reacquired = await second.TryAcquireAsync(CancellationToken.None);
        Assert.NotNull(reacquired);
        await reacquired.DisposeAsync();
    }
}
