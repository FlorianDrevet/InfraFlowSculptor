using Microsoft.Extensions.Configuration;

public sealed class SecretDependencyCheck(IConfiguration configuration) : IDependencyCheck
{
    public string Name => "secret";

    public Task<DependencyCheckOutcome?> CheckAsync(CancellationToken cancellationToken)
    {
        var secret = configuration["Payments:ApiKey"];
        if (secret is null)
        {
            return Task.FromResult<DependencyCheckOutcome?>(null);
        }

        return Task.FromResult<DependencyCheckOutcome?>(string.IsNullOrWhiteSpace(secret)
            ? new DependencyCheckOutcome(false, "variable vide")
            : new DependencyCheckOutcome(true, null));
    }
}
