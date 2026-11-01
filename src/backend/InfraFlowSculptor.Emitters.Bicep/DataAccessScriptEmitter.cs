using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.Bicep;

internal static class DataAccessScriptEmitter
{
    public static IReadOnlyList<GeneratedScript> Emit(ComponentPlan component)
    {
        ArgumentNullException.ThrowIfNull(component);

        var accesses = component.Targets
            .SelectMany(target => target.DataAccess)
            .ToArray();
        if (accesses.Length == 0)
        {
            return [];
        }

        var applyScript = accesses[0].ApplyScript;
        var removeScript = accesses[0].RemoveScript;
        if (accesses.Any(access => !string.Equals(access.ApplyScript, applyScript, StringComparison.Ordinal)
                || !string.Equals(access.RemoveScript, removeScript, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Data-access scripts for component '{component.ComponentCode}' must be identical across targets.");
        }

        return
        [
            new GeneratedScript("scripts/data-access.sql", applyScript),
            new GeneratedScript("scripts/data-access-remove.sql", removeScript)
        ];
    }
}

internal sealed record GeneratedScript(string RelativePath, string Content);
