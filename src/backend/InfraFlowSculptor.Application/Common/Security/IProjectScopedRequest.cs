namespace InfraFlowSculptor.Application.Common.Security;

public interface IProjectScopedRequest
{
    Guid ProjectId { get; }
}
