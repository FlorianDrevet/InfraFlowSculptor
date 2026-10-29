namespace InfraFlowSculptor.Engine.Model;

public sealed record GlobalNameReservationSnapshot(string ResourceType, string Name, Guid ProjectId);
