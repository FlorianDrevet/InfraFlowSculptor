# 02 — Backend

## 1. Solution

`src/backend/InfraFlowSculptor.slnx` :

```
src/backend/
├── global.json                         SDK 10.0.203, rollForward latestFeature
├── Directory.Build.props               net10.0, nullable, implicit usings, TreatWarningsAsErrors, analyzers
├── Directory.Packages.props            versions centrales (DT-03)
├── aspire.config.json                  { "appHost": { "path": "InfraFlowSculptor.AppHost/InfraFlowSculptor.AppHost.csproj" } }
├── InfraFlowSculptor.AppHost/
├── InfraFlowSculptor.ServiceDefaults/
├── InfraFlowSculptor.Api/
├── InfraFlowSculptor.Worker/
├── InfraFlowSculptor.Application/
├── InfraFlowSculptor.Contracts/
├── InfraFlowSculptor.Domain/
├── InfraFlowSculptor.Infrastructure/
├── InfraFlowSculptor.Catalog/          (embarque ../../catalog/**)
├── InfraFlowSculptor.Engine/
├── InfraFlowSculptor.Emitters.Bicep/
├── InfraFlowSculptor.Emitters.AzureDevOps/
├── InfraFlowSculptor.Emitters.InstallKit/
├── InfraFlowSculptor.Mcp/              (jalon 3)
└── tests/
    ├── InfraFlowSculptor.Domain.Tests/
    ├── InfraFlowSculptor.Catalog.Tests/
    ├── InfraFlowSculptor.Engine.Tests/
    ├── InfraFlowSculptor.Emitters.Tests/
    ├── InfraFlowSculptor.Application.Tests/
    ├── InfraFlowSculptor.Infrastructure.Tests/
    ├── InfraFlowSculptor.Api.Tests/
    ├── InfraFlowSculptor.Architecture.Tests/
    └── InfraFlowSculptor.Acceptance.Tests/   (projet de référence de bout en bout)
```

`Directory.Build.props` :

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

`InvariantGlobalization` évite les écarts de culture que VPD a rencontrés dans ses tests SQLite (tri
décimal) et garantit un formatage identique des valeurs générées sur toutes les machines.

## 2. Une tranche CQRS

Chaque cas d'utilisation est une tranche verticale, nommée d'après son `UC-…` :

```
Application/Projects/
├── Commands/
│   └── CreateProject/
│       ├── CreateProjectCommand.cs          record : IRequest<ErrorOr<ProjectResult>> (Mediator), [RequiresPermission(...)]
│       ├── CreateProjectCommandValidator.cs AbstractValidator<CreateProjectCommand> — contrôle à la saisie
│       └── CreateProjectCommandHandler.cs   IRequestHandler<,>
├── Queries/
│   └── GetProject/
│       ├── GetProjectQuery.cs
│       └── GetProjectQueryHandler.cs
└── Common/
    └── ProjectResult.cs                     résultat applicatif (jamais le DTO public)

Contracts/Projects/
├── CreateProjectRequest.cs
└── ProjectResponse.cs

Api/Controllers/ProjectsController.cs        MapProjectsEndpoints(this RouteGroupBuilder v1)
Api/Common/Mapping/ProjectsMappingConfig.cs  Mapster : Request → Command, Result → Response
```

Points de terminaison (évolution E16 de [DT-02](01-decisions.md#dt-02--backend--template-cqrs-modernisé-selon-vole-papillon-damour)) :

```csharp
public static class ProjectsController
{
    public static RouteGroupBuilder MapProjectsEndpoints(this RouteGroupBuilder v1)
    {
        var projects = v1.MapGroup("/projects").WithTags("Projects");

        projects.MapPost("/", async (CreateProjectRequest request, IMediator mediator, IMapper mapper, CancellationToken ct) =>
            {
                var result = await mediator.Send(mapper.Map<CreateProjectCommand>(request), ct);
                return result.Match(
                    project => Results.Created($"/v1/projects/{project.Id}", mapper.Map<ProjectResponse>(project)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.CreateProject)
            .WithSummary("Créer un projet (UC-PRJ-01)")
            .Produces<ProjectResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write);

        return v1;
    }
}
```

Comportements du médiateur (`Mediator`, [DT-22](01-decisions.md#dt-22--mediator-à-la-place-de-mediatr)), dans cet ordre : `LoggingBehavior` → `ValidationBehavior` (template, FluentValidation)
→ `AuthorizationBehavior` (permission, portée composant, effets indirects, [DEC-89](../specs/03-decisions.md))
→ `UnitOfWorkBehavior` (commandes seulement : une transaction, version du modèle, audit, outbox).

## 3. Domaine

- Agrégats sous `Domain/<Nom>Aggregate/` (convention du template) : `OrganizationAggregate`,
  `ProjectAggregate` (projet, environnements, conventions, plan de publication), `ComponentAggregate`
  (composant, groupes de ressources, ressources, enfants, surcharges, présences), `LinkAggregate`,
  `AppSettingAggregate`, `RevisionAggregate`, `PublicationAggregate`, `DeploymentTrackingAggregate`,
  `AuditAggregate`, `ApiTokenAggregate`, `InvitationAggregate`.
- Les invariants **locaux** vivent dans l'agrégat (format d'un code, unicité dans un agrégat, dernier
  propriétaire). Les règles **globales** (validation du modèle) vivent dans le moteur. Une règle n'est
  écrite qu'à un endroit ([20 § 2](../specs/20-validation.md)).
- Erreurs : `Domain/Common/Errors/Errors.<Domaine>.cs` (convention du template), le `Code` est le code de
  règle de la spec.

## 4. Persistance

- `IfsDbContext` (Infrastructure) implémente `IIfsDbContext` (Application, évolution E11) qui expose les
  `DbSet` en lecture pour les requêtes. Les gestionnaires de commande chargent et modifient les agrégats via
  le contexte ; l'enregistrement est fait **une fois** par `UnitOfWorkBehavior`.
- Conventions : `snake_case`, `timestamptz` UTC, `jsonb` pour les instantanés et les différences, convertisseurs
  des identifiants fortement typés, `version` jeton de concurrence, filtres globaux par organisation
  ([DT-24](01-decisions.md#dt-24--isolation-multi-organisations)) et suppression logique des projets
  (30 jours, [UC-PRJ-04](../specs/11-projets-et-environnements.md)).
- Migrations EF dans `Infrastructure/Persistence/Migrations/`, nommées par l'étape du plan
  (`J0_04_AuditEvents`). Appliquées au démarrage en développement (`DatabaseMigrationPolicy`, évolution
  E10) ; en Azure par un job Container Apps qui exécute le bundle de migration avant le déploiement
  ([08](08-hebergement.md)).
- Tables transverses : `outbox_messages`, `idempotency_keys`, `scheduled_job_leases`, `audit_events`.

## 5. Authentification et autorisation

| Élément | Fichier |
|---|---|
| Schémas `Oidc`, `ApiToken`, schéma de politique `Bearer` | `Api/Authentication/AuthenticationSetup.cs` |
| Gestionnaire des jetons d'API | `Infrastructure/Authentication/ApiTokenAuthenticationHandler.cs` |
| Utilisateur courant `(tid, oid)`, adresse vérifiée | `Application/Common/Security/ICurrentUser.cs`, `Infrastructure/Authentication/VerifiedEmailResolver.cs` |
| Organisation active | `Api/Common/OrganizationContextMiddleware.cs` (en-tête `X-Ifs-Organization`) |
| Permissions et rôles prédéfinis | `Domain/Common/Security/Permission.cs`, `ProjectRoles.cs` (table de [10 § 4.2](../specs/10-organisations-et-acces.md)) |
| Contrôle projet/composant | `Application/Common/Security/IProjectAuthorizer.cs` + `AuthorizationBehavior` |
| Politiques ASP.NET | `Api/Common/AuthorizationPolicies.cs` : `Member`, `OrganizationAdmin`, `Internal` |
| Portées de jeton | `Domain/ApiTokenAggregate/ApiTokenScope.cs` ; chaque commande déclare sa portée minimale |

## 6. Configuration

- Clés de configuration en constantes (`Infrastructure/Configuration/ConfigurationKeys.cs`), options typées
  validées au démarrage (`ValidateDataAnnotations().ValidateOnStart()`).
- Secrets locaux : `dotnet user-secrets` de l'AppHost (mot de passe PostgreSQL) ; en Azure,
  références Key Vault des Container Apps.
- Aucune chaîne de connexion en clair dans `appsettings*.json` : Aspire injecte `ConnectionStrings__*` en
  local ; en Azure, seulement `Azure__<nom>__Endpoint` et l'identité managée. Tous les clients Azure (PostgreSQL,
  blob, Service Bus, Redis, Key Vault, ACS) passent par `AzureConnectionResolver`
  ([DT-33](01-decisions.md#dt-33--identité-managée-partout--chaîne-de-connexion-seulement-en-local)).

## 7. Ce qui est transverse et doit exister avant la première fonctionnalité

| Élément | Étape |
|---|---|
| Squelette, versions, analyse statique | S-02, S-03 |
| Tests et règles d'architecture | S-04 |
| AppHost et émulateurs | S-05 |
| Persistance, migrations, filtres, version | S-06 |
| Authentification OIDC locale, `/v1/me` | S-07 |
| Worker, outbox, files à sessions | S-08 |
| Erreurs `problem+json`, idempotence, limites | S-03, S-06, S-08 |
| OpenAPI au build | S-14 |
