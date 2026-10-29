using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public interface IDataAccessProvider : IKeyed<DataEngine>
{
    string CatalogEngineName { get; }

    IReadOnlyList<string> GetRoles(DataAccessLevel level);

    string GetApplyScript();

    string GetRemoveScript();
}
