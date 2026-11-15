using InfraFlowSculptor.Application.Common.Jobs;

namespace InfraFlowSculptor.Application.GitConnections;

public sealed record DeleteGitConnectionSecretJob(string SecretName) : IJob;
