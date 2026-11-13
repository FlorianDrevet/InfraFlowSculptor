namespace InfraFlowSculptor.Domain.RevisionAggregate;

public enum RevisionState
{
    Queued,
    Generating,
    Generated,
    Failed
}
