using System.Text.Json;
using AwesomeAssertions;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Targets;
using InfraFlowSculptor.Engine.Tests.Builders;

namespace InfraFlowSculptor.Engine.Tests.Targets;

public sealed class TargetResolverTests
{
    private static readonly Guid DevId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid PrdId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid SingleId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid GroupId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [Fact]
    public void PerEnvironmentTargetsFollowPromotionOrderAndUseTheGroupRegion()
    {
        var component = CreateComponent("PerEnvironment", allEnvironments: true, selectedEnvironmentIds: []);
        var snapshot = new ModelSnapshotBuilder()
            .WithEnvironment(Environment(PrdId, "prd", "uksouth", position: 2))
            .WithEnvironment(Environment(DevId, "dev", "francecentral", position: 1) with
            {
                Tags = new Dictionary<string, string> { ["stage"] = "development" }
            })
            .WithComponent(component)
            .Build();

        var targets = TargetResolver.ResolveResourceTargets(snapshot, component, component.Resources[0]);

        targets.Select(target => target.Target.Code).Should().Equal("dev", "prd");
        targets.Select(target => target.Region).Should().Equal("northeurope", "northeurope");
        targets.All(target => target.IsPresent).Should().BeTrue();
        targets[0].Target.Tags["stage"].Should().Be("development");
    }

    [Fact]
    public void SelectedPerEnvironmentTargetsExcludeOtherProjectEnvironments()
    {
        var component = CreateComponent("PerEnvironment", allEnvironments: false, selectedEnvironmentIds: [DevId]);
        var snapshot = new ModelSnapshotBuilder()
            .WithEnvironment(Environment(DevId, "dev", "francecentral", position: 1))
            .WithEnvironment(Environment(PrdId, "prd", "uksouth", position: 2))
            .WithComponent(component)
            .Build();

        var targets = TargetResolver.ResolveResourceTargets(snapshot, component, component.Resources[0]);

        targets.Select(target => target.Target.Id).Should().Equal(DevId);
    }

    [Fact]
    public void RejectsEnvironmentsWithoutIdsInsteadOfSilentlyDroppingThem()
    {
        var component = CreateComponent("PerEnvironment", allEnvironments: true, selectedEnvironmentIds: []);
        var snapshot = new ModelSnapshotBuilder()
            .WithEnvironment(new EnvironmentSnapshot("dev", "francecentral"))
            .WithComponent(component)
            .Build();

        var act = () => TargetResolver.ResolveResourceTargets(snapshot, component, component.Resources[0]);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Every project environment must have a non-empty id. (Parameter 'snapshot')");
    }

    [Fact]
    public void ExistingResourceIsAbsentUntilItsTargetHasAnExistingId()
    {
        var component = CreateComponent("PerEnvironment", allEnvironments: true, selectedEnvironmentIds: []);
        var resource = component.Resources[0] with
        {
            IsExisting = true,
            ExistingIds = new Dictionary<Guid, string> { [PrdId] = "/subscriptions/test/resource" },
            Presences = new Dictionary<Guid, bool> { [DevId] = false, [PrdId] = true }
        };
        component = component with { Resources = [resource] };
        var snapshot = new ModelSnapshotBuilder()
            .WithEnvironment(Environment(DevId, "dev", "francecentral", position: 1))
            .WithEnvironment(Environment(PrdId, "prd", "uksouth", position: 2))
            .WithComponent(component)
            .Build();

        var targets = TargetResolver.ResolveResourceTargets(snapshot, component, resource);

        targets.Select(target => target.IsPresent).Should().Equal(false, true);
    }

    [Fact]
    public void SingleComponentResolvesOnlyItsOwnTargetAndDoesNotInheritProjectEnvironmentPresence()
    {
        var component = CreateComponent("Single", allEnvironments: false, selectedEnvironmentIds: [], singleTargetId: SingleId);
        var snapshot = new ModelSnapshotBuilder()
            .WithEnvironment(Environment(DevId, "dev", "francecentral", position: 1))
            .WithEnvironment(Environment(PrdId, "prd", "uksouth", position: 2))
            .WithComponent(component)
            .Build();

        var targets = TargetResolver.ResolveResourceTargets(snapshot, component, component.Resources[0]);

        var target = targets.Should().ContainSingle().Subject;
        target.Target.Id.Should().Be(SingleId);
        target.Target.Code.Should().Be("shared");
        target.Target.IsSingle.Should().BeTrue();
        target.Region.Should().Be("westeurope");
        target.IsPresent.Should().BeTrue();
    }

    [Fact]
    public void ResourceGroupIsPresentOnlyInTargetsWhereAtLeastOneResourceIsPresent()
    {
        var component = CreateComponent("PerEnvironment", allEnvironments: true, selectedEnvironmentIds: []);
        var thirdTargetId = Guid.Parse("55555555-5555-4555-8555-555555555555");
        var firstResource = component.Resources[0] with
        {
            Presences = new Dictionary<Guid, bool> { [DevId] = true, [PrdId] = false, [thirdTargetId] = false }
        };
        var secondResource = component.Resources[0] with
        {
            Id = Guid.NewGuid(),
            Presences = new Dictionary<Guid, bool> { [DevId] = false, [PrdId] = true, [thirdTargetId] = false }
        };
        component = component with { Resources = [firstResource, secondResource] };
        var snapshot = new ModelSnapshotBuilder()
            .WithEnvironment(Environment(DevId, "dev", "francecentral", position: 1))
            .WithEnvironment(Environment(PrdId, "prd", "uksouth", position: 2))
            .WithEnvironment(Environment(thirdTargetId, "test", "westeurope", position: 3))
            .WithComponent(component)
            .Build();

        var targets = TargetResolver.ResolveResourceGroupTargets(snapshot, component, component.ResourceGroups[0]);

        targets.Select(target => target.Target.Code).Should().Equal("dev", "prd", "test");
        targets.Select(target => target.IsPresent).Should().Equal(true, true, false);
    }

    private static EnvironmentSnapshot Environment(Guid id, string code, string region, int position) =>
        new(code, region)
        {
            Id = id,
            Name = code,
            Position = position,
            SubscriptionId = Guid.NewGuid()
        };

    private static ComponentSnapshot CreateComponent(
        string mode,
        bool allEnvironments,
        IReadOnlyList<Guid> selectedEnvironmentIds,
        Guid? singleTargetId = null)
    {
        var group = new ResourceGroupSnapshot(
            GroupId,
            "main",
            mode == "Single" ? null : "northeurope",
            new Dictionary<string, string>(),
            new Dictionary<Guid, string>());
        var resource = new ResourceSnapshot(
            Guid.NewGuid(),
            GroupId,
            "KeyVault",
            "main",
            string.Empty,
            false,
            new Dictionary<string, JsonElement>(),
            new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(),
            new Dictionary<Guid, bool>(),
            new Dictionary<Guid, string>(),
            new Dictionary<Guid, string>(),
            new Dictionary<string, string>(),
            []);
        return new ComponentSnapshot(
            Guid.NewGuid(),
            "Core",
            "core",
            mode,
            selectedEnvironmentIds,
            singleTargetId is { } targetId ? new ComponentSingleTargetSnapshot(
                targetId,
                "shared",
                Guid.NewGuid(),
                "westeurope",
                "deployment",
                false,
                [],
                []) : null,
            [group],
            [resource])
        {
            TargetsAllEnvironments = allEnvironments
        };
    }
}
