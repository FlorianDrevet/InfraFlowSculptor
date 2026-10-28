using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Naming;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Engine.Naming;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Queries.PreviewProjectNaming;

[RequiresPermission(Permission.ProjectRead)]
public sealed record PreviewProjectNamingQuery(
    Guid ProjectId,
    string Preset,
    string DefaultTemplate,
    string ResourceGroupTemplate,
    IReadOnlyList<NamingOverrideValue> TypeOverrides)
    : IQuery<ErrorOr<NamingPreviewResult>>, IProjectScopedRequest;
