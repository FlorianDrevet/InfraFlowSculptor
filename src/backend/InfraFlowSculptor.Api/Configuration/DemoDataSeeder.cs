using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Infrastructure.Azure;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Infrastructure.Persistence.Admin;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InfraFlowSculptor.Api.Configuration;

public static class DemoDataSeeder
{
    private static readonly OrganizationId ContosoId = new(Guid.Parse("33333333-3333-4333-8333-333333333333"));
    private static readonly OrganizationId FabrikamId = new(Guid.Parse("44444444-4444-4444-8444-444444444444"));
    public static readonly ProjectId ShopProjectId = new(Guid.Parse("77777777-7777-4777-8777-777777777777"));

    private static readonly DemoUser[] Users =
    [
        new("11111111-1111-1111-1111-111111111111", "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "Alice Martin", "alice@contoso.example"),
        new("11111111-1111-1111-1111-111111111111", "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb", "Bob Bernard", "bob@contoso.example"),
        new("11111111-1111-1111-1111-111111111111", "cccccccc-cccc-4ccc-8ccc-cccccccccccc", "Chloé Petit", "chloe@contoso.example"),
        new("22222222-2222-2222-2222-222222222222", "dddddddd-dddd-4ddd-8ddd-dddddddddddd", "David Moreau", "david@fabrikam.example"),
        new("9188040d-6c67-4c5b-b112-36a304b66dad", "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee", "Emma Laurent", "emma@outlook.example"),
        new("11111111-1111-1111-1111-111111111111", "ffffffff-ffff-4fff-8fff-ffffffffffff", "Nina Moreau", "nina@contoso.example")
    ];

    public static bool IsInvocation(IReadOnlyList<string> args) =>
        args.Count >= 2
        && string.Equals(args[0], "seed", StringComparison.OrdinalIgnoreCase)
        && string.Equals(args[1], "demo", StringComparison.OrdinalIgnoreCase);

    public static async Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder([]);
        var credential = new IfsAzureCredential();
        using var dataSource = PostgresDataSourceFactory.Create(builder.Configuration, "ifs", credential);
        var options = new DbContextOptionsBuilder<IfsDbContext>()
            .UseNpgsql(dataSource)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var dbContext = new IfsDbContext(options, new NoCurrentOrganization());
        await dbContext.Database.MigrateAsync();
        await SeedAsync(dbContext, TimeProvider.System);
        Console.WriteLine("Les profils de démonstration, Contoso, Fabrikam et le projet Contoso/shop sont prêts.");
    }

    public static async Task SeedAsync(IfsDbContext dbContext, TimeProvider clock)
    {
        var profiles = new Dictionary<string, UserProfile>(StringComparer.Ordinal);
        foreach (var user in Users)
        {
            var tenantId = Guid.Parse(user.TenantId);
            var objectId = Guid.Parse(user.ObjectId);
            var profile = await dbContext.UserProfiles.SingleOrDefaultAsync(candidate =>
                candidate.TenantId == tenantId && candidate.ObjectId == objectId);

            if (profile is null)
            {
                var created = UserProfile.Create(
                    UserProfileId.New(clock),
                    new UserKey(tenantId, objectId),
                    user.DisplayName,
                    user.Email,
                    clock.GetUtcNow());
                profile = created.Value;
                dbContext.UserProfiles.Add(profile);
            }
            else
            {
                profile.SynchronizeIdentity(user.DisplayName, user.Email, clock.GetUtcNow());
            }

            profiles.Add(user.ObjectId, profile);
        }

        var contoso = await dbContext.Organizations
            .Include(organization => organization.Members)
            .SingleOrDefaultAsync(organization => organization.Id == ContosoId);
        if (contoso is null)
        {
            contoso = OrganizationAggregate.Create(ContosoId, "Contoso", profiles[Users[0].ObjectId].Id).Value;
            dbContext.Organizations.Add(contoso);
        }

        AddMemberIfMissing(contoso, profiles[Users[1].ObjectId].Id, OrganizationRole.Member);
        AddMemberIfMissing(contoso, profiles[Users[2].ObjectId].Id, OrganizationRole.Member);

        var fabrikam = await dbContext.Organizations
            .Include(organization => organization.Members)
            .SingleOrDefaultAsync(organization => organization.Id == FabrikamId);
        if (fabrikam is null)
        {
            fabrikam = OrganizationAggregate.Create(FabrikamId, "Fabrikam", profiles[Users[3].ObjectId].Id).Value;
            dbContext.Organizations.Add(fabrikam);
        }

        await dbContext.SaveChangesAsync();
        await new EfDemoProjectSeeder(dbContext).SeedShopAsync(
            ShopProjectId,
            ContosoId,
            profiles[Users[0].ObjectId].Id,
            profiles[Users[1].ObjectId].Id,
            profiles[Users[2].ObjectId].Id);
    }

    private static void AddMemberIfMissing(
        OrganizationAggregate organization,
        UserProfileId profileId,
        OrganizationRole role)
    {
        if (organization.Members.All(member => member.UserProfileId != profileId))
        {
            _ = organization.AddMember(profileId, [role]);
        }
    }

    private sealed record DemoUser(string TenantId, string ObjectId, string DisplayName, string Email);

    private sealed class NoCurrentOrganization : ICurrentOrganization
    {
        public OrganizationId? Id => null;
    }
}
