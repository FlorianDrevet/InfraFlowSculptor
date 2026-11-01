using System.Text.Json;
using InfraFlowSculptor.Domain.Common.Extensibility;

namespace InfraFlowSculptor.Engine.Validation.Constraints;

public interface IResourceConstraintEvaluator : IKeyed<string>
{
    IReadOnlyList<string> GetReferencedProperties(JsonElement constraint);

    ResourceConstraintFailure? GetFailure(
        JsonElement constraint,
        IReadOnlyDictionary<string, JsonElement> values);
}
