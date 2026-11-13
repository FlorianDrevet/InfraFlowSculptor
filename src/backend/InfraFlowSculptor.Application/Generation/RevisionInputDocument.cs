using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Application.Generation;

public sealed record RevisionInputDocument(string Schema, ModelSnapshot Snapshot);
