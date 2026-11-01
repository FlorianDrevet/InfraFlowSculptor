using System.Text;
using System.Text.Json;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.Bicep;

internal static class ReleaseDataEmitter
{
    public static GeneratedFile Emit(ComponentPlan component, TargetPlan target)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(target);

        var release = target.Release;
        var content = new StringBuilder();
        content.AppendLine("{");
        WriteString(content, "schema", release.Schema, true);
        WriteString(content, "project", release.Project, true);
        WriteString(content, "component", release.Component, true);
        WriteString(content, "target", release.Target, true);
        WriteString(content, "unit", release.Unit, true);
        WriteString(content, "subscriptionId", release.SubscriptionId.ToString("D"), true);
        WriteString(content, "location", release.Location, true);
        WriteBoolean(content, "protected", release.Protected, true);
        WriteNullableString(content, "deploymentIdentityObjectId", release.DeploymentIdentityObjectId, true);
        WriteDependencies(content, release.Dependencies, true);
        WriteSecretWrites(content, release.SecretWrites, true);
        WriteSecretReferences(content, release.SecretReferences, true);
        WriteAppOwnedState(content, release.AppOwnedState, true);
        WriteDataAccess(content, release.DataAccess, true);
        WriteStringArray(content, "accessObjectTypes", release.AccessObjectTypes, true);
        WriteMultilineStringArray(content, "restartOnSecretChange", release.RestartOnSecretChange,
            release.ExplicitAccessRevocations is not null);
        if (release.ExplicitAccessRevocations is not null)
        {
            WriteMultilineStringArray(content, "explicitAccessRevocations", release.ExplicitAccessRevocations, false);
        }

        content.AppendLine("}");
        return new GeneratedFile(
            $"{component.ComponentCode}/infra/release.{target.Target.Code}.json",
            content.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static void WriteDependencies(StringBuilder content, IReadOnlyList<ReleaseDependencyPlan> dependencies, bool comma)
    {
        if (dependencies.Count == 0)
        {
            WriteEmptyArray(content, "dependencies", comma);
            return;
        }

        content.AppendLine("  \"dependencies\": [");
        for (var dependencyIndex = 0; dependencyIndex < dependencies.Count; dependencyIndex++)
        {
            var dependency = dependencies[dependencyIndex];
            content.AppendLine("    {");
            WriteString(content, "component", dependency.Component, true, 6);
            content.AppendLine("      \"resources\": [");
            for (var resourceIndex = 0; resourceIndex < dependency.Resources.Count; resourceIndex++)
            {
                var resource = dependency.Resources[resourceIndex];
                content.AppendLine("        {");
                WriteString(content, "id", resource.Id, false, 10);
                content.Append("        }");
                content.AppendLine(resourceIndex + 1 < dependency.Resources.Count ? "," : string.Empty);
            }

            content.AppendLine("      ]");
            content.Append("    }");
            content.AppendLine(dependencyIndex + 1 < dependencies.Count ? "," : string.Empty);
        }

        content.Append("  ]");
        content.AppendLine(comma ? "," : string.Empty);
    }

    private static void WriteSecretWrites(StringBuilder content, IReadOnlyList<ReleaseSecretWritePlan> values, bool comma)
    {
        if (values.Count == 0)
        {
            WriteEmptyArray(content, "secretWrites", comma);
            return;
        }

        content.AppendLine("  \"secretWrites\": [");
        for (var index = 0; index < values.Count; index++)
        {
            var item = values[index];
            content.AppendLine("    {");
            WriteString(content, "variable", item.Variable, true, 6);
            WriteString(content, "vault", item.Vault, true, 6);
            WriteString(content, "secret", item.Secret, true, 6);
            WriteString(content, "source", item.Source, false, 6);
            content.Append("    }");
            content.AppendLine(index + 1 < values.Count ? "," : string.Empty);
        }

        content.Append("  ]");
        content.AppendLine(comma ? "," : string.Empty);
    }

    private static void WriteSecretReferences(StringBuilder content, IReadOnlyList<ReleaseSecretReferencePlan> values, bool comma)
    {
        if (values.Count == 0)
        {
            WriteEmptyArray(content, "secretReferences", comma);
            return;
        }

        content.AppendLine("  \"secretReferences\": [");
        for (var index = 0; index < values.Count; index++)
        {
            var item = values[index];
            content.AppendLine("    {");
            WriteString(content, "vault", item.Vault, true, 6);
            WriteString(content, "secret", item.Secret, true, 6);
            WriteString(content, "writer", item.Writer, false, 6);
            content.Append("    }");
            content.AppendLine(index + 1 < values.Count ? "," : string.Empty);
        }

        content.Append("  ]");
        content.AppendLine(comma ? "," : string.Empty);
    }

    private static void WriteAppOwnedState(StringBuilder content, IReadOnlyList<ReleaseAppOwnedStatePlan> values, bool comma)
    {
        if (values.Count == 0)
        {
            WriteEmptyArray(content, "appOwnedState", comma);
            return;
        }

        content.AppendLine("  \"appOwnedState\": [");
        for (var index = 0; index < values.Count; index++)
        {
            var item = values[index];
            content.AppendLine("    {");
            WriteString(content, "resourceId", item.ResourceId, true, 6);
            WriteString(content, "parameter", item.Parameter, true, 6);
            WriteString(content, "kind", item.Kind, true, 6);
            WriteString(content, "bootstrapImage", item.BootstrapImage, false, 6);
            content.Append("    }");
            content.AppendLine(index + 1 < values.Count ? "," : string.Empty);
        }

        content.Append("  ]");
        content.AppendLine(comma ? "," : string.Empty);
    }

    private static void WriteDataAccess(StringBuilder content, IReadOnlyList<ReleaseDataAccessPlan> values, bool comma)
    {
        if (values.Count == 0)
        {
            WriteEmptyArray(content, "dataAccess", comma);
            return;
        }

        content.AppendLine("  \"dataAccess\": [");
        for (var index = 0; index < values.Count; index++)
        {
            var item = values[index];
            content.AppendLine("    {");
            WriteString(content, "server", item.Server, true, 6);
            WriteString(content, "database", item.Database, true, 6);
            content.AppendLine("      \"principal\": {");
            WriteString(content, "resourceId", item.Principal.ResourceId, true, 8);
            WriteString(content, "kind", item.Principal.Kind, false, 8);
            content.AppendLine("      },");
            WriteString(content, "level", item.Level, true, 6);
            WriteString(content, "exposure", item.Exposure, false, 6);
            content.Append("    }");
            content.AppendLine(index + 1 < values.Count ? "," : string.Empty);
        }

        content.Append("  ]");
        content.AppendLine(comma ? "," : string.Empty);
    }

    private static void WriteStringArray(StringBuilder content, string name, IReadOnlyList<string> values, bool comma)
    {
        content.Append("  ").Append(Json(name)).Append(": [");
        content.Append(string.Join(", ", values.Select(Json)));
        content.Append(']');
        content.AppendLine(comma ? "," : string.Empty);
    }

    private static void WriteMultilineStringArray(StringBuilder content, string name, IReadOnlyList<string> values, bool comma)
    {
        if (values.Count == 0)
        {
            WriteEmptyArray(content, name, comma);
            return;
        }

        content.AppendLine("  \"" + name + "\": [");
        for (var index = 0; index < values.Count; index++)
        {
            content.Append("    ").Append(Json(values[index]));
            content.AppendLine(index + 1 < values.Count ? "," : string.Empty);
        }

        content.Append("  ]");
        content.AppendLine(comma ? "," : string.Empty);
    }

    private static void WriteEmptyArray(StringBuilder content, string name, bool comma) =>
        content.Append("  ").Append(Json(name)).Append(": []").AppendLine(comma ? "," : string.Empty);

    private static void WriteString(StringBuilder content, string name, string value, bool comma, int indentation = 2) =>
        content.Append(' ', indentation).Append(Json(name)).Append(": ").Append(Json(value)).AppendLine(comma ? "," : string.Empty);

    private static void WriteNullableString(StringBuilder content, string name, string? value, bool comma, int indentation = 2) =>
        content.Append(' ', indentation).Append(Json(name)).Append(": ").Append(value is null ? "null" : Json(value)).AppendLine(comma ? "," : string.Empty);

    private static void WriteBoolean(StringBuilder content, string name, bool value, bool comma) =>
        content.Append("  ").Append(Json(name)).Append(": ").Append(value ? "true" : "false").AppendLine(comma ? "," : string.Empty);

    private static string Json(string value) => JsonSerializer.Serialize(value);
}
