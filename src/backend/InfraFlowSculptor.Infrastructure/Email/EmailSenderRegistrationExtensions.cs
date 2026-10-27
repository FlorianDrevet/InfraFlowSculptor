using Azure.Communication.Email;
using InfraFlowSculptor.Application.Common.Notifications;
using InfraFlowSculptor.Infrastructure.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfraFlowSculptor.Infrastructure.Email;

public static class EmailSenderRegistrationExtensions
{
    public static IServiceCollection AddIfsEmailSender(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connection = AzureConnectionResolver.Resolve(
            configuration,
            EmailConfigurationKeys.ConnectionName,
            new IfsAzureCredential());
        var senderAddress = configuration[EmailConfigurationKeys.SenderAddress];
        if (string.IsNullOrWhiteSpace(senderAddress))
        {
            throw new InvalidOperationException(
                $"Configuration '{EmailConfigurationKeys.SenderAddress}' is required to send email.");
        }

        if (connection.Mode == AzureConnectionMode.ConnectionString)
        {
            var endpoint = MailPitSmtpEndpointParser.Parse(connection.ConnectionString!);
            services.TryAddSingleton<IEmailSender>(new SmtpEmailSender(endpoint));
            services.TryAddSingleton(new EmailSenderOptions(senderAddress));
            return services;
        }

        services.AddIfsEmailClient(EmailConfigurationKeys.ConnectionName);
        services.TryAddSingleton<IEmailSender, AzureCommunicationEmailSender>();
        services.TryAddSingleton(new EmailSenderOptions(senderAddress));
        return services;
    }
}
