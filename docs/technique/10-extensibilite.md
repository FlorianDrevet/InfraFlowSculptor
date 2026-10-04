# 10 — Extensibilité : ajouter une fonction sans refactoriser

> Objectif : chaque fonction des lots 1 à 4 ([04](../specs/04-perimetre-et-lots.md)) s'ajoute en **écrivant du code
> nouveau à un endroit prévu**, pas en modifiant du code existant partout. Ce document liste les points d'extension,
> l'étape qui les crée, et ce qu'ils permettront d'ajouter. Décisions : [DT-35 à DT-39](01-decisions.md#dt-35--registres-et-stratégies-pas-de-switch).
>
> Règle de lecture pour Luna : quand une étape crée un point d'extension, elle crée **l'interface, le registre, au
> moins une implémentation et le test du registre** — jamais les implémentations des lots suivants
> ([P9](../specs/01-principes.md) : rien de ce qui n'est pas livré n'apparaît, même désactivé).

## 1. Les quatre mécanismes transverses

### 1.1 Registres et stratégies, jamais de `switch` sur une dimension qui grandit

Décision : [DT-35](01-decisions.md#dt-35--registres-et-stratégies-pas-de-switch).

Toute dimension que la feuille de route agrandit est une **clé** (énumération ou chaîne stable) servie par un
**registre** d'implémentations enregistrées en DI :

```csharp
public interface IKeyed<TKey> { TKey Key { get; } }
public sealed class Registry<TKey, TService>(IEnumerable<TService> services) where TService : IKeyed<TKey>
{
    public TService Get(TKey key);            // erreur explicite « <clé> non prise en charge » si absente
    public bool Supports(TKey key);
    public IReadOnlyCollection<TKey> Keys { get; }
}
```

Test d'architecture (`Architecture.Tests/NoSwitchOnExtensionKeysTests`) : aucun `switch`/`if` sur `IacLanguage`,
`CiPlatform`, `GitProviderKind`, `LinkKind`, `DeploymentStrategyKind`, `NotificationChannel` hors des classes
`*Registry` et des implémentations elles-mêmes.

### 1.2 Les commandes du modèle sont des données

Décision : [DT-36](01-decisions.md#dt-36--commandes-du-modèle-comme-données-et-espaces-de-travail).

Brouillons (J2-05), propositions (J2-06), restauration (J2-04), import (J3-05, lot 3), `preview_change` du MCP (J3-01),
demandes d'accès (J2-07) : **tous** rejouent des commandes du modèle. Dès le jalon 0 :
- chaque commande qui modifie le modèle est un `record` sérialisable portant `[ModelCommand("AddResource")]` (nom
  stable, versionné) et implémente `IModelCommand` ; elle cible un **espace de travail** (`ModelWorkspaceId` : le
  modèle principal aujourd'hui, un brouillon demain) ;
- `IModelCommandExecutor.ExecuteAsync(workspace, IReadOnlyList<IModelCommand>, ExecutionMode)` exécute une liste en
  **une** transaction, avec `ExecutionMode.Apply` ou `ExecutionMode.DryRun` (tout est calculé — effets, constats,
  résumé — puis la transaction est annulée) ;
- l'API appelle l'exécuteur avec une liste d'une commande ; J2 et J3 l'appelleront avec des listes, sans toucher aux
  commandes ;
- `ModelCommandSerializer` (JSON polymorphe par nom de commande) + test : chaque `IModelCommand` fait l'aller-retour.

### 1.3 Événements de domaine par l'outbox

Décision : [DT-37](01-decisions.md#dt-37--événements-de-domaine-par-loutbox).

Les agrégats lèvent des événements (`ResourceAdded`, `RevisionGenerated`, `PublicationCompleted`,
`DeploymentStateChanged`…). `UnitOfWorkBehavior` les écrit dans `outbox_messages` dans la même transaction ; le worker
les distribue aux `IDomainEventHandler<T>`. Ajouter une réaction = ajouter un gestionnaire :

| Réaction | Arrive à |
|---|---|
| Notifications e-mail (invitation, échec de déploiement…) | J0 |
| Jeux de modifications de l'historique | J1-03 (les données avant/après existent depuis J0-04) |
| Index de recherche `Ctrl+K` | J3-03 |
| Centre de notifications, résumé quotidien | J3-03 |
| Webhooks, Teams, Slack | Lot 2, vague G |
| Télémétrie produit | J0-32 |

### 1.4 Disponibilité des fonctions et limites de plan

Décision : [DT-38](01-decisions.md#dt-38--disponibilité-des-fonctions-et-limites-de-plan).

- `FeatureCatalog` (Domain) : chaque fonction a une clé (`Features.GitHubRepositories`, `Features.Drafts`…) et un
  état `Released`, `EarlyAccess` (activable par organisation, [UC-EXP-06](../specs/40-exploitation-ifs.md)) ou
  `NotReleased`. `IFeatureAvailability.IsEnabled(feature, organization)`.
- API : `.RequireFeature(Features.X)` sur les groupes de routes (404 si non disponible) ; `GET /v1/features` pour
  l'interface.
- Interface : `FeatureAvailabilityService` ; le `NavigationRegistry` et les zones d'écran ne s'affichent que si la
  fonction est disponible ([P9](../specs/01-principes.md)).
- Valeurs d'énumérations visibles (langage, plateforme, fournisseur git, stratégie…) : filtrées par le même catalogue
  (`IacLanguage.Terraform` existe dans le code dès J0 mais n'est proposé qu'une fois `Features.TerraformEmitter`
  publiée).
- `IPlanLimitGuard.EnsureCanCreate(organization, LimitKind)` appelé par toute commande de création (projet,
  environnement, ressource, membre actif) dès J0 ; implémentation « illimitée » jusqu'à J3-07 qui la remplace par les
  plans de [DEC-78](../specs/03-decisions.md), sans toucher aux commandes.

## 2. Points d'extension par domaine

| Point d'extension | Interface / registre | Créé à | Implémentations au lot 1 | Ajouts prévus |
|---|---|---|---|---|
| Langage d'infrastructure | `IInfrastructureEmitter : IKeyed<IacLanguage>` + `InfrastructureEmitterRegistry` ; contrôles par `IOutputChecker : IKeyed<IacLanguage>` | J0-22 | Bicep | Terraform (P6 proto J1-11, L3), OpenTofu, Pulumi ×6 (L4) |
| Variante d'un langage | `IacLanguage` + `PulumiLanguage?` dans la clé composite `EmitterKey` | J0-22 | — | Pulumi TS, C#, Python, Go, Java, YAML |
| Source des modules | `IModuleSourceStrategy : IKeyed<ModuleSource>`, choisie **par type** ([RG-GEN-21](../specs/21-generation-et-revisions.md)) | J0-22 | AVM registre public | AVM embarqués, modules IFS (L2-G), modules du client (L3) |
| Plateforme CI | `IPipelineEmitter : IKeyed<CiPlatform>` ; `IInstallKitPlatformEmitter : IKeyed<CiPlatform>` (la partie Azure du kit est commune) | J0-23 | Azure DevOps | GitHub Actions (L2-D), GitLab CI (L3) |
| Étapes de pipeline | Descripteurs d'étapes (données) + `IStepTranslator : IKeyed<CiPlatform>` (commande → YAML de la plateforme, [RG-APP-09](../specs/19-applications-build-et-deploiement.md)) | J0-19, J0-23 | Cache, scan | Tests, couverture, Sonar… (L2-C) |
| Stratégie de déploiement applicatif | `IDeploymentStrategy : IKeyed<DeploymentStrategyKind>` (prérequis d'infrastructure implicites + étapes logiques du stage) | J0-19 | Directe | Slot, bleu/vert, progressive, Flex (L2-C) |
| Profil de build | Descripteur de profil (données) | J0-19 | Conteneur | .NET, Node, Angular, Python, Java… (J2-01) |
| Fournisseur git | `IGitProvider : IKeyed<GitProviderKind>` + `GitProviderRegistry` | J0-26 | Azure Repos (+ Gitea en dev) | GitHub (J1-07), GitLab (L3) |
| Lecture des exécutions CI | `ICiRunReader : IKeyed<CiPlatform>` ; analyse du rapport `ifs-report.json` **commune** | J0-30 | Azure DevOps | GitHub (L2-D), GitLab (L3) ; dérive (L2-E) = un nouveau type de rapport |
| Types de ressources | Descripteurs JSON ([P7](../specs/01-principes.md)) | J0-06 | 9 puis 17 types | Tous les types des lots 2-4, sans code |
| Types de liaison | `ILinkKindHandler : IKeyed<LinkKind>` (validation de la cible, implicites produits, dépendance de création, forme dans le plan) | J0-15 | Hébergement, journalisation, diagnostics, télémétrie, tirage d'image, accès, accès aux données, dépendance de valeur, lecture de secret | Lecture de configuration (J1-02), point de terminaison privé, intégration sortante, appairage (L2-A), usage d'IA (L2-B), back-end, demande d'accès (J2-07) |
| Accès aux données par moteur de base | `IDataAccessProvider : IKeyed<DataEngine>` (script idempotent, retrait) | J0-15 | Azure SQL | PostgreSQL (J2-01), Cosmos (L2-B), Redis (politique, J2-01) |
| Exposition réseau | `Exposure` record polymorphe (`Public`, `Restricted(ranges)`, `Private(...)`) + `IExposureHandler` | J0-17 | Publique, restreinte | Privée, points de terminaison, DNS (L2-A) |
| Règles de validation | `IValidationRule` (code, gravité, lot, découverte par DI) ; règles **issues de données** via `IRuleSource` | J0-20 | Règles du lot 1 | Politiques d'organisation et dérogations (L2-E), règles réseau et IA (L2) |
| Corrections en un clic | `IFindingFix : IKeyed<string /* code */>` produisant des `IModelCommand` | J0-20 | 5 corrections | Toute nouvelle règle |
| Canaux de notification | `INotificationChannel : IKeyed<NotificationChannel>` + catalogue d'événements notifiables | J0-03 | E-mail | Application (J3-03), webhooks, Teams, Slack (L2-G) |
| Secrets d'IFS | `ISecretStore` | J0-26 | Key Vault, dev | — |
| Lecture d'Azure (facultative) | `IAzureReadConnection` (port déclaré, aucune implémentation au lot 1) | L2-G | — | Disponibilité des noms, sélection des existantes, quotas IA |
| Import | Produit des `IModelCommand` → proposition ([DT-36](01-decisions.md#dt-36--commandes-du-modèle-comme-données-et-espaces-de-travail)) | J3-05 | JSON `ifs-project/v1` | ARM, Bicep, groupe de ressources (L3) |
| Export / format | `ifs-project/vN` avec migrations `IProjectSchemaMigration` | J0-25 | v1 | vN |
| Coûts | Section `pricing` du descripteur (réservée dans le schéma dès J0-06) + `ICostEstimator` | L2-E | — | Estimation, budgets |
| Objets commentables, historisés, auditables | `ObjectRef (ObjectType, Guid Id)` unique pour audit, historique, constats, notifications, commentaires | S-06 | — | Commentaires et mentions (L2-G) |
| MCP | Métadonnées sur chaque commande et requête : `[ModelCommand]`, `[TokenScope]`, `[AdministrativeOperation]` | S-03 (attributs), J0 (pose) | — | Outils MCP générés (J3-01) |

## 3. Côté interface

| Point d'extension | Mécanisme | Créé à |
|---|---|---|
| Navigation | `NavigationRegistry` : chaque fonctionnalité enregistre ses entrées (portée, ordre, clé de fonction) | S-13 |
| Routes | Chaque fonctionnalité expose `FEATURE_ROUTES` chargées paresseusement (`loadChildren`), gardées par `featureGuard(Features.X)` | S-13 |
| Onglets d'une ressource | Générés depuis le descripteur ; un onglet = un composant enregistré dans `ResourceTabRegistry` (clé = section du descripteur) | J0-14 |
| Grille de propriétés | Éditeurs par `kind` de propriété (`PropertyEditorRegistry` : booléen, entier, énumération, texte, liste, CIDR…) | J0-14 |
| Liaisons | Formulaire d'une liaison piloté par le descripteur du type de liaison (`LinkFormRegistry`) | J0-18 |
| Thème, langue | `ThemeService`, `LanguageService` ([DT-30](01-decisions.md#dt-30--thème-sombre-seul-changement-de-thème-prêt), [DT-34](01-decisions.md#dt-34--langues--français-et-anglais-commutables)) | S-10, S-13 |
| Fonctions disponibles | `FeatureAvailabilityService` (lit `/v1/features`) | J0-01 |

## 4. Ce qu'on ne fait pas en avance

Anticiper, c'est poser les **coutures**, pas écrire les fonctions : pas de colonne, d'écran ni de classe métier pour le
réseau privé, l'IA, Terraform ou les politiques avant leur lot. Une interface sans implémentation n'existe que si elle
est déjà appelée par du code livré (exemple : `IPlanLimitGuard`, appelé dès J0 avec une implémentation illimitée).

## 5. Contrôle à chaque verrou

La revue ([`revue-verrou`](../../.claude/skills/revue-verrou/SKILL.md)) vérifie : aucun `switch` sur une clé d'extension
hors registre ; toute commande du modèle est un `IModelCommand` sérialisable ; tout effet externe passe par un événement
ou un travail en file ; toute route d'une fonction porte sa clé de fonction ; aucune fonction non livrée n'est visible.
