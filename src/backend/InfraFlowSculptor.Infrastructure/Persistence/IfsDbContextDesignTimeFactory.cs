using InfraFlowSculptor.Application.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InfraFlowSculptor.Infrastructure.Persistence;

public sealed class IfsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IfsDbContext>
{
    public IfsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IfsDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=ifs_design;Username=ifs_design;Password=ifs_design;SSL Mode=Disable")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new IfsDbContext(options, new EmptyCurrentOrganization());
    }

    private sealed class EmptyCurrentOrganization : ICurrentOrganization
    {
        public InfraFlowSculptor.Domain.Common.Identifiers.OrganizationId? Id => null;
    }
}
