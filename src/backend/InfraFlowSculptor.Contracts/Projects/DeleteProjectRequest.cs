namespace InfraFlowSculptor.Contracts.Projects;

public sealed record DeleteProjectRequest(string NameConfirmation, int ExpectedVersion);
