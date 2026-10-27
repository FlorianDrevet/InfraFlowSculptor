using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class SqlConnectionStringFactoryTests
{
    [Fact]
    public void Create_WhenUserAssignedIdentityEnvironmentVariableExists_UsesSystemManagedIdentity()
    {
        const string identityClientId = "user-assigned-identity-id";
        var priorClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", identityClientId);

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Sql:Server"] = "sql.example.test",
                    ["Sql:Database"] = "witness"
                })
                .Build();

            var connectionString = new SqlConnectionStringFactory().Create(configuration);

            connectionString.DataSource.Should().Be("sql.example.test");
            connectionString.InitialCatalog.Should().Be("witness");
            connectionString.Authentication.Should().Be(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity);
            connectionString.UserID.Should().BeEmpty();
            connectionString.Password.Should().BeEmpty();
            connectionString.Encrypt.Should().Be(SqlConnectionEncryptOption.Mandatory);
            connectionString.TrustServerCertificate.Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", priorClientId);
        }
    }
}
