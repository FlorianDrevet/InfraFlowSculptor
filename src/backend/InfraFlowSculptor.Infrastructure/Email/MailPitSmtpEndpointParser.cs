using System.Data.Common;

namespace InfraFlowSculptor.Infrastructure.Email;

public static class MailPitSmtpEndpointParser
{
    private const string EndpointProperty = "endpoint";

    public static SmtpEndpoint Parse(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        try
        {
            var properties = new DbConnectionStringBuilder { ConnectionString = connectionString };
            if (!properties.TryGetValue(EndpointProperty, out var endpointValue)
                || endpointValue is not string endpointText
                || !Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint)
                || endpoint.Scheme != "smtp"
                || string.IsNullOrWhiteSpace(endpoint.Host)
                || endpoint.IsDefaultPort
                || endpoint.Port is < 1 or > 65535)
            {
                throw new FormatException("The MailPit email connection string must include endpoint=smtp://host:port.");
            }

            return new SmtpEndpoint(endpoint.Host, endpoint.Port);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The MailPit email connection string is invalid.", exception);
        }
    }
}
