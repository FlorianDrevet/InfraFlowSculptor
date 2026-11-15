using InfraFlowSculptor.Application.Common.Jobs;

namespace InfraFlowSculptor.Application.Publications;

public sealed record WritePublicationJob(Guid PublicationId) : IJob;
