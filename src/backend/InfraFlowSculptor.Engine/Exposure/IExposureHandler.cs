using InfraFlowSculptor.Domain.Common.Extensibility;

namespace InfraFlowSculptor.Engine.Exposure;

public interface IExposureHandler : IKeyed<ExposureMode>
{
    Exposure Build(IReadOnlyList<string> ranges);
}
