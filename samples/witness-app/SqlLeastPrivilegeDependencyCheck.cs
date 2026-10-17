using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public sealed class SqlLeastPrivilegeDependencyCheck(
    IConfiguration configuration,
    SqlConnectionStringFactory connectionStringFactory) : IDependencyCheck
{
    public string Name => "sql-least-privilege";

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
        await using var command = new SqlCommand(
            "CREATE TABLE dbo.witness_forbidden (id int NOT NULL);",
            connection,
            transaction);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
            return new DependencyCheckOutcome(false, "CREATE TABLE autorisé");
        }
        catch (SqlException exception) when (exception.Number == 262)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new DependencyCheckOutcome(true, null);
        }
    }
}
