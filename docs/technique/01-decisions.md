# 01 — Décisions techniques

Chaque décision a un identifiant stable `DT-nn`, cité par le plan et le code (commentaire `// DT-nn` quand
un choix surprendrait un relecteur). Une décision ne se réécrit pas : on la remplace par une nouvelle qui
la cite. Les décisions marquées **⚖️ à confirmer** sont prises pour que le plan soit exécutable, mais
l'utilisateur peut les inverser avant l'étape qui les applique ; le plan indique cette étape.

## Index

| DT | Sujet | Statut |
|---|---|---|
| [DT-01](#dt-01--monodépôt) | Monodépôt | Décidée |
| [DT-02](#dt-02--backend--template-cqrs-modernisé-selon-vole-papillon-damour) | Backend : template CQRS modernisé selon Vole-Papillon-Damour | Décidée |
| [DT-03](#dt-03--versions-épinglées) | Versions épinglées | Décidée |
| [DT-04](#dt-04--postgresql-17) | PostgreSQL 17 | Décidée |
| [DT-05](#dt-05--minimal-api-v1-openapi-intégré--scalar) | Minimal API `/v1`, OpenAPI intégré + Scalar | Décidée |
| [DT-06](#dt-06--erreurs--erroror--problemjson) | Erreurs : `ErrorOr` → `problem+json` | Décidée |
| [DT-07](#dt-07--authentification--oidc-entra-en-azure-keycloak-en-local) | Authentification : OIDC, Entra en Azure, Keycloak en local | Décidée |
| [DT-08](#dt-08--worker--service-net-et-service-bus-pas-azure-functions) | Worker : service .NET + Service Bus, pas Azure Functions | Décidée |
| [DT-09](#dt-09--fichiers-des-révisions-dans-le-stockage-blob) | Fichiers des révisions dans le stockage blob | Décidée |
| [DT-10](#dt-10--redis-pour-le-cache-et-les-limites) | Redis pour le cache et les limites | Décidée |
| [DT-11](#dt-11--e-mail--acs-en-azure-mailpit-en-local) | E-mail : ACS en Azure, MailPit en local | Décidée |
| [DT-12](#dt-12--secrets-propres-à-ifs--key-vault) | Secrets propres à IFS : Key Vault | Décidée |
| [DT-13](#dt-13--fournisseurs-git-derrière-un-port-unique--gitea-comme-émulateur) | Fournisseurs git derrière un port unique ; Gitea comme émulateur | Décidée |
| [DT-14](#dt-14--moteur-pur-et-catalogue-json) | Moteur pur et catalogue JSON | Décidée |
| [DT-15](#dt-15--émetteurs-écrits-à-la-main-sans-moteur-de-gabarits) | Émetteurs écrits à la main, sans moteur de gabarits | Décidée |
| [DT-16](#dt-16--contrôles-de-sortie-dans-le-worker) | Contrôles de sortie dans le worker | Décidée |
| [DT-17](#dt-17--logique-de-release-en-module-powershell-livré-au-client) | Logique de release en module PowerShell livré au client | Décidée |
| [DT-18](#dt-18--frontend--angular-22-généré-par-ng-template) | Frontend : Angular 22 généré par `ng-template` | Décidée |
| [DT-19](#dt-19--design-system--strata-reproduit-en-composants-angular) | Design system : Strata reproduit en composants Angular | Décidée |
| [DT-20](#dt-20--client-http-généré-depuis-openapi) | Client HTTP généré depuis OpenAPI | Décidée |
| [DT-21](#dt-21--outillage-de-test) | Outillage de test | Décidée |
| [DT-22](#dt-22--mediator-à-la-place-de-mediatr) | `Mediator` à la place de MediatR | Décidée |
| [DT-23](#dt-23--identifiants-et-concurrence) | Identifiants et concurrence | Décidée |
| [DT-24](#dt-24--isolation-multi-organisations) | Isolation multi-organisations | Décidée |
| [DT-25](#dt-25--journal-daudit-en-ajout-seul-données-personnelles-à-part) | Journal d'audit en ajout seul, données personnelles à part | Décidée |
| [DT-26](#dt-26--temps-et-horloge) | Temps et horloge | Décidée |
| [DT-27](#dt-27--hébergement-dans-azure-france-central) | Hébergement dans Azure France Central | Décidée |
| [DT-28](#dt-28--langue-du-code-et-de-linterface) | Langue du code et de l'interface | Décidée |
| [DT-29](#dt-29--projet-pilote-de-référence) | Projet pilote de référence | Décidée |
| [DT-30](#dt-30--thème-sombre-seul-changement-de-thème-prêt) | Thème sombre seul, changement de thème prêt | Décidée |
| [DT-31](#dt-31--idempotence-des-commandes) | Idempotence des commandes | Décidée |
| [DT-32](#dt-32--files-et-équité) | Files et équité | Décidée |
| [DT-33](#dt-33--identité-managée-partout--chaîne-de-connexion-seulement-en-local) | Identité managée partout ; chaîne de connexion seulement en local | Décidée |
| [DT-34](#dt-34--langues--français-et-anglais-commutables) | Langues : français et anglais commutables | Décidée |
| [DT-35](#dt-35--registres-et-stratégies-pas-de-switch) | Registres et stratégies, pas de `switch` | Décidée |
| [DT-36](#dt-36--commandes-du-modèle-comme-données-et-espaces-de-travail) | Commandes du modèle comme données, espaces de travail | Décidée |
| [DT-37](#dt-37--événements-de-domaine-par-loutbox) | Événements de domaine par l'outbox | Décidée |
| [DT-38](#dt-38--disponibilité-des-fonctions-et-limites-de-plan) | Disponibilité des fonctions et limites de plan | Décidée |
| [DT-39](#dt-39--journalisation-et-diagnostics-sont-des-dépendances-de-création) | Journalisation et diagnostics : dépendances de création (écart de spec) | Décidée |
| [DT-40](#dt-40--ordre-des-comportements-du-médiateur) | Ordre des comportements du médiateur | Décidée |
| [DT-41](#dt-41--build-en-mode-code-au-jalon-2) | Build en mode Code au jalon 2 (écart de spec) | Décidée |
| [DT-42](#dt-42--coût-des-preuves-éphémères-azure) | Coût des preuves éphémères Azure (North Europe, profil minimal, suppression par projet) | ⚖️ Stratégie approuvée ; plafond chiffré à confirmer avant Azure |

---

### DT-01 — Monodépôt

Backend, frontend, catalogue, sorties de référence, application témoin, infrastructure d'IFS et
documentation vivent dans ce dépôt (arborescence : [00 § 4](00-vue-d-ensemble.md)). Un changement de
modèle touche presque toujours l'API, le client généré et un écran : un seul dépôt, une seule revue.

### DT-02 — Backend : template CQRS modernisé selon Vole-Papillon-Damour

**Base.** Le template `dotnet new templatewebcqrs` du dépôt
[FlorianDrevet/template-CQRS](https://github.com/FlorianDrevet/template-CQRS) : couches `Domain`,
`Application`, `Infrastructure`, `Api`, `Contracts`, CQRS par MediatR (remplacé par `Mediator`, [DT-22](#dt-22--mediator-à-la-place-de-mediatr)), validation FluentValidation,
erreurs `ErrorOr`, mapping Mapster, points de terminaison en classes statiques `XxxController`.

**Évolutions reprises du dépôt [Vole-Papillon-Damour](https://github.com/FlorianDrevet/Vole-Papillon-Damour)**
(`src/Backend/`), qui a fait vivre ce template en production :

| # | Évolution | Où dans VPD | Application à IFS |
|---|---|---|---|
| E1 | SDK `10.0.203`, `rollForward: latestFeature` | `global.json` | Identique |
| E2 | Gestion centrale des paquets, versions à jour | `Directory.Packages.props` | Identique, versions de [DT-03](#dt-03--versions-épinglées) |
| E3 | Orchestration locale Aspire 13.5 (`AppHost`, `aspire.config.json`) | `Vole_Papillon_Damour.AppHost` | Étendue à tous les émulateurs ([05](05-execution-locale.md)) |
| E4 | Authentification Entra par `Microsoft.Identity.Web`, rôles lus dans `roles`, mapping entrant désactivé | `Program.cs`, `09-auth-and-build.md` | Multi-tenant + comptes personnels ; plus de compte local ([DT-07](#dt-07--authentification--oidc-entra-en-azure-keycloak-en-local)) |
| E5 | Schéma de politique `Bearer` qui aiguille selon l'émetteur du jeton | `Program.cs` | Aiguille entre jeton OIDC et jeton d'API `ifs_` |
| E6 | Limitation de débit par politiques nommées, après l'authentification | `Common/RateLimiting/` | Politiques `read`, `write`, `generate`, `publish` ([EXG-21](../specs/27-exigences-non-fonctionnelles.md)) |
| E7 | En-têtes transférés (ingress Container Apps) | `Program.cs` | Identique |
| E8 | OpenTelemetry + exportateur Azure Monitor si la chaîne de connexion existe | `Common/Observability/` | Porté par `ServiceDefaults` |
| E9 | Contrôles de santé `/health` + santé de la base | `Infrastructure/Health/` | `/health` (prêt) et `/alive` (vivant) |
| E10 | Politique de migration : au démarrage en développement seulement | `Common/DatabaseMigrationPolicy.cs` | Identique ; en Azure, un job de migration ([08](08-hebergement.md)) |
| E11 | `IProjectDbContext` exposé à l'Application pour les requêtes ; dépôts seulement quand ils apportent quelque chose | `Application/Common/Interfaces/Persistence/` | Identique ; pas de `BaseRepository` qui enregistre à chaque opération |
| E12 | Unité de travail explicite, `RowVersion`/version sur les agrégats concurrents | `technique/01-decisions.md` (DT-06 VPD) | Version de concurrence sur chaque agrégat modifiable ([DT-23](#dt-23--identifiants-et-concurrence)) |
| E13 | `EnumValueObject<TEnum>` pour les statuts persistés | `Domain/Common/Models/` | Identique |
| E14 | Projets de tests séparés par couche (Domain, Application, Infrastructure, Api) | `*.tests` | Plus `Engine.Tests`, `Emitters.Tests`, `Architecture.Tests`, `Acceptance.Tests` |
| E15 | Outbox pour les effets externes | `Persistence/Outbox/` | Remplacée par l'envoi Service Bus transactionnel via outbox ([DT-32](#dt-32--files-et-équité)) |
| E16 | Endpoint minimal API : `IMediator` + `result.Match(ok, error => error.Result())`, `.WithName()`, `.RequireAuthorization()` | `Api/Controllers/*.cs` | Identique, sous un groupe `/v1` |
| E17 | Pas de constante magique : politiques, revendications, clés de configuration, noms de routes en constantes | `copilot-instructions.md` | Identique |
| E18 | Un type public par fichier | idem | Identique |

**Ce qui est retiré du template** : l'enregistrement et la connexion locale (`/auth/register`,
`/auth/login`), `IHashPassword`, `IJwtGenerator`, `JwtSettings` (IFS n'a ni compte local ni mot de passe,
[RG-ORG-01](../specs/10-organisations-et-acces.md)) ; `BaseRepository` ; `IDateTimeProvider` (remplacé par
`TimeProvider`, [DT-26](#dt-26--temps-et-horloge)) ; le paquet `FluentValidation.AspNetCore` (déprécié) ;
`MediatR.Extensions.Microsoft.DependencyInjection` (intégré à MediatR depuis la v12).

**Ce qui diffère volontairement de VPD** : OpenAPI intégré + Scalar au lieu de Swashbuckle
([DT-05](#dt-05--minimal-api-v1-openapi-intégré--scalar)) ; worker .NET au lieu d'Azure Functions
([DT-08](#dt-08--worker--service-net-et-service-bus-pas-azure-functions)) ; PostgreSQL au lieu de SQL
Server ([DT-04](#dt-04--postgresql-17)) ; assertions AwesomeAssertions au lieu de FluentAssertions
([DT-21](#dt-21--outillage-de-test)).

### DT-03 — Versions épinglées

Les versions vivent dans `src/backend/Directory.Packages.props`, `src/frontend/ifs-web/package.json` et
`catalog/<version>/pins.json`. Valeurs de départ (2026-10-03) :

| Composant | Version |
|---|---|
| SDK .NET | `10.0.203` (`global.json`, `rollForward: latestFeature`) |
| Aspire (`Aspire.Hosting.*`, `Aspire.*` clients, `Aspire.Hosting.Testing`) | `13.5.3` (intégrations en préversion : dernière `13.5.x-preview`) |
| EF Core, ASP.NET Core, `Microsoft.Extensions.*` | `10.0.7` |
| Npgsql EF Core | dernière `10.0.x` |
| `EFCore.NamingConventions` | dernière `10.0.x` |
| `Mediator.Abstractions`, `Mediator.SourceGenerator` | dernière `3.x` stable |
| ErrorOr | `2.0.1` |
| FluentValidation (+ `DependencyInjectionExtensions`) | `12.1.1` |
| Mapster (+ `DependencyInjection`) | `10.0.7` |
| `Microsoft.Identity.Web` | `4.14.2` |
| `Scalar.AspNetCore` | dernière `2.x` |
| `Azure.Identity` | `1.21.0` |
| `Microsoft.Data.SqlClient` (application témoin seulement, [DT-33](#dt-33--identité-managée-partout--chaîne-de-connexion-seulement-en-local)) | dernière `6.x` stable |
| `Azure.Storage.Blobs` | `12.27.0` |
| `Azure.Messaging.ServiceBus` | dernière `7.x` |
| `Azure.Security.KeyVault.Secrets` | dernière `4.x` |
| `Azure.Communication.Email` | `1.1.0` |
| `Azure.Monitor.OpenTelemetry.AspNetCore` | `1.5.0` |
| `JsonSchema.Net` | dernière `7.x` |
| `YamlDotNet` | dernière `16.x` |
| xUnit / runner / `Microsoft.NET.Test.Sdk` | `2.9.3` / `3.1.5` / `18.5.1` |
| AwesomeAssertions | dernière `9.x` |
| NSubstitute | `5.3.0` |
| Verify.Xunit | dernière `31.x` |
| Testcontainers.PostgreSql | dernière `4.x` |
| NetArchTest.Rules | `1.3.2` |
| Node | `24.15.0` (`.nvmrc`) |
| Angular / CLI / `@floriandrevet/ng-template` | `22.x` / `22.1.x` / `22.0.0` |
| Bicep CLI | dernière `0.x` stable au moment de S-01, épinglée dans `tools/versions.json` |
| PowerShell | `7.5.x` ou plus récent |
| Azure CLI | dernière stable |

**Règle sans arbitrage.** Si une version indiquée n'existe pas sur le registre, prendre la plus récente
version stable de la **même version majeure**, l'écrire dans le fichier de versions concerné et dans
`.github/memory/09-auth-and-build.md`, et le signaler dans la demande de revue suivante. Si aucune version
de la même majeure n'existe, s'arrêter (question pour Claude).

### DT-04 — PostgreSQL 17

Confirmée (2026-10-04). Base relationnelle unique d'IFS : PostgreSQL 17 (Azure Database for
PostgreSQL serveur flexible en Azure, conteneur `postgres:17` par Aspire en local), EF Core + Npgsql,
conventions de nommage `snake_case` (`EFCore.NamingConventions`).

**Pourquoi pas SQL Server, comme VPD.** IFS stocke beaucoup de documents structurés immuables (instantanés
du modèle pour l'historique, plans de déploiement, descripteurs, jeux de modifications) ; `jsonb` indexable
les sert mieux. Le serveur flexible coûte moins cher qu'Azure SQL à charge équivalente pour un SaaS
naissant, et les tests d'intégration démarrent en quelques secondes avec Testcontainers.

**Si l'utilisateur préfère SQL Server** : remplacer le fournisseur EF, `jsonb` par `nvarchar(max)` +
colonnes calculées, et Testcontainers.MsSql. Aucune autre étape du plan ne change.

### DT-05 — Minimal API `/v1`, OpenAPI intégré + Scalar

- Points de terminaison en classes statiques `XxxController` (évolution E16), regroupées sous
  `app.MapGroup("/v1")` ; chaque classe expose `MapXxxEndpoints(this RouteGroupBuilder v1)`.
- Document OpenAPI 3.1 par `Microsoft.AspNetCore.OpenApi` (le dernier commit du template, avec son
  `BearerSecuritySchemeTransformer`), exposé sur `/openapi/v1.json`, et produit **au build** dans
  `src/backend/InfraFlowSculptor.Api/openapi/v1.json` (`Microsoft.Extensions.ApiDescription.Server`) pour
  générer le client Angular ([DT-20](#dt-20--client-http-généré-depuis-openapi)) et, au jalon 3, les outils
  MCP ([RG-MCP-05](../specs/25-agent-ia-mcp.md)).
- Interface de test : Scalar sur `/scalar` en développement, configurée pour le flux code + PKCE contre le
  fournisseur OIDC local.
- Chaque point de terminaison porte `.WithName("<Verbe><Objet>")` (nom stable de commande, réutilisé par le
  MCP), `.WithSummary(...)`, `.Produces<T>()` et `.ProducesProblem(...)`.

### DT-06 — Erreurs : `ErrorOr` → `problem+json`

[EXG-15](../specs/27-exigences-non-fonctionnelles.md) exige le code de règle et les erreurs par champ.

| Cas | Statut | Corps |
|---|---|---|
| Validation à la saisie (FluentValidation ou règle de domaine) | 400 | `ValidationProblemDetails` : `errors` par champ, chaque entrée porte `code` (`RG-NOM-01`…) et `message` |
| Objet d'une autre organisation, projet invisible | 404 | `code: "NOT_FOUND"` — jamais 403 ([RG-ORG-13](../specs/10-organisations-et-acces.md)) |
| Permission manquante sur un projet visible | 403 | `code: "FORBIDDEN"`, `permission: "publier"` |
| Version périmée | 409 | `code: "CONFLICT_VERSION"`, `currentVersion`, `current` (état à jour) ([RG-UI-10](../specs/26-interface.md)) |
| Conflit métier (unicité, dernier propriétaire…) | 409 | `code: "<règle>"` |
| Limite de débit | 429 | en-tête `Retry-After` ([EXG-21](../specs/27-exigences-non-fonctionnelles.md)) |
| Limite de plan | 402 | `code: "PLAN_LIMIT"`, `limit`, `plan` ([10 § 3](../specs/10-organisations-et-acces.md)) |
| Exception non gérée | 500 | `code: "INTERNAL"`, `traceId` ; aucun détail |

Le `Error.Code` d'`ErrorOr` **est** le code de règle (`"RG-PRJ-01"`, `"VAL-NOM-LONGUEUR"`) ; les
métadonnées (`Error.Metadata`) portent le champ et les valeurs utiles. La correspondance est centralisée
dans `Api/Errors/ProblemDetailsMapper.cs` (remplace `ErrorOrStatusCode.cs` du template, qui testait des
codes en dur).

### DT-07 — Authentification : OIDC, Entra en Azure, Keycloak en local

Confirmée (2026-10-04). Guide d'utilisation de Keycloak : [09](09-keycloak.md).

- **En Azure** : Entra ID multi-tenant ; les comptes Microsoft personnels
  ([RG-ORG-01](../specs/10-organisations-et-acces.md), [DEC-59](../specs/03-decisions.md)) arrivent au jalon 2
  ([04 § 2.2](../specs/04-perimetre-et-lots.md), étape J2-10). Une inscription d'application « IFS API »
  (`api://<id>/access_as_user`) et une « IFS Web » (SPA), en `signInAudience = AzureADMultipleOrgs` jusqu'à J2-10,
  puis `AzureADandPersonalMicrosoftAccount`. Côté API, `Microsoft.Identity.Web` avec `TenantId = common` et
  validation d'émetteur multi-tenant ; côté SPA, `angular-auth-oidc-client` sur
  `https://login.microsoftonline.com/common/v2.0`.
- **En local** : **Keycloak** joue l'émulateur d'Entra (royaume `ifs`, importé au démarrage par Aspire),
  avec des utilisateurs de démonstration qui portent les mêmes revendications qu'Entra (`oid`, `tid`,
  `email`, `name`, `email_verified`). Il permet de tester plusieurs organisations, tenants et comptes
  personnels sans Entra ([05 § 3](05-execution-locale.md)).
- **Pourquoi `angular-auth-oidc-client` plutôt que MSAL** (que VPD utilise) : un seul code client pour Entra
  et Keycloak, configuré à l'exécution par `config.json`. MSAL ne parle pas à un fournisseur OIDC générique.
- **Jetons d'API** `ifs_…` ([10 § 7](../specs/10-organisations-et-acces.md)) : gestionnaire
  d'authentification dédié (`ApiTokenAuthenticationHandler`) qui retrouve l'empreinte SHA-256 du jeton.
  Le schéma de politique `Bearer` (évolution E5) aiguille : préfixe `ifs_` → jeton d'API, sinon → OIDC.
- **Identité stable** d'un utilisateur : `(tid, oid)` ([RG-ORG-02](../specs/10-organisations-et-acces.md)).
- **Adresse vérifiée** ([UC-ORG-04](../specs/10-organisations-et-acces.md)) : `IVerifiedEmailResolver`
  retient, dans l'ordre, `verified_primary_email` (revendication facultative Entra), `email` si
  `xms_edov = true`, `email` si `tid` est le tenant des comptes personnels
  (`9188040d-6c67-4c5b-b112-36a304b66dad`), `email` si `email_verified = true` (Keycloak). Sinon : aucune
  adresse vérifiée, l'acceptation d'invitation est refusée.
- **Back-office** ([40 § 1](../specs/40-exploitation-ifs.md)) : politique `Internal` qui exige le tenant
  d'IFS (configuration `Ifs:InternalTenantId`), un rôle d'application interne (`Ifs.Support`,
  `Ifs.CatalogEditor`, `Ifs.PlatformAdmin`) et `amr` contenant `mfa`.

**Si l'utilisateur préfère MSAL** : n'utiliser que des inscriptions Entra de développement en local ;
Keycloak disparaît de l'AppHost et les tests manuels multi-organisations exigent plusieurs comptes Entra.

### DT-08 — Worker : service .NET et Service Bus, pas Azure Functions

Confirmée (2026-10-04).

**À quoi sert le worker.** L'API répond vite (< 300 ms, [EXG-07](../specs/27-exigences-non-fonctionnelles.md)) ; tout
ce qui est long, externe ou planifié part au worker :

| Travail | Pourquoi pas dans l'API |
|---|---|
| Générer une révision (moteur, émetteurs, puis `bicep build/lint/format`, analyse PowerShell) | Jusqu'à 30 s pour 300 ressources ; besoin des binaires `bicep` et `pwsh` |
| Préparer et écrire une publication (lecture des dépôts, diff, commit, pull request) | Appels Azure DevOps / GitHub lents et faillibles, à réessayer |
| Lire les pipelines Azure DevOps toutes les 2 à 15 minutes ([RG-SUI-01](../specs/28-suivi-des-deploiements.md)) | Tâche planifiée |
| E-mails, expirations de jetons, purges (audit 13 mois, révisions 90 jours, projets supprimés 30 jours) | Tâches planifiées ou différées |

**Pourquoi Service Bus « à sessions ».** [EXG-23](../specs/27-exigences-non-fonctionnelles.md) exige que les
générations et publications passent par des **files par organisation servies équitablement** : une organisation qui
lance 50 générations ne doit pas retarder celle qui en lance une. Une file Service Bus à sessions le fait sans code
maison : chaque message porte l'identifiant de son organisation comme identifiant de session ; les messages d'une même
session sont traités dans l'ordre et un à la fois ; le processeur passe d'une session à l'autre, donc d'une organisation
à l'autre ([DT-32](#dt-32--files-et-équité)). L'API écrit le travail dans sa propre transaction (outbox) : il n'est
jamais perdu, ni envoyé pour une commande annulée.

**Pourquoi pas Azure Functions** (que VPD utilise) :
1. **Équité** : les déclencheurs Service Bus de Functions lisent des sessions, mais le contrôle fin (sessions
   simultanées, budgets par organisation, report d'un message quand une organisation dépasse son budget) est plus simple
   et plus testable dans un hôte .NET qui tient lui-même son `ServiceBusSessionProcessor`.
2. **Outils dans l'image** : le contrôle de sortie exige la CLI Bicep, PowerShell et PSScriptAnalyzer. Une Container App
   avec notre propre image les embarque naturellement ; Functions impose son image de base.
3. **Expérience VPD** : le NEXT.md de VPD (2026-09-24) décrit des incidents de résolution de `Azure.Functions.Sdk` dans
   Docker et dans Aspire, et l'exécution locale exige Azure Functions Core Tools. Un hôte .NET démarre dans Aspire comme
   l'API, sans outil de plus, se débogue pareil et se teste avec `Aspire.Hosting.Testing`.
4. **Exploitation** : même environnement Container Apps que l'API, même identité managée, même télémétrie, mise à
   l'échelle sur la longueur des files (règle KEDA Service Bus des Container Apps).

**Ce qu'on perd** : la facturation à l'exécution de Functions. Le suivi planifié impose de toute façon un worker
toujours actif : le gain serait nul.

### DT-09 — Fichiers des révisions dans le stockage blob

Les fichiers d'une révision sont stockés dans le conteneur `revisions`, **adressés par empreinte SHA-256**
(`sha256/<2 premiers caractères>/<empreinte>`) : deux révisions qui partagent un fichier ne le stockent
qu'une fois. La base garde l'arborescence (chemin → empreinte, taille). L'archive téléchargeable
(`archives/<revisionId>.zip`) est produite à la demande puis mise en cache. Les fichiers des révisions non
publiées sont purgés après 90 jours ([RG-GEN-05](../specs/21-generation-et-revisions.md)). Les exports de
projet vont dans `exports/`, les rapports de release lus dans `reports/`.

### DT-10 — Redis pour le cache et les limites

`HybridCache` (mémoire + Redis) pour les résultats coûteux et déterministes : constats par version du
modèle, plan de déploiement par (projet, version du modèle, catalogue, cible). Redis porte aussi les
compteurs de limitation de débit partagés entre réplicas à partir du jalon 1. Azure Managed Redis en
Azure, conteneur Redis en local.

### DT-11 — E-mail : ACS en Azure, MailPit en local

Port `IEmailSender` ; adaptateur Azure Communication Services en Azure, SMTP (MailKit) vers **MailPit** en
local, dont l'interface web montre chaque e-mail envoyé. Gabarits HTML + texte, FR et EN, sous
`InfraFlowSculptor.Infrastructure/Email/Templates/`. Aucune donnée sensible dans un e-mail
([RG-UI-14](../specs/26-interface.md)).

### DT-12 — Secrets propres à IFS : Key Vault

Les seuls secrets conservés par IFS sont les jetons git de repli ([EXG-02](../specs/27-exigences-non-fonctionnelles.md)).
Port `ISecretStore` ; adaptateur Key Vault en Azure. En local, l'émulateur Key Vault communautaire
(`AzureKeyVaultEmulator`) si son intégration Aspire existe pour Aspire 13 ; sinon l'adaptateur
`DevelopmentSecretStore` (fichier chiffré par Data Protection sous le dossier de données de l'AppHost),
enregistré **uniquement** quand l'environnement est `Development`. Aucun secret n'est jamais écrit en base
ni dans un journal.

### DT-13 — Fournisseurs git derrière un port unique ; Gitea comme émulateur

Port `IGitProvider` (lister les dépôts, lire une branche et un fichier, créer une branche, écrire un commit
multi-fichiers, ouvrir ou mettre à jour une pull request, lire l'état d'une pull request). Adaptateurs :

| Adaptateur | Lot | Authentification |
|---|---|---|
| `AzureReposProvider` | 1 (jalon 0) | Principal de service d'IFS (inscription Entra « IFS Git »), jeton Entra pour la ressource Azure DevOps ; repli : jeton personnel avec expiration |
| `GitHubProvider` | 1 (jalon 1) | Application GitHub d'IFS, jetons d'installation ; repli : jeton fine-grained |
| `GiteaProvider` | Développement et tests seulement | Utilisateur local de l'émulateur Gitea lancé par Aspire |

`GiteaProvider` n'est enregistré que si `Ifs:Development:EnableGitea = true` **et** que l'environnement est
`Development` ; il n'apparaît jamais dans l'interface en Azure ([P9](../specs/01-principes.md)). Il permet de
publier et de voir des pull requests sans compte Azure DevOps.

Azure DevOps Pipelines n'a pas d'émulateur : le suivi est testé contre des réponses enregistrées
(WireMock.Net dans les tests) et, en recette, contre une vraie organisation Azure DevOps.

### DT-14 — Moteur pur et catalogue JSON

- `InfraFlowSculptor.Engine` est une bibliothèque **pure** : entrée = instantané immuable du modèle
  (`ModelSnapshot`) + catalogue ; sortie = résultats (noms, valeurs, implicites, constats, plan). Aucune
  dépendance à EF, ASP.NET, au réseau ni à l'horloge (l'horodatage est une entrée).
- Le catalogue est un ensemble de fichiers JSON sous `catalog/<version>/` (`types/<Type>.json`,
  `steps/<Step>.json`, `regions.json`, `roles.json`, `pins.json`), validés par un schéma JSON
  (`catalog/schema/`), embarqués dans `InfraFlowSculptor.Catalog` comme ressources. Une version publiée
  ne change jamais ([DEC-107](../specs/03-decisions.md)) : un test vérifie l'empreinte de chaque version
  publiée.
- Le format détaillé est dans [03 § 2](03-moteur-et-generation.md).

### DT-15 — Émetteurs écrits à la main, sans moteur de gabarits

Chaque émetteur construit ses fichiers avec un écrivain dédié (`BicepWriter`, `YamlWriter`,
`PowerShellWriter`, `MarkdownWriter`) qui maîtrise l'indentation, l'ordre, les commentaires et les fins de
ligne `LF`. Pas de Scriban/Razor : le déterminisme octet pour octet
([RG-GEN-03](../specs/21-generation-et-revisions.md)) et le formatage officiel
([RG-GEN-13](../specs/21-generation-et-revisions.md)) se testent mieux sur du code que sur des gabarits.
Les fichiers **statiques** livrés au client (module PowerShell de release, modèles YAML partagés) sont des
ressources embarquées copiées telles quelles, avec leur en-tête.

### DT-16 — Contrôles de sortie dans le worker

L'image du worker contient la CLI Bicep (version épinglée), PowerShell 7 et PSScriptAnalyzer. Contrôles
([RG-GEN-04](../specs/21-generation-et-revisions.md), [RG-GEN-14](../specs/21-generation-et-revisions.md)) :
`bicep build`, `bicep build-params` par cible, `bicep lint` (zéro avertissement), `bicep format` (aucune
différence) ; YAML Azure DevOps validé contre le schéma officiel épinglé
(`catalog/<version>/schemas/azure-pipelines.json`) ; scripts PowerShell analysés par le parseur PowerShell et
PSScriptAnalyzer (zéro erreur). Les modules AVM du registre public sont restaurés dans un cache local du
worker. Un point d'extension est remplacé par un bouchon vide pendant le contrôle (RG-GEN-04).

### DT-17 — Logique de release en module PowerShell livré au client

Les étapes de la release d'infrastructure ([22 § 3.1](../specs/22-pipelines.md)) — journal d'opérations,
aperçu et empreinte des effets, relecture sous verrou, révocations, écritures de plan de données, rapport —
sont écrites **une fois** dans un module PowerShell lisible, `IfsRelease.psm1`, publié dans
`.ifs/templates/scripts/` de chaque destination. Les pipelines générés l'appellent avec un fichier de
données par composant et par cible (`<composant>/infra/release.<cible>.json`). Le module est couvert par
des tests Pester dans ce dépôt (`reference/release-module/tests/`). Il est d'abord écrit et prouvé à la main
(phase P), puis embarqué tel quel par l'émetteur.

### DT-18 — Frontend : Angular 22 généré par `ng-template`

Application `src/frontend/ifs-web` générée par le schéma `@floriandrevet/ng-template`
([FlorianDrevet/angular_template](https://github.com/FlorianDrevet/angular_template)) avec :

```
--ssr=false --ui=tailwind --auth=oidc --i18n=true --docker=true --ci=false
```

- `ssr=false` : tout est derrière une connexion ; rien à indexer.
- `ui=tailwind` : le design system Strata dit de générer la configuration Tailwind depuis ses tokens.
- `auth=oidc` : [DT-07](#dt-07--authentification--oidc-entra-en-azure-keycloak-en-local).
- `i18n=true` : français et anglais complets ([EXG-13](../specs/27-exigences-non-fonctionnelles.md)), Transloco.
- `docker=true` : image nginx avec `config.json` régénéré au démarrage (« build once, deploy anywhere »).
- `ci=false` : la CI est celle du monodépôt (`.github/workflows/ci.yml`).

Conventions héritées du template : composants autonomes, sans zone (`provideZonelessChangeDetection`),
`OnPush`, signaux, `HttpClient` avec intercepteurs, Vitest, ESLint typé, Prettier.

### DT-19 — Design system : Strata reproduit en composants Angular

Strata ([`docs/design/strata/`](../design/strata/index.html)) n'est pas une librairie à importer : c'est la
spécification que le code suit (son README, « Passer au code »).

1. `tokens.json` → `src/styles/tokens.css` (variables `--ifs-<nom>`) et bloc `@theme` Tailwind, **générés**
   par `npm run tokens` (script `scripts/build-tokens.mjs`) ; jamais écrits à la main.
2. Composants Angular `app-ds-*` qui reproduisent la géométrie de `components/bundle.css` (classes `st-*`) :
   correspondance dans [04 § 4](04-frontend.md).
3. Icônes d'interface : tracés `ICON_PATHS` du bundle, recopiés dans `ds/icon/icon-paths.ts`.
   Icônes Azure : SVG officiels extraits des data URI du bundle vers `public/azure-icons/`, indexés par
   type ([note de licence](../design/strata/assets/Azure/README.md) : à valider avant la commercialisation).
4. Polices Instrument Sans et JetBrains Mono auto-hébergées (`@fontsource`), pas de Google Fonts en
   production.

### DT-20 — Client HTTP généré depuis OpenAPI

`ng-openapi-gen` génère, depuis `src/backend/InfraFlowSculptor.Api/openapi/v1.json`, les modèles et
fonctions d'appel dans `src/frontend/ifs-web/src/app/core/api/generated/` (jamais modifié à la main). La CI
échoue si le client généré n'est pas à jour. Les services de fonctionnalité enveloppent le client généré
avec `httpResource`/signaux. Si `ng-openapi-gen` ne supporte pas Angular 22 à l'étape S-14, utiliser
`openapi-typescript` (types) + fonctions écrites sur `HttpClient`, sans autre changement de structure.

### DT-21 — Outillage de test

Confirmée (2026-10-04).

| Besoin | Outil |
|---|---|
| Tests .NET | xUnit `2.9.3` (comme VPD), NSubstitute |
| Assertions | **AwesomeAssertions** (fork Apache-2.0 de FluentAssertions 7, même API). FluentAssertions 8, utilisé par VPD, exige une licence commerciale payante pour un produit vendu |
| Instantanés (sorties générées) | Verify.Xunit, fichiers `*.verified.*` |
| Base réelle | Testcontainers.PostgreSql |
| API de bout en bout côté serveur | `Aspire.Hosting.Testing` + `WebApplicationFactory` |
| Architecture | NetArchTest.Rules |
| Faux services HTTP (Azure DevOps, GitHub) | WireMock.Net |
| Module PowerShell de release | Pester 5 |
| Frontend unitaire | Vitest (défaut du template) + Angular Testing Library |
| Bout en bout navigateur | Playwright (`@playwright/test`), contre l'AppHost Aspire |
| Accessibilité | `@axe-core/playwright` |

### DT-22 — `Mediator` à la place de MediatR

Confirmée (2026-10-04) : **`Mediator`** (martinothamar/Mediator, MIT). MediatR (template et VPD) est sous licence
commerciale depuis la v13 : gratuit (« Community ») sous 5 M$ de chiffre d'affaires avec une clé à demander, payant
au-delà. Options étudiées :

| Option | Licence | Changement par rapport au template | Avis |
|---|---|---|---|
| **`Mediator`** (martinothamar/Mediator, génération de source) | MIT | Mêmes notions (`IRequest<T>`, `IRequestHandler<,>`, `IPipelineBehavior<,>`, `IMediator.Send`) ; gestionnaires en `ValueTask` ; enregistrement par `services.AddMediator(...)` ; aucune réflexion à l'exécution, plus rapide, erreurs de câblage détectées **à la compilation** | **Recommandée** |
| MediatR 14 avec clé de licence | Commerciale (Community gratuite sous 5 M$) | Aucun | Possible ; dépendance à une licence renouvelable |
| Wolverine | MIT | Autre modèle (messages, conventions, bus intégré) ; réécrit les tranches | Trop éloigné du template |
| Médiateur écrit à la main | — | ~150 lignes à maintenir | Inutile quand `Mediator` existe |

**Mise en œuvre** (dernière `3.x`) : `Mediator.Abstractions` dans Application, `Mediator.SourceGenerator`
dans les projets hôtes (Api, Worker). Adaptations de la tranche du template : `Task<ErrorOr<T>>` →
`ValueTask<ErrorOr<T>>` ; `IPipelineBehavior<TMessage, TResponse>.Handle(TMessage message,
MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)` avec `next(message, ct)`. Le reste (validation,
autorisation, unité de travail, `ErrorOr`) est identique. Application à l'étape S-03.

### DT-23 — Identifiants et concurrence

- Identifiants : `Guid` version 7 (`Guid.CreateVersion7(timeProvider.GetUtcNow())`), fortement typés
  (`readonly record struct ProjectId(Guid Value)`), convertis par une convention EF unique. Jamais exposé
  de clé séquentielle.
- Concurrence optimiste ([DEC-37](../specs/03-decisions.md)) : colonne `version` (entier) sur chaque agrégat
  modifiable, incrémentée à chaque écriture, jeton de concurrence EF. L'API la renvoie (`version`) et
  l'exige dans chaque commande de modification (`expectedVersion`). Écart → 409
  ([DT-06](#dt-06--erreurs--erroror--problemjson)).
- Version du modèle d'un projet ([RG-DON-05](../specs/05-modele-de-donnees.md)) : colonne `model_version`
  de `projects`, incrémentée dans la même transaction que toute modification d'un objet du modèle.

### DT-24 — Isolation multi-organisations

- Toute table d'un objet client porte `organization_id` ; un filtre de requête global EF le restreint à
  l'organisation active (`ICurrentOrganization`). Les requêtes sans filtre (`IgnoreQueryFilters`) sont
  interdites hors du dossier `Infrastructure/Persistence/Admin/` (vérifié par un test d'architecture).
- L'organisation active vient de l'en-tête `X-Ifs-Organization` (identifiant), vérifié contre les
  adhésions de l'utilisateur à chaque requête ; un jeton d'API porte sa propre organisation.
- L'autorisation projet/composant est faite par `IProjectAuthorizer` dans un comportement du médiateur, à partir
  d'un attribut `[RequiresPermission(Permission.ModelEdit, Scope = ...)]` sur chaque commande ou requête.
- [EXG-01](../specs/27-exigences-non-fonctionnelles.md) : la suite `Api.Tests/Isolation/` énumère tous les
  points de terminaison (`EndpointDataSource`) et échoue si l'un d'eux n'a pas de scénario d'isolation
  déclaré ([06 § 4](06-tests-et-qualite.md)).

### DT-25 — Journal d'audit en ajout seul, données personnelles à part

Table `audit_events` alimentée dans la transaction de la commande. Un déclencheur PostgreSQL refuse `UPDATE` et
`DELETE` (seule la purge planifiée à 13 mois, par une fonction `SECURITY DEFINER` dédiée, peut supprimer). L'acteur
est un identifiant opaque (`actor_ref`) ; son nom est résolu **à la lecture**. Les valeurs personnelles d'une différence
(nom, adresse) vont dans `audit_personal_data (event_id, subject_user_id, field, value)`, la différence `jsonb` n'en
garde qu'une marque. Pseudonymiser un compte ([EXG-06](../specs/27-exigences-non-fonctionnelles.md)) = supprimer ses
lignes de `audit_personal_data` et son profil, sans jamais modifier `audit_events` (corrige la contradiction relevée
par la revue du plan du 2026-10-04, PLAN-07).

### DT-26 — Temps et horloge

`TimeProvider` (.NET) injecté partout, `FakeTimeProvider` dans les tests ; stockage en UTC (`timestamptz`) ;
affichage relatif/absolu côté interface selon Strata. Remplace `IDateTimeProvider` du template.

### DT-27 — Hébergement dans Azure France Central

IFS est hébergé en `francecentral` ([DEC-04](../specs/03-decisions.md), [EXG-06](../specs/27-exigences-non-fonctionnelles.md)) :
Container Apps (api, worker, web, mcp), PostgreSQL serveur flexible, Service Bus Standard, stockage,
Key Vault, Azure Managed Redis, Application Insights + Log Analytics, ACS Email. Infrastructure écrite en
Bicep avec AVM dans `infra/`, déployée par GitHub Actions avec fédération OIDC. Détails : [08](08-hebergement.md).

### DT-28 — Langue du code et de l'interface

- Code, API, schéma de base, noms de fichiers : **anglais**, en utilisant **exactement** les noms techniques
  du glossaire ([02](../specs/02-glossaire.md)) : `Component`, `DeploymentTarget`, `EnvironmentOverride`,
  `Link`, `LinkKind`, `Finding`, `Revision`, `PublishPlan`, `AccessObject`…
- Interface : français par défaut, anglais complet ; libellés tirés **mot pour mot** de la maquette.
- Documentation, messages de commit, demandes et revues : français.
- Codes de règles (`RG-…`, `VAL-…`) : inchangés partout.

### DT-29 — Projet pilote de référence

Le jalon 0 a besoin d'un projet d'acceptation dans le catalogue du pilote ([04 § 2.0](../specs/04-perimetre-et-lots.md)).
Il est défini dans [`../plan/reference-pilote.md`](../plan/reference-pilote.md) : un sous-ensemble du projet de
référence ([90](../specs/90-projet-de-reference.md)), avec la base dans son propre composant pour exercer
[P9](../specs/04-perimetre-et-lots.md), et une liaison d'accès vers Log Analytics pour démontrer la
révocation ([P2](../specs/04-perimetre-et-lots.md)) sans type hors du catalogue du pilote.

### DT-30 — Thème sombre seul, changement de thème prêt

Confirmée (2026-10-04). Strata n'a qu'un thème sombre ; [RG-UI-07](../specs/26-interface.md) demande clair, sombre et
système. L'interface est sombre, **mais tout ce qu'il faut pour ajouter le thème clair existe dès le socle** :

1. **Tokens par thème** : `scripts/build-tokens.mjs` lit `color.themes` de `tokens.json` et produit un bloc par thème —
   `:root, :root[data-theme="dark"] { … }` pour le thème par défaut, `:root[data-theme="light"] { … }` dès que
   `tokens.json` porte les valeurs claires (même nom de token, une valeur par thème :
   `"values": { "dark": "#0b0e13", "light": "#ffffff" }`). Les composants n'utilisent que `--ifs-*` : aucun écran ne
   change quand le thème clair arrive.
2. **`ThemeService`** (`core/theme/theme.service.ts`) : signal `preference` (`system` | `dark` | `light`), signal
   `effective` calculé (préférence, sinon `prefers-color-scheme`), écrit `data-theme` et `color-scheme` sur `<html>`,
   suit les changements du système, persiste localement puis côté serveur (préférence du profil, J0-32), expose
   `availableThemes` lu dans `themes.generated.ts` (produit par `build-tokens.mjs`).
3. **`app-theme-switch`** (`shell/theme-switch/`) : `app-ds-segmented` « Système / Sombre / Clair » dans le menu
   utilisateur de la barre latérale et dans le Profil. **Affiché seulement si `availableThemes.length > 1`**
   ([P9](../specs/01-principes.md)) : caché aujourd'hui, il apparaît sans code nouveau quand Strata publie le clair.
   Testé dès S-13 avec un jeu de tokens de test à deux thèmes.
4. **Ajouter le thème clair** = réexporter Strata (skill `exporter-design`), `npm run tokens`, vérifier les captures en
   clair. Rien d'autre (étape J3-08, ou plus tôt si Strata est prêt).

### DT-31 — Idempotence des commandes

[RG-UI-15](../specs/26-interface.md) : chaque commande de modification accepte l'en-tête `Idempotency-Key`
(UUID généré par le client). Table `idempotency_keys (organization_id, key, request_hash, response,
created_at)` conservée 24 h ; une même clé avec la même requête renvoie la réponse enregistrée, avec une
requête différente → 422 `IDEMPOTENCY_KEY_REUSED`. Les publications utilisent en plus leur identifiant
d'opération ([RG-PUB-20](../specs/24-depots-et-publication.md)).

### DT-32 — Files et équité

- Une file Service Bus **à sessions** par nature de travail (`generation`, `publication`, `tracking`,
  `notifications`), identifiant de session = identifiant d'organisation.
- **Rotation forcée** : le processeur libère la session (`ReleaseSession()`) après **chaque** message ; une
  organisation qui alimente sa file en continu n'occupe donc jamais une place plus d'un travail. `SessionIdleTimeout`
  seul ne suffit pas : il ne libère qu'une session vide (revue du plan, PLAN-06).
- **Mesure, pas supposition** : le test `Sessions_are_served_fairly_under_saturation` (S-08) sature toutes les places
  avec des organisations alimentées en continu et borne le délai de prise en charge d'une nouvelle organisation
  (places × durée d'un travail × 3).
- **Variante B, à appliquer si ce test échoue** (l'ordre de remise des sessions par Service Bus n'est pas contractuel) :
  planificateur équitable en base — table `job_queue (id, organization_id, kind, payload, enqueued_at, state,
  started_at)` et `organization_dispatch (organization_id, kind, last_dispatched_at)` ; le worker prend le travail
  `Pending` de l'organisation servie le moins récemment (`ORDER BY last_dispatched_at NULLS FIRST, enqueued_at`,
  `FOR UPDATE SKIP LOCKED`) ; Service Bus ne sert plus qu'au réveil (message sans contenu). Les gestionnaires
  `IJobHandler<T>` et `IJobDispatcher` ne changent pas.
- Envoi fiable : table `outbox_messages` écrite dans la transaction de la commande, relayée par le worker
  (`OutboxRelay`), à au moins une fois ; chaque gestionnaire est idempotent.
- Budgets par organisation (`organization_budgets`) contrôlés avant exécution : un dépassement replanifie le message
  (30 s) et libère la session, sans toucher aux autres organisations.
- La position dans la file est exposée par l'API (`queuePosition`) pour l'écran.

### DT-33 — Identité managée partout ; chaîne de connexion seulement en local

Décidée (2026-10-04, demande de l'utilisateur). **En Azure, IFS ne détient aucune clé ni aucun mot de passe pour ses
propres services** : chaque connexion passe par l'identité managée du processus (une identité affectée par Container
App, exposée par `AZURE_CLIENT_ID`). En local, Aspire fournit des chaînes de connexion vers les émulateurs, qui ne
connaissent pas Entra.

**Règle unique de résolution** — `Infrastructure/Azure/AzureConnectionResolver.cs`, utilisée par **toutes** les
inscriptions de clients (aucun client Azure n'est construit autrement : test d'architecture) :

1. si `ConnectionStrings:<nom>` existe et n'est pas vide → on l'utilise **telle quelle** (local : Azurite, émulateur
   Service Bus, PostgreSQL et Redis en conteneur, MailPit) ;
2. sinon on lit `Azure:<nom>:Endpoint` (URL du service, ou hôte pour PostgreSQL et Redis) et on s'authentifie par
   **identité managée** : `ManagedIdentityCredential(AZURE_CLIENT_ID)` quand le processus tourne dans Azure (variable
   `IDENTITY_ENDPOINT` présente), sinon `DefaultAzureCredential` sans les modes interactifs (développeur connecté par
   `az login` qui vise un vrai service Azure depuis son poste) ;
3. ni l'un ni l'autre → échec **au démarrage**, message citant la ressource et les deux clés attendues.

Le résolveur renvoie `AzureConnection { Name, Mode (ConnectionString | ManagedIdentity), ConnectionString?, Endpoint?,
Credential? }` ; chaque fabrique de client choisit son constructeur selon `Mode`. Le `TokenCredential` est un
singleton (`IfsAzureCredential`) partagé par tous les clients.

| Service | Nom | Local (chaîne de connexion) | Azure (point de terminaison + identité managée) | Réglage de la ressource Azure (J0-31) |
|---|---|---|---|---|
| PostgreSQL | `ifs` | Conteneur `postgres`, mot de passe Aspire | `Azure:ifs:Endpoint` = hôte, `Azure:ifs:Database`, `Azure:ifs:Username` = nom de l'identité ; mot de passe = jeton Entra (`https://ossrdbms-aad.database.windows.net/.default`) renouvelé par `NpgsqlDataSourceBuilder.UsePeriodicPasswordProvider` | Authentification **Entra seule** ; identités déclarées comme rôles PostgreSQL |
| Stockage blob | `blobs` | Azurite | `BlobServiceClient(Uri, credential)` | Clé partagée **désactivée** |
| Service Bus | `servicebus` | Émulateur | `ServiceBusClient(espace de noms, credential)` | `disableLocalAuth = true` |
| Redis | `redis` | Conteneur | `Microsoft.Azure.StackExchangeRedis` : `ConfigureForAzureWithUserAssignedManagedIdentityAsync` | Entra seule, clés d'accès désactivées |
| Key Vault | `keyvault` | Émulateur, ou `DevelopmentSecretStore` ([DT-12](#dt-12--secrets-propres-à-ifs--key-vault)) | `SecretClient(Uri, credential)` | RBAC ; Key Vault Secrets Officer pour le worker seulement |
| E-mail (ACS) | `email` | MailPit (SMTP) | `EmailClient(Uri, credential)` | Accès par clé non utilisé ; rôle d'envoi attribué à l'identité du worker |
| Application Insights | — | Tableau de bord Aspire (OTLP) | Exportateur Azure Monitor avec `Credential` | `DisableLocalAuth = true` (ingestion par Entra) |
| Registre de conteneurs | — | — | Tirage des images par l'identité (`AcrPull`) | Utilisateur admin désactivé |
| Azure DevOps (publication, suivi) | — | Gitea / WireMock | Jeton Entra de l'application « IFS Git » obtenu par **fédération depuis l'identité managée** du worker, sans secret client | Informations d'identification fédérées sur l'inscription |
| Validation des jetons utilisateurs | — | Keycloak | Clés publiques OIDC d'Entra | — |

**Exceptions assumées** (secrets qui ne viennent pas d'IFS) : jetons git **de repli** fournis par les clients
([DEC-23](../specs/03-decisions.md)) et clé privée de l'application GitHub (jalon 1), stockés dans Key Vault et lus par
identité managée.

**Exception SQL de l'application témoin** (précision du 2026-10-06, Claude, question P-01 ; P9). La règle ci-dessus
(`AZURE_CLIENT_ID`) vaut pour IFS et pour les clients Azure SDK du témoin (Log Analytics, App Configuration, Service
Bus). Pour **SQL**, le témoin doit prouver l'identité **système** alors que la Container App porte aussi l'identité
affectée `id api` : il utilise `Microsoft.Data.SqlClient` avec `Authentication=Active Directory Managed Identity` et
**sans `User Id`** (sans `User Id`, SqlClient demande le jeton de l'identité système). Écartés : `Active Directory
Default` (passe par `DefaultAzureCredential`, dont l'identité managée par défaut vient de `AZURE_CLIENT_ID` → mauvaise
identité) et `AccessTokenCallback` avec `ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)` (plus de code
pour le même effet, et SqlClient interdit de le combiner avec `Authentication`). Cette exception ne s'applique pas à
IFS (PostgreSQL passe par Npgsql, ligne du tableau ci-dessus). `Microsoft.Data.SqlClient` est épinglé en
[DT-03](#dt-03--versions-épinglées), uniquement pour `samples/witness-app/`.

**Vérifications** : `AzureConnectionResolverTests` (chaîne présente → mode chaîne ; absente + point de terminaison →
identité managée ; chaîne vide = absente ; aucune → erreur explicite) ; test d'architecture (aucun `new
BlobServiceClient(`, `new ServiceBusClient(`, `new SecretClient(`, `new EmailClient(` hors de
`Infrastructure/Azure/`) ; recette J0-31 : aucune clé ni mot de passe dans les paramètres des Container Apps,
`allowSharedKeyAccess: false`, `disableLocalAuth: true`, PostgreSQL en Entra seul.

### DT-34 — Langues : français et anglais commutables

Décidée (2026-10-04). Transloco (option `i18n` du template), `fr` par défaut, `en` complet ([EXG-13](../specs/27-exigences-non-fonctionnelles.md),
[RG-UI-08](../specs/26-interface.md)).

- `LanguageService` (`core/i18n/language.service.ts`) : langue active = préférence du profil (serveur, à partir de J0-01),
  sinon choix local mémorisé, sinon langue du navigateur si `fr` ou `en`, sinon `fr` ; met à jour `<html lang>`,
  `TranslocoService.setActiveLang`, et les formats de dates et de nombres (données de locale `fr` et `en` enregistrées,
  formatage par `Intl` avec la langue active — pas de `LOCALE_ID` figé).
- `app-language-switch` (`shell/language-switch/`) : `app-ds-segmented` « FR / EN » dans le menu utilisateur de la barre
  latérale (dès S-13) et dans le Profil ([Profile](../design/maquette-v1/preview/Profile.html), J0-32) ; changement
  **immédiat**, sans rechargement.
- L'intercepteur envoie `Accept-Language` ; l'API rend ses messages (constats, `problem+json`) dans cette langue ; les
  codes de règles ne sont jamais traduits.
- CI : `npm run i18n:check` — toute clé de `fr.json` existe dans `en.json` et inversement, aucune clé inutilisée.

### DT-35 — Registres et stratégies, pas de `switch`

Décidée (2026-10-04). Toute dimension que la feuille de route agrandit (langage, plateforme CI, fournisseur git, type de
liaison, stratégie de déploiement, moteur de base pour l'accès aux données, canal de notification, source des modules)
est une clé servie par un `Registry<TKey, TService>` d'implémentations `IKeyed<TKey>` enregistrées en DI. Ajouter une
valeur = ajouter une implémentation. Interdit par test d'architecture : `switch`/`if` sur ces clés hors des registres.
Liste complète et étape de création : [10 § 2](10-extensibilite.md#2-points-dextension-par-domaine).

### DT-36 — Commandes du modèle comme données, et espaces de travail

Décidée (2026-10-04). Brouillons, propositions, restauration, import, aperçu « à blanc » du MCP et demandes d'accès
rejouent des commandes : chaque commande qui modifie le modèle est un `record` sérialisable `IModelCommand` portant
`[ModelCommand("<Nom>")]` et ciblant un `ModelWorkspaceId` (le modèle principal au jalon 0, un brouillon au jalon 2).
`IModelCommandExecutor` exécute une liste de commandes en une transaction, en mode `Apply` ou `DryRun` (effets,
constats et résumé calculés, puis annulation). L'API envoie des listes d'une commande. Créé à J0-07.
Détail : [10 § 1.2](10-extensibilite.md#12-les-commandes-du-modèle-sont-des-données).

### DT-37 — Événements de domaine par l'outbox

Décidée (2026-10-04). Les agrégats lèvent des événements de domaine ; `UnitOfWorkBehavior` les écrit dans
`outbox_messages` dans la transaction de la commande ; le worker les distribue aux `IDomainEventHandler<T>`. Toute
réaction (e-mail, historique, index de recherche, webhooks) est un gestionnaire ajouté, jamais du code dans la commande.
Créé à S-06 (écriture) et S-08 (distribution). Détail : [10 § 1.3](10-extensibilite.md#13-événements-de-domaine-par-loutbox).

### DT-38 — Disponibilité des fonctions et limites de plan

Décidée (2026-10-04). `FeatureCatalog` (clé, état `Released|EarlyAccess|NotReleased`), `IFeatureAvailability`,
`.RequireFeature()` sur les routes, `GET /v1/features`, filtrage des valeurs d'énumération et de la navigation : c'est
le mécanisme unique de [P9](../specs/01-principes.md) et de l'accès anticipé ([UC-EXP-06](../specs/40-exploitation-ifs.md)).
`IPlanLimitGuard` est appelé par toute commande de création dès J0 (implémentation illimitée jusqu'à J3-07). Créé à
J0-01. Détail : [10 § 1.4](10-extensibilite.md#14-disponibilité-des-fonctions-et-limites-de-plan).

### DT-39 — Journalisation et diagnostics sont des dépendances de création

Décidée (2026-10-04), **écart de spec signalé**. [RG-CMP-05](../specs/12-composants-et-groupes-de-ressources.md) ne cite
ni la journalisation ni les diagnostics parmi les dépendances de création, et exclut les sorties « calculables depuis le
nom ». Or un paramètre de diagnostic ou une liaison de journalisation exige que l'espace Log Analytics **existe** au
déploiement, même si son identifiant se calcule : déployer `data` avant `core` dans un abonnement vide échouerait
([P1](../specs/04-perimetre-et-lots.md)). Le moteur traite donc « Journalisation » et « Diagnostics » comme dépendances de
création (`ILinkKindHandler.CreatesDeploymentDependency = true`). Claude propose l'amendement de RG-CMP-05 dans les specs
(nouvelle `DEC-113`) au verrou R-03.

### DT-40 — Ordre des comportements du médiateur

Décidée (2026-10-04). `LoggingBehavior` → `AuthorizationBehavior` → `ValidationBehavior` → `UnitOfWorkBehavior`.
L'autorisation passe **avant** la validation : un utilisateur sans accès au projet doit recevoir « introuvable »
([RG-ORG-13](../specs/10-organisations-et-acces.md)), jamais des erreurs de validation qui révéleraient l'existence ou la
forme de l'objet. Les validateurs ne contrôlent que la forme de la requête ; ils ne lisent pas la base.

### DT-41 — Build en mode Code au jalon 2

Décidée (2026-10-04), **écart de spec signalé**. [04 § 2.1](../specs/04-perimetre-et-lots.md) place « build conteneur et
code » au jalon 1, mais les onze types du jalon 1 sont des conteneurs : les profils de build Code arrivent avec Web App et
Function App (J2-01). Claude propose l'amendement de 04 § 2.1 avec celui de RG-CMP-05 ([DT-39](#dt-39--journalisation-et-diagnostics-sont-des-dépendances-de-création)) au verrou R-03.

### DT-42 — Coût des preuves éphémères Azure

⚖️ **Stratégie approuvée** par l'utilisateur le 2026-10-07 : North Europe, profil minimal, vérifications régulières des coûts et suppression limitée aux ressources créées par Luna. **Plafond chiffré et estimation actualisée à confirmer avant toute commande Azure.** Complète [DT-29](#dt-29--projet-pilote-de-référence) ; ne
remplace aucune décision : [DT-27](#dt-27--hébergement-dans-azure-france-central) (hébergement d'IFS en France Central)
reste vraie, elle ne concerne pas les cibles du projet pilote. Aucune preuve P1–P9, aucune assertion et aucun résultat
attendu ne change ; seuls changent la région, les SKU, le moment de la suppression et le garde-fou de dépense.

1. **Région unique `northeurope`** pour les trois cibles du pilote. Un préflight bloquant vérifie région, SKU et offre ;
   en cas d'indisponibilité on **s'arrête** (aucun repli West Europe, aucun changement de région sans nouvelle `DT`).
2. **Une seule souscription** (dev = prd = shared) et un code projet isolé par jeu (`shop42`, `shop43`, `shop44`). Aucune
   preuve n'exige deux souscriptions ; les jeux sont déployés **l'un après l'autre**, jamais en parallèle.
3. **Profil de coût des preuves** ([reference-pilote § 1.6](../plan/reference-pilote.md#16-profil-de-coût-des-preuves)) :
   ACR `Basic` ; SQL serverless `GP_S_Gen5_1`, `minCapacity` 0,5, pause automatique, zone redondante **explicitement
   désactivée** (le module AVM `sql/server:0.22.0` la met à `true` si elle est omise) ; Container Apps min 0 / max 1,
   environnement non redondant ; Application Insights 90 j ; Log Analytics **inchangé** (dev 30 j, prd 90 j : P3b exige
   90 → 120 → 90) avec un plafond d'ingestion quotidien.
4. **Offre SQL gratuite** pour **prd seulement** (`useFreeLimit`, `freeLimitExhaustionBehavior = AutoPause`) seulement si le préflight confirme
   l'éligibilité **et** la compatibilité du module. Le schéma AVM local la décrit « une base par souscription », la
   documentation Microsoft « dix bases » : décalage **non tranché**, jamais promis. Sinon : serverless payant, estimation
   recalculée et **nouvelle confirmation** ; jamais de poursuite silencieuse.
5. **Application du profil** : la sortie `reference/pilot/` reste la vérité des émetteurs du jalon 0 (le catalogue du pilote
   ne porte pas `autoPauseDelay`, `minCapacity`, `useFreeLimit`, `zoneRedundant` de base, plafond d'ingestion) ; seule la
   région y change. Le profil est appliqué **à la publication** (`Publish-PilotReference.ps1`, comme la table des noms) et
   la chaîne de révisions est rejouée sur un clone ainsi profilé. Implémentation par Luna en P-08 (point 0).
6. **Suppression au plus tôt** : un jeu Azure est supprimé dès sa dernière preuve dépendante (shop42 après P8 ; shop43 :
   `orders`, `data`, `platform` et l'identité détachée après P4, `core` et le kit après P5 ; shop44 après P7), à condition
   que ses captures Azure DevOps soient enregistrées et ses résultats consignés. Les preuves Azure DevOps et
   `resultats.md` restent jusqu'à R-03. Détail : [recette § 10](../plan/recettes/01-preuves.md#10-supprimer-les-ressources-azure-et-consigner-les-résultats).
7. **Verrou de dépense** : aucune commande Azure tant que l'utilisateur n'a pas confirmé un **plafond** et l'**estimation
   actualisée** ([recette § 0](../plan/recettes/01-preuves.md#0-verrou-de-coût-et-préflight-bloquants)). Un budget Azure est
   une alerte retardée, pas un coupe-circuit : le plafond est tenu par la durée de vie courte des ressources et les
   contrôles de fin de session, pas par Azure.
8. **Non prouvé depuis le dépôt** (préflights bloquants, rien n'est supposé) : commande exacte de passage `denyDelete` →
   `none` d'une pile existante ; liste exacte des IDs gérés par chaque pile au moment du nettoyage ; éligibilité à l'offre gratuite ; limites ACR Basic
   face à l'image témoin ; plafond d'ingestion sans effet sur `/health/dependencies`.
9. **Inventaire avant/après création** ([inventaires](../plan/preuves/inventaires-azure.md)) : capturer les ressources déjà présentes dans les portées ciblées, puis consigner chaque ID, groupe et pile créé par Luna. Au nettoyage, ne supprimer que ces IDs ; `deleteAll` n'est permis que si tous les éléments gérés par la pile sont dans cet inventaire.
