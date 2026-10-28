using Azure;
using Azure.Communication.Email;
using InfraFlowSculptor.Application.Common.Notifications;

namespace InfraFlowSculptor.Infrastructure.Email;

public sealed class AzureCommunicationEmailSender(EmailClient emailClient) : IEmailSender
{
    public async Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        var content = new EmailContent(email.Subject)
        {
            Html = email.HtmlBody,
            PlainText = email.TextBody
        };
        var message = new EmailMessage(email.From, email.To, content);
        await emailClient.SendAsync(WaitUntil.Completed, message, cancellationToken);
    }
}
