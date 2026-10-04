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
| [DT-04](#dt-04--postgresql-17) | PostgreSQL 17 | ⚖️ à confirmer avant S-06 |
| [DT-05](#dt-05--minimal-api-v1-openapi-intégré--scalar) | Minimal API `/v1`, OpenAPI intégré + Scalar | Décidée |
| [DT-06](#dt-06--erreurs--erroror--problemjson) | Erreurs : `ErrorOr` → `problem+json` | Décidée |
| [DT-07](#dt-07--authentification--oidc-entra-en-azure-keycloak-en-local) | Authentification : OIDC, Entra en Azure, Keycloak en local | ⚖️ à confirmer avant S-07 |
| [DT-08](#dt-08--worker--service-net-et-service-bus-pas-azure-functions) | Worker : service .NET + Service Bus, pas Azure Functions | ⚖️ à confirmer avant S-08 |
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
| [DT-21](#dt-21--outillage-de-test) | Outillage de test | ⚖️ à confirmer avant S-04 (licence) |
| [DT-22](#dt-22--mediatr-et-sa-licence) | MediatR et sa licence | ⚖️ à confirmer avant S-03 |
| [DT-23](#dt-23--identifiants-et-concurrence) | Identifiants et concurrence | Décidée |
| [DT-24](#dt-24--isolation-multi-organisations) | Isolation multi-organisations | Décidée |
| [DT-25](#dt-25--journal-daudit-en-ajout-seul) | Journal d'audit en ajout seul | Décidée |
| [DT-26](#dt-26--temps-et-horloge) | Temps et horloge | Décidée |
| [DT-27](#dt-27--hébergement-dans-azure-france-central) | Hébergement dans Azure France Central | Décidée |
| [DT-28](#dt-28--langue-du-code-et-de-linterface) | Langue du code et de l'interface | Décidée |
| [DT-29](#dt-29--projet-pilote-de-référence) | Projet pilote de référence | Décidée |
| [DT-30](#dt-30--thème-clair) | Thème clair | ⚖️ à confirmer avant J3 |
| [DT-31](#dt-31--idempotence-des-commandes) | Idempotence des commandes | Décidée |
| [DT-32](#dt-32--files-et-équité) | Files et équité | Décidée |

---

### DT-01 — Monodépôt

Backend, frontend, catalogue, sorties de référence, application témoin, infrastructure d'IFS et
documentation vivent dans ce dépôt (arborescence : [00 § 4](00-vue-d-ensemble.md)). Un changement de
modèle touche presque toujours l'API, le client généré et un écran : un seul dépôt, une seule revue.

### DT-02 — Backend : template CQRS modernisé selon Vole-Papillon-Damour

**Base.** Le template `dotnet new templatewebcqrs` du dépôt
[FlorianDrevet/template-CQRS](https://github.com/FlorianDrevet/template-CQRS) : couches `Domain`,
`Application`, `Infrastructure`, `Api`, `Contracts`, CQRS par MediatR, validation FluentValidation,
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
| MediatR | `14.1.0` |
| ErrorOr | `2.0.1` |
| FluentValidation (+ `DependencyInjectionExtensions`) | `12.1.1` |
| Mapster (+ `DependencyInjection`) | `10.0.7` |
| `Microsoft.Identity.Web` | `4.14.2` |
| `Scalar.AspNetCore` | dernière `2.x` |
| `Azure.Identity` | `1.21.0` |
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

⚖️ **À confirmer avant S-06.** Base relationnelle unique d'IFS : PostgreSQL 17 (Azure Database for
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

⚖️ **À confirmer avant S-07.**

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

⚖️ **À confirmer avant S-08.** Le worker est un hôte générique .NET (`Microsoft.Extensions.Hosting`) déployé
en Container App sans ingress. Il consomme Azure Service Bus (émulateur en local) et exécute les tâches
planifiées avec un bail en base (une seule instance exécute une tâche donnée).

**Pourquoi pas Azure Functions, comme VPD** : l'équité par organisation
([EXG-23](../specs/27-exigences-non-fonctionnelles.md)) repose sur des **sessions** Service Bus servies à tour
de rôle ([DT-32](#dt-32--files-et-équité)) et sur des budgets par organisation que le worker contrôle
lui-même ; le contrôle de sortie a besoin des binaires `bicep` et `pwsh` dans l'image ; et VPD a rencontré
des incidents de résolution du SDK Functions dans Docker et dans Aspire (NEXT.md de VPD, 2026-09-24). Un
hôte .NET simple supprime ces trois difficultés.

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

⚖️ **À confirmer avant S-04 (licence).**

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

### DT-22 — MediatR et sa licence

⚖️ **À confirmer avant S-03.** MediatR (template et VPD) est sous licence commerciale depuis la v13 : gratuit
(« Community ») pour une entreprise dont le chiffre d'affaires est inférieur à 5 M$, avec une clé de licence
à demander ; payant au-delà. IFS garde MediatR `14.1.0` et lit la clé dans la configuration
`MediatR:LicenseKey` (secret utilisateur en local, Key Vault en Azure). Sans clé, MediatR journalise un
avertissement mais fonctionne. **Alternative** si l'utilisateur la préfère : `Mediator` (génération de
source, MIT), API très proche ; le remplacement touche uniquement `Application/DependencyInjection.cs`, les
signatures de gestionnaires (`ValueTask`) et les comportements.

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
- L'autorisation projet/composant est faite par `IProjectAuthorizer` dans un comportement MediatR, à partir
  d'un attribut `[RequiresPermission(Permission.ModelEdit, Scope = ...)]` sur chaque commande ou requête.
- [EXG-01](../specs/27-exigences-non-fonctionnelles.md) : la suite `Api.Tests/Isolation/` énumère tous les
  points de terminaison (`EndpointDataSource`) et échoue si l'un d'eux n'a pas de scénario d'isolation
  déclaré ([06 § 4](06-tests-et-qualite.md)).

### DT-25 — Journal d'audit en ajout seul

Table `audit_events` alimentée dans la transaction de la commande. Un déclencheur PostgreSQL refuse
`UPDATE` et `DELETE` (seule la purge planifiée à 13 mois, par une fonction `SECURITY DEFINER` dédiée, peut
supprimer). Différence avant/après en `jsonb`. Pseudonymisation après suppression d'un compte par
remplacement de l'auteur par un identifiant opaque dans une table de correspondance supprimée
([EXG-06](../specs/27-exigences-non-fonctionnelles.md)).

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

### DT-30 — Thème clair

⚖️ **À confirmer avant J3.** Strata n'a qu'un thème sombre ; [RG-UI-07](../specs/26-interface.md) demande
clair, sombre et système. Jusqu'à ce que Strata publie son thème clair (mêmes noms de tokens, second jeu de
valeurs), l'interface est sombre et la préférence de thème n'est **pas affichée** ([P9](../specs/01-principes.md)).
Les composants n'utilisent que des variables `--ifs-*` : le thème clair s'ajoutera sans toucher aux écrans.

### DT-31 — Idempotence des commandes

[RG-UI-15](../specs/26-interface.md) : chaque commande de modification accepte l'en-tête `Idempotency-Key`
(UUID généré par le client). Table `idempotency_keys (organization_id, key, request_hash, response,
created_at)` conservée 24 h ; une même clé avec la même requête renvoie la réponse enregistrée, avec une
requête différente → 422 `IDEMPOTENCY_KEY_REUSED`. Les publications utilisent en plus leur identifiant
d'opération ([RG-PUB-20](../specs/24-depots-et-publication.md)).

### DT-32 — Files et équité

- Une file Service Bus **à sessions** par nature de travail (`generation`, `publication`, `tracking`,
  `notifications`), identifiant de session = identifiant d'organisation : les messages d'une organisation
  sont traités dans l'ordre, et le processeur à sessions sert les organisations à tour de rôle
  ([EXG-23](../specs/27-exigences-non-fonctionnelles.md)).
- Envoi fiable : table `outbox_messages` écrite dans la transaction de la commande, relayée vers Service Bus
  par le worker (`OutboxRelay`), à au moins une fois ; chaque gestionnaire est idempotent.
- Budgets par organisation (générations par heure…) : table `organization_budgets`, contrôlés avant la mise
  en file ; un dépassement ralentit (message différé) et prévient, sans toucher aux autres organisations.
- La position dans la file est exposée par l'API (`queuePosition`) pour l'écran.
