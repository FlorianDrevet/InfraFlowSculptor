using InfraFlowSculptor.Application.Common.Jobs;

namespace InfraFlowSculptor.Application.Publications;

public sealed record PreparePublicationJob(Guid PublicationId) : IJob;
