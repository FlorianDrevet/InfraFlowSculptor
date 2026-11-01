using InfraFlowSculptor.Application.Naming;

namespace InfraFlowSculptor.Application.Projects;

public sealed record ProjectNamingSettings(
    Guid ProjectId,
    int Version,
    string Preset,
    string DefaultTemplate,
    string ResourceGroupTemplate,
    bool IsPublished,
    IReadOnlyList<NamingOverrideValue> TypeOverrides);
