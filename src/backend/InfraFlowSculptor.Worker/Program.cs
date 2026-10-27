using InfraFlowSculptor.Application;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Notifications;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Infrastructure.Azure;
using InfraFlowSculptor.Infrastructure.Email;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Infrastructure.Persistence.Admin;
using InfraFlowSculptor.Infrastructure.Persistence.Auditing;
using InfraFlowSculptor.Worker.Jobs;
using InfraFlowSculptor.Worker.Services;
using InfraFlowSculptor.Worker;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddIfsDbContext("ifs");
builder.Services.AddIfsServiceBusClient("servicebus");
builder.Services.AddIfsEmailSender(builder.Configuration, builder.Environment);
builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddWorkerSecurityContext();
builder.Services.AddScoped<WorkerOrganizationContext>();
builder.Services.AddScoped<ICurrentOrganization>(services =>
    services.GetRequiredService<WorkerOrganizationContext>());
builder.Services.AddScoped<IOrganizationJobBudgetStore, EfOrganizationJobBudgetStore>();
builder.Services.AddScoped<IScheduledJobLeaseStore, EfScheduledJobLeaseStore>();
builder.Services.AddScoped<IdempotencyKeyPurge>();
builder.Services.AddScoped<IAuditWriter, EfAuditWriter>();
builder.Services.AddScoped<IAuditEventPurger, EfAuditEventPurger>();
builder.Services.AddJobHandlersFromAssembly(typeof(PingJob).Assembly);
builder.Services.AddJobHandlersFromAssembly(typeof(PingJobHandler).Assembly);
builder.Services.AddScoped<INotificationChannel, EmailNotificationChannel>();
builder.Services.AddScoped<NotificationChannelRegistry>();
builder.Services.AddScoped<IScheduledJob, PurgeIdempotencyKeysJob>();
builder.Services.AddScoped<IScheduledJob, PurgeAuditEventsJob>();
builder.Services.AddHostedService<WorkerDatabaseReadinessService>();
builder.Services.AddHostedService<OutboxRelayService>();
builder.Services.AddHostedService<DomainEventDispatcherService>();
builder.Services.AddHostedService<SessionJobProcessorService>();
builder.Services.AddHostedService<ScheduledJobRunner>();

await builder.Build().RunAsync();
