namespace InfraFlowSculptor.Application.Common.Errors;

public static class FindingAcknowledgmentErrors
{
    public const string AlreadyAcknowledgedCode = "FINDING_ALREADY_ACKNOWLEDGED";

    public const string AlreadyAcknowledgedMessage =
        "This finding has already been acknowledged for the current object version.";
}
