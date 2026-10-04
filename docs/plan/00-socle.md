# Phase S — Socle technique

**But.** Un dépôt où tout démarre d'une commande (`aspire run`), où chaque couche existe avec ses tests,
ses conventions et sa CI, avant la première fonctionnalité. Rien de fonctionnel n'est livré ici : la seule
« fonction » visible est la connexion avec un utilisateur de démonstration.

**Critère de sortie.** Le verrou [`R-01`](#-r-01--revue-du-socle) approuvé et la recette
[`recettes/00-socle.md`](recettes/00-socle.md) passée.

**Branche du segment** : `impl/socle`, créée depuis `origin/main` à S-01.

---

### S-01 — Prérequis de la machine et garde-fous du plan

| | |
|---|---|
| **Spécifications** | — |
| **Technique** | [DT-03](../technique/01-decisions.md#dt-03--versions-épinglées), [plan § Verrous](README.md#les-verrous-) |
| **Dépend de** | — |
| **Commit** | `chore(depot): outillage de base et garde-fous du plan` |

🎯 **Objectif.** Toute machine (la vôtre, celle de Luna) vérifie ses outils en une commande, et le dépôt
refuse mécaniquement de franchir un verrou.

🔧 **À faire.**
1. `git fetch origin main && git switch -c impl/socle origin/main`.
2. Créer `tools/versions.json` :
   ```json
   { "dotnet": "10.0.203", "node": "24.15.0", "aspire": "13.5.3", "python": "3.11",
     "pwsh": "7.5.0", "bicep": "<dernière stable>", "docker": "27.0.0" }
   ```
   Pour `bicep`, relever la dernière version stable publiée (`https://github.com/Azure/bicep/releases`) et
   l'écrire en clair.
3. Créer `tools/dev/check-prereqs.ps1` (PowerShell 7) : pour chaque outil de `versions.json`, plus `git`,
   `az`, `gh`, lit la version installée, la compare (version minimale), vérifie que Docker répond
   (`docker info`), affiche un tableau `Outil | Attendu | Trouvé | OK`, et sort en code 1 si un outil
   manque. Installation suggérée affichée pour chaque manque (`winget install …`,
   `dotnet tool install -g aspire.cli --version 13.5.3`, `az bicep install`).
4. Créer à la racine : `.gitignore` (union des modèles officiels `VisualStudio`, `Node`, `JetBrains`, plus
   `graphify-out/`, `**/TestResults/`, `**/playwright-report/`, `**/test-results/`), `.editorconfig`
   (UTF-8, `LF`, 4 espaces en C#, 2 en TS/JSON/YAML/MD, `insert_final_newline = true`), `.gitattributes` :
   ```
   * text=auto eol=lf
   *.ps1 text eol=lf
   *.png binary
   *.zip binary
   ```
   et `.nvmrc` contenant `24.15.0`.
5. Créer `.githooks/pre-commit` (sh, exécutable) :
   ```sh
   #!/bin/sh
   python tools/plan/gate.py precommit || exit 1
   ```
   et l'activer : `git config core.hooksPath .githooks`. Ajouter cette commande à la section « Reprendre le
   travail » de `README.md`.
6. Écrire les tests de `tools/plan/gate.py` (le script existe déjà) dans `tools/plan/tests/test_gate.py`
   (`unittest`, bibliothèque standard) : sur un plan et un `NEXT.md` temporaires (copiés dans un dossier
   temporaire, `ROOT` remplacé par patch), vérifier `lint` (identifiant dupliqué, rubrique 🧪 manquante),
   `done` (avance à l'étape suivante, écrit le journal), `request` (exige la demande), `approve` (exige
   « Verdict : APPROUVÉ »), `check` (code 3 en attente de revue), `precommit` (refuse `src/x.cs`, accepte
   `docs/plan/revues/R-01-demande.md`). Ne pas modifier le comportement de `gate.py` sauf défaut prouvé par
   un test ; signaler tout défaut dans la demande de revue.

✅ **Vérification automatique.**
- `pwsh tools/dev/check-prereqs.ps1` → code 0 sur la machine de Luna (sinon : installer, ou noter le manque
  dans `NEXT.md` 📌 si l'installation demande vos droits).
- `python -m unittest discover tools/plan/tests` → tous verts.
- `python tools/plan/gate.py lint` → « Plan cohérent ».

🧪 **Test manuel.**
1. Dans un terminal PowerShell 7 à la racine : `pwsh tools/dev/check-prereqs.ps1`.
   → Un tableau, toutes les lignes `OK`. Une ligne `KO` donne la commande d'installation : l'exécuter, relancer.
2. `python tools/plan/gate.py status`.
   → « Étape courante : S-01 … Statut : EN_COURS » (ou `A_FAIRE`) et « Prochain verrou : R-01 ».
3. `git config core.hooksPath` → `.githooks`.

🧠 **Mémoire.** `.github/memory/09-auth-and-build.md` : prérequis et commande de vérification.
`.github/memory/02-project-structure.md` : `tools/`, `.githooks/`.

📌 **Hors dépôt.** Outils installés sur votre machine (versions) dans le tableau « Machine » de `NEXT.md`.

### S-02 — Squelette backend généré depuis le template CQRS

| | |
|---|---|
| **Technique** | [DT-02](../technique/01-decisions.md#dt-02--backend--template-cqrs-modernisé-selon-vole-papillon-damour), [02 § 1](../technique/02-backend.md) |
| **Dépend de** | S-01 |
| **Commit** | `chore(backend): générer le squelette depuis template-CQRS` |

🎯 **Objectif.** Le code du template, **tel quel**, dans `src/backend/`, pour que l'étape suivante montre en
diff chaque évolution appliquée.

🔧 **À faire.**
1. ```powershell
   git clone https://github.com/FlorianDrevet/template-CQRS "$env:TEMP/template-CQRS"
   dotnet new install "$env:TEMP/template-CQRS"
   New-Item -ItemType Directory -Force src/backend | Out-Null
   dotnet new templatewebcqrs -n InfraFlowSculptor -o "$env:TEMP/ifs-gen"
   ```
2. Copier dans `src/backend/` : `Directory.Packages.props`, `global.json`, le fichier `.slnx` renommé
   `InfraFlowSculptor.slnx`, et le **contenu** de `src/` (les dossiers `InfraFlowSculptor.Api`,
   `.Application`, `.Contracts`, `.Domain`, `.Infrastructure`) directement sous `src/backend/` — pas de
   `src/backend/src/`. Ne pas copier `.idea/`, `.template.config/`, `README.md` du template.
3. Corriger les chemins des projets dans `InfraFlowSculptor.slnx` (`InfraFlowSculptor.Api/InfraFlowSculptor.Api.csproj`…).
4. Renommer `InfraFlowSculptor.Api/VPD.Api.http` en `InfraFlowSculptor.Api/InfraFlowSculptor.Api.http`.
5. `dotnet new uninstall "$env:TEMP/template-CQRS"`.

✅ **Vérification automatique.** `dotnet build src/backend/InfraFlowSculptor.slnx` réussit (les
avertissements du template sont tolérés **à cette étape seulement**). `git status` ne montre que
`src/backend/`.

🧪 **Test manuel.** Ouvrir `src/backend/InfraFlowSculptor.slnx` dans Rider : cinq projets, nommés
`InfraFlowSculptor.*`, aucune référence à `Web.Template.CQRS`.

🧠 **Mémoire.** `02-project-structure.md` : `src/backend/` et les cinq projets.

### S-03 — Moderniser le squelette (évolutions de Vole-Papillon-Damour)

| | |
|---|---|
| **Technique** | [DT-02](../technique/01-decisions.md#dt-02--backend--template-cqrs-modernisé-selon-vole-papillon-damour) (E1–E18), [DT-03](../technique/01-decisions.md#dt-03--versions-épinglées), [DT-05](../technique/01-decisions.md#dt-05--minimal-api-v1-openapi-intégré--scalar), [DT-06](../technique/01-decisions.md#dt-06--erreurs--erroror--problemjson), [DT-22](../technique/01-decisions.md#dt-22--mediator-à-la-place-de-mediatr), [DT-26](../technique/01-decisions.md#dt-26--temps-et-horloge) |
| **Dépend de** | S-02 |
| **Commit** | `refactor(backend): moderniser le squelette selon les évolutions VPD` |

🎯 **Objectif.** Un backend .NET 10 propre, sans avertissement, avec l'API `/v1`, OpenAPI, Scalar, les
erreurs `problem+json`, sans aucune trace d'authentification locale.

🔧 **À faire.**
1. `global.json` : `{"sdk":{"version":"10.0.203","rollForward":"latestFeature"}}`.
2. `Directory.Build.props` : contenu exact de [technique 02 § 1](../technique/02-backend.md#1-solution) ; retirer
   des `.csproj` les propriétés devenues redondantes (`TargetFramework`, `Nullable`, `ImplicitUsings`).
3. `Directory.Packages.props` : versions de [DT-03](../technique/01-decisions.md#dt-03--versions-épinglées) ;
   **retirer** `FluentValidation.AspNetCore`, `MediatR.Extensions.Microsoft.DependencyInjection`,
   `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.SqlServer.Design`,
   `Npgsql.EntityFrameworkCore.PostgreSQL.Design`, `System.IdentityModel.Tokens.Jwt` ; **ajouter**
   `FluentValidation.DependencyInjectionExtensions`, `Scalar.AspNetCore`.
4. **Supprimer** l'authentification locale et ce qui n'a pas d'usage dans IFS : dossiers et fichiers
   `Application/Authentication/`, `Contracts/Authentication/`, `Api/Controllers/AuthenticationController.cs`,
   `Api/Common/Mapping/AuthenticationMappingConfig.cs`, `Infrastructure/Authentication/`,
   `Application/Common/Interfaces/Authentication/`, `Domain/UserAggregate/`, `Domain/Common/Errors/Errors.User.cs`,
   `Errors.Authentication.cs`, `Application/Common/Interfaces/Persistence/` (IRepository, IUserRepository),
   `Infrastructure/Persistence/` (sera réécrit en S-06), `Application/Common/Interfaces/Services/` et
   `Infrastructure/Services/` (blob et date : remplacés plus tard par `TimeProvider` et l'adaptateur blob de
   J0-22).
5. `Domain/Common/Models/` : garder `Entity`, `AggregateRoot`, `ValueObject` ; ajouter `EnumValueObject<TEnum>`
   recopié de VPD (évolution E13) ; ajouter `IHasVersion` (`int Version { get; }`).
6. **Remplacer MediatR par `Mediator`** ([DT-22](../technique/01-decisions.md#dt-22--mediator-à-la-place-de-mediatr)) :
   retirer `MediatR` de `Directory.Packages.props` et de tous les `.csproj` ; ajouter `Mediator.Abstractions`
   et `Mediator.SourceGenerator` **dans Application** (le générateur s'exécute dans le projet qui contient les
   gestionnaires), dernière version stable `3.x` ; dans `Application/DependencyInjection.cs` (`AddApplication()`, appelé
   par l'Api puis par le Worker à S-08), `services.AddMediator(o => { o.ServiceLifetime = ServiceLifetime.Scoped; o.PipelineBehaviors =
   [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)]; });` ; remplacer les `using MediatR;` par
   `using Mediator;` ; gestionnaires `ValueTask<ErrorOr<T>> Handle(TRequest request, CancellationToken ct)` ;
   `ValidationBehavior` du template adapté à `ValueTask<TResponse> Handle(TMessage message,
   MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)` avec `await next(message, ct)` ;
   `LoggingBehavior` (nouveau : journalise le nom de la requête et la durée, jamais le contenu) ; validateurs
   FluentValidation par `AddValidatorsFromAssembly`. Les points de terminaison gardent `IMediator` et `Send`.
   Vérifier `grep -ri mediatr src/backend` → aucun résultat. L'ordre final des comportements est celui de [DT-40](../technique/01-decisions.md#dt-40--ordre-des-comportements-du-médiateur) :
   chaque étape qui en ajoute un l'insère à sa place.
7. `Api/Errors/` : remplacer `ErrorOrStatusCode.cs` par `ProblemDetailsMapper.cs` (extension
   `ToProblem(this List<Error>)`) qui applique le tableau de [DT-06](../technique/01-decisions.md#dt-06--erreurs--erroror--problemjson)
   à partir de `ErrorType` et de `Error.Metadata` (clés constantes dans `Api/Errors/ProblemMetadataKeys.cs` :
   `field`, `permission`, `currentVersion`, `current`, `limit`, `plan`) ; `ErrorHandling.cs` renvoie
   `code: "INTERNAL"` et `traceId` (`Activity.Current?.Id ?? HttpContext.TraceIdentifier`), sans détail.
8. `Api/Program.cs` réécrit dans cet ordre : `AddServiceDefaults()` (créer à cette étape le projet
   `InfraFlowSculptor.ServiceDefaults` par `dotnet new aspire-servicedefaults -n InfraFlowSculptor.ServiceDefaults`
   avec les modèles Aspire `13.5.3`, et ajouter dans `Extensions.cs` l'exportateur Azure Monitor quand
   `APPLICATIONINSIGHTS_CONNECTION_STRING` existe — évolution E8) ; `AddProblemDetails()` ; en-têtes transférés
   (évolution E7, code de VPD `Program.cs`) ; CORS depuis `Cors:AllowedOrigins` ; `AddOpenApiExtensions()`
   (template) ; limitation de débit (évolution E6) avec quatre politiques par utilisateur ou jeton
   (`RateLimitingPolicies.Read` 600/min, `Write` 120/min, `Generate` 10/min, `Publish` 5/min — partition :
   revendication `oid`, sinon adresse IP) et rejet en 429 `problem+json` avec `Retry-After` ;
   `AddApplication()`, `AddInfrastructure()`, `AddPresentation()`. Pipeline : `UseForwardedHeaders`,
   `UseExceptionHandler` (ErrorHandling), `UseStatusCodePages` (problem+json), `UseCors`, `UseAuthentication`,
   `UseAuthorization`, `UseRateLimiter`, `MapDefaultEndpoints()` (santé), `MapOpenApi()` et
   `MapScalarApiReference()` en développement, puis `var v1 = app.MapGroup("/v1");` et les contrôleurs.
9. `Api/Controllers/SystemController.cs` : `GET /v1/version` → `{ "version": "<AssemblyInformationalVersion>",
   "environment": "<nom>" }`, anonyme, nom `GetVersion`.
9 bis. `Application/Common/Extensibility/` : `IKeyed<TKey>`, `Registry<TKey, TService>` (clé absente → erreur
    explicite citant la clé ; test) ; attributs `ModelCommandAttribute`, `TokenScopeAttribute`,
    `AdministrativeOperationAttribute`, `RequiresFeatureAttribute` (posés par les étapes suivantes) — [DT-35](../technique/01-decisions.md#dt-35--registres-et-stratégies-pas-de-switch), [DT-36](../technique/01-decisions.md#dt-36--commandes-du-modèle-comme-données-et-espaces-de-travail),
    [technique 10](../technique/10-extensibilite.md).
10. Constantes : `Api/Common/EndpointNames.cs`, `Api/Common/RateLimitingPolicies.cs`,
    `Api/Common/AuthorizationPolicies.cs` (vide pour l'instant), `Infrastructure/Configuration/ConfigurationKeys.cs`
    (`Cors:AllowedOrigins`…).
11. Remplacer tout `.WithOpenApi()` (déprécié en .NET 10) par les métadonnées `.WithSummary()`/`.Produces*()`.

✅ **Vérification automatique.**
- `dotnet build src/backend/InfraFlowSculptor.slnx` : **0 avertissement, 0 erreur**.
- `dotnet run --project src/backend/InfraFlowSculptor.Api` puis : `GET /alive` → 200 ; `GET /v1/version` → 200 ;
  `GET /v1/nope` → 404 `application/problem+json` ; `GET /openapi/v1.json` → 200.
- `grep -ri "password\|jwt\|register" src/backend --include=*.cs` → aucun résultat.

🧪 **Test manuel.**
1. `dotnet run --project src/backend/InfraFlowSculptor.Api`, ouvrir l'URL `…/scalar` affichée.
   → Scalar affiche « InfraFlowSculptor API v1 » avec l'opération `GetVersion`.
2. Exécuter `GET /v1/version` dans Scalar → 200, `environment: "Development"`.
3. Ouvrir `…/v1/inexistant` dans le navigateur → JSON avec `"status": 404` et un `traceId`.

🧠 **Mémoire.** `02-project-structure.md`, `09-auth-and-build.md` (commandes build/run, versions),
`10-api-endpoints.md` (`/v1/version`, conventions d'endpoint), `03-domain-model.md` (modèles de base).

### S-04 — Projets de tests et règles d'architecture

| | |
|---|---|
| **Technique** | [DT-21](../technique/01-decisions.md#dt-21--outillage-de-test), [06](../technique/06-tests-et-qualite.md) |
| **Dépend de** | S-03 ; ⚖️ DT-21 confirmée |
| **Commit** | `test(backend): projets de tests et règles d'architecture` |

🎯 **Objectif.** Chaque couche a son projet de test, et les frontières entre couches sont vérifiées
mécaniquement dès maintenant.

🔧 **À faire.**
1. Créer sous `src/backend/tests/` les projets xUnit (`dotnet new xunit`, puis références centrales) :
   `InfraFlowSculptor.Domain.Tests`, `.Application.Tests`, `.Infrastructure.Tests`, `.Api.Tests`,
   `.Architecture.Tests`. (Les projets `Catalog.Tests`, `Engine.Tests`, `Emitters.Tests` et `Acceptance.Tests`
   naissent avec leurs projets testés.) Paquets : `xunit`, `xunit.runner.visualstudio`,
   `Microsoft.NET.Test.Sdk`, `AwesomeAssertions`, `NSubstitute`, `coverlet.collector` ; `Api.Tests` :
   `Microsoft.AspNetCore.Mvc.Testing` ; `Architecture.Tests` : `NetArchTest.Rules`.
2. Ajouter les projets à `InfraFlowSculptor.slnx` dans un dossier de solution `/tests/`.
3. `Domain.Tests/Common/ValueObjectTests.cs` : égalité et hachage de `ValueObject` et `EnumValueObject`.
4. `Api.Tests/Common/ApiFactory.cs` (`WebApplicationFactory<Program>`, environnement `Testing`) ;
   `Api.Tests/System/VersionEndpointTests.cs` : `GET /v1/version` → 200 ; route inconnue → 404 problem+json
   avec `traceId`.
5. `Architecture.Tests/LayerDependencyTests.cs` : règles de [technique 06 § 3](../technique/06-tests-et-qualite.md#3-tests-darchitecture-extraits-obligatoires)
   applicables aux projets existants (Domain, Application, Contracts, Api) ; `OneTopLevelTypePerFileTests.cs`
   (lit les `.cs` de `src/backend` hors `tests/`, `obj/`, `bin/`, `Migrations/` et échoue si un fichier
   déclare plus d'un type public de premier niveau).
5 bis. `Architecture.Tests/NoSwitchOnExtensionKeysTests.cs` : aucun `switch` ni comparaison sur les énumérations
   déclarées dans `ExtensionKeys.Types` (liste vide à ce stade, complétée par chaque étape qui crée une clé) hors des
   classes `*Registry` et de leurs implémentations ([DT-35](../technique/01-decisions.md#dt-35--registres-et-stratégies-pas-de-switch)).
6. Créer `.github/test-debt.md` (tableau vide : `Étape | Code non couvert | Raison | Étape qui solde`).

✅ **Vérification automatique.** `dotnet test src/backend/InfraFlowSculptor.slnx` : tous verts, 0 avertissement.

🧪 **Test manuel.** Dans Rider, onglet *Unit Tests* : les cinq projets apparaissent ; lancer « Run All » → vert.
Supprimer temporairement `.WithName(EndpointNames.GetVersion)` n'est **pas** demandé ; à la place, ajouter dans
`Domain` une référence à `Microsoft.EntityFrameworkCore`, lancer les tests → `LayerDependencyTests` échoue en
nommant la dépendance ; annuler la modification.

🧠 **Mémoire.** `09-auth-and-build.md` (commande de test), `06-agents-skills.md` (règles d'architecture).

### S-05 — AppHost Aspire et émulateurs

| | |
|---|---|
| **Technique** | [05](../technique/05-execution-locale.md), [DT-33](../technique/01-decisions.md#dt-33--identité-managée-partout--chaîne-de-connexion-seulement-en-local), [09 — Keycloak](../technique/09-keycloak.md), [DT-07](../technique/01-decisions.md#dt-07--authentification--oidc-entra-en-azure-keycloak-en-local), [DT-12](../technique/01-decisions.md#dt-12--secrets-propres-à-ifs--key-vault), [DT-13](../technique/01-decisions.md#dt-13--fournisseurs-git-derrière-un-port-unique--gitea-comme-émulateur) |
| **Dépend de** | S-04 |
| **Commit** | `feat(local): orchestrer l'API et les émulateurs avec Aspire` |

🎯 **Objectif.** `aspire run` démarre l'API et **tous** les émulateurs de [technique 05 § 2](../technique/05-execution-locale.md#2-ressources-de-lapphost).

🔧 **À faire.**
1. `dotnet new aspire-apphost -n InfraFlowSculptor.AppHost -o src/backend/InfraFlowSculptor.AppHost`
   (modèles Aspire `13.5.3`) ; `src/backend/aspire.config.json` : `{"appHost":{"path":"InfraFlowSculptor.AppHost/InfraFlowSculptor.AppHost.csproj"}}`.
2. Paquets d'hébergement (versions DT-03) : `Aspire.Hosting.PostgreSQL`, `Aspire.Hosting.Azure.Storage`,
   `Aspire.Hosting.Azure.ServiceBus`, `Aspire.Hosting.Redis`, `Aspire.Hosting.Keycloak`,
   `CommunityToolkit.Aspire.Hosting.MailPit`, `Aspire.Hosting.JavaScript`.
3. `AppHost/ResourceNames.cs` : constantes `postgres`, `ifs` (base), `storage`, `blobs`, `servicebus`,
   `generation`, `publication`, `tracking`, `notifications`, `redis`, `mailpit`, `keycloak`, `gitea`, `api`,
   `worker`, `web`.
4. `AppHost/Program.cs` :
   - paramètres secrets `postgres-password`, `keycloak-admin-password` ;
   - `AddPostgres(postgres).WithDataVolume().WithLifetime(ContainerLifetime.Persistent).WithPgWeb()` puis
     `.AddDatabase(ifs)` ;
   - `AddAzureStorage(storage).RunAsEmulator(e => e.WithDataVolume().WithLifetime(ContainerLifetime.Persistent))`
     puis `.AddBlobs(blobs)` ;
   - `AddAzureServiceBus(servicebus).RunAsEmulator()` puis, pour chacune des quatre files,
     `AddServiceBusQueue(<nom>).WithProperties(q => q.RequiresSession = true)` ;
   - `AddRedis(redis).WithRedisInsight()` ;
   - `AddMailPit(mailpit)` ;
   - `AddKeycloak(keycloak, port: 8080, adminPassword: …).WithDataVolume().WithRealmImport("./Realms")` ;
     **repli** si `Aspire.Hosting.Keycloak` n'existe pas pour 13.5 : `AddContainer(keycloak,
     "quay.io/keycloak/keycloak", "26.4")` avec `start-dev --import-realm`, montage de `./Realms` sur
     `/opt/keycloak/data/import`, port 8080 fixe ;
   - `AddContainer(gitea, "gitea/gitea", "1.24").WithHttpEndpoint(port: 3000, targetPort: 3000, name: "http",
     isProxied: false).WithVolume("ifs-gitea-data", "/data").WithEnvironment("GITEA__security__INSTALL_LOCK", "true")
     .WithEnvironment("GITEA__server__ROOT_URL", "http://localhost:3000/")` ;
   - Key Vault : chercher sur nuget.org une intégration Aspire de l'émulateur Key Vault compatible 13.x
     (`AzureKeyVaultEmulator.Aspire.Hosting`) ; si elle existe, l'ajouter (`keyvault`) ; sinon ne rien
     ajouter et écrire « émulateur Key Vault indisponible : DevelopmentSecretStore » dans
     `.github/memory/08-runtime-and-orchestration.md` ;
   - `AddProject<Projects.InfraFlowSculptor_Api>(api)` avec `WithReference` + `WaitFor` sur la base, les
     blobs, Service Bus, Redis, MailPit, Keycloak ; variables `Auth__Authority =
     http://localhost:8080/realms/ifs`, `Auth__Audience = ifs-api`, `Auth__Provider = Keycloak`,
     `Cors__AllowedOrigins__0 = http://localhost:4200`, `Ifs__Development__EnableGitea = true` ;
     `WithExternalHttpEndpoints()`.
5. `AppHost/Realms/ifs-realm.json` : royaume `ifs`, clients `ifs-web`, `ifs-api`, `ifs-scalar`, mappers et les
   sept utilisateurs de [technique 05 § 3](../technique/05-execution-locale.md#3-utilisateurs-de-démonstration-royaume-keycloak-ifs)
   avec leurs attributs (`oid` = GUID fixe par utilisateur, écrit dans le tableau de `05-execution-locale.md`
   par Luna dans la même étape), `email_verified` (faux pour nina), rôles internes pour ops.
6. **Résolution des connexions** ([DT-33](../technique/01-decisions.md#dt-33--identité-managée-partout--chaîne-de-connexion-seulement-en-local)): créer `Infrastructure/Azure/AzureConnectionResolver.cs`,
   `AzureConnection.cs`, `AzureConnectionMode.cs`, `IfsAzureCredential.cs` (singleton : `ManagedIdentityCredential`
   avec `AZURE_CLIENT_ID` si `IDENTITY_ENDPOINT` existe, sinon `DefaultAzureCredential` sans modes interactifs) et des
   extensions `AddIfsBlobServiceClient("blobs")`, `AddIfsServiceBusClient("servicebus")`, `AddIfsRedis("redis")`
   (paquets `Azure.Storage.Blobs`, `Azure.Messaging.ServiceBus`, `StackExchange.Redis`,
   `Microsoft.Azure.StackExchangeRedis`) qui appliquent la règle de DT-33 : `ConnectionStrings:<nom>` si présente
   (émulateurs Aspire), sinon `Azure:<nom>:Endpoint` + identité managée, sinon erreur au démarrage. Les clients
   « Aspire.* » ne sont **pas** utilisés pour construire ces clients (ils n'appliquent pas cette règle) ; la télémétrie
   et la santé sont ajoutées à la main (`AddAzureClientsCore`, contrôles de santé `AspNetCore.HealthChecks.*`).
   Tests `Infrastructure.Tests/Azure/AzureConnectionResolverTests.cs` (les quatre cas de DT-33) et règle
   d'architecture « aucun `new BlobServiceClient(` / `new ServiceBusClient(` / `ConnectionMultiplexer.Connect` hors
   de `Infrastructure/Azure/` ». L'API enregistre ces clients et les contrôles de santé (`/health`).
7. `tools/dev/gitea-init.ps1` : attend que `http://localhost:3000/api/healthz` réponde ; crée par
   `docker exec <conteneur gitea> gitea admin user create --admin --username ifs-dev --password <param>
   --email ifs-dev@contoso.example --must-change-password=false` (ignore « already exists ») ; crée par l'API
   Gitea (authentification basique) l'organisation `contoso` et les dépôts `shop`, `shop-infra`, `shop-app`
   initialisés avec `main` ; crée un jeton d'accès `ifs-dev` et l'écrit avec
   `dotnet user-secrets set "Gitea:Token" <jeton> --project src/backend/InfraFlowSculptor.AppHost`.
   Idempotent : une seconde exécution n'échoue pas et ne crée rien en double.
8. `tests/InfraFlowSculptor.Acceptance.Tests` (nouveau, `Aspire.Hosting.Testing`) :
   `AppHostStartupTests.Api_is_healthy_when_all_emulators_run` — démarre l'AppHost, attend `api` en état
   « Running », `GET /health` → 200. Trait `Category=Acceptance`.

✅ **Vérification automatique.**
- `dotnet build` sans avertissement.
- `dotnet test src/backend/tests/InfraFlowSculptor.Acceptance.Tests` → vert (Docker requis).

🧪 **Test manuel.**
1. Docker Desktop démarré. `cd src/backend ; aspire run`. → Le tableau de bord s'ouvre ; en 3 minutes au plus,
   toutes les ressources sont « Running » ou « Healthy ».
2. Cliquer le lien de `pgweb` → la base `ifs` est vide.
3. Cliquer `mailpit` → boîte vide.
4. Cliquer `redis` → RedisInsight se connecte.
5. Ouvrir `http://localhost:8080/realms/ifs/account` → se connecter `alice@contoso.example` /
   `Ifs-Demo-2026!` → la page de compte Keycloak affiche Alice.
6. `pwsh tools/dev/gitea-init.ps1` → « 3 dépôts prêts ». Ouvrir `http://localhost:3000` → l'organisation
   `contoso` et ses trois dépôts. Relancer le script → aucune erreur.
7. Arrêter (`Ctrl+C`), relancer `aspire run` → les données Gitea et PostgreSQL sont conservées (volumes).

🧠 **Mémoire.** `08-runtime-and-orchestration.md` (ressources, ports, volumes, repli Keycloak/Key Vault),
`09-auth-and-build.md` (`aspire run`, secrets utilisateur).

📌 **Hors dépôt.** Mémoire Docker allouée (6 Go recommandés), dans `NEXT.md` § Machine.

### S-06 — Persistance de base : PostgreSQL, conventions, concurrence, idempotence

| | |
|---|---|
| **Technique** | [DT-04](../technique/01-decisions.md#dt-04--postgresql-17), [DT-23](../technique/01-decisions.md#dt-23--identifiants-et-concurrence), [DT-24](../technique/01-decisions.md#dt-24--isolation-multi-organisations), [DT-31](../technique/01-decisions.md#dt-31--idempotence-des-commandes), [02 § 4](../technique/02-backend.md#4-persistance) |
| **Spécifications** | [RG-DON-02](../specs/05-modele-de-donnees.md), [RG-DON-05](../specs/05-modele-de-donnees.md), [DEC-37](../specs/03-decisions.md), [RG-UI-15](../specs/26-interface.md) |
| **Dépend de** | S-05 ; ⚖️ DT-04 confirmée |
| **Commit** | `feat(persistance): contexte EF PostgreSQL, conventions et idempotence` |

🎯 **Objectif.** Toutes les futures tables héritent sans effort de l'isolation par organisation, de la
concurrence optimiste, des identifiants v7 et de l'idempotence des commandes.

🔧 **À faire.**
1. Paquets Infrastructure : `Npgsql.EntityFrameworkCore.PostgreSQL`, `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`
   (seulement pour `EnrichNpgsqlDbContext` : santé, traces, métriques), `EFCore.NamingConventions`,
   `Microsoft.EntityFrameworkCore.Design`. Tests : `Testcontainers.PostgreSql`. Extension `AddIfsDbContext("ifs")` qui
   suit [DT-33](../technique/01-decisions.md#dt-33--identité-managée-partout--chaîne-de-connexion-seulement-en-local): chaîne `ConnectionStrings:ifs` si présente (conteneur Aspire) ; sinon `NpgsqlDataSourceBuilder` sur
   `Azure:ifs:Endpoint`, `Azure:ifs:Database`, `Azure:ifs:Username`, SSL requis, et
   `UsePeriodicPasswordProvider` qui demande un jeton Entra (`https://ossrdbms-aad.database.windows.net/.default`) à
   `IfsAzureCredential` toutes les 50 minutes ; puis `builder.EnrichNpgsqlDbContext<IfsDbContext>()`. Test : avec
   seulement `Azure:ifs:*`, la source de données est construite sans mot de passe et appelle le fournisseur de jeton
   (faux `TokenCredential`).
2. `Domain/Common/Identifiers/StronglyTypedId.cs` : interface marqueur `IStronglyTypedId { Guid Value { get; } }` ;
   les identifiants sont des `readonly record struct XxxId(Guid Value) : IStronglyTypedId` avec
   `static XxxId New(TimeProvider clock)` (Guid v7).
3. `Application/Common/Persistence/IIfsDbContext.cs` (évolution E11) et
   `Application/Common/Security/ICurrentOrganization.cs` (`OrganizationId? Id`).
4. `Infrastructure/Persistence/IfsDbContext.cs` : `UseSnakeCaseNamingConvention()` ; dans
   `ConfigureConventions`, convertisseur générique pour tout `IStronglyTypedId` ; dans `OnModelCreating`,
   pour toute entité `IOrganizationOwned` (nouvelle interface Domain : `OrganizationId OrganizationId`), filtre
   `e => e.OrganizationId == _currentOrganization.Id` ; pour toute entité `IHasVersion`, `Version` jeton de
   concurrence ; `SaveChangesAsync` incrémente `Version` des entités modifiées.
5. Tables transverses (configurations EF + entités dans `Infrastructure/Persistence/Common/`) :
   `outbox_messages (id, organization_id, queue, session_id, type, payload jsonb, created_at, sent_at, attempts)`,
   `idempotency_keys (organization_id, key, request_hash, status_code, response jsonb, created_at)` avec clé
   primaire `(organization_id, key)`, `scheduled_job_leases (job_name pk, holder, expires_at)`.
5 bis. `Domain/Common/ObjectRef.cs` (`ObjectType` énumération + `Guid Id`) : référence unique d'un objet pour l'audit,
   l'historique, les constats, les notifications et plus tard les commentaires ([technique 10](../technique/10-extensibilite.md) § 2).
5 ter. `UnitOfWorkBehavior` (Application, comportement du médiateur, commandes seulement) : une transaction ;
   `SaveChangesAsync` une fois ; collecte des événements de domaine levés par les agrégats
   (`AggregateRoot.DomainEvents`, `IDomainEvent`) et écriture dans `outbox_messages` (file vide = événement de domaine)
   dans la **même** transaction ([DT-37](../technique/01-decisions.md#dt-37--événements-de-domaine-par-loutbox)) ; traduction du conflit de concurrence (point 6). Tests : un événement levé est
   dans l'outbox après la commande ; une commande en échec n'en laisse aucun.
6. `Infrastructure/Persistence/ConcurrencyConflictException` traduite par `UnitOfWork` en
   `Error.Conflict("CONFLICT_VERSION")` ; le mapping 409 existe déjà (S-03).
7. `Api/Common/Idempotency/IdempotencyEndpointFilter.cs` : appliqué par une extension
   `.WithIdempotency()` aux points de terminaison d'écriture ; comportement exact de
   [DT-31](../technique/01-decisions.md#dt-31--idempotence-des-commandes) ; purge des clés de plus de 24 h par la
   tâche planifiée de S-08 (en attendant, la requête de purge existe et est testée).
8. `Api/Common/DatabaseMigrationPolicy.cs` (copie adaptée de VPD, évolution E10) : migrations au démarrage si
   l'environnement est `Development` ou `Testing`.
9. Migration initiale `S_06_Socle` :
   `dotnet ef migrations add S_06_Socle -p src/backend/InfraFlowSculptor.Infrastructure -s src/backend/InfraFlowSculptor.Api -o Persistence/Migrations`.
   Fabrique de conception `IfsDbContextDesignTimeFactory` (chaîne de connexion factice locale).
10. Tests `Infrastructure.Tests/Persistence/` (Testcontainers, une base par classe de test) :
    `OrganizationFilterTests` (une entité de test `IOrganizationOwned` définie dans le projet de test et mappée
    par un `IfsDbContext` de test : les lignes d'une autre organisation sont invisibles) ;
    `ConcurrencyTests` (deux contextes modifient la même ligne → le second lève le conflit) ;
    `IdempotencyStoreTests`. `Api.Tests/Common/IdempotencyFilterTests` : même clé + même corps → même réponse,
    une seule exécution ; même clé + autre corps → 422 `IDEMPOTENCY_KEY_REUSED`.

✅ **Vérification automatique.** `dotnet test` vert ; `aspire run` applique la migration (journal de l'API :
« Applied migration S_06_Socle »).

🧪 **Test manuel.**
1. `aspire run`, ouvrir pgweb.
   → Tables `outbox_messages`, `idempotency_keys`, `scheduled_job_leases`, `__EFMigrationsHistory` (ligne
   `S_06_Socle`), toutes en `snake_case`.
2. Arrêter puis relancer : aucune nouvelle migration, aucune erreur.

🧠 **Mémoire.** `05-data-and-storage.md` (conventions, filtres, version, tables transverses, commande de
migration).

### S-07 — Authentification OIDC et utilisateur courant

| | |
|---|---|
| **Technique** | [DT-07](../technique/01-decisions.md#dt-07--authentification--oidc-entra-en-azure-keycloak-en-local), [02 § 5](../technique/02-backend.md#5-authentification-et-autorisation) |
| **Spécifications** | [RG-ORG-01](../specs/10-organisations-et-acces.md), [RG-ORG-02](../specs/10-organisations-et-acces.md), [UC-ORG-04](../specs/10-organisations-et-acces.md) (adresse vérifiée) |
| **Dépend de** | S-06 ; ⚖️ DT-07 confirmée |
| **Commit** | `feat(auth): authentifier par OIDC et exposer l'utilisateur courant` |

🎯 **Objectif.** L'API accepte les jetons de Keycloak en local et d'Entra en Azure par la même configuration,
et connaît `(tid, oid)` et l'adresse vérifiée de l'appelant.

🔧 **À faire.**
1. `Api/Authentication/AuthenticationSetup.cs` : options `AuthOptions { Provider (Keycloak|Entra), Authority,
   Audience, TenantId }` (section `Auth`, validées au démarrage).
   - `Keycloak` : `AddJwtBearer(Schemes.Oidc)` avec `Authority`, `Audience`, `MapInboundClaims = false`,
     `TokenValidationParameters.NameClaimType = "name"`, `RoleClaimType = "roles"`,
     `RequireHttpsMetadata = false` **seulement** en développement.
   - `Entra` : `AddMicrosoftIdentityWebApi(...)` sous le nom `Schemes.Oidc`, `TenantId = "common"`, validation
     d'émetteur multi-tenant de Microsoft.Identity.Web, mêmes types de revendications (évolution E4).
   - `AddPolicyScheme("Bearer")` : `ForwardDefaultSelector` → `Schemes.ApiToken` si l'en-tête commence par
     `Bearer ifs_`, sinon `Schemes.Oidc` (évolution E5). Le schéma `ApiToken` est enregistré avec un
     gestionnaire qui renvoie `NoResult` (rempli en J1).
2. `Application/Common/Security/ICurrentUser.cs` : `UserKey Key` (`TenantId`, `ObjectId`), `DisplayName`,
   `VerifiedEmail?`, `IsAuthenticated`, `AuthenticationKind` (`Oidc`, `ApiToken`) ;
   `Infrastructure/Authentication/HttpCurrentUser.cs`.
3. `Infrastructure/Authentication/VerifiedEmailResolver.cs` : les quatre règles de DT-07, dans l'ordre ;
   constante du tenant des comptes personnels dans `EntraConstants.cs`.
4. `Api/Common/AuthorizationPolicies.cs` : `Member` = utilisateur authentifié ; politique par défaut
   `Member` (`SetFallbackPolicy`) — les points de terminaison anonymes portent `.AllowAnonymous()`
   explicitement (`/v1/version`, santé, OpenAPI).
5. `Api/Controllers/MeController.cs` : `GET /v1/me` → `{ tenantId, objectId, displayName, verifiedEmail }`
   (`GetMe`).
6. Scalar en développement : authentification OAuth2 *authorization code* + PKCE, client `ifs-scalar`,
   portée `openid profile email`.
7. Tests : `Api.Tests/Common/TestTokens.cs` émet des jetons signés par une clé symétrique de test ; en
   environnement `Testing`, `AuthenticationSetup` valide cette clé (option `Auth:TestSigningKey`, refusée hors
   `Testing`). `MeEndpointTests` : sans jeton → 401 problem+json ; jeton d'alice → ses identifiants ; jeton de
   nina (`email_verified=false`) → `verifiedEmail: null`. `VerifiedEmailResolverTests` : un test par règle et
   un test d'ordre de priorité. `AuthenticationSetupTests` : `Auth:TestSigningKey` refusée en `Production`.

✅ **Vérification automatique.** `dotnet test` vert ; build sans avertissement.

🧪 **Test manuel.**
1. `aspire run`, ouvrir `/scalar` de l'API, bouton *Authorize* → page de connexion Keycloak → alice.
2. `GET /v1/me` → `displayName: "Alice Martin"`, `tenantId: "11111111-…"`, `verifiedEmail: "alice@contoso.example"`.
3. Se déconnecter (vider l'autorisation), se connecter en `nina@contoso.example` → `verifiedEmail: null`.
4. Sans autorisation, `GET /v1/me` → 401.

🧠 **Mémoire.** `09-auth-and-build.md` (schémas, fournisseurs, revendications, utilisateurs de démonstration).

### S-08 — Worker, outbox et files à sessions

| | |
|---|---|
| **Technique** | [DT-08](../technique/01-decisions.md#dt-08--worker--service-net-et-service-bus-pas-azure-functions), [DT-32](../technique/01-decisions.md#dt-32--files-et-équité) |
| **Spécifications** | [EXG-23](../specs/27-exigences-non-fonctionnelles.md) |
| **Dépend de** | S-07 ; ⚖️ DT-08 confirmée |
| **Commit** | `feat(worker): relais d'outbox, files à sessions et tâches planifiées` |

🎯 **Objectif.** Un travail mis en file par l'API dans sa transaction est exécuté une fois par le worker, les
organisations étant servies à tour de rôle, et les tâches planifiées ne s'exécutent qu'une fois même avec
plusieurs réplicas.

🔧 **À faire.**
1. `dotnet new worker -n InfraFlowSculptor.Worker -o src/backend/InfraFlowSculptor.Worker` ; références :
   Application, Infrastructure, ServiceDefaults ; `builder.AddServiceDefaults()` ; `AddIfsDbContext("ifs")` et
   `AddIfsServiceBusClient("servicebus")` (règle de [DT-33](../technique/01-decisions.md#dt-33--identité-managée-partout--chaîne-de-connexion-seulement-en-local), jamais un client construit autrement).
2. `Application/Common/Jobs/` : `JobQueue` (enum : `Generation`, `Publication`, `Tracking`, `Notifications`,
   noms de files en constantes), `IJob` (marqueur), `JobEnvelope (JobId, OrganizationId, Type, Payload,
   TraceParent)`, `IJobDispatcher.Enqueue<TJob>(JobQueue, OrganizationId, TJob)` qui écrit dans
   `outbox_messages` (même transaction que la commande), `IJobHandler<TJob>`.
2 bis. Worker : `DomainEventDispatcherService` — lit les messages d'outbox sans file (événements de domaine), les
   distribue à chaque `IDomainEventHandler<T>` enregistré, idempotence par `(événement, gestionnaire)` dans
   `processed_jobs` ([DT-37](../technique/01-decisions.md#dt-37--événements-de-domaine-par-loutbox)) ; test avec un gestionnaire de test.
3. Worker : `OutboxRelayService` (BackgroundService) lit par lots les messages non envoyés (`FOR UPDATE SKIP
   LOCKED`), les envoie sur la file avec `SessionId = organization_id` et `MessageId = id`, marque `sent_at` ;
   réessai exponentiel, `attempts` incrémenté.
4. Worker : `SessionJobProcessorService` — un `ServiceBusSessionProcessor` par file, `MaxConcurrentSessions = 8`,
   `MaxConcurrentCallsPerSession = 1`, `SessionIdleTimeout = 5 s` (une session vide est libérée et la suivante
   servie) ; désérialise l'enveloppe, résout `IJobHandler<T>`, exécute dans une portée DI avec
   `ICurrentOrganization` positionné, complète le message ; idempotence : table `processed_jobs (job_id pk,
   processed_at)` vérifiée avant exécution (migration `S_08_Jobs`).
5. Worker : `ScheduledJobRunner` + `IScheduledJob { string Name; TimeSpan Interval; Task RunAsync(...) }`,
   bail `scheduled_job_leases` (prise atomique `UPDATE … WHERE expires_at < now()`). Première tâche planifiée :
   `PurgeIdempotencyKeysJob` (toutes les heures, supprime les clés > 24 h).
6. Propagation de trace : `TraceParent` de l'activité courante écrit dans l'enveloppe, restauré dans le worker
   (`ActivitySource` `InfraFlowSculptor.Jobs`).
7. Démonstration, **développement seulement** : `Api/Controllers/DevelopmentController.cs` mappé si
   `Environment.IsDevelopment()` : `POST /v1/dev/ping-job` → met en file `PingJob` ; `PingJobHandler` (worker)
   journalise « PingJob {JobId} traité pour {OrganizationId} ».
8. AppHost : `AddProject<Projects.InfraFlowSculptor_Worker>(worker)` avec les mêmes références et `WaitFor`.
9. Tests : `Application.Tests/Jobs/JobDispatcherTests` (l'outbox reçoit l'enveloppe) ;
   `Infrastructure.Tests/Jobs/ScheduledJobLeaseTests` (deux exécuteurs concurrents → une seule exécution) ;
   `Acceptance.Tests/JobsTests.Ping_job_is_processed_once` (via l'AppHost : appel de `/v1/dev/ping-job`, attente
   de la ligne `processed_jobs`) ; `Acceptance.Tests/JobsTests.Sessions_are_served_fairly` (20 travaux de
   l'organisation A puis 1 de B : le travail de B se termine avant le 10ᵉ de A ; chaque `PingJob` attend 200 ms).

✅ **Vérification automatique.** `dotnet test` (acceptation comprise) vert.

🧪 **Test manuel.**
1. `aspire run` ; Scalar connecté en alice ; `POST /v1/dev/ping-job` → 202.
2. Tableau de bord → ressource `worker` → journaux : « PingJob … traité ».
3. Onglet *Traces* : la trace de la requête contient les segments `api` **et** `worker` reliés.
4. pgweb : `outbox_messages.sent_at` renseigné, une ligne dans `processed_jobs`.

🧠 **Mémoire.** `08-runtime-and-orchestration.md` (worker, files, sessions, tâches planifiées).

### S-09 — Application Angular générée depuis le template

| | |
|---|---|
| **Technique** | [DT-18](../technique/01-decisions.md#dt-18--frontend--angular-22-généré-par-ng-template), [04 § 1-2, § 6](../technique/04-frontend.md) |
| **Dépend de** | S-08 |
| **Commit** | `feat(web): générer l'application Angular depuis ng-template` |

🎯 **Objectif.** L'application Angular existe, démarre dans Aspire, connecte l'utilisateur par Keycloak et
appelle `/v1/me`.

🔧 **À faire.**
1. Générer selon [technique 04 § 1](../technique/04-frontend.md#1-génération-et-place-dans-le-dépôt). Si
   `npx` échoue avec 401/404 sur `@floriandrevet/ng-template` (paquet GitHub Packages) :
   ```powershell
   git clone https://github.com/FlorianDrevet/angular_template "$env:TEMP/angular_template"
   cd "$env:TEMP/angular_template"; npm ci; npm run build; npm pack
   cd <dépôt>/src/frontend
   npx -p @angular/cli@22 -p "$env:TEMP/angular_template/floriandrevet-ng-template-22.0.0.tgz" ng new ifs-web --collection=@floriandrevet/ng-template --ssr=false --ui=tailwind --auth=oidc --i18n=true --docker=true --ci=false --skip-git
   ```
2. Vérifier la génération (`npm run lint`, `npm test -- --watch=false`, `npm run build`) **avant** toute
   modification, et commiter-la dans ce même commit d'étape.
3. `public/config.json` et `core/config/app-config.token.ts` : étendre `AppConfig` à
   `{ apiUrl: string; oidc: { provider: 'keycloak' | 'entra'; authority: string; clientId: string; scope: string } }`.
4. `scripts/write-config.mjs` : écrit `public/config.json` depuis `IFS_API_URL`, `IFS_OIDC_PROVIDER`,
   `IFS_OIDC_AUTHORITY`, `IFS_OIDC_CLIENT_ID`, `IFS_OIDC_SCOPE` (valeurs par défaut locales si absentes) ;
   `package.json` : `"prestart": "node scripts/write-config.mjs"`. Le point d'entrée Docker du template est
   adapté pour écrire le même JSON.
5. `core/auth/oidc.providers.ts` : remplacer les constantes par `StsConfigHttpLoader` qui lit
   `/config.json` (configuration de [technique 04 § 6](../technique/04-frontend.md#6-configuration-à-lexécution-et-authentification)).
   Portée Keycloak : `openid profile email offline_access`.
6. Page d'accueil provisoire `features/home/` : « Bonjour {{ displayName }} » depuis `GET /v1/me` (client écrit
   à la main ici, remplacé par le client généré en S-14).
7. Transloco ([DT-34](../technique/01-decisions.md#dt-34--langues--français-et-anglais-commutables)) : langue par défaut `fr`, disponibles `fr`, `en` ; textes de l'accueil dans
   `public/i18n/fr.json` et `en.json` ; `core/i18n/language.service.ts` (ordre : choix local mémorisé, langue du
   navigateur si `fr` ou `en`, sinon `fr` — la préférence serveur s'ajoute en J0-01) qui met à jour `<html lang>` ;
   script `npm run i18n:check` (clés identiques dans les deux fichiers, aucune clé inutilisée) ajouté à `lint`.
8. AppHost : `AddJavaScriptApp(web, "../../frontend/ifs-web").WithRunScript("start").WithArgs("--", "--port",
   "4200").WithHttpEndpoint(port: 4200, targetPort: 4200, name: "http", isProxied: false)` (forme de VPD),
   variables `IFS_API_URL` = point de terminaison https de `api`, `IFS_OIDC_*` (Keycloak, client `ifs-web`),
   `WaitFor(api)`, `WaitFor(keycloak)`.

✅ **Vérification automatique.** Dans `src/frontend/ifs-web` : `npm run lint`, `npm test -- --watch=false`,
`npm run build` verts. `dotnet test` (acceptation) toujours vert.

🧪 **Test manuel.**
1. `aspire run` ; ouvrir `http://localhost:4200`.
   → Redirection vers la page Keycloak ; se connecter en `bob@contoso.example`.
   → Retour sur l'application : « Bonjour Bob Durand ».
2. Recharger la page → toujours connecté, pas de nouvelle connexion.
3. Ouvrir `http://localhost:4200/config.json` → `apiUrl` pointe vers l'API locale, `provider: "keycloak"`.

🧠 **Mémoire.** `04-frontend.md` (génération, options, config, auth), `08-runtime-and-orchestration.md` (`web`).

### S-10 — Tokens Strata, polices et styles de base

| | |
|---|---|
| **Technique** | [DT-19](../technique/01-decisions.md#dt-19--design-system--strata-reproduit-en-composants-angular) |
| **Design** | [Strata — README](../design/strata/README.md), [`tokens.json`](../design/strata/tokens.json), [aperçu](../design/strata/index.html) |
| **Dépend de** | S-09 |
| **Commit** | `feat(web): tokens Strata, polices et styles de base` |

🎯 **Objectif.** Toutes les valeurs de style de l'application viennent de `tokens.json`, par génération.

🔧 **À faire.**
1. `scripts/build-tokens.mjs` (Node, sans dépendance) : lit `../../../docs/design/strata/tokens.json` et écrit
   - `src/styles/tokens.css` : `:root { --ifs-<nom>: <valeur>; … }` pour `color`, `spacing`, `radius`,
     `shadow`, `size`, et `--ifs-font-sans`, `--ifs-font-mono`, l'échelle typographique (`--ifs-text-page-title`
     28px… d'après `type.groups`) ;
   - `src/styles/theme.css` : bloc Tailwind v4 `@theme { --color-<nom>: var(--ifs-<nom>); --spacing-<n>…;
     --radius-<nom>…; --font-sans…; }` ;
   - **un bloc par thème** ([DT-30](../technique/01-decisions.md#dt-30--thème-sombre-seul-changement-de-thème-prêt)) : thème par défaut sous `:root, :root[data-theme="<défaut>"]`, chaque autre thème
     de `color.themes` sous `:root[data-theme="<id>"]` (valeurs prises dans `values.<id>` d'un token, sinon `value`) ;
   - `src/app/core/theme/themes.generated.ts` : `export const AVAILABLE_THEMES = ['dark'] as const; export const
     DEFAULT_THEME = 'dark';` (liste lue dans `color.themes`) ;
   - en-tête « Généré depuis docs/design/strata/tokens.json — ne pas modifier ».
   Option `--check` : régénère en mémoire et sort en code 1 si les fichiers diffèrent. Scripts npm `tokens`,
   `tokens:check`.
2. Polices : `@fontsource/instrument-sans` (400, 500, 600), `@fontsource/jetbrains-mono` (400, 500), importées
   dans `src/styles/fonts.css` ; aucune requête vers Google Fonts.
3. `src/styles/base.css` : `body` sur `--ifs-bg`, texte `--ifs-text`, Instrument Sans 14/20 ; liens
   `--ifs-signal-text` ; `:focus-visible` (anneau `--ifs-signal` 2 px, décalage 2 px) ; sélection ;
   `code, kbd, .mono` en JetBrains Mono ; barres de défilement sombres.
4. `styles.css` importe dans l'ordre : `tailwindcss`, `theme.css`, `tokens.css`, `fonts.css`, `base.css`.
5. Galerie de développement : route `/dev/design-system` (enregistrée seulement si `!environment.production`
   — utiliser `isDevMode()`), page « Fondations » : nuancier de toutes les couleurs (nom, valeur, usage),
   échelle typographique, espacements, rayons.
6. Tests Vitest `scripts/build-tokens.spec.mjs` : sur un `tokens.json` réduit, le CSS produit est exactement
   celui attendu ; un `tokens.json` de test à **deux thèmes** produit deux blocs et
   `AVAILABLE_THEMES = ['dark', 'light']` ; `--check` détecte une différence.

✅ **Vérification automatique.** `npm run tokens:check`, `npm run lint`, `npm test`, `npm run build`.

🧪 **Test manuel.**
1. Ouvrir côte à côte `docs/design/strata/index.html` (double-clic) et `http://localhost:4200/dev/design-system`.
   → Mêmes couleurs, mêmes noms ; police Instrument Sans visible (le « g » à double panse).
2. Outils de développement → onglet Réseau, recharger : aucune requête vers `fonts.googleapis.com`.

🧠 **Mémoire.** `11-frontend-design-system.md` (génération des tokens, règles d'usage).

### S-11 — Composants Strata de base

| | |
|---|---|
| **Technique** | [04 § 4](../technique/04-frontend.md#4-correspondance-strata--angular) |
| **Design** | [composants Strata](../design/strata/index.html#composants) : `Button`, `Icon`, `Badge`, `TextField`, `Segmented`, `Toggle`, `Tabs`, `Banner`, `Panel` ; géométrie : [`components/bundle.css`](../design/strata/components/bundle.css) ; API : [`components/index.d.ts`](../design/strata/components/index.d.ts) ; règles : `components/<Nom>/README.md` |
| **Dépend de** | S-10 |
| **Commit** | `feat(web): composants Strata de base` |

🎯 **Objectif.** Les neuf composants de base existent en Angular, fidèles à Strata, testés, visibles dans la
galerie.

🔧 **À faire.** Pour chacun, dans `src/app/ds/<nom>/` : composant autonome `OnPush`, sélecteur `app-ds-<nom>`,
entrées en signaux (`input()`), sorties (`output()`), styles recopiés des règles `.st-<nom>*` de `bundle.css`
en remplaçant chaque `var(--x, repli)` par `var(--ifs-x)`, test Vitest + Angular Testing Library, section dans
la galerie (page « Composants ») reproduisant **les mêmes exemples** que `components/<Nom>/preview.html`.

| Composant | Entrées | Règles à tester |
|---|---|---|
| `app-ds-icon` | `name`, `size` (16), `strokeWidth` (1.75), `label` | `aria-hidden` sans `label` ; `role="img"` + `aria-label` avec. Tracés `ICON_PATHS` recopiés de `bundle.js` dans `icon-paths.ts` |
| `app-ds-button` | `variant` (primary, secondary, ghost, danger), `size` (md, sm), `icon`, `iconPosition`, `href`, `type`, `disabled` | `<a>` si `href`, sinon `<button type="button">` ; icône seule exige `aria-label` (erreur en développement sinon) |
| `app-ds-badge` | `tone`, `mono`, `dot`, `icon` | Le texte est toujours rendu (jamais la seule couleur) |
| `app-ds-text-field` | `label`, `hint`, `error`, `mono`, `changed`, + `ControlValueAccessor` | `<label for>`, `aria-describedby` vers hint/erreur, `aria-invalid` |
| `app-ds-segmented` | `options`, `value` (modèle), `mono`, `ariaLabel` | `role="group"`, `aria-pressed` |
| `app-ds-toggle` | `checked` (modèle), `label`, `ariaLabel` | `role="switch"`, `aria-checked`, clavier Espace |
| `app-ds-tabs` | `items` (`id`, `label`, `count`, `countTone`), `active` (modèle) | `role="tablist"`, flèches gauche/droite |
| `app-ds-banner` | `tone`, `title`, contenu projeté, emplacement `action` | `role="status"` (signal, success) ou `alert` (danger) |
| `app-ds-panel` | `title`, `subtitle`, `padded`, emplacement `actions` | En-tête `<h2>`/`<h3>` selon `level` |

✅ **Vérification automatique.** `npm run lint`, `npm test` (≥ 1 test par règle du tableau), `npm run build`.

🧪 **Test manuel.**
1. Ouvrir `docs/design/strata/rendered/Button.html` et la galerie, page « Composants », section Button.
   → Mêmes variantes, mêmes hauteurs (36 et 28 px), mêmes couleurs ; survol et focus visibles.
2. Répéter pour chaque composant (les huit autres fichiers `rendered/*.html`).
3. Navigation au clavier seul (Tab, Espace, flèches) dans la galerie : chaque contrôle est atteignable et
   actionnable, le focus est toujours visible.

🧠 **Mémoire.** `11-frontend-design-system.md` (composants disponibles et leurs entrées).

### S-12 — Composants produit Strata et motifs

| | |
|---|---|
| **Design** | `ResourceIcon`, `ResourceRow`, `GeneratedName`, `GoldenPathRail` ; motif Dialogue, tableau dense, sélecteur (README Strata § Mise en page) ; [icônes Azure](../design/strata/assets/Azure/README.md) |
| **Dépend de** | S-11 |
| **Commit** | `feat(web): composants produit Strata et icônes Azure` |

🎯 **Objectif.** Les composants propres au produit et les motifs de mise en page sont prêts pour les écrans.

🔧 **À faire.**
1. `scripts/extract-azure-icons.mjs` : lit `docs/design/strata/components/bundle.js`, extrait la table
   `resourceTypes` et les data URI SVG, écrit `public/azure-icons/<fichier>.svg` (contenu SVG décodé, non
   modifié) et `src/app/ds/resource-icon/resource-types.ts` (type → libellé, abréviation, catégorie, fichier).
   Script npm `icons`. Les fichiers générés sont commités.
2. `app-ds-resource-icon` (`type`, `size` 28, `variant` icon|tile, `alt`) : `<img>` de l'icône officielle, ou
   tuile d'abréviation (fond `surface-3`, mono, couleur de catégorie) si `variant="tile"` ou si le type n'a pas
   d'icône. Respect des règles Microsoft (aucun filtre, `alt` = libellé par défaut).
3. `app-ds-resource-row`, `app-ds-generated-name` (bordure pointillée, coche/croix de disponibilité, `env` en
   mono), `app-ds-golden-path` (quatre étapes, états `done|attention|error|current|pending`, `meta`, `hint`).
4. `app-ds-dialog` sur `@angular/cdk/dialog` : `surface-1`, bordure `line-strong`, `radius-xl`,
   `shadow-overlay`, voile `overlay-scrim`, titre `<h2>` relié par `aria-labelledby`, piège du focus, Échap.
5. `app-ds-table` (en-têtes mono capitales 11 px, lignes `line-subtle`, défilement horizontal **dans** la boîte
   sous sa largeur minimale), `app-ds-select` (sélecteur natif stylé comme dans la maquette).
6. Galerie : sections correspondantes, mêmes exemples que les `preview.html`.
7. Tests Vitest : tuile de repli quand le type est inconnu ; dialogue (focus initial, Échap, retour du focus) ;
   tableau (aucun débordement de la page à 390 px — test de style calculé).

✅ **Vérification automatique.** `npm run lint`, `npm test`, `npm run build`.

🧪 **Test manuel.**
1. Galerie → `ResourceIcon` : comparer à `docs/design/strata/rendered/ResourceIcon.html` (25 icônes + tuiles).
2. Ouvrir un dialogue de démonstration, appuyer sur Tab plusieurs fois → le focus reste dans le dialogue ;
   Échap → fermeture, le focus revient au bouton d'ouverture.
3. Réduire la fenêtre à 390 px → le tableau défile dans sa boîte, la page ne défile pas horizontalement.

🧠 **Mémoire.** `11-frontend-design-system.md`.

### S-13 — Coquille de l'application, connexion et Playwright

| | |
|---|---|
| **Spécifications** | [26 § 3](../specs/26-interface.md) (RG-UI-05, 09, 16), [P9](../specs/01-principes.md) |
| **Maquette** | [Sidebar](../design/maquette-v1/preview/Sidebar.html), [Login](../design/maquette-v1/preview/Login.html), structure de [Main](../design/maquette-v1/preview/Main.html) (barre supérieure, fil d'Ariane, zone de contenu) — **zones exclues** : recherche `Ctrl K` (J3), Propositions (J2), Notifications (J3), sélecteur d'organisation (J0-01) |
| **Dépend de** | S-12 |
| **Commit** | `feat(web): coquille, connexion et tests de bout en bout` |

🎯 **Objectif.** La structure de toutes les pages (barre latérale 236 px, barre supérieure 56 px, contenu
1 280 px max), la page de connexion, la gestion des erreurs d'API, et le premier test Playwright.

🔧 **À faire.**
1. `shell/` : `ShellComponent` (mise en page de Strata § Mise en page), `SidebarComponent` alimentée par un
   `NavigationRegistry` : chaque fonctionnalité **enregistre** ses entrées (portée `workspace`, `project`,
   `organization`, `backoffice`) au moment où elle est livrée — aucune entrée n'est écrite pour une fonction
   absente ([P9](../specs/01-principes.md)). Logo trois couches en SVG en ligne (copié de `Sidebar.html`).
   `TopbarComponent` : fil d'Ariane mono à gauche, emplacement `actions` à droite. Sous 760 px : barre latérale
   masquée, bouton menu qui l'ouvre en tiroir (`app-ds-dialog` latéral).
2. `features/auth/login/` : reproduction de `Login.html` (libellés exacts) ; bouton « Continuer avec
   Microsoft » qui déclenche la connexion OIDC (Keycloak en local).
3. `core/http/` : intercepteurs de [technique 04 § 6](../technique/04-frontend.md#6-configuration-à-lexécution-et-authentification)
   — `X-Ifs-Organization` (lu dans un `ActiveOrganizationStore` vide jusqu'à J0-01), `Idempotency-Key`
   (`crypto.randomUUID()` par commande, conservé pour les réessais), conversion `problem+json` → `ApiError
   { status, code, message, fieldErrors, traceId }`.
4. Pages `not-found` (template) et `error` (`ApiError` 500 : « Une erreur s'est produite. Référence : <traceId> »).
4 bis. **Thème** ([DT-30](../technique/01-decisions.md#dt-30--thème-sombre-seul-changement-de-thème-prêt)) : `core/theme/theme.service.ts` (préférence `system|dark|light`, thème effectif, attributs
   `data-theme` et `color-scheme` sur `<html>`, suivi de `prefers-color-scheme`, mémorisation locale) appliqué au
   démarrage avant le premier rendu (`provideAppInitializer`) ; `shell/theme-switch/` (`app-ds-segmented` « Système /
   Sombre / Clair ») **rendu seulement si `AVAILABLE_THEMES.length > 1`** — donc absent aujourd'hui. Tests Vitest :
   avec un seul thème, le contrôle n'est pas dans le DOM ; avec deux thèmes simulés, il apparaît, et choisir « Clair »
   pose `data-theme="light"`.
4 ter. **Langue** ([DT-34](../technique/01-decisions.md#dt-34--langues--français-et-anglais-commutables)) : `shell/language-switch/` (`app-ds-segmented` « FR / EN ») dans le menu utilisateur en
   bas de la barre latérale (forme de `Sidebar.html`) ; changement immédiat via `LanguageService`, sans rechargement ;
   intercepteur `Accept-Language`. Test Vitest : basculer sur EN change un libellé de la coquille et `<html lang>`.
5. Playwright : `npm i -D @playwright/test @axe-core/playwright`, `playwright.config.ts` (projets `desktop`
   1440×900 et `mobile` 390×844, `baseURL` `http://localhost:4200`), `e2e/support/login.ts` (remplit le
   formulaire Keycloak), `e2e/shell.spec.ts` : connexion en alice → coquille visible ; à 390 px aucun défilement
   horizontal (`document.documentElement.scrollWidth <= innerWidth`) ; `axe` sans violation `serious` ou
   `critical` ; capture enregistrée dans `e2e/__captures__/`. Script npm `e2e` (suppose `aspire run` lancé).

✅ **Vérification automatique.** `npm run lint`, `npm test`, `npm run build`, `npm run e2e` (AppHost lancé).

🧪 **Test manuel.**
1. Se déconnecter puis ouvrir `http://localhost:4200` → page de connexion identique à
   `docs/design/maquette-v1/preview/Login.html` (textes, disposition).
2. Se connecter (chloe) → coquille : logo, barre latérale vide sauf « Accueil », fil d'Ariane.
3. Réduire à 390 px → la barre latérale disparaît, un bouton menu l'ouvre ; aucun défilement horizontal.
4. Arrêter la ressource `api` dans le tableau de bord, recharger → page d'erreur avec une référence.
5. Menu utilisateur → « EN » → les libellés de la coquille passent en anglais immédiatement ; recharger → l'anglais
   est conservé ; revenir à « FR ».
6. Aucun choix de thème n'est visible (un seul thème existe) ; outils de développement → `<html data-theme="dark">`.

🧠 **Mémoire.** `04-frontend.md` (coquille, registre de navigation, intercepteurs, e2e).

### S-14 — OpenAPI au build et client Angular généré

| | |
|---|---|
| **Technique** | [DT-05](../technique/01-decisions.md#dt-05--minimal-api-v1-openapi-intégré--scalar), [DT-20](../technique/01-decisions.md#dt-20--client-http-généré-depuis-openapi) |
| **Dépend de** | S-13 |
| **Commit** | `feat(api): produire OpenAPI au build et générer le client Angular` |

🎯 **Objectif.** Le contrat HTTP est un fichier versionné ; le client Angular en est dérivé ; un écart casse la CI.

🔧 **À faire.**
1. Api : paquet `Microsoft.Extensions.ApiDescription.Server`, propriétés `OpenApiGenerateDocumentsOnBuild=true`,
   `OpenApiDocumentsDirectory=$(MSBuildProjectDirectory)/openapi` ; le fichier
   `src/backend/InfraFlowSculptor.Api/openapi/v1.json` est commité.
2. Frontend : `ng-openapi-gen` (dernière version compatible Angular 22 ; sinon repli de
   [DT-20](../technique/01-decisions.md#dt-20--client-http-généré-depuis-openapi)), `ng-openapi-gen.json` (entrée
   ce fichier, sortie `src/app/core/api/generated`), scripts `api:generate` et `api:check` (génère dans un
   dossier temporaire et compare ; code 1 si différence).
3. Remplacer l'appel manuel à `/v1/me` (S-09) par le client généré.
4. `Api.Tests/OpenApi/OpenApiDocumentTests` : le document généré à l'exécution est identique au fichier
   commité (sinon : « régénérez avec dotnet build »).

✅ **Vérification automatique.** `dotnet build` puis `git diff --exit-code src/backend/InfraFlowSculptor.Api/openapi/v1.json` ;
`npm run api:check` ; tests verts.

🧪 **Test manuel.** Ouvrir `src/backend/InfraFlowSculptor.Api/openapi/v1.json` : opérations `GetVersion` et
`GetMe` avec leurs schémas. Accueil de l'application : « Bonjour … » toujours affiché.

🧠 **Mémoire.** `10-api-endpoints.md` (fichier OpenAPI, commande de régénération), `04-frontend.md` (client généré).

### S-15 — Intégration continue

| | |
|---|---|
| **Technique** | [06 § 6](../technique/06-tests-et-qualite.md#6-ci-githubworkflowsciyml) |
| **Dépend de** | S-14 |
| **Commit** | `ci: build, tests, chaîne d'approvisionnement et garde-fou du plan` |

🎯 **Objectif.** Chaque push et chaque pull request exécute toute la vérification ; une vulnérabilité
critique bloque ([EXG-05](../specs/27-exigences-non-fonctionnelles.md)).

🔧 **À faire.**
1. `.github/workflows/ci.yml` (déclencheurs `push` sur toutes les branches, `pull_request` vers `main`) avec
   les jobs `backend`, `acceptance`, `frontend`, `supply-chain` de [technique 06 § 6](../technique/06-tests-et-qualite.md)
   (`e2e`, `release-module` et `images` arrivent avec leurs étapes) ; .NET via `actions/setup-dotnet` et
   `global.json`, Node via `.nvmrc`, cache NuGet et npm ; publication des résultats de test (TRX, JUnit) en
   artefacts.
2. Le job `plan` reste dans `.github/workflows/plan-gate.yml` (déjà présent).
3. Badge CI dans `README.md`.

✅ **Vérification automatique.** Pousser la branche : tous les jobs verts sur GitHub.

🧪 **Test manuel.** Sur GitHub, onglet *Actions* : le dernier run de `impl/socle` est vert ; ouvrir le job
`supply-chain` → « no vulnerable packages ».

🧠 **Mémoire.** `09-auth-and-build.md` (CI).

📌 **Hors dépôt.** Protection de la branche `main` (CI obligatoire, pull request obligatoire) : à activer par
vous dans *Settings → Branches* ; consigner dans `NEXT.md`.

### S-16 — Observabilité transverse

| | |
|---|---|
| **Spécifications** | [EXG-11](../specs/27-exigences-non-fonctionnelles.md), [DEC-83](../specs/03-decisions.md) |
| **Dépend de** | S-15 |
| **Commit** | `feat(observabilite): traces, journaux structurés et métriques par organisation` |

🎯 **Objectif.** Toute requête et tout travail sont traçables de bout en bout, journalisés sans donnée
personnelle ni contenu du modèle, et comptés par organisation.

🔧 **À faire.**
1. `Application/Common/Observability/IfsTelemetry.cs` : `ActivitySource` et `Meter` `InfraFlowSculptor` ;
   compteurs `ifs.generations`, `ifs.publications`, `ifs.output_check_failures`, `ifs.jobs` avec l'étiquette
   `organization_id` (identifiant opaque) ; enregistrés dans `ServiceDefaults`.
2. Portée de journal par requête et par travail : `OrganizationId`, `ProjectId`, `TraceId` — jamais de nom, de
   valeur, d'adresse e-mail ni d'identifiant Azure ([DEC-83](../specs/03-decisions.md)).
3. Test `Application.Tests/Observability/LoggingBehaviorTests` : le contenu d'une commande n'apparaît jamais
   dans les journaux (enregistreur de test).

✅ **Vérification automatique.** `dotnet test` vert.

🧪 **Test manuel.** Tableau de bord Aspire → *Metrics* → `worker` : le compteur `ifs.jobs` augmente après un
`POST /v1/dev/ping-job` ; *Structured logs* : les entrées portent `OrganizationId`.

🧠 **Mémoire.** `08-runtime-and-orchestration.md` (télémétrie).

### S-17 — Mémoire, graphe de code et démarrage rapide

| | |
|---|---|
| **Technique** | [`.github/skills/memory-management/SKILL.md`](../../.github/skills/memory-management/SKILL.md) |
| **Dépend de** | S-16 |
| **Commit** | `docs(memoire): consigner le socle et le graphe de code` |

🎯 **Objectif.** Une session neuve (Luna, Claude, vous) comprend le socle en lisant `MEMORY.md` et ses fichiers.

🔧 **À faire.**
1. Relire et compléter chaque fichier de `.github/memory/` d'après le code réel (structure, commandes
   vérifiées, ports, ressources, conventions) ; ajouter une ligne à `changelog.md`.
2. Graphify : `python -m pip install graphifyy` (si le paquet n'existe pas sous ce nom, utiliser la commande
   d'installation de la skill graphify de l'utilisateur et l'écrire dans `07-code-graph.md`), puis la commande
   de construction sans LLM de `.github/memory/07-code-graph.md`. `graphify-out/` reste ignoré par git.
3. `README.md` : section « Démarrer » (prérequis, `check-prereqs`, `core.hooksPath`, `aspire run`,
   `gitea-init`, utilisateurs de démonstration, URL).

✅ **Vérification automatique.** `python tools/plan/gate.py lint` ; liens de `MEMORY.md` valides (tous les
fichiers cités existent).

🧪 **Test manuel.** Sur un clone neuf du dépôt (`git clone … ifs-test`), suivre uniquement la section
« Démarrer » du README jusqu'à voir « Bonjour Alice » ; noter dans `recettes/suivi.md` toute étape manquante.

🧠 **Mémoire.** Tous les fichiers.

### 🔒 R-01 — Revue du socle

**Périmètre** : `S-01` à `S-17`. **Branche** : `impl/socle` → pull request `[R-01] Revue du socle` vers `main`.

**Luna** : skill [`demander-revue`](../../.agents/skills/demander-revue/SKILL.md) ; captures de la galerie
(1440 et 390) et de la coquille dans `docs/plan/revues/captures/R-01/`.

**Recette utilisateur** : [`recettes/00-socle.md`](recettes/00-socle.md), résultats dans `recettes/suivi.md`.

**Claude vérifie en particulier** :
- les évolutions E1–E18 et les suppressions de [DT-02](../technique/01-decisions.md#dt-02--backend--template-cqrs-modernisé-selon-vole-papillon-damour) ;
- aucune dépendance interdite, aucun avertissement, aucun secret commité ;
- l'AppHost lance **tous** les émulateurs de [technique 05](../technique/05-execution-locale.md) ;
- l'isolation par organisation est structurelle (filtres, en-tête, politique par défaut authentifiée) ;
- la fidélité des composants `app-ds-*` à Strata (captures contre `docs/design/strata/rendered/`) ;
- `gate.py`, le crochet et la CI empêchent réellement de franchir un verrou.

**Après approbation** : vous fusionnez ; branche suivante `impl/preuves`.
