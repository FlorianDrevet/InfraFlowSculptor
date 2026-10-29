using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class IfsDbContextConcurrencyExceptionTests
{
    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation, "ux_finding_acknowledgments_identity", true)]
    [InlineData(PostgresErrorCodes.UniqueViolation, "pk_organization_members", false)]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, "ux_finding_acknowledgments_identity", false)]
    [InlineData(PostgresErrorCodes.UniqueViolation, "another_unique_index", false)]
    public void FindingAcknowledgmentClassifierRequiresTheExactUniqueConstraint(
        string sqlState,
        string constraintName,
        bool expected)
    {
        var postgresException = new PostgresException(
            "duplicate key",
            "ERROR",
            "ERROR",
            sqlState,
            "",
            "",
            0,
            0,
            "",
            "",
            "public",
            "finding_acknowledgments",
            "",
            "",
            constraintName,
            "",
            "",
            "");
        var exception = new DbUpdateException("The write failed.", postgresException);

        Assert.Equal(expected, IfsDbContext.IsConcurrentFindingAcknowledgment(exception));
    }
}
