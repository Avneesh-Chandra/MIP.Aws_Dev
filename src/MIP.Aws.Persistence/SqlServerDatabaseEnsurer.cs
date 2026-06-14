using Microsoft.Data.SqlClient;

namespace MIP.Aws.Persistence;

/// <summary>
/// Creates a SQL Server database if it does not exist (connects to <c>master</c>).
/// Used for auxiliary databases such as Hangfire when EF migrations only target the main catalog.
/// </summary>
public static class SqlServerDatabaseEnsurer
{
    public static async Task EnsureDatabaseExistsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var target = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(target.InitialCatalog))
        {
            throw new InvalidOperationException("Connection string must specify Initial Catalog (database name).");
        }

        var database = target.InitialCatalog;
        target.InitialCatalog = "master";

        await using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var check = new SqlCommand("SELECT 1 FROM sys.databases WHERE name = @name", connection))
        {
            check.Parameters.AddWithValue("@name", database);
            var exists = await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
            if (exists)
            {
                return;
            }
        }

        var escaped = database.Replace("]", "]]", StringComparison.Ordinal);
        var bracketed = "[" + escaped + "]";
        await using var create = new SqlCommand($"CREATE DATABASE {bracketed}", connection);
        await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
