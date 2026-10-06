using InfraFlowSculptor.Application;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Infrastructure.Azure;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Infrastructure.Persistence.Admin;
using InfraFlowSculptor.Worker.Jobs;
using InfraFlowSculptor.Worker.Services;
using InfraFlowSculptor.Worker;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddIfsDbContext("ifs");
builder.Services.AddIfsServiceBusClient("servicebus");
builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddScoped<WorkerOrganizationContext>();
builder.Services.AddScoped<ICurrentOrganization>(services =>
    services.GetRequiredService<WorkerOrganizationContext>());
builder.Services.AddScoped<IOrganizationJobBudgetStore, EfOrganizationJobBudgetStore>();
builder.Services.AddScoped<IScheduledJobLeaseStore, EfScheduledJobLeaseStore>();
builder.Services.AddScoped<IdempotencyKeyPurge>();
builder.Services.AddJobHandlersFromAssembly(typeof(PingJob).Assembly);
builder.Services.AddJobHandlersFromAssembly(typeof(PingJobHandler).Assembly);
builder.Services.AddScoped<IScheduledJob, PurgeIdempotencyKeysJob>();
builder.Services.AddOpenTelemetry().WithTracing(tracing =>
    tracing.AddSource(JobActivity.SourceName));
builder.Services.AddHostedService<WorkerDatabaseReadinessService>();
builder.Services.AddHostedService<OutboxRelayService>();
builder.Services.AddHostedService<DomainEventDispatcherService>();
builder.Services.AddHostedService<SessionJobProcessorService>();
builder.Services.AddHostedService<ScheduledJobRunner>();

await builder.Build().RunAsync();
