using AzureKeyVaultEmulator.Aspire.Hosting;
using Aspire.Hosting;
using InfraFlowSculptor.AppHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var persistentContainers = builder.Configuration.GetValue<bool>("AppHost:PersistentContainers");
var isTesting = builder.Environment.IsEnvironment("Testing");
var containerLifetime = persistentContainers
    ? ContainerLifetime.Persistent
    : ContainerLifetime.Session;

var postgresBuilder = builder
    .AddPostgres(ResourceNames.Postgres, password: postgresPassword)
    .WithImageTag("17");
if (persistentContainers)
{
    postgresBuilder = postgresBuilder.WithDataVolume("ifs-postgres-17-data");
}

var postgres = postgresBuilder.WithLifetime(containerLifetime);
if (!isTesting)
{
    postgres = postgres.WithPgWeb();
}

var database = postgres.AddDatabase(ResourceNames.Database);

var storage = builder
    .AddAzureStorage(ResourceNames.Storage)
    .RunAsEmulator(emulator =>
    {
        if (persistentContainers)
        {
            emulator.WithDataVolume();
        }

        emulator.WithLifetime(containerLifetime);
    });
var blobs = storage.AddBlobs(ResourceNames.Blobs);

var serviceBus = builder
    .AddAzureServiceBus(ResourceNames.ServiceBus)
    .RunAsEmulator(emulator => emulator
        // Aspire applies this lifetime to the emulator and its SQL sidecar.
        .WithLifetime(containerLifetime)
        // A cold SQL Server needs the emulator's default readiness window.
        .WithEnvironment("SQL_WAIT_INTERVAL", "15")
        .WithConfiguration(configuration =>
        {
            configuration["UserConfig"]!["Namespaces"]![0]!["Name"] = "sbemulatorns";
        }));

foreach (var queueName in new[]
{
    ResourceNames.Generation,
    ResourceNames.Publication,
    ResourceNames.Tracking,
    ResourceNames.Notifications
})
{
    serviceBus
        .AddServiceBusQueue(queueName)
        .WithProperties(queue => queue.RequiresSession = true);
}

var redis = builder
    .AddRedis(ResourceNames.Redis)
    .WithLifetime(containerLifetime);
if (!isTesting)
{
    redis = redis.WithRedisInsight();
}

var api = builder
    .AddProject<Projects.InfraFlowSculptor_Api>(ResourceNames.Api)
    .WithReference(database)
    .WithReference(blobs)
    .WithReference(serviceBus)
    .WithReference(redis)
    .WaitFor(postgres)
    .WaitFor(storage)
    .WaitFor(serviceBus)
    .WaitFor(redis)
    .WithEnvironment("Auth__Authority", "https://localhost:8080/realms/ifs")
    .WithEnvironment("Auth__Audience", "ifs-api")
    .WithEnvironment("Auth__Provider", "Keycloak")
    .WithEnvironment("Cors__AllowedOrigins__0", "http://localhost:4200")
    .WithEnvironment("Ifs__Development__EnableGitea", isTesting ? "false" : "true")
    .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/alive");

if (isTesting)
{
    var testSigningKey = builder.Configuration["Auth:TestSigningKey"]
        ?? throw new InvalidOperationException("Auth:TestSigningKey is required in the Testing environment.");
    api = api.WithEnvironment("Auth__TestSigningKey", testSigningKey);
}
else
{
    var mailpit = builder
        .AddMailPit(ResourceNames.MailPit)
        .WithLifetime(containerLifetime);
    api = api.WithReference(mailpit).WaitFor(mailpit);

    var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);
    var keycloakBuilder = builder
        .AddKeycloak(ResourceNames.Keycloak, port: 8080, adminPassword: keycloakAdminPassword);
    if (persistentContainers)
    {
        keycloakBuilder = keycloakBuilder.WithDataVolume();
    }

    var keycloak = keycloakBuilder
        .WithLifetime(containerLifetime)
        .WithRealmImport("./Realms");
    api = api.WithReference(keycloak).WaitFor(keycloak);

    builder.AddAzureKeyVaultEmulator(ResourceNames.KeyVault);

    var giteaBuilder = builder
        .AddContainer(ResourceNames.Gitea, "gitea/gitea", "1.24")
        .WithHttpEndpoint(
            port: persistentContainers ? 3000 : null,
            targetPort: 3000,
            name: "http",
            isProxied: false)
        .WithEnvironment("GITEA__security__INSTALL_LOCK", "true")
        .WithEnvironment("GITEA__server__ROOT_URL", "http://localhost:3000/");
    if (persistentContainers)
    {
        giteaBuilder = giteaBuilder
            .WithContainerName("ifs-gitea")
            .WithVolume("ifs-gitea-data", "/data");
    }

    _ = giteaBuilder.WithLifetime(containerLifetime);
}

var worker = builder.AddProject<Projects.InfraFlowSculptor_Worker>(ResourceNames.Worker)
    .WithReference(database)
    .WithReference(serviceBus)
    .WaitFor(postgres)
    .WaitFor(serviceBus)
    .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName);
var configuredMaxConcurrentSessions = builder.Configuration.GetValue<int?>("Jobs:MaxConcurrentSessions");
if (isTesting || configuredMaxConcurrentSessions.HasValue)
{
    worker = worker.WithEnvironment(
        "Jobs__MaxConcurrentSessions",
        (configuredMaxConcurrentSessions ?? 2).ToString(System.Globalization.CultureInfo.InvariantCulture));
}

builder.Build().Run();
