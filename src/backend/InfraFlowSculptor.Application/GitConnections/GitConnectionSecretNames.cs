namespace InfraFlowSculptor.Application.GitConnections;

internal static class GitConnectionSecretNames
{
    private const string Prefix = "git-conn-";

    public static string CreatePersonalAccessTokenName(Guid connectionId) =>
        $"{Prefix}{connectionId:N}-{Guid.NewGuid():N}";
}
