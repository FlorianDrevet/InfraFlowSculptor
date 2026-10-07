using InfraFlowSculptor.Api.Controllers;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Domain.Common.Models;
using NetArchTest.Rules;
using System.Xml.Linq;

namespace InfraFlowSculptor.Architecture.Tests;

public sealed class LayerDependencyTests
{
    private static readonly string[] ExpectedDomainPackageReferences = ["ErrorOr"];

    [Fact]
    public void DomainProjectReferencesOnlyErrorOr()
    {
        var projectPath = Path.Combine(
            ProductionSources.BackendRoot,
            "InfraFlowSculptor.Domain",
            "InfraFlowSculptor.Domain.csproj");
        var project = XDocument.Load(projectPath);
        var packageReferences = project.Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .OfType<string>()
            .ToArray();

        Assert.Equal(ExpectedDomainPackageReferences, packageReferences);
        Assert.Empty(project.Descendants("ProjectReference"));
    }

    [Fact]
    public void DomainDoesNotDependOnOuterLayersOrInfrastructureFrameworks()
    {
        var result = Types.InAssembly(typeof(Entity<Guid>).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "InfraFlowSculptor.Application",
                "InfraFlowSculptor.Contracts",
                "InfraFlowSculptor.Infrastructure",
                "InfraFlowSculptor.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Azure.")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void ApplicationDoesNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(typeof(Registry<,>).Assembly)
            .ShouldNot()
            .HaveDependencyOn("InfraFlowSculptor.Infrastructure")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void ApiControllersAreStaticAndDoNotDependOnInfrastructure()
    {
        var apiAssembly = typeof(SystemController).Assembly;
        var controllers = apiAssembly.GetTypes()
            .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(controllers);
        Assert.All(controllers, controller =>
            Assert.True(controller.IsAbstract && controller.IsSealed, $"{controller.FullName} must be static."));

        var result = Types.InAssembly(apiAssembly)
            .That()
            .HaveNameEndingWith("Controller")
            .ShouldNot()
            .HaveDependencyOn("InfraFlowSculptor.Infrastructure")
            .GetResult();

        AssertSuccessful(result);
    }

    private static void AssertSuccessful(TestResult result)
    {
        var failingTypes = result.FailingTypeNames;
        var message = failingTypes is null
            ? "The architecture rule reported a failure without identifying a type."
            : string.Join(", ", failingTypes);

        Assert.True(result.IsSuccessful, message);
    }
}
