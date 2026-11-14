namespace InfraFlowSculptor.Domain.PublicationAggregate;

public enum PublicationState
{
    Preparing,
    NeedsConfirmation,
    Prepared,
    Publishing,
    PartiallyPublished,
    Completed,
    Failed
}
