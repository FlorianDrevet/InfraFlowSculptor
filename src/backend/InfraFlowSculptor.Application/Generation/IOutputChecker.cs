using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Application.Generation;

public interface IOutputChecker : IKeyed<IacLanguage>
{
    ValueTask CheckAsync(
        IReadOnlyList<GeneratedFile> files,
        CatalogPins pins,
        CancellationToken cancellationToken);
}
