using InfraFlowSculptor.Domain.RevisionAggregate;

namespace InfraFlowSculptor.Application.Generation;

public static class RevisionChangeFactory
{
    private const string Added = "Added";
    private const string Changed = "Changed";
    private const string Removed = "Removed";

    public static IReadOnlyList<RevisionChange> Create(
        IReadOnlyCollection<RevisionFile>? previousFiles,
        IReadOnlyCollection<RevisionFile> currentFiles)
    {
        ArgumentNullException.ThrowIfNull(currentFiles);

        var previous = previousFiles?.ToDictionary(file => file.RelativePath, StringComparer.Ordinal)
            ?? new Dictionary<string, RevisionFile>(StringComparer.Ordinal);
        var current = currentFiles.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        var changes = new List<RevisionChange>();

        foreach (var path in previous.Keys.Union(current.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!previous.TryGetValue(path, out var prior))
            {
                changes.Add(CreateChange(Added, path));
            }
            else if (!current.TryGetValue(path, out var next))
            {
                changes.Add(CreateChange(Removed, path));
            }
            else if (!string.Equals(prior.Sha256, next.Sha256, StringComparison.Ordinal)
                || prior.Size != next.Size)
            {
                changes.Add(CreateChange(Changed, path));
            }
        }

        return changes;
    }

    private static RevisionChange CreateChange(string category, string path) => category switch
    {
        Added => new RevisionChange(category, path, "Fichier ajouté", "File added"),
        Changed => new RevisionChange(category, path, "Fichier modifié", "File changed"),
        Removed => new RevisionChange(category, path, "Fichier supprimé", "File removed"),
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };
}
