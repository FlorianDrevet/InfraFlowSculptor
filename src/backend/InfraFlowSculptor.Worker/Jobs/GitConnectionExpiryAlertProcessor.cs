using System.Globalization;
using System.Net;
using InfraFlowSculptor.Application.Common.Notifications;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed class GitConnectionExpiryAlertProcessor(
    IIfsDbContext dbContext,
    WorkerOrganizationContext organizationContext,
    IEmailSender emailSender,
    IConfiguration configuration,
    TimeProvider clock)
{
    private const string DefaultSenderAddress = "no-reply@infraflowsculptor.local";

    public async Task CheckOrganizationAsync(OrganizationId organizationId, CancellationToken cancellationToken)
    {
        organizationContext.Set(organizationId);
        var organization = await dbContext.Query<OrganizationAggregate>()
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);
        if (organization is null)
        {
            return;
        }

        var administratorIds = organization.Members
            .Where(member => member.Roles.Contains(OrganizationRole.Administrator))
            .Select(member => member.UserProfileId)
            .ToArray();
        var profileRows = await dbContext.Query<UserProfile>()
            .Where(profile => administratorIds.Contains(profile.Id) && profile.Email != null)
            .Select(profile => new { profile.Email, profile.PreferredLanguage })
            .ToArrayAsync(cancellationToken);
        var administrators = profileRows
            .Select(profile => new AdministratorEmail(profile.Email!, profile.PreferredLanguage))
            .ToArray();
        if (administrators.Length == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var connections = await dbContext.Query<GitConnection>()
            .Where(connection => connection.AuthenticationMode == GitConnectionAuthenticationMode.PersonalAccessToken
                && connection.PersonalAccessTokenExpiresAt != null)
            .ToArrayAsync(cancellationToken);
        foreach (var connection in connections)
        {
            var expiresAt = connection.PersonalAccessTokenExpiresAt!.Value;
            var daysRemaining = expiresAt.UtcDateTime.Date.Subtract(today.ToDateTime(TimeOnly.MinValue)).Days;
            if (daysRemaining < 0)
            {
                continue;
            }

            if (daysRemaining <= 15 && !connection.Expiry15DayAlertSent)
            {
                await SendAlertAsync(connection, administrators, daysRemaining, expiresAt, cancellationToken);
                _ = connection.MarkExpiryAlertSent(15);
            }

            if (daysRemaining <= 3 && !connection.Expiry3DayAlertSent)
            {
                await SendAlertAsync(connection, administrators, daysRemaining, expiresAt, cancellationToken);
                _ = connection.MarkExpiryAlertSent(3);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SendAlertAsync(
        GitConnection connection,
        IReadOnlyCollection<AdministratorEmail> administrators,
        int daysRemaining,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        foreach (var administrator in administrators)
        {
            var french = administrator.PreferredLanguage == "fr";
            var safeName = WebUtility.HtmlEncode(connection.Name);
            var expirationDate = expiresAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var subject = french
                ? $"Expiration du jeton Git — {connection.Organization}"
                : $"Git token expiration — {connection.Organization}";
            var html = french
                ? $"<p>Le jeton de repli de la connexion <strong>{safeName}</strong> expire le {expirationDate}, dans {daysRemaining} jour(s). Renouvelez-le dans les paramètres des connexions de l’organisation.</p>"
                : $"<p>The fallback token for connection <strong>{safeName}</strong> expires on {expirationDate}, in {daysRemaining} day(s). Renew it in the organization's connection settings.</p>";
            var text = french
                ? $"Le jeton de repli de la connexion « {connection.Name} » expire le {expirationDate}, dans {daysRemaining} jour(s). Renouvelez-le dans les paramètres des connexions de l’organisation."
                : $"The fallback token for connection '{connection.Name}' expires on {expirationDate}, in {daysRemaining} day(s). Renew it in the organization's connection settings.";
            var email = new OutgoingEmail(
                configuration["Email:SenderAddress"] ?? DefaultSenderAddress,
                administrator.Email,
                subject,
                html,
                text);
            await emailSender.SendAsync(email, cancellationToken);
        }
    }

    private sealed record AdministratorEmail(string Email, string PreferredLanguage);
}
