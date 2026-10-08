using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public sealed class SqlConnectionStringFactory
{
    public SqlConnectionStringBuilder Create(IConfiguration configuration)
    {
        var server = configuration["Sql:Server"];
        var database = configuration["Sql:Database"];
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException("Sql:Server et Sql:Database doivent être configurés.");
        }

        return new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity,
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = false,
            ConnectTimeout = 15
        };
    }
}
