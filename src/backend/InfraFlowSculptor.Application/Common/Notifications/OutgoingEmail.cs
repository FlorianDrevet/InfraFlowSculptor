namespace InfraFlowSculptor.Application.Common.Notifications;

public sealed record OutgoingEmail(
    string From,
    string To,
    string Subject,
    string HtmlBody,
    string TextBody);
