using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Application.Generation;

public sealed record PipelineStepTranslation(
    string Name,
    bool Blocking,
    ApplicationStepPosition Position,
    string Template,
    IReadOnlyList<PipelineStepParameterTranslation> Parameters);
