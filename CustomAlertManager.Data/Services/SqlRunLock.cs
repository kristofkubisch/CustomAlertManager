using CustomAlertManager.Scheduler.Interfaces;
using Microsoft.Data.SqlClient;

namespace CustomAlertManager.Data.Services;

// Holds an exclusive SQL Server application lock for the life of the returned lease.
// The lock is tied to a dedicated connection, so it is also released if the process dies.
public sealed class SqlRunLock(string connectionString) : IRunLock
{
    private const string Resource = "CustomAlertManager.AlertRun";

    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;
                SELECT @result;
                """;
            command.Parameters.AddWithValue("@resource", Resource);

            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (result < 0)
            {
                await connection.DisposeAsync();
                return null;
            }

            return new Lease(connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class Lease(SqlConnection connection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';";
                command.Parameters.AddWithValue("@resource", Resource);
                await command.ExecuteNonQueryAsync();
            }

            await connection.DisposeAsync();
        }
    }
}
