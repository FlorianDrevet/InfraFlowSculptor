using AwesomeAssertions;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Tags;
using InfraFlowSculptor.Engine.Targets;
using InfraFlowSculptor.Engine.Tests.Builders;

namespace InfraFlowSculptor.Engine.Tests.Tags;

public sealed class TagResolverTests
{
    private static readonly Guid DevId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid GroupId = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [Fact]
    public void ResourceTagsOverrideGroupComponentEnvironmentAndProjectTagsCaseInsensitively()
    {
        var target = Environment();
        var resource = Resource(new Dictionary<string, string>
        {
            ["Owner"] = "resource",
            ["resourceOnly"] = "resource"
        });
        var component = Component(resource, new Dictionary<string, string>
        {
            ["owner"] = "component",
            ["componentOnly"] = "component"
        });
        var snapshot = new ModelSnapshotBuilder()
            .WithProjectTags(Pair("owner", "project"), Pair("projectOnly", "project"))
            .WithEnvironment(target with { Tags = Tags(Pair("OWNER", "environment"), Pair("environmentOnly", "environment")) })
            .WithComponent(component)
            .Build();
        var resolvedTarget = new ResolvedResourceTarget(
            new ResolvedTarget(DevId, "dev", Guid.NewGuid(), "francecentral", Tags(
                Pair("owner", "environment"),
                Pair("environmentOnly", "environment")), false),
            "francecentral",
            true);

        var tags = TagResolver.Resolve(snapshot, component, component.ResourceGroups[0], resource, resolvedTarget);

        tags["OWNER"].Should().Be("resource");
        tags["projectOnly"].Should().Be("project");
        tags["environmentOnly"].Should().Be("environment");
        tags["componentOnly"].Should().Be("component");
        tags["resourceOnly"].Should().Be("resource");
    }

    [Fact]
    public void ResourceGroupTagsOverrideComponentEnvironmentAndProjectTags()
    {
        var resource = Resource(new Dictionary<string, string>());
        var component = Component(resource, Tags(Pair("costCenter", "component"))) with
        {
            ResourceGroups = [Group(Tags(Pair("costCenter", "group")))]
        };
        var snapshot = new ModelSnapshotBuilder()
            .WithProjectTags(Pair("costCenter", "project"))
            .WithEnvironment(Environment() with { Tags = Tags(Pair("costCenter", "environment")) })
            .WithComponent(component)
            .Build();
        var resolvedTarget = new ResolvedResourceTarget(
            new ResolvedTarget(DevId, "dev", Guid.NewGuid(), "francecentral", Tags(Pair("costCenter", "environment")), false),
            "francecentral",
            true);

        var tags = TagResolver.Resolve(snapshot, component, component.ResourceGroups[0], resource, resolvedTarget);

        tags["costCenter"].Should().Be("group");
    }

    [Fact]
    public void SystemTagsOverrideUserTagsWhenEnabled()
    {
        var resource = Resource(Tags(Pair("ifs-project", "spoofed"), Pair("managed-by", "other")));
        var component = Component(resource, new Dictionary<string, string>());
        var snapshot = new ModelSnapshotBuilder().WithComponent(component).Build();
        var target = new ResolvedResourceTarget(
            new ResolvedTarget(DevId, "dev", Guid.NewGuid(), "francecentral", Tags(), false),
            "francecentral",
            true);

        var tags = TagResolver.Resolve(snapshot, component, component.ResourceGroups[0], resource, target);

        tags["ifs-project"].Should().Be("shop");
        tags["ifs-component"].Should().Be("core");
        tags["ifs-environment"].Should().Be("dev");
        tags["managed-by"].Should().Be("infraflowsculptor");
    }

    [Fact]
    public void SingleTargetUsesItsCodeAsTheSystemEnvironmentTag()
    {
        var resource = Resource(new Dictionary<string, string>());
        var component = Component(resource, new Dictionary<string, string>(), single: true);
        var snapshot = new ModelSnapshotBuilder().WithComponent(component).Build();
        var target = new ResolvedResourceTarget(
            new ResolvedTarget(Guid.NewGuid(), "shared", Guid.NewGuid(), "westeurope", Tags(), true),
            "westeurope",
            true);

        var tags = TagResolver.Resolve(snapshot, component, component.ResourceGroups[0], resource, target);

        tags["ifs-environment"].Should().Be("shared");
    }

    [Fact]
    public void SystemTagsAreOmittedWhenDisabled()
    {
        var resource = Resource(new Dictionary<string, string>());
        var component = Component(resource, new Dictionary<string, string>());
        var snapshot = new ModelSnapshotBuilder().WithSystemTagsEnabled(false).WithComponent(component).Build();
        var target = new ResolvedResourceTarget(
            new ResolvedTarget(DevId, "dev", Guid.NewGuid(), "francecentral", Tags(), false),
            "francecentral",
            true);

        var tags = TagResolver.Resolve(snapshot, component, component.ResourceGroups[0], resource, target);

        tags.Should().NotContainKey("ifs-project");
        tags.Should().NotContainKey("managed-by");
    }

    [Fact]
    public void SnapshotBuilderPreservesProjectSystemTagSettingUnlessExplicitlyOverridden()
    {
        var snapshot = new ModelSnapshotBuilder()
            .WithProject(new ProjectSnapshot("Boutique en ligne", "shop", string.Empty)
            {
                SystemTagsEnabled = false
            })
            .Build();

        snapshot.Project.SystemTagsEnabled.Should().BeFalse();
    }

    private static EnvironmentSnapshot Environment() => new("dev", "francecentral")
    {
        Id = DevId,
        Name = "Development",
        Position = 1,
        SubscriptionId = Guid.NewGuid()
    };

    private static ComponentSnapshot Component(
        ResourceSnapshot resource,
        IReadOnlyDictionary<string, string> tags,
        bool single = false) => new(
        Guid.NewGuid(),
        "Core",
        "core",
        single ? "Single" : "PerEnvironment",
        single ? [] : [DevId],
        single ? new ComponentSingleTargetSnapshot(Guid.NewGuid(), "shared", Guid.NewGuid(), "westeurope", "deployment", false, [], []) : null,
        [Group(new Dictionary<string, string>())],
        [resource])
    {
        TargetsAllEnvironments = false,
        Tags = tags
    };

    private static ResourceGroupSnapshot Group(IReadOnlyDictionary<string, string> tags) => new(
        GroupId,
        "main",
        null,
        tags,
        new Dictionary<Guid, string>());

    private static ResourceSnapshot Resource(IReadOnlyDictionary<string, string> tags) => new(
        Guid.NewGuid(),
        GroupId,
        "KeyVault",
        "main",
        string.Empty,
        false,
        new Dictionary<string, System.Text.Json.JsonElement>(),
        new Dictionary<Guid, IReadOnlyDictionary<string, System.Text.Json.JsonElement>>(),
        new Dictionary<Guid, bool>(),
        new Dictionary<Guid, string>(),
        new Dictionary<Guid, string>(),
        tags,
        []);

    private static KeyValuePair<string, string> Pair(string key, string value) => new(key, value);

    private static Dictionary<string, string> Tags(params KeyValuePair<string, string>[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase);
}
