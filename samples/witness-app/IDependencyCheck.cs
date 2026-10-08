public interface IDependencyCheck
{
    string Name { get; }

    Task<DependencyCheckOutcome?> CheckAsync(CancellationToken cancellationToken);
}
