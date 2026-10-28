using InfraFlowSculptor.Application.Common.Events;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Notifications;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Infrastructure.Email;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed class InvitationCreatedHandler(
    IJobDispatcher jobDispatcher,
    IfsDbContext dbContext,
    EmailSenderOptions emailOptions,
    IConfiguration configuration) : DomainEventHandler<InvitationCreated>
{
    public override async Task HandleAsync(
        InvitationCreated domainEvent,
        DomainEventExecutionContext context,
        CancellationToken cancellationToken)
    {
        var organizationId = new OrganizationId(domainEvent.OrganizationId);
        var organizationName = await dbContext.Organizations
            .Where(candidate => candidate.Id == organizationId)
            .Select(candidate => candidate.Name)
            .SingleAsync(cancellationToken);
        var baseUrl = configuration[InvitationConfigurationKeys.BaseUrl]
            ?? throw new InvalidOperationException(
                $"Configuration '{InvitationConfigurationKeys.BaseUrl}' is required to create invitation links.");
        var invitationUrl = new Uri(
            new Uri(baseUrl, UriKind.Absolute),
            $"/invitations/{Uri.EscapeDataString(domainEvent.Token)}");
        var rendered = InvitationEmailTemplateRenderer.Render(
            domainEvent.Language,
            organizationName,
            invitationUrl,
            domainEvent.ExpiresAt);
        var email = new OutgoingEmail(
            emailOptions.SenderAddress,
            domainEvent.Email,
            rendered.Subject,
            rendered.HtmlBody,
            rendered.TextBody);

        jobDispatcher.Enqueue(
            JobQueue.Notifications,
            organizationId,
            new SendNotificationJob(NotificationChannel.Email, email));
    }
}
