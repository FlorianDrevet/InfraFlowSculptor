namespace InfraFlowSculptor.Infrastructure.Email;

public sealed record RenderedEmailTemplate(string Subject, string HtmlBody, string TextBody);
