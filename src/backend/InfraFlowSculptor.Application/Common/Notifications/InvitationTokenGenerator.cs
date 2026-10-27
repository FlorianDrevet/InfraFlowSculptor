using System.Security.Cryptography;

namespace InfraFlowSculptor.Application.Common.Notifications;

public static class InvitationTokenGenerator
{
    private const int TokenByteLength = 32;

    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenByteLength))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
