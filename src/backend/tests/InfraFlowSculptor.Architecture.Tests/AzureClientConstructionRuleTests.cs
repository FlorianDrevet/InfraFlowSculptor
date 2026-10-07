using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace InfraFlowSculptor.Architecture.Tests;

public sealed class AzureClientConstructionRuleTests
{
    [Fact]
    public void AzureClientsAreConstructedOnlyInsideInfrastructureAzure()
    {
        var violations = ProductionSources.SyntaxTrees
            .Where(tree => !IsAzureInfrastructureFile(tree.FilePath))
            .SelectMany(tree => tree.GetRoot().DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Where(creation => creation.Type.ToString() is "BlobServiceClient" or "ServiceBusClient")
                .Select(creation => $"{tree.FilePath}: {creation.Type}"))
            .Concat(FindRedisConnectionCalls())
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"Azure client construction must stay in Infrastructure/Azure: {string.Join(", ", violations)}");
    }

    private static IEnumerable<string> FindRedisConnectionCalls()
    {
        foreach (var tree in ProductionSources.SyntaxTrees.Where(tree => !IsAzureInfrastructureFile(tree.FilePath)))
        {
            var calls = tree.GetRoot().DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "ConnectionMultiplexer" },
                    Name.Identifier.ValueText: "Connect" or "ConnectAsync"
                });

            foreach (var call in calls)
            {
                yield return $"{tree.FilePath}: {call.Expression}";
            }
        }
    }

    private static bool IsAzureInfrastructureFile(string path)
    {
        var relativePath = Path.GetRelativePath(ProductionSources.BackendRoot, path);
        return relativePath.StartsWith(
            Path.Combine("InfraFlowSculptor.Infrastructure", "Azure") + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }
}
