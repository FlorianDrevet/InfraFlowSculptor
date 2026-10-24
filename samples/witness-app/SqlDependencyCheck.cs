using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public sealed class SqlDependencyCheck(
    IConfiguration configuration,
    SqlConnectionStringFactory connectionStringFactory) : IDependencyCheck
{
    public string Name => "sql";

    public async Task<DependencyCheckOutcome?> CheckAsync(CancellationToken cancellationToken)
    {
        var server = configuration["Sql:Server"];
        var database = configuration["Sql:Database"];
        if (string.IsNullOrWhiteSpace(server) && string.IsNullOrWhiteSpace(database))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
        {
            return new DependencyCheckOutcome(false, "configuration SQL incomplète");
        }

        await using var connection = new SqlConnection(connectionStringFactory.Create(configuration).ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var id = Guid.NewGuid();
        const string expectedValue = "witness-check";
        await using (var insert = new SqlCommand(
            "INSERT INTO dbo.witness_checks (id, written_at, value) VALUES (@id, @writtenAt, @value);",
            connection,
            transaction))
        {
            insert.Parameters.Add("@id", System.Data.SqlDbType.UniqueIdentifier).Value = id;
            insert.Parameters.Add("@writtenAt", System.Data.SqlDbType.DateTime2).Value = DateTime.UtcNow;
            insert.Parameters.Add("@value", System.Data.SqlDbType.NVarChar, 100).Value = expectedValue;
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var read = new SqlCommand(
            "SELECT value FROM dbo.witness_checks WHERE id = @id;",
            connection,
            transaction))
        {
            read.Parameters.Add("@id", System.Data.SqlDbType.UniqueIdentifier).Value = id;
            var actualValue = await read.ExecuteScalarAsync(cancellationToken) as string;
            if (!string.Equals(actualValue, expectedValue, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new DependencyCheckOutcome(false, "lecture après écriture invalide");
            }
        }

        await transaction.RollbackAsync(cancellationToken);
        return new DependencyCheckOutcome(true, null);
    }
}
