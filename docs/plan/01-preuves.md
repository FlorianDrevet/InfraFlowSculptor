# Phase P — Preuves

**But.** Prouver sur Azure, **avant** de développer le pilote, les garanties P1–P5 et P7–P9 du registre des
preuves ([04 § 7.2](../specs/04-perimetre-et-lots.md)), avec une sortie écrite **à la main** pour le projet pilote
de référence ([`reference-pilote.md`](reference-pilote.md)). Cette sortie devient la **vérité** que les émetteurs
du jalon 0 devront reproduire octet pour octet ([technique 03 § 5.1](../technique/03-moteur-et-generation.md#51-vérité-de-sortie)).

P6 (prototypes Terraform et GitHub Actions) n'est pas ici : il appartient au garde-fou du lot 1 et se fait au
jalon 1 ([03-jalon-1](03-jalon-1-tranche-verticale.md)).

**Critère de sortie.** Le verrou [`R-03`](#-r-03--revue-des-preuves) approuvé : chaque preuve a un résultat
consigné dans `docs/plan/preuves/resultats.md`, et Claude a fait passer leur statut à « prototypée » dans
[04 § 7.2](../specs/04-perimetre-et-lots.md).

**Branche du segment** : `impl/preuves`.

**Partage des rôles.** Luna écrit tout ce qui est dans le dépôt et le vérifie hors ligne (compilation Bicep,
analyse PowerShell, tests Pester, schéma YAML). **Vous** exécutez ce qui touche à Azure et à Azure DevOps, en
suivant la recette pas à pas. Luna n'a besoin d'aucun accès à vos comptes.

---

### P-01 — Application témoin

| | |
|---|---|
| **Spécifications** | [04 § 2.0](../specs/04-perimetre-et-lots.md) (conteneur témoin), [90 § 4.5](../specs/90-projet-de-reference.md) |
| **Dépend de** | R-01 |
| **Commit** | `feat(temoin): application témoin et contrôle de ses dépendances` |

🎯 **Objectif.** Une application conteneur minimale dont `/health/dependencies` prouve, avec son identité,
chaque accès que le modèle a câblé.

🔧 **À faire.**
1. `samples/witness-app/` : projet ASP.NET Core minimal API .NET 10 (`WitnessApp.csproj`, hors de
   `InfraFlowSculptor.slnx`), `Dockerfile` multi-étapes (`mcr.microsoft.com/dotnet/sdk:10.0` →
   `mcr.microsoft.com/dotnet/aspnet:10.0`, utilisateur non root, port 8080), `README.md`.
2. Points de terminaison :
   - `GET /health` → 200 `{ "status": "ok", "version": "<variable IMAGE_TAG>" }`.
   - `GET /health/dependencies` → 200 si tous les contrôles réussissent, 503 sinon ; corps
     `{ "checks": [ { "name", "ok", "durationMs", "error" } ] }`, sans jamais afficher de valeur secrète.
3. Contrôles, chacun **activé seulement si sa configuration existe** (sinon `ok: null, "skipped"`) :
   | Nom | Configuration | Ce qu'il fait |
   |---|---|---|
   | `secret` | `Payments__ApiKey` | La variable est présente et non vide (Container Apps l'a résolue depuis Key Vault) |
   | `logs` | `LogAnalytics__WorkspaceId` | `LogsQueryClient.QueryWorkspaceAsync(id, "print 1", 5 min)` avec `DefaultAzureCredential` |
   | `sql` | `Sql__Server`, `Sql__Database` | Connexion `Microsoft.Data.SqlClient` avec `Authentication=Active Directory Managed Identity` et **sans `User Id`** (identité **système**, [DT-33](../technique/01-decisions.md#dt-33--identité-managée-partout--chaîne-de-connexion-seulement-en-local)) ; insertion puis lecture d'une ligne dans `dbo.witness_checks` (table **préparée** par `sql/witness-schema.sql`, jamais créée par l'application) ; puis contrôle `sql-least-privilege` : un `CREATE TABLE dbo.witness_forbidden(...)` doit être **refusé** (le contrôle échoue s'il réussit) |
   | `appconfig` | `AZURE_APPCONFIG_ENDPOINT` | Lecture de la clé `orders:maxItemsPerOrder` (jalon 1) |
   | `servicebus` | `ServiceBus__Namespace` | Envoi d'un message sur `order-created` (jalon 1) |
   `DefaultAzureCredential` utilise `AZURE_CLIENT_ID` (identité affectée `id api`) **sauf** pour SQL, qui
   utilise l'identité système : c'est ce qu'exige P9. Contrat SQL, sans choix laissé à l'implémentation :
   - chaîne de connexion construite par `SqlConnectionStringBuilder` : `DataSource` = `Sql__Server`,
     `InitialCatalog` = `Sql__Database`, `Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity`
     (`Active Directory Managed Identity`), `Encrypt = true`, **aucun** `User Id`, **aucun** mot de passe ;
   - **interdits** pour SQL : `Authentication=Active Directory Default` (s'appuie sur `DefaultAzureCredential`, donc sur
     `AZURE_CLIENT_ID` → identité `id api`, ce que P9 interdit), un `User Id` (désignerait une identité affectée),
     `AccessTokenCallback` / `AccessToken` et toute lecture de `AZURE_CLIENT_ID` dans le code SQL ;
   - paquet `Microsoft.Data.SqlClient` (pas `System.Data.SqlClient`), version épinglée selon
     [DT-03](../technique/01-decisions.md#dt-03--versions-épinglées) dans le fichier de versions de `samples/witness-app/`.
3 bis. `samples/witness-app/sql/witness-schema.sql` : script idempotent qui crée `dbo.witness_checks (id uniqueidentifier
   primary key, written_at datetime2, value nvarchar(100))`. Il est exécuté **une fois par cible** par un membre du
   groupe administrateur SQL (étape de la recette, comme le ferait la migration de schéma d'un client) : les droits de
   l'application restent exactement `db_datareader` + `db_datawriter` ([RG-LIA-20](../specs/16-liaisons-identites-et-acces.md)).
4. Tests `samples/witness-app/tests/` (xUnit) : un contrôle sans configuration est `skipped` ; un contrôle en
   erreur rend 503 et un message sans valeur secrète ; `/health` renvoie `IMAGE_TAG` ; la fabrique de chaîne SQL
   produit `Authentication=Active Directory Managed Identity`, un `UserID` vide, et ne lit jamais `AZURE_CLIENT_ID`
   (test avec la variable positionnée).

✅ **Vérification automatique.** `dotnet test samples/witness-app/tests` vert ; `docker build samples/witness-app`
réussit.

🧪 **Test manuel.**
1. `docker run -p 8080:8080 -e IMAGE_TAG=local <image>` puis `http://localhost:8080/health` → `"version":"local"`.
2. `http://localhost:8080/health/dependencies` → 200, tous les contrôles `skipped`.
3. Relancer avec `-e Payments__ApiKey=` (vide) → 503, contrôle `secret` en erreur « variable vide ».

🧠 **Mémoire.** `02-project-structure.md` (`samples/witness-app`).

### P-02 — Bicep du projet pilote, écrit à la main

| | |
|---|---|
| **Spécifications** | [21 § 6.1-6.2](../specs/21-generation-et-revisions.md), [15](../specs/15-catalogue.md) (types du pilote), [16](../specs/16-liaisons-identites-et-acces.md), [17 § 5](../specs/17-parametres-applicatifs-et-secrets.md), [RG-LIA-16](../specs/16-liaisons-identites-et-acces.md), [DEC-99](../specs/03-decisions.md) |
| **Référence** | [`reference-pilote.md`](reference-pilote.md) § 1–3 |
| **Dépend de** | P-01 |
| **Commit** | `feat(reference): code Bicep du projet pilote de référence` |

🎯 **Objectif.** Le code d'infrastructure exact qu'IFS devra générer pour les quatre composants, idiomatique et
sans avertissement.

🔧 **À faire.**
1. `reference/pilot/pins.json` : pour chaque type du pilote, le module AVM `br/public:avm/res/...` et sa **dernière
   version publiée à la date de l'étape** (lire `https://github.com/Azure/bicep-registry-modules`, dossier du
   module, `CHANGELOG.md` ou `version.json`) ; la version de la CLI Bicep ; l'image de démarrage des Container
   Apps (`mcr.microsoft.com/k8se/quickstart` **épinglée par empreinte** `@sha256:…`). Ajouter la date de lecture.
   Modules attendus : `operational-insights/workspace`, `insights/component`, `key-vault/vault`,
   `managed-identity/user-assigned-identity`, `container-registry/registry`, `app/managed-environment`,
   `app/container-app`, `sql/server` (porte les bases ; sinon ressource directe pour la base),
   `authorization/role-assignment/rg-scope` (ou ressource `Microsoft.Authorization/roleAssignments` directe si
   le module ne permet pas le nom déterministe).
2. Pour chaque composant (`core`, `data`, `platform`, `orders`), dans `reference/pilot/bicep-azdo/<composant>/infra/` :
   - `types.bicep` : un type utilisateur exporté par ressource (`@export() type caApiConfig = { deploy: bool,
     name: string, … }`), un type `target`, un type `resourceGroup` ; chaque champ avec `@description`.
   - `main.bicep` : `targetScope = 'subscription'` ; paramètres typés (`param target targetConfig`,
     `param resourceGroups resourceGroupsConfig`, un paramètre par ressource) ; groupes de ressources créés,
     puis un module par ressource à la portée de son groupe, conditionné par `deploy` ; références externes
     en `existing` à partir du nom et de l'abonnement ([RG-GEN-11](../specs/21-generation-et-revisions.md)) ;
     attributions de rôle avec nom `guid(portée, principal, rôle)` et `principalType: 'ServicePrincipal'`,
     déclarées **avant** les ressources qui en ont besoin (`dependsOn`) ; paramètres de diagnostic ; en-tête de
     [RG-GEN-08](../specs/21-generation-et-revisions.md) ; sections commentées par groupe de ressources ;
     aucune sortie inutilisée ; aucune valeur propre à une cible.
   - `main.<cible>.bicepparam` : `using 'main.bicep'` ; **toutes** les valeurs effectives de toutes les
     propriétés applicables ([RG-RES-02](../specs/14-modele-des-ressources.md)), noms en clair, tags effectifs
     ([RG-PRJ-05/06](../specs/11-projets-et-environnements.md)), présence. Forme de l'extrait de
     [90 § 3.3](../specs/90-projet-de-reference.md).
3. Container App `ca api` : secret Key Vault (`keyVaultUrl` + identité `id api`) référencé par `secretRef` pour
   `Payments__ApiKey` ([17 § 5](../specs/17-parametres-applicatifs-et-secrets.md)) ; registre tiré par `id api` ;
   identité système **et** `id api` ; image lue au déploiement : paramètre `caApi.image` vide dans le
   `.bicepparam`, renseigné par la release (image en service, sinon image de démarrage de `pins.json`) — c'est
   [RG-APP-02](../specs/19-applications-build-et-deploiement.md).
4. `orders/infra/scripts/data-access.sql` : script idempotent de [RG-LIA-21](../specs/16-liaisons-identites-et-acces.md)
   pour Azure SQL, paramétré par variables `sqlcmd` (`$(PrincipalName)`, `$(ClientId)`, `$(Roles)`) :
   `CREATE USER … WITH SID = <SID dérivé de l'identifiant client>, TYPE = E` si absent, marque
   `EXEC sp_addextendedproperty 'ifs-managed', 'true', 'USER', …`, ajustement des rôles au niveau voulu,
   retrait des rôles en trop **seulement** pour les utilisateurs marqués. Plus `data-access-remove.sql` pour
   [RG-LIA-23](../specs/16-liaisons-identites-et-acces.md).
5. `<composant>/infra/release.<cible>.json` : données de la release, schéma
   `reference/release-module/schemas/release-data.schema.json` (à écrire dans cette étape) :
   ```json
   {
     "schema": "ifs-release/v1",
     "project": "shop", "component": "orders", "target": "dev",
     "unit": "ifs-shop-orders-dev",
     "subscriptionId": "<A>", "location": "francecentral", "protected": false,
     "deploymentIdentityObjectId": null,
     "dependencies": [ { "component": "core", "resources": [ { "id": "/subscriptions/<A>/resourceGroups/rg-shop-core-main-dev/providers/Microsoft.KeyVault/vaults/kv-shop-main-dev" } ] } ],
     "secretWrites": [],
     "secretReferences": [ { "vault": "kv-shop-main-dev", "secret": "payments-api-key", "writer": "core" } ],
     "appOwnedState": [ { "resourceId": "…/containerApps/ca-shop-api-dev", "parameter": "caApi.image", "kind": "containerAppImage", "bootstrapImage": "<pins>" } ],
     "dataAccess": [ { "server": "sql-shop-orders-dev.database.windows.net", "database": "sqldb-shop-orders-dev",
                       "principal": { "resourceId": "…/containerApps/ca-shop-api-dev", "kind": "systemAssigned" },
                       "level": "ReadWrite", "exposure": "Public" } ],
     "accessObjectTypes": [ "Microsoft.Authorization/roleAssignments", "Microsoft.Insights/diagnosticSettings" ],
     "restartOnSecretChange": [ "…/containerApps/ca-shop-api-dev" ]
   }
   ```
   **Contrat des secrets** (corrige l'ambiguïté écrivain / consommateur) : `secretWrites` = secrets que **ce** composant
   écrit (`{ variable, vault, secret, source: "pipeline" | "generated" }`) — présent seulement dans le
   `release.<cible>.json` du composant écrivain, ici `core` (`[{ "variable": "MAIN_PAYMENTS_API_KEY", "vault":
   "kv-shop-main-dev", "secret": "payments-api-key", "source": "pipeline" }]`) ; `secretReferences` = secrets que ce
   composant consomme sans les écrire, vérifiés **par leur existence dans le coffre**, jamais par la valeur : `orders`
   ne reçoit donc jamais `MAIN_PAYMENTS_API_KEY`.
   `deploymentIdentityObjectId` est `null` dans la sortie de référence ; la release le lit dans l'identité du
   run (`az ad signed-in-user` n'existe pas pour un principal de service : utiliser
   `az account show --query user.name` puis `az ad sp show --id`).
6. Vérifier chaque composant hors ligne : `bicep build main.bicep`, `bicep build-params main.<cible>.bicepparam`,
   `bicep lint main.bicep` (zéro avertissement), `bicep format --stdout main.bicep` identique au fichier.
   Script `reference/tools/Test-ReferenceBicep.ps1` qui fait tout cela pour tous les composants.

✅ **Vérification automatique.** `pwsh reference/tools/Test-ReferenceBicep.ps1` → code 0, zéro avertissement.

🧪 **Test manuel.** Ouvrir `reference/pilot/bicep-azdo/orders/infra/main.dev.bicepparam` et le comparer au tableau
[`reference-pilote.md` § 2.1](reference-pilote.md#21-noms-azure) : chaque nom Azure apparaît en clair, à l'identique ;
aucune valeur `dev` dans `main.bicep` (recherche « dev » dans le fichier → seulement dans les commentaires).

🧠 **Mémoire.** `02-project-structure.md` (`reference/`), `05-data-and-storage.md` (format `release.<cible>.json`).

### P-03 — Module de release PowerShell

| | |
|---|---|
| **Spécifications** | [22 § 3.1](../specs/22-pipelines.md) (étapes 1–13), [DEC-85](../specs/03-decisions.md), [DEC-86](../specs/03-decisions.md), [DEC-87](../specs/03-decisions.md), [DEC-90](../specs/03-decisions.md), [DEC-91](../specs/03-decisions.md), [DEC-100](../specs/03-decisions.md), [DEC-102](../specs/03-decisions.md), [RG-PAR-14/15/17](../specs/17-parametres-applicatifs-et-secrets.md), [RG-NET-03](../specs/18-reseau-et-exposition.md), [91 T01–T04, T07, T16](../specs/91-scenarios-critiques.md) |
| **Technique** | [DT-17](../technique/01-decisions.md#dt-17--logique-de-release-en-module-powershell-livré-au-client) |
| **Dépend de** | P-02 |
| **Commit** | `feat(reference): module de release avec journal d'opérations et reprise` |

🎯 **Objectif.** Le cœur de la promesse « livraison sûre » : un module lisible qui exécute la release d'une
unité de déploiement dans une cible, reprenable à tout moment, sans doublon.

🔧 **À faire.**
1. `reference/pilot/bicep-azdo/.ifs/templates/scripts/IfsRelease.psm1` (PowerShell 7, `Set-StrictMode -Version
   Latest`, `$ErrorActionPreference = 'Stop'`), fonctions publiques exportées et documentées (aide
   `.SYNOPSIS` en français, lisible par le client) :

   | Fonction | Étape de [22 § 3.1](../specs/22-pipelines.md) | Contrat |
   |---|---|---|
   | `Read-IfsReleaseData` | — | Lit et valide `release.<cible>.json` (schéma) |
   | `Read-IfsOperationJournal` | 1 (Aperçu) | **Lecture seule**, sans bail : un journal absent est un journal vide ; aucun appel d'écriture ([22 § 3.1](../specs/22-pipelines.md) : l'Aperçu ne modifie rien) |
   | `Open-IfsOperationJournal` | 6 (Déploiement) | Crée le blob `ifs-operations/<unit>.json` du compte `stifs<projet><cible>` s'il n'existe pas, prend un bail exclusif de 60 s renouvelé toutes les 30 s par une tâche de fond jusqu'à `Close-IfsOperationJournal` (dans le `finally`) ; si le processus propriétaire meurt, le renouvelleur s'arrête et le bail expire au plus tard 60 s après son dernier renouvellement ; un bail tenu par un autre run → attente bornée (10 min) puis échec explicite |
   | `Get-IfsPendingOperation` | 1, 7 | Opérations `ToDo` ou `Started` d'une exécution précédente |
   | `Test-IfsDependency` | 2 | Chaque ressource de `dependencies` existe (`az resource show --ids`), sinon erreur « Déployez d'abord <composant> en <cible>. » |
   | `Test-IfsSecretVariable` | 3 | Chaque `secretWrites.variable` de source `pipeline` a une valeur dans l'environnement de l'étape (mappée par le YAML), sinon erreur listant les variables vides et le groupe `ifs-<projet>-<cible>` |
   | `Test-IfsSecretReference` | 3 | Chaque `secretReferences` existe dans son coffre (`az keyvault secret list --vault-name … --query "[?name=='…'].id"`, sans lire la valeur) ; sinon erreur « Déployez d'abord <writer> en <cible> » |
   | `Invoke-IfsPreview` | 4 | What-if de portée abonnement + comparaison avec les ressources gérées par la pile (`az stack sub show`) ; produit `ifs-preview.json` et un résumé Markdown en sections : opérations reprises ; créées / modifiées / recréées ; détachées ou supprimées ; **accès révoqués** ; écritures de plan de données ; limites de l'aperçu |
   | `Get-IfsEffectFingerprint` | 4, 5 | SHA-256 du JSON canonique des effets, **hors** état appartenant au pipeline applicatif (`appOwnedState`) — [DEC-102](../specs/03-decisions.md) |
   | `Read-IfsAppOwnedState` | 5 | Image en service (et, plus tard, trafic) relue sous verrou ; image de démarrage si la ressource n'existe pas |
   | `Add-IfsOperation`, `Set-IfsOperationState` | 6, 7, 10, 11 | Identifiant stable `sha256(unit|target|kind|objectId)` ; états `ToDo` → `Started` → `Done` ; `Done` avec note « déjà absent » si l'objet n'existe plus ([RG-PIP-12](../specs/22-pipelines.md)) |
   | `Invoke-IfsStackDeployment` | 9 | `az stack sub create --action-on-unmanage detachAll`, `--deny-settings-mode denyDelete` et `--deny-settings-excluded-principals <identité de déploiement>` si la cible est protégée, sinon `none` ; renvoie les ressources détachées |
   | `Invoke-IfsRevocation` | 10 | Supprime chaque objet d'autorisation sorti de la pile (types `accessObjectTypes`), même en cible protégée |
   | `Invoke-IfsSecretWrite` | 8, 11 | Écrit un secret seulement si la valeur a changé ; redémarre `restartOnSecretChange` |
   | `Invoke-IfsDataAccess` | 11 | Règle de pare-feu temporaire `ifs-temp-<runId>` si exposition restreinte (et nettoyage des `ifs-temp-*` > 2 h) ; jeton `az account get-access-token --resource https://database.windows.net/` ; exécute `data-access.sql` avec le module `SqlServer` (`Invoke-Sqlcmd -AccessToken`, version épinglée dans `pins.json`) ; réessais 10 min pour les droits tout juste attribués |
   | `Write-IfsReleaseReport` | 13 | `ifs-report.json` : schéma `ifs-report/v1` (révision et commit lus dans `.ifs/manifest.json`, empreinte du manifeste, cible, étapes, copie du journal, ressources détachées, accès révoqués, résultat) ; appelé dans un `finally` |
2. Injection de panne **pour les preuves** : si la variable d'environnement `IFS_TEST_ABORT_AFTER` vaut
   `deploy-unit`, le module termine le processus (`[Environment]::Exit(137)`) juste après l'étape 9, sans
   rapport. Documenté dans l'aide comme « réservé aux tests ; sans effet si absent ». (Son maintien dans la
   sortie générée sera tranché au verrou R-03.)
3. Scripts appelés par les pipelines : `Invoke-IfsInfraPreview.ps1` (étapes 1–4, publie `ifs-preview` en
   artefact et le résumé dans le run), `Invoke-IfsInfraDeploy.ps1` (étapes 5–13). L'étape 5 a **deux** contrôles, dans cet ordre :
   (a) **révision plus récente** ([91 T04, variante](../specs/91-scenarios-critiques.md)) : lire `.ifs/manifest.json`
   à la tête de la branche par défaut (`git fetch origin <branche> && git show origin/<branche>:<chemin>`) ; si sa
   révision est plus récente que celle de l'artefact du run **et** que l'empreinte des fichiers de cette unité y diffère,
   arrêt sans mutation : « Une révision plus récente (<n>) modifie ce composant ; la release qu'elle a déclenchée
   l'appliquera après une nouvelle approbation. » ;
   (b) **effets** ([DEC-87](../specs/03-decisions.md), [DEC-102](../specs/03-decisions.md)) : l'aperçu est recalculé
   **sur l'artefact figé du run** contre l'état Azure courant ; empreinte différente de celle approuvée → arrêt :
   « Les effets ont changé depuis l'aperçu approuvé ; relancez la release pour une nouvelle approbation. »
   `Invoke-IfsAppDeploy.ps1` (vérification préalable [RG-APP-16](../specs/19-applications-build-et-deploiement.md),
   nouvelle révision avec l'image par empreinte, contrôle de santé 2xx sous 5 min, `ifs-app-report.json`).
4. Schémas JSON sous `reference/release-module/schemas/` : `release-data`, `operation-journal`, `ifs-report`,
   `ifs-preview`, `ifs-app-report`.
5. Tests Pester 5 dans `reference/release-module/tests/` — `az` est **simulé** (fonction `az` remplacée par un
   faux qui enregistre les appels et rejoue des réponses JSON de `tests/fixtures/`) :
   - l'Aperçu, journal absent compris, ne fait **aucun** appel d'écriture ni de bail (le faux `az` échoue si on en fait) ;
   - le journal est écrit **avant** la première mutation ; une reprise ne duplique aucune opération ;
   - `orders` (références seulement) n'exige aucune variable secrète ; `core` échoue à l'étape 3 si `MAIN_PAYMENTS_API_KEY` est vide ;
   - une révision plus récente fusionnée pendant l'attente d'approbation et qui change les fichiers de l'unité → arrêt à l'étape 5 sans mutation ;
   - T02 : journal avec une révocation `ToDo` → la release suivante la fait **en premier** ;
   - T03 : échec du déploiement de la pile → rapport « partiellement appliquée » avec les ressources créées ;
   - T04 : image en service différente de celle de l'aperçu → empreinte inchangée, image reconduite ;
     changement d'un rôle → empreinte différente → arrêt sans mutation ;
   - T16 : opérations de la révision 3 au journal, release de la révision 4 → reprises citées avec leur révision ;
   - secret inchangé → aucune écriture, aucun redémarrage ;
   - objet déjà absent → `Done` « déjà absent » ;
   - variable de secret vide → échec à l'étape 3, aucune mutation ;
   - rapport toujours écrit, sauf injection de panne.
6. Analyse : `Invoke-ScriptAnalyzer -Recurse -Settings PSGallery` sans erreur ni avertissement sur
   `.ifs/templates/scripts/`.

✅ **Vérification automatique.** `pwsh -c "Invoke-Pester reference/release-module/tests -CI"` vert ;
PSScriptAnalyzer propre.

🧪 **Test manuel.** `pwsh -c "Import-Module ./reference/pilot/bicep-azdo/.ifs/templates/scripts/IfsRelease.psm1; Get-Help Invoke-IfsPreview -Full"`
→ une aide en français, compréhensible sans connaître IFS.

🧠 **Mémoire.** `08-runtime-and-orchestration.md` (module de release : rôle, schémas, tests).

### P-04 — Pipelines Azure DevOps du projet pilote

| | |
|---|---|
| **Spécifications** | [22](../specs/22-pipelines.md) (§ 2, 3, 4, 6 colonne Azure DevOps), [19 § 5, 8](../specs/19-applications-build-et-deploiement.md), [DEC-101](../specs/03-decisions.md), [RG-SUI-02](../specs/28-suivi-des-deploiements.md) |
| **Dépend de** | P-03 |
| **Commit** | `feat(reference): pipelines Azure DevOps du projet pilote` |

🎯 **Objectif.** Les pipelines PR, CI et Release de chaque composant et de l'application, autonomes dans leur
destination ([RG-PIP-01](../specs/22-pipelines.md)).

🔧 **À faire.**
1. Modèles partagés `.ifs/templates/` : `infra-pr.yml` (contrôles du langage, sans connexion Azure),
   `infra-ci.yml` (contrôles + artefact `infra`), `infra-release.yml` (paramètres : composant, liste ordonnée
   des cibles avec `protected`), `infra-target-stages.yml` (stage `Preview_<cible>` : job **sans
   environnement**, connexion `ifs-<projet>-<cible>`, `Invoke-IfsInfraPreview.ps1` ; stage `Deploy_<cible>` :
   job de déploiement sur l'environnement `<projet>-<cible>`, connexion `ifs-<projet>-<cible>`,
   `Invoke-IfsInfraDeploy.ps1` ; le groupe de variables `ifs-<projet>-<cible>` est lié aux **deux** stages
   (`Preview_<cible>` et `Deploy_<cible>`) **seulement** dans les pipelines du composant écrivain, et chaque variable de
   `secretWrites` est mappée explicitement dans le `env:` des seules étapes qui l'utilisent (contrôle de l'étape 3 en
   Aperçu, écriture des étapes 8/11 en Déploiement) ; les composants consommateurs n'ont ni groupe ni mapping), `app-pr.yml`, `app-ci.yml` (build `buildx`, scan Trivy bloquant à `CRITICAL`,
   cache, poussée avec la connexion `ifs-<projet>-<cible du registre>-app`, tag `<build>-<sha court>`,
   publication de l'**empreinte** de l'image en artefact), `app-release.yml`, `app-target-stage.yml`
   (environnement `<projet>-<cible>`, connexion applicative, `Invoke-IfsAppDeploy.ps1`).
2. Pipelines par composant (`<composant>/infra/pipelines/{pr,ci,release}.yml`) et par application
   (`orders/apps/api/pipelines/{pr,ci,release}.yml`) : déclencheurs par chemins ([RG-PIP-09](../specs/22-pipelines.md)),
   enchaînement CI → Release par ressource de pipeline, nom d'exécution lu dans le manifeste à l'exécution
   (`<projet> · <composant> · rev <n> · <cible>` — étape `Set-IfsRunName` dans les modèles, [RG-SUI-02](../specs/28-suivi-des-deploiements.md)),
   en-tête de [RG-GEN-08](../specs/21-generation-et-revisions.md), une étape commentée par bloc.
3. Exécuteurs : `vmImage: ubuntu-latest` (aucun exécuteur déclaré dans le pilote).
4. Validation hors ligne : `reference/tools/Test-ReferencePipelines.ps1` valide chaque YAML contre le schéma
   Azure Pipelines épinglé (`reference/tools/schemas/azure-pipelines.json`, téléchargé depuis le dépôt
   `microsoft/azure-pipelines-vscode`, fichier `service-schema.json`, version notée dans `pins.json`) après
   conversion YAML → JSON (module `powershell-yaml` épinglé) ; vérifie que chaque modèle référencé existe.

✅ **Vérification automatique.** `pwsh reference/tools/Test-ReferencePipelines.ps1` → code 0.

🧪 **Test manuel.** Lire `orders/infra/pipelines/release.yml` puis `.ifs/templates/infra-target-stages.yml` :
pour `prd`, le stage `Preview_prd` n'a pas d'environnement et le stage `Deploy_prd` utilise
l'environnement `shop-prd` ; le secret `MAIN_PAYMENTS_API_KEY` n'apparaît que dans `core` (écrivain).

🧠 **Mémoire.** `08-runtime-and-orchestration.md` (pipelines de référence).

### P-05 — Kit d'installation du projet pilote

| | |
|---|---|
| **Spécifications** | [23](../specs/23-kit-installation.md) (§ 2, 3.1, 3.4, 4), [RG-LIA-18](../specs/16-liaisons-identites-et-acces.md), [DEC-88](../specs/03-decisions.md), [DEC-99](../specs/03-decisions.md), [DEC-101](../specs/03-decisions.md), [DEC-111](../specs/03-decisions.md), [EXG-20](../specs/27-exigences-non-fonctionnelles.md) |
| **Dépend de** | P-04 |
| **Commit** | `feat(reference): kit d'installation du projet pilote` |

🎯 **Objectif.** Trois actions humaines suffisent : exécuter le script Azure, exécuter le pipeline
d'installation, saisir le secret.

🔧 **À faire.**
1. `.ifs/install/azure-setup.ps1` : paramètres `-AzureDevOpsOrganization`, `-AzureDevOpsProject`,
   `-TenantId`, `-WhatIf` ; pour chaque cible (`dev`, `prd`, `shared`) les étapes 1–6 et 8 de
   [23 § 2](../specs/23-kit-installation.md) (groupe `rg-ifs-shop-<cible>`, identités `id-ifs-deploy-shop-<cible>`
   et `id-ifs-app-shop-<cible>`, identifiants fédérés des deux service connections, droits — dont **RBAC
   Administrator conditionné** à la liste exacte de rôles de [reference-pilote](reference-pilote.md) et
   Azure Deployment Stack Owner —, adhésion au groupe `sg-shop-sql-admins` ou étape « à faire » avec la
   commande exacte, stockage technique `stifsshop<cible>` avec `ifs-operations`, service connections
   fédérées via `az devops service-endpoint create`) ; marque `managed-by: infraflowsculptor` et version du
   kit (tag `ifs-kit-revision`) ; refus d'un kit plus ancien ([RG-INS-07](../specs/23-kit-installation.md)) ;
   suppression des seuls identifiants fédérés IFS obsolètes (`ifs-ado-*`) et conservation des identifiants étrangers
   ([RG-INS-05](../specs/23-kit-installation.md)) ; rapport final
   par cible ([RG-INS-02](../specs/23-kit-installation.md)) ; noms techniques assainis et raccourcis de façon
   déterministe ([RG-INS-03](../specs/23-kit-installation.md)). Les groupes des composants sont créés en amont
   uniquement s'ils sont une portée d'attribution déléguée du plan RG-LIA-18 ; leur nom, région et tags viennent
   de `resourceGroups.main`. Un groupe existant doit avoir la région attendue et les tags `managed-by`,
   `ifs-project`, `ifs-component` et `ifs-environment` exacts. En cas d'écart, l'installation s'arrête avant toute
   création de groupe ; le groupe `data`, sans rôle délégué, reste créé par Bicep.
2. `.ifs/install/install.pipeline.yml` : étapes 1–6 bis, 8 et 9 de [23 § 3.1](../specs/23-kit-installation.md)
   par l'API REST Azure DevOps avec `$(System.AccessToken)` : environnements, approbations (groupe
   « Shop Release Approvers »), **contrôle de verrou exclusif** `lockBehavior: sequential`
   ([RG-PIP-05](../specs/22-pipelines.md)), groupes de variables (variable secrète créée vide, jamais modifiée),
   définitions de pipelines dans `\shop\<composant>[\<application>]`, contrôles des connexions (branche,
   modèle requis, approbation sur la connexion applicative protégée), autorisations, politique de validation
   de build sur `main` ; vérification de préparation par cible ([DEC-111](../specs/03-decisions.md)) ; rapport.
3. `.ifs/install/SETUP.md` : liste de contrôle de [23 § 4](../specs/23-kit-installation.md), commandes exactes,
   état de chaque étape (automatique / à faire).
4. Tests Pester (`reference/release-module/tests/Kit.Tests.ps1`), `az` simulé : création de groupes distincts avec
   leur nom, région et tags exacts ; idempotence à la seconde exécution ; refus des groupes existants dont la région,
   la marque `managed-by` ou les tags IFS sont absents ou incorrects, sans mutation ; `data` n'est pas précréé ;
   `-WhatIf` ne fait aucun appel d'écriture ; seul un identifiant fédéré IFS obsolète est supprimé et les identifiants
   étrangers sont conservés ; kit plus ancien refusé ; condition RBAC contient exactement les rôles attendus.

✅ **Vérification automatique.** Pester vert ; PSScriptAnalyzer propre ; `Test-ReferencePipelines.ps1` inclut
`install.pipeline.yml`.

🧪 **Test manuel.** Lire `SETUP.md` comme un client qui ne connaît pas IFS : chaque étape « à faire » dit qui doit
la faire, avec quels droits, et donne la commande exacte.

🧠 **Mémoire.** `08-runtime-and-orchestration.md` (kit).

### P-06 — README.ifs.md, manifeste d'exemple et contrôles en CI

| | |
|---|---|
| **Spécifications** | [DEC-75](../specs/03-decisions.md), [RG-PUB-08](../specs/24-depots-et-publication.md), [RG-GEN-03](../specs/21-generation-et-revisions.md) |
| **Dépend de** | P-05 |
| **Commit** | `feat(reference): documentation générée, manifeste et contrôles en CI` |

🎯 **Objectif.** La sortie de référence est complète et vérifiée à chaque push.

🔧 **À faire.**
1. `README.ifs.md` : architecture (diagramme Mermaid des composants et liaisons), noms par cible, ordre de
   déploiement, comment déployer, comment revenir en arrière, points d'extension et leur contrat
   ([RG-GEN-16](../specs/21-generation-et-revisions.md)).
2. `reference/pilot/manifest.example.json` (**hors** de `reference/pilot/bicep-azdo/`, qui ne contient que la sortie
   générée, comparée intégralement et sans exception) : projet, révision `1`, liste des fichiers gérés avec SHA-256 ;
   script `reference/tools/Update-ManifestExample.ps1` qui le recalcule.
3. `reference/tools/Test-ReferenceDeterminism.ps1` : fins de ligne `LF`, UTF-8 sans BOM, ligne finale, en-tête
   présent sur chaque fichier commentable.
4. CI : job `release-module` (Pester, PSScriptAnalyzer, `Test-Reference*.ps1`, Bicep CLI installée).
5. `reference/README.md` : rôle de ce dossier (vérité des émetteurs), règle de modification (justifiée en
   demande de revue).

✅ **Vérification automatique.** Job `release-module` vert sur GitHub.

🧪 **Test manuel.** Ouvrir `README.ifs.md` dans l'aperçu Markdown de Rider ou de GitHub : le diagramme s'affiche,
les noms correspondent à [`reference-pilote.md` § 2.1](reference-pilote.md#21-noms-azure).

🧠 **Mémoire.** `02-project-structure.md`.

### 🔒 R-02 — Revue de la sortie de référence (avant Azure)

**Périmètre** : `P-01` à `P-06`. **Branche** : `impl/preuves` → pull request `[R-02] Sortie de référence du pilote`.

**Claude vérifie en particulier** : sécurité du kit (portées des droits, condition RBAC, fédérations,
`denyDelete` et exclusions, aucun secret dans un fichier, aucune connexion Azure dans les pipelines de PR),
ordre des étapes de la release et écriture du journal **avant** toute mutation, empreinte des effets,
idempotence, lisibilité pour un client ([P10](../specs/01-principes.md)), conformité aux formes de [21 § 6.2](../specs/21-generation-et-revisions.md).

**Recette utilisateur** : aucune (rien n'est encore déployé) ; lecture de `SETUP.md` (P-05 🧪).

**Après approbation** : la pull request reste ouverte (même segment) ; Luna continue avec `P-07` sur la même
branche.

### P-07 — Outillage des preuves et recette pas à pas

| | |
|---|---|
| **Spécifications** | [04 § 2.0](../specs/04-perimetre-et-lots.md) (séquence de démonstration), [91](../specs/91-scenarios-critiques.md) |
| **Dépend de** | R-02 |
| **Commit** | `docs(preuves): outillage et recette des preuves P1 à P9` |

🎯 **Objectif.** Vous pouvez exécuter chaque preuve sans rien deviner.

🔧 **À faire.**
1. `tools/proofs/Publish-PilotReference.ps1` : paramètres `-RepositoryPath` (clone local de votre dépôt Azure
   Repos `shop`), `-ProjectCode` (défaut `shop`), `-SubscriptionDev`, `-SubscriptionPrd`, `-SubscriptionShared`,
   `-SqlAdminGroupObjectIdDev/Prd` ; copie uniquement les fichiers suivis de `reference/pilot/bicep-azdo/**` et
   `samples/witness-app/**` (vers `src/api/`), remplace les valeurs **par la table explicite** des noms de
   [reference-pilote § 2.1](reference-pilote.md#21-noms-azure) (pas de remplacement global de « shop »), recalcule
   `.ifs/manifest.json` avec empreintes par composant et filiation `baseline`, puis affiche le `git status` sans
   commiter. Une publication réelle exige les deux répertoires source propres et un clone cible propre ; `-WhatIf`
   reste utilisable sur un arbre source modifié et n'écrit que dans un répertoire temporaire.
2. `tools/proofs/New-ProofRevision.ps1` : valide la révision et la filiation déclarées dans `revision.json`, vérifie
   que les changements du clone sont couverts par les empreintes du manifeste, puis applique au clone les modifications
   **écrites à la main** qui simulent les révisions 2 à 5 des critères 6–12 de
   [reference-pilote § 4](reference-pilote.md#4-critères-dacceptation-du-jalon-0) (fichiers fournis dans
   `reference/pilot/revisions/rev<n>/`), ainsi que les overlays indépendants `p2` et `p3-role` des critères 8 et 9a.
   Le manifeste recalculé conserve une empreinte logique indépendante des fins de ligne Windows `autocrlf`.
3. `docs/plan/recettes/01-preuves.md` : la recette complète, dans l'ordre P1, P9, P3, P8, P2, P4, P5, P7 ; pour
   chacune : état initial, actions (portail, Azure DevOps, commandes exactes), résultat attendu, preuve à
   recueillir (lien du run, extrait du rapport, capture) ; durée estimée ; nettoyage.
3 bis. `tools/proofs/Save-AdoRecordings.ps1` : pour un run Azure DevOps donné (organisation, projet, identifiant), conserve
     les réponses JSON brutes avant leur anonymisation pour préserver les timestamps textuels, puis écrit de manière
     transactionnelle les réponses build, timeline, liste et contenu textuel des artefacts `ifs-report`, `ifs-preview`,
     `ifs-app-report`, et un instantané des approbations du projet dans
     `src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/<scénario>/`. Les pseudonymes
     déterministes préservent les relations répétées et un scan final refuse les identifiants ou secrets résiduels.
     Le schéma API des approbations ne fournit pas de RunId : l'instantané est contextualisé par le run demandé, mais
     les approbations ne lui sont pas attribuées automatiquement.
   La recette demande de l'exécuter après chaque preuve (release réussie, partiellement appliquée, interrompue, commit de
   fusion différent, attente d'approbation).
4. `docs/plan/preuves/resultats.md` : tableau `Preuve | Date | Résultat (OK/KO) | Preuve recueillie |
   Remarques`, vide.
5. Reporter les constats mineurs de R-02 selon la revue approuvée : aucun nouveau constat mineur n'a été relevé
   ([R-02](revues/R-02-revue.md#constats)); il n'y a donc aucun point mineur à reprendre dans P-07/P-08.

✅ **Vérification automatique.** Analyse statique PowerShell, fixture d'anonymisation (timestamps, secrets, identifiants
liés et relations `parentId`), `Publish-PilotReference.ps1 -WhatIf` avec contrôle que la cible reste inchangée, chaîne
`p2 → rev3 → rev4 → rev5` sur des clones temporaires, rejet des filiations incorrectes et des fichiers parasites,
et application d'overlay après un checkout forcé `core.autocrlf=true`.

🧪 **Test manuel.** Lire `recettes/01-preuves.md` ; vérifier que chaque prérequis est à votre portée (droits
Owner sur les abonnements, administrateur de projet Azure DevOps, groupe Entra créable).

🧠 **Mémoire.** `08-runtime-and-orchestration.md` (outillage des preuves).

📌 **Hors dépôt.** Abonnements, organisation et projet Azure DevOps, groupes Entra créés : dans `NEXT.md`.

### P-08 — Exécution des preuves (accompagnement)

| | |
|---|---|
| **Spécifications** | [04 § 7.2](../specs/04-perimetre-et-lots.md) |
| **Dépend de** | P-07 |
| **Commit** | `fix(reference): <correction>` (une par défaut constaté), puis `docs(preuves): résultats` |

🎯 **Objectif.** Les preuves sont exécutées par vous ; Luna corrige les défauts **d'exécution** (faute de frappe,
paramètre d'API, version de commande `az`) constatés pendant la recette.

🔧 **À faire.**
1. `python tools/plan/gate.py wait-recette P-08` (statut `EN_ATTENTE_DE_RECETTE`) puis arrêt. Conduite unique à chaque
   nouvelle session :
   - l'utilisateur ne transmet rien → `gate.py check` renvoie 3 : s'arrêter ;
   - l'utilisateur transmet des résultats (KO, partiels ou « recette terminée ») → `gate.py resume P-08`, traiter ces
     résultats (point 2), les consigner, enregistrer les réponses Azure DevOps fournies (point 3 bis de P-07) ; puis
     `gate.py wait-recette P-08` s'il en reste, ou `gate.py done P-08` si l'utilisateur a déclaré la recette terminée.
2. Pour chaque KO transmis (journal du run, message) : si la cause est une erreur de mise en œuvre de la sortie
   de référence (le comportement attendu est clair dans la spec et le plan), corriger, ajouter le test Pester
   qui l'aurait détectée, commiter `fix(reference): …` ; si la cause est une question de conception, ne rien
   corriger : question pour Claude.
3. Consigner chaque résultat transmis dans `docs/plan/preuves/resultats.md`.

✅ **Vérification automatique.** Après chaque correction : `Test-Reference*.ps1` et Pester verts.

🧪 **Test manuel.** La recette [`recettes/01-preuves.md`](recettes/01-preuves.md), en entier.

🧠 **Mémoire.** `08-runtime-and-orchestration.md` : ce que les preuves ont appris (limites du what-if des piles,
délais de propagation RBAC observés, temps de chaque release).

📌 **Hors dépôt.** Ressources Azure créées par les preuves (à supprimer ou garder pour J0) : dans `NEXT.md`.

### 🔒 R-03 — Revue des preuves

**Périmètre** : `P-07`, `P-08`, et les résultats de `docs/plan/preuves/resultats.md`.

**Claude** : analyse chaque KO, décide des changements de conception (nouvelles `DT-nn`, corrections de la sortie
de référence en tâches `R-03-Cn`), décide du sort de `IFS_TEST_ABORT_AFTER` dans la sortie générée, met à jour le
statut des preuves dans [04 § 7.2](../specs/04-perimetre-et-lots.md) (« prototypée » si la preuve est passée sur la
sortie écrite à la main ; « vérifiée » viendra quand IFS la générera, au jalon 0), puis vérifie que le jalon 0
reste exécutable tel qu'écrit.

**Recette utilisateur** : [`recettes/01-preuves.md`](recettes/01-preuves.md) complète.

**Après approbation** : vous fusionnez ; branche suivante `impl/j0-acces`.
