namespace InfraFlowSculptor.Domain.PublicationAggregate;

public enum PublicationTargetState
{
    Preparing,
    NeedsConfirmation,
    Prepared,
    Publishing,
    Completed,
    Failed
}
