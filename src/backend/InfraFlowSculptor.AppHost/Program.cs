using AzureKeyVaultEmulator.Aspire.Hosting;
using Aspire.Hosting;
using InfraFlowSculptor.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);

var postgres = builder
    .AddPostgres(ResourceNames.Postgres, password: postgresPassword)
    .WithImageTag("17")
    .WithDataVolume("ifs-postgres-17-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgWeb();
var database = postgres.AddDatabase(ResourceNames.Database);

var storage = builder
    .AddAzureStorage(ResourceNames.Storage)
    .RunAsEmulator(emulator => emulator
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent));
var blobs = storage.AddBlobs(ResourceNames.Blobs);

var serviceBus = builder
    .AddAzureServiceBus(ResourceNames.ServiceBus)
    .RunAsEmulator(emulator => emulator.WithConfiguration(configuration =>
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
    .WithRedisInsight();

var mailpit = builder.AddMailPit(ResourceNames.MailPit);

var keycloak = builder
    .AddKeycloak(ResourceNames.Keycloak, port: 8080, adminPassword: keycloakAdminPassword)
    .WithDataVolume()
    .WithRealmImport("./Realms");

builder.AddAzureKeyVaultEmulator(ResourceNames.KeyVault);

var gitea = builder
    .AddContainer(ResourceNames.Gitea, "gitea/gitea", "1.24")
    .WithContainerName("ifs-gitea")
    .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "http", isProxied: false)
    .WithVolume("ifs-gitea-data", "/data")
    .WithEnvironment("GITEA__security__INSTALL_LOCK", "true")
    .WithEnvironment("GITEA__server__ROOT_URL", "http://localhost:3000/");

var api = builder
    .AddProject<Projects.InfraFlowSculptor_Api>(ResourceNames.Api)
    .WithReference(database)
    .WithReference(blobs)
    .WithReference(serviceBus)
    .WithReference(redis)
    .WithReference(mailpit)
    .WithReference(keycloak)
    .WaitFor(postgres)
    .WaitFor(storage)
    .WaitFor(serviceBus)
    .WaitFor(redis)
    .WaitFor(mailpit)
    .WaitFor(keycloak)
    .WithEnvironment("Auth__Authority", "https://localhost:8080/realms/ifs")
    .WithEnvironment("Auth__Audience", "ifs-api")
    .WithEnvironment("Auth__Provider", "Keycloak")
    .WithEnvironment("Cors__AllowedOrigins__0", "http://localhost:4200")
    .WithEnvironment("Ifs__Development__EnableGitea", "true")
    .WithExternalHttpEndpoints();

builder.Build().Run();
