using System.Collections.Immutable;

namespace InfraFlowSculptor.Engine.Exposure;

public sealed record RestrictedExposure(ImmutableArray<string> Ranges) : Exposure(ExposureMode.Restricted);
