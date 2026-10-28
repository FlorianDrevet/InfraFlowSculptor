namespace InfraFlowSculptor.Contracts.Projects;

public sealed record DeleteProjectEnvironmentRequest(bool ConfirmDeletion, int ExpectedVersion);
