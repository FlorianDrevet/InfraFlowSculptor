using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed class AzureSqlDataAccessProvider : IDataAccessProvider
{
    private const string ApplyResourceName = "InfraFlowSculptor.Engine.Links.AzureSql.DataAccess.sql";
    private const string RemoveResourceName = "InfraFlowSculptor.Engine.Links.AzureSql.DataAccess.Remove.sql";
    private static readonly Dictionary<DataAccessLevel, IReadOnlyList<string>> RolesByLevel =
        new Dictionary<DataAccessLevel, IReadOnlyList<string>>
        {
            [DataAccessLevel.Lecture] = ["db_datareader"],
            [DataAccessLevel.LectureEcriture] = ["db_datareader", "db_datawriter"],
            [DataAccessLevel.Schema] = ["db_datareader", "db_datawriter", "db_ddladmin"]
        };

    public DataEngine Key => DataEngine.AzureSql;

    public string CatalogEngineName => "azure-sql";

    public IReadOnlyList<string> GetRoles(DataAccessLevel level) => RolesByLevel.TryGetValue(level, out var roles)
        ? roles
        : throw new ArgumentOutOfRangeException(nameof(level), level, "The data access level is unsupported.");

    public string GetApplyScript() => ReadResource(ApplyResourceName);

    public string GetRemoveScript() => ReadResource(RemoveResourceName);

    private static string ReadResource(string name)
    {
        using var stream = typeof(AzureSqlDataAccessProvider).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The SQL resource '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
