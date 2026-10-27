namespace InfraFlowSculptor.Contracts.Projects;

public sealed record SetProjectTagsRequest(IReadOnlyDictionary<string, string> Tags, int ExpectedVersion);
