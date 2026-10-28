using InfraFlowSculptor.Application.Common.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace InfraFlowSculptor.Infrastructure.Email;

public sealed class SmtpEmailSender(SmtpEndpoint endpoint) : IEmailSender
{
    public async Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(email.From));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;
        message.Body = new BodyBuilder
        {
            HtmlBody = email.HtmlBody,
            TextBody = email.TextBody
        }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(endpoint.Host, endpoint.Port, SecureSocketOptions.None, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
