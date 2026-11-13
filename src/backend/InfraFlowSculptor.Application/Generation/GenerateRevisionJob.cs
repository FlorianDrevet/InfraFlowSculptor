using InfraFlowSculptor.Application.Common.Jobs;

namespace InfraFlowSculptor.Application.Generation;

public sealed record GenerateRevisionJob(Guid RevisionId) : IJob;
