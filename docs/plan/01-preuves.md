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

🎯 **Objectif.** Après le feu vert de l'utilisateur du 2026-10-07, Luna exécute les préflights, les preuves, les captures
Azure DevOps et leur consignation. Luna corrige les défauts **d'exécution** ; une question de conception reste soumise à
la revue Claude. Une intervention de l'utilisateur n'est demandée que si une étape exige son authentification ou une
action impossible à réaliser avec les accès déjà ouverts.

🔧 **À faire.**
0. **Profil de coût des preuves** ([DT-42](../technique/01-decisions.md#dt-42--coût-des-preuves-éphémères-azure),
   [reference-pilote § 1.6](reference-pilote.md#16-profil-de-coût-des-preuves)), **hors ligne et avant la recette** (à
   faire quand l'utilisateur relance P-08 par `gate.py resume P-08` ; ne pas toucher au statut autrement) :
   1. Aligner sur `francecentral` tous les champs de région des ressources Azure dans `reference/pilot/bicep-azdo/**`,
      y compris le `--location` des What-If d'installation, les paramètres Bicep, les fichiers de release, les overlays
      de révision, les fixtures et les tests qui les affirment. Les objets Azure DevOps et Entra sans région restent
      inchangés. Recalculer ensuite `reference/pilot/manifest.example.json`
      (`reference/tools/Update-ManifestExample.ps1`). Commit `fix(reference): région francecentral des cibles du pilote`.
   2. `tools/proofs/Publish-PilotReference.ps1` : appliquer, **à la publication seulement**, le profil du § 1.6 (SKU ACR,
      SKU et paramètres SQL, zone du CAE, réplicas, rétention `appi`, plafond Log Analytics) ; `reference/pilot/` ne reçoit
      que la région. Les propriétés SQL ajoutées passent par `types.bicep`/`main.bicep` **du clone** (module AVM
      `sql/server:0.22.0` : `autoPauseDelay`, `minCapacity`, `zoneRedundant`, `useFreeLimit`, `freeLimitExhaustionBehavior`),
      lues en option `-SqlFreeOffer` (faux par défaut, **prd seulement** ; dev reste serverless payant). Commit
      `feat(preuves): profil de coût des preuves`.
   3. Tests : `Publish-PilotReference.ps1 -WhatIf` affiche chaque substitution du profil ; Pester publie un clone temporaire,
      applique `p2 → rev3 → rev4 → rev5`, `rev2` et `p3-role` sur un clone **profilé** ; le profil est idempotent et ne
      laisse aucune écriture partielle si une valeur est inattendue ; `bicep build` du clone profilé sans avertissement ;
      tous les emplacements Azure du clone valent `francecentral` et aucun emplacement ne vaut `northeurope` ;
      `GP_Gen5_2` et `Standard` (ACR) sont absents du clone.
0 bis. **Contrat d'empreinte approuvée/appliquée** ([DT-43](../technique/01-decisions.md#dt-43--empreinte-dapercu-approuvée-liée-à-lempreinte-appliquée)),
   **hors ligne, avant le nouveau run P8** (le run #740 est fonctionnellement réussi mais sa preuve est incomplète : son
   `ifs-report` n'a pas d'`approvedPreviewFingerprint`). **Test d'abord**, un test rouge par comportement, puis
   l'implémentation minimale. Fichiers concernés, sans autre :
   - code exécuté : `reference/pilot/bicep-azdo/.ifs/templates/scripts/IfsRelease.psm1` (`Invoke-IfsInfraDeploy`,
     `Write-IfsReleaseReport`) ;
   - schéma v1 **conservé sans modification** : `reference/pilot/bicep-azdo/.ifs/templates/schemas/ifs-report.schema.json` et sa
     copie `reference/release-module/schemas/ifs-report.schema.json` ;
   - schéma v2 **ajouté** : `reference/pilot/bicep-azdo/.ifs/templates/schemas/ifs-report.v2.schema.json` (contenu exact :
     [DT-43 point 5](../technique/01-decisions.md#dt-43--empreinte-dapercu-approuvée-liée-à-lempreinte-appliquée)) ;
   - tests : `reference/release-module/tests/IfsRelease.Tests.ps1` (il importe le module du clone, mais valide aujourd'hui
     contre `reference/release-module/schemas/ifs-report.schema.json`).

   Il n'y a ni chargeur ni migration à écrire : `tools/proofs/AdoRecordingsTransport.psm1` télécharge les artefacts tels quels
   et aucun chargeur de rapports n'existe ; les captures existantes ne sont pas réécrites (DT-43 point 6).
   1. Test contrat du schéma (`IfsRelease.Tests.ps1`, `Test-Json -SchemaFile`) : contre `ifs-report.v2.schema.json`, un rapport
      sans `approvedPreviewFingerprint` est invalide, un rapport avec le champ est valide, un champ qui n'est pas 64 caractères
      hexadécimaux minuscules est invalide, un rapport dont `schema` vaut `ifs-report/v1` est invalide. Contre le schéma v1
      conservé (les deux fichiers), chaque artefact `ifs-report` existant sous
      `src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/` (dont #738 et #740) reste valide ;
      c'est le minimum qui garantit la lecture des captures v1. Le test qui valide aujourd'hui le rapport **produit** contre
      `reference/release-module/schemas/ifs-report.schema.json` valide désormais contre le schéma v2 du clone (celui que le
      code exécute).
   2. Test contrat du rapport (`Invoke-IfsInfraDeploy` avec `az` simulé, comme les tests existants) : `approvedPreviewFingerprint`
      est la copie littérale de `fingerprint` de l'aperçu approuvé fourni dans `-PreviewPath`, pour un rapport `Succeeded` **et**
      pour un rapport `FailedBeforeMutation` produit après lecture de cet aperçu ; `manifestFingerprint` garde sa valeur et son
      calcul (`Get-IfsEffectFingerprint` appliqué au manifeste) ; aucun test ne compare les deux champs. Un échec avant le `try`
      (aperçu approuvé illisible) n'écrit toujours aucun rapport (comportement actuel inchangé).
   3. Test de l'application (même base) : le contrôle existant est conservé. Si l'aperçu recalculé a une `fingerprint` différente
      de celle de l'aperçu approuvé, l'exception actuelle (« Les effets ont changé depuis l'aperçu approuvé… ») est levée,
      `result` vaut `FailedBeforeMutation`, aucune opération du journal n'est passée à `Started` ou `Done`, `stack sub create`
      n'est pas appelé ; si elles sont égales, l'application continue. Aucun nouveau code d'erreur. Si un test existant couvre
      déjà ce comportement, le conserver et ne pas le dupliquer.
   4. Implémentation minimale : créer `ifs-report.v2.schema.json` ; dans `IfsRelease.psm1`, ajouter à `Write-IfsReleaseReport` le
      paramètre obligatoire `-ApprovedPreviewFingerprint`, émettre `schema = 'ifs-report/v2'` et la propriété
      `approvedPreviewFingerprint`, valider via `Get-IfsSchemaPath 'ifs-report.v2.schema.json'`, adapter le message d'erreur et le
      commentaire qui citent `ifs-report/v1` ; dans le `finally` de `Invoke-IfsInfraDeploy`, passer `$approved.fingerprint`. Ne pas
      modifier la comparaison `$current.fingerprint -ne $approved.fingerprint`, ni `manifestFingerprint`, ni les schémas v1.
      Adapter tout autre appelant de `Write-IfsReleaseReport` trouvé par recherche dans le dépôt. Recalculer
      `reference/pilot/manifest.example.json` (`reference/tools/Update-ManifestExample.ps1`) car un fichier est ajouté sous
      `reference/pilot/bicep-azdo/`.
   5. Mettre à jour la recette ([§ 5](recettes/01-preuves.md#5-p8--livraison-applicative-pendant-lattente-dinfrastructure))
      seulement si l'implémentation change une commande qu'elle cite ; le clone publié se met à jour par l'étape 0 ter (pas
   de republication : `Publish-PilotReference.ps1` refuse un clone qui contient déjà les fichiers). Commit
      `fix(reference): lier l'empreinte approuvée à l'empreinte appliquée`. Ni rejouer #738, ni réapprouver #740, ni
      réécrire leurs captures : le parcours positif exige un **nouveau** run.
0 ter. **Mise à jour du clone publié `C:/src/shop42`** (arbitrage Claude du 2026-10-17 : **option A**, aucun nouvel outil, aucun
   overlay), **après** l'étape 0 bis commitée et **avant** le nouveau run P8. Ce n'est pas une étape de code : c'est une
   procédure d'exécution, et elle ne change aucun fichier du dépôt IFS.
   - **Pourquoi pas un outil ni un overlay.** (a) Le clone `shop42` est le seul à devoir être mis à jour : `shop43` et `shop44`
     sont des publications neuves depuis un commit qui contient déjà le correctif, et `shop42` est supprimé après P8
     ([DT-42](../technique/01-decisions.md#dt-42--coût-des-preuves-éphémères-azure) point 6) ; un outil serait écrit pour un seul usage.
     (b) Un overlay de `New-ProofRevision.ps1` simule une **révision du modèle** (il avance `revision` et `proofLineage`, il est
     lié aux critères 6–12) ; une correction de la référence n'est pas une révision, et un `changes.patch` sur
     `IfsRelease.psm1` dupliquerait un fichier généré. (c) Les fichiers du clone ne sont pas des copies de la source : la
     table de remplacement les transforme (`projet shop` → `projet shop42` dans l'en-tête de `IfsRelease.psm1`). On ne les
     reconstruit donc **pas à la main** : on les prend dans une **publication neuve** faite par le script existant.
   - **Amendement du 2026-10-17 (répétition générale, option B).** La répétition a montré que « équivalent à une publication
     neuve » est **faux** pour `shop42` : la publication neuve de `b5f617e` *ajoute* `.ifs/install/tests/IfsInstall.Tests.ps1`,
     *retire* `data/infra/server-diagnostics.bicep` (absent de la source IFS à `HEAD` et à `ca0baca`, mais suivi dans le clone et
     compté dans son manifeste) et change `units.data.fingerprint` (`c590eaaf…2e0eb` → `67a463bb…32994`). Une copie de la
     publication neuve changerait donc l'empreinte de `data`, que les pipelines lisent. Le clone n'est pas reconstructible
     depuis la source ; **on ne le reconstruit pas**. Mise à jour **strictement ciblée** à la place : un fichier modifié, un
     fichier ajouté, tout le reste conservé.
   - **Invariant à prouver (remplace l'équivalence à une publication neuve).** Après l'opération :
     (1) `core/`, `data/`, `platform/`, `orders/` sont inchangés (mêmes chemins, mêmes `sha256`, donc mêmes
     `units.*.fingerprint` que le manifeste *actuel* du clone, pas ceux de la publication neuve) ; (2) `revision` 2 et
     `proofLineage` `p3-role` ; (3) l'ensemble des chemins gérés = l'ancien ensemble + le schéma v2, rien de retiré, en
     particulier `data/infra/server-diagnostics.bicep` et les deux fichiers P8 ; (4) seuls `.ifs/templates/scripts/IfsRelease.psm1`
     (pris dans la publication neuve) et `.ifs/templates/schemas/ifs-report.v2.schema.json` (idem, ajouté) changent dans le
     dépôt, hors manifeste ; (5) le manifeste est recalculé sur l'**arbre réel** et le décrit exactement (dont les deux
     fichiers P8, dont l'empreinte passe de « source publiée » à « clone »). Les deux fichiers mis à jour sont sous
     `.ifs/templates/` et `src/api/` n'entre dans aucune empreinte d'unité (`Write-ProofManifest` ne hache par composant que
     `core/`, `data/`, `platform/`, `orders/`) : ni le contrôle de révision plus récente ni l'empreinte des effets ne voient
     le changement.
   - **Non-objectifs.** Ne **pas** ajouter `IfsInstall.Tests.ps1` (test Pester du kit d'installation, lu par aucun pipeline ;
     l'ajouter élargirait le périmètre sans raison). Ne **pas** supprimer ni expliquer `server-diagnostics.bicep` : c'est un fichier
     suivi et présent dans l'arbre du clone et dans le manifeste de `shop42`. On le conserve parce qu'une mise à jour ciblée ne
     doit changer aucun autre chemin. Son origine et son éventuel déploiement ne sont **pas établis** (aucune référence trouvée
     dans les Bicep et YAML du clone) ; à consigner tel quel dans `resultats.md`, sans conséquence sur P8.
   - **Dérogation P8 (arbitrage Claude du 2026-10-17, valable pour `shop42` seulement).** L'audit de tous les fichiers gérés
     de `C:/src/shop42` à `91c022e…` (révision 2, `p3-role`, commit de manifeste `ca0baca…`, 98 fichiers) constate deux
     écarts, tous deux sous `src/api/`, introduits par la PR 73 (marqueur d'assembly `ifs-proof=p8` dans `Program.cs`, test
     `Assembly_ContainsP8ImageMarker` + `using System.Reflection;` dans le fichier de tests) sans que le manifeste ait été
     recalculé. Ce code est requis pour la preuve de l'image D2 de P8 : **on le conserve** et on corrige le manifeste, qui
     était périmé pour ces deux fichiers. Un écart n'est autorisé que si **toutes** les conditions suivantes sont vraies ; sinon
     `BLOQUE` :

     | Chemin | Empreinte du manifeste (= source publiée neuve) | Empreinte autorisée du clone |
     |---|---|---|
     | `src/api/Program.cs` | `5a5b3037356e2f667a56ebf1ad483e2f673420a9f64701dfaa425ed2f7648ba6` | `876d3356ac1d6481c49b16ae5643f0c27e2d1d5457a91dc5696c6d60e104f4de` |
     | `src/api/tests/WitnessAppEndpointTests.cs` | `cee53e008f7568d8f6aa6037f5c48ae4212d3841acac664df8d7c1b66513c172` | `2c450e0c5aab684794fce47f39d42553a49790de40b89c58ca518670f84e0a1b` |

     1. **Périmètre figé.** `shop42`, `HEAD` = `91c022ebed81c1b5a08155a810f21ba676cea96d`, arbre propre, `revision` 2,
        `proofLineage` `p3-role`. Toute autre valeur : la dérogation ne s'applique pas (pas d'extension à un autre clone ni à
        une autre empreinte ; `shop43`/`shop44` sont des publications neuves sans marqueur).
     2. **Exactement deux divergences.** L'empreinte logique (CRLF→LF) de **chacun des fichiers du manifeste** est recalculée ;
        l'ensemble des fichiers divergents doit être exactement ces deux chemins. Un troisième fichier, un fichier absent ou un
        fichier non UTF-8 : arrêt.
     3. **Empreintes exactes.** Le manifeste du clone porte l'empreinte de la colonne 2 pour chaque chemin, la publication neuve
        porte la même, et le clone porte l'empreinte de la colonne 3. Une autre valeur, même « proche » : arrêt.
     4. **Changement additif uniquement.** Entre la publication neuve et le clone (fins de ligne ignorées), `rtk proxy git diff --no-index --numstat`
        donne 0 ligne supprimée et au moins une ligne ajoutée pour chaque fichier ; le texte ajouté de `Program.cs` contient
        `ifs-proof` et `p8`, celui du fichier de tests contient `using System.Reflection;` et `Assembly_ContainsP8ImageMarker`.
        Entre le commit de publication `225cb57` et `HEAD`, `rtk git -C C:/src/shop42 diff --numstat` donne aussi 0 ligne supprimée pour ces deux
        chemins. Les empreintes du 3 garantissent le contenu exact ; ce contrôle garantit que c'est bien un ajout.
     5. **Le reste est strict.** Les 96 autres fichiers du manifeste du clone valent leur empreinte, `rtk git -C C:/src/shop42 status` n'a aucun
        changement hors fichiers du manifeste. La comparaison à la publication neuve ne sert qu'à **choisir les deux fichiers à
        copier** ; elle ne sert plus de référence d'équivalence (voir l'amendement ci-dessus).
     6. **Fin de procédure.** Le manifeste recalculé par `Write-ProofManifest` égale l'**ancien** manifeste du clone pour tous
        les chemins sauf : `IfsRelease.psm1` (empreinte de la publication neuve), le schéma v2 (ajouté, empreinte de la
        publication neuve), et les deux fichiers P8 (empreinte du clone). Les quatre `units.*.fingerprint` sont **égales à
        celles de l'ancien manifeste**.

     **Sémantique de `commit` (arbitrage Claude du 2026-10-17).** Après cette opération, `commit` = le commit IFS d'où
     proviennent `IfsRelease.psm1` et le schéma v2 (celui de la publication neuve, `b5f617e…` en entier), et **non** « le commit
     dont l'arbre est publié ». Il ne l'était déjà plus : le clone contient `server-diagnostics.bicep` (absent de `ca0baca`) et son
     `IfsInstall.psm1` correspond à la source récente alors que `commit` valait `ca0baca…`. Garder `ca0baca…` serait faux pour
     `IfsRelease.psm1` et ne permettrait pas de retrouver le contrat v2 ; mettre `b5f617e…` ne revendique aucune équivalence. Les
     exceptions à l'équivalence (`server-diagnostics.bicep` conservé, `IfsInstall.Tests.ps1` absent, deux fichiers P8) se
     consignent dans le message du commit Azure Repos et dans `resultats.md` (ligne P8, remarques). **Contrôle préalable** :
     rechercher dans `reference/pilot/bicep-azdo/.ifs/` et dans les YAML du clone toute lecture de `manifest.commit` /
     `.commit` ; attendu : aucune lecture hors affichage du nom de run. Si une lecture existe, **BLOQUE**.
   - **État du clone attendu : `revision` 2, `proofLineage` `p3-role`** (P3a a fusionné `p3-role` dans `main` ; l'overlay `p2`
     n'est jamais appliqué à `shop42`, cf. recette § 6). Ne pas supposer : la procédure lit l'état et s'arrête s'il est autre.
   - **Préalables.** `rtk git -C $ifsRoot status --porcelain -- reference/pilot/bicep-azdo samples/witness-app` vide et `HEAD`
     contient le commit de l'étape 0 bis ; Pester et `Test-Reference*.ps1` verts ; dans `C:/src/shop42` : arbre propre, branche
     `fix/p08-approved-preview-fingerprint` créée sur `origin/main`, `rtk git -C C:/src/shop42 fetch origin` puis `origin/main` = `91c022e…`.
   1. **Répétition générale, sans toucher `C:/src/shop42`** : `rtk git clone --no-hardlinks --branch fix/p08-approved-preview-fingerprint C:/src/shop42 $env:TEMP/ifs-shop42-rehearsal`,
      puis le bloc ci-dessous avec `$target = "$env:TEMP/ifs-shop42-rehearsal"`. Elle exerce aussi le cas `core.autocrlf` du poste
      (les empreintes sont logiques, indépendantes des fins de ligne). Le bloc supprime `$fresh` en dernière ligne, après
      vérification de son chemin exact. **Nettoyage de la répétition** : `$env:TEMP/ifs-shop42-rehearsal`, dans la même session
      PowerShell (la fonction `Remove-TempCopy` du bloc est définie dès sa première ligne, même si le bloc s'est arrêté plus
      loin), seulement avec `Remove-TempCopy "$env:TEMP/ifs-shop42-rehearsal" 'ifs-shop42-rehearsal'` ; la fonction refuse tout autre
      chemin. Faire de même pour un `$fresh` resté en place après un arrêt du bloc : `Remove-TempCopy $fresh 'ifs-shop42-fresh'`.
      Ne jamais passer `C:/src/shop42` à cette fonction. Contrôler ensuite que les deux chemins n'existent plus
      (`Test-Path -LiteralPath` vaut `False`) ; ne supprimer aucune autre copie.
   2. **Exécution réelle** : le même bloc avec `$target = 'C:/src/shop42'`.

      ```powershell
      $ifsRoot = 'C:/Users/florian.drevet/RiderProjects/InfraFlowSculptor'
      $target  = 'C:/src/shop42'                       # ou la répétition générale
      $fresh   = Join-Path $env:TEMP 'ifs-shop42-fresh' # ne doit pas exister
      Import-Module "$ifsRoot/tools/proofs/ProofTools.psm1" -Force
      $utf8 = [Text.UTF8Encoding]::new($false)
      function Remove-TempCopy([string]$Path, [string]$Leaf) {   # seule suppression récursive autorisée : exactement $env:TEMP/<Leaf>, sous GetTempPath()
          $full     = [IO.Path]::GetFullPath($Path)
          $expected = [IO.Path]::GetFullPath((Join-Path $env:TEMP $Leaf))
          $root     = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
          if ($full -ine $expected -or $full.Length -le $root.Length -or -not $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
              throw "Suppression refusee : '$full' n'est pas '$expected' sous '$root'."
          }
          $item = Get-Item -LiteralPath $full -Force -ErrorAction Stop
          if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Suppression refusee : '$full' est un lien." }
          Remove-Item -LiteralPath $full -Recurse -Force
      }
      function Get-Param($rel, $key) {
          $m = [regex]::Match([IO.File]::ReadAllText("$target/$rel", $utf8), "$key\s*:\s*'(?<v>[^']+)'")
          if (-not $m.Success) { throw "$key absent de $rel" }
          $m.Groups['v'].Value
      }
      $before = Get-Content "$target/.ifs/manifest.json" -Raw | ConvertFrom-Json -AsHashtable -Depth 100
      $rev = [int]$before.revision; $lineage = [string]$before.proofLineage
      $old = @{}; foreach ($f in $before.files) { $old[[string]$f.path] = [string]$f.sha256 }
      $oldUnits = @{}; foreach ($c in 'core','data','platform','orders') { $oldUnits[$c] = [string]$before.units[$c].fingerprint }

      # 0. Dérogation P8 : seuls ces deux fichiers peuvent différer du manifeste, avec ces empreintes exactes
      $p8 = @(
          @{ Path = 'src/api/Program.cs'
             Manifest = '5a5b3037356e2f667a56ebf1ad483e2f673420a9f64701dfaa425ed2f7648ba6'
             Clone    = '876d3356ac1d6481c49b16ae5643f0c27e2d1d5457a91dc5696c6d60e104f4de'
             Added    = @('ifs-proof', 'p8') },
          @{ Path = 'src/api/tests/WitnessAppEndpointTests.cs'
             Manifest = 'cee53e008f7568d8f6aa6037f5c48ae4212d3841acac664df8d7c1b66513c172'
             Clone    = '2c450e0c5aab684794fce47f39d42553a49790de40b89c58ca518670f84e0a1b'
             Added    = @('using System.Reflection;', 'Assembly_ContainsP8ImageMarker') }
      )
      $hashOf = { param($file) & (Get-Module ProofTools) { param($x) Get-ProofLogicalFileHash -Path $x } $file }  # fonction privée du module, appelée telle quelle
      if ("$rev/$lineage" -cne '2/p3-role' -or [string]$before.project -cne 'shop42') { throw "Perimetre de la derogation depasse : $($before.project) $rev/$lineage." }
      if ((rtk git -C $target rev-parse HEAD).Trim() -cne '91c022ebed81c1b5a08155a810f21ba676cea96d') { throw 'HEAD du clone different de 91c022e.' }
      if (@(rtk git -C $target status --porcelain).Count -ne 0) { throw 'Le clone n''est pas propre.' }
      $diverged = @($before.files | Where-Object { (& $hashOf "$target/$($_.path)") -cne [string]$_.sha256 } | ForEach-Object { [string]$_.path } | Sort-Object)
      $allowed  = @($p8 | ForEach-Object { $_.Path } | Sort-Object)
      if (($diverged -join '|') -cne ($allowed -join '|')) { throw "Divergences hors derogation. trouvees=[$diverged] autorisees=[$allowed]" }
      rtk git -C $target cat-file -e '225cb57^{commit}'; if ($LASTEXITCODE -ne 0) { throw '225cb57 introuvable ou ambigu.' }
      $expected = $before | ConvertTo-Json -Depth 100 | ConvertFrom-Json -AsHashtable -Depth 100   # copie ; $before reste intact
      foreach ($e in $p8) {
          $entry = @($expected.files | Where-Object { [string]$_.path -ceq $e.Path })
          if ($entry.Count -ne 1 -or [string]$entry[0].sha256 -cne $e.Manifest) { throw "Empreinte du manifeste inattendue : $($e.Path)" }
          if ((& $hashOf "$target/$($e.Path)") -cne $e.Clone) { throw "Empreinte du clone inattendue : $($e.Path)" }
          $ns = (rtk git -C $target diff --numstat 225cb57 HEAD -- $e.Path) -split '\s+'
          if ($ns.Count -lt 3 -or $ns[1] -cne '0' -or $ns[0] -notmatch '^[1-9]\d*$') { throw "Changement depuis 225cb57 non additif : $($e.Path)" }
          $entry[0].sha256 = $e.Clone
      }
      [void](Assert-ProofManifestWorkingTree -RepositoryPath $target -Manifest $expected)   # les 98 fichiers + statut git, avec les deux seules empreintes autorisées

      # 1. Publication neuve avec les paramètres lus dans le clone, puis le même overlay que lui
      rtk git init $fresh
      $publish = @{
          RepositoryPath = $fresh; ProjectCode = [string]$before.project
          SubscriptionDev = Get-Param 'core/infra/main.dev.bicepparam' 'subscriptionId'
          SubscriptionPrd = Get-Param 'core/infra/main.prd.bicepparam' 'subscriptionId'
          SubscriptionShared = Get-Param 'platform/infra/main.shared.bicepparam' 'subscriptionId'
          SqlAdminGroupObjectIdDev = Get-Param 'data/infra/main.dev.bicepparam' 'administratorGroupObjectId'
          SqlAdminGroupObjectIdPrd = Get-Param 'data/infra/main.prd.bicepparam' 'administratorGroupObjectId'
      }
      $sqlFree = [IO.File]::ReadAllText("$target/data/infra/main.prd.bicepparam", $utf8) -match '(?m)^\s*useFreeLimit:\s*true\s*$'
      & "$ifsRoot/tools/proofs/Publish-PilotReference.ps1" @publish -SqlFreeOffer:$sqlFree
      switch ("$rev/$lineage") {
          '1/baseline' { }
          '2/p3-role'  { & "$ifsRoot/tools/proofs/New-ProofRevision.ps1" -RepositoryPath $fresh -Revision p3-role }
          default      { throw "Etat du clone inattendu : revision $rev, filiation '$lineage'." }
      }

      # 2. Écart de la publication neuve, connu et borné (répétition du 2026-10-17). Elle ne sert qu'à choisir les deux fichiers à copier.
      #    Attendu, exactement : modifié = IfsRelease.psm1 ; ajoutés = schéma v2 + IfsInstall.Tests.ps1 ; retiré = server-diagnostics.bicep ;
      #    fingerprints core/platform/orders identiques, data différente. Tout autre écart = arrêt.
      $ref = Get-Content "$fresh/.ifs/manifest.json" -Raw | ConvertFrom-Json -AsHashtable -Depth 100
      $new = @{}; foreach ($f in $ref.files) { $new[[string]$f.path] = [string]$f.sha256 }
      $changed = @($new.Keys | Where-Object { $old.ContainsKey($_) -and $old[$_] -cne $new[$_] } | Sort-Object)
      $added   = @($new.Keys | Where-Object { -not $old.ContainsKey($_) } | Sort-Object)
      $removed = @($old.Keys | Where-Object { -not $new.ContainsKey($_) } | Sort-Object)
      $wantAdded = @('.ifs/install/tests/IfsInstall.Tests.ps1', '.ifs/templates/schemas/ifs-report.v2.schema.json')
      if (($changed -join '|') -cne '.ifs/templates/scripts/IfsRelease.psm1' -or
          ($added -join '|') -cne ($wantAdded -join '|') -or ($removed -join '|') -cne 'data/infra/server-diagnostics.bicep') {
          throw "Ecart inattendu. modifies=[$changed] ajoutes=[$added] retires=[$removed]"
      }
      foreach ($c in 'core','platform','orders') {
          if ($before.units[$c].fingerprint -cne $ref.units[$c].fingerprint) { throw "Fingerprint d'unite change : $c" }
      }
      if ($before.units['data'].fingerprint -cne 'c590eaaf6552b716149ac5f6a90951b3840042faf939cca7ec0712a80672e0eb' -or
          $ref.units['data'].fingerprint -cne '67a463bb79fe2869867d0cad6c4623736ac5c1069f33cd79e5c48b4ed4d32994') {
          throw 'Fingerprint data different de celui observe a la repetition : arret.'   # c'est lui que la mise a jour ciblee doit preserver
      }
      foreach ($e in $p8) {   # la source neuve égale le manifeste ; le clone n'y ajoute que des lignes
          if ($new[$e.Path] -cne $e.Manifest) { throw "Publication neuve inattendue : $($e.Path)" }
          $ns = (rtk proxy git diff --no-index --ignore-cr-at-eol --numstat -- "$fresh/$($e.Path)" "$target/$($e.Path)") -split '\s+'
          if ($ns.Count -lt 3 -or $ns[1] -cne '0' -or $ns[0] -notmatch '^[1-9]\d*$') { throw "Ecart P8 non additif : $($e.Path)" }
          $addedText = (@(rtk proxy git diff --no-index --ignore-cr-at-eol --unified=0 -- "$fresh/$($e.Path)" "$target/$($e.Path)") | Where-Object { $_ -match '^\+[^+]' }) -join "`n"
          foreach ($needle in $e.Added) { if (-not $addedText.Contains($needle)) { throw "Marqueur P8 absent ($needle) : $($e.Path)" } }
      }

      # 3. Application ciblée : deux fichiers pris dans la publication neuve, TOUT le reste du clone conservé.
      #    ManagedPaths = anciens chemins dans leur ancien ordre + le schéma, inséré à sa place ordinale (l'ordre des fichiers data/ fonde units.data).
      $schema = '.ifs/templates/schemas/ifs-report.v2.schema.json'
      $oldOrder = @($before.files | ForEach-Object { [string]$_.path })
      [string[]]$managed = $oldOrder + $schema; [Array]::Sort($managed, [StringComparer]::Ordinal)   # tri ordinal, comme Get-ProofManagedFileList
      if (($oldOrder -join '|') -cne (($managed | Where-Object { $_ -cne $schema }) -join '|')) { throw 'Ordre des anciens chemins different du tri ordinal : arret.' }   # avant toute copie : une précondition en échec ne laisse aucun fichier modifié
      foreach ($p in '.ifs/templates/scripts/IfsRelease.psm1', $schema) {
          Copy-Item -LiteralPath "$fresh/$p" -Destination "$target/$p" -Force
      }
      $before.commit = $ref.commit
      Write-ProofManifest -RepositoryPath $target -Manifest $before -ManagedPaths $managed -Revision $rev -ProofLineage $lineage

      # 4. Contrôles finaux : le manifeste décrit l'arbre réel ; tout sauf 4 chemins égale l'ANCIEN manifeste du clone
      $after = Get-Content "$target/.ifs/manifest.json" -Raw | ConvertFrom-Json -AsHashtable -Depth 100
      $now = @{}; foreach ($f in $after.files) { $now[[string]$f.path] = [string]$f.sha256 }
      $want = $old.Clone()
      $want['.ifs/templates/scripts/IfsRelease.psm1'] = $new['.ifs/templates/scripts/IfsRelease.psm1']   # publication neuve
      $want[$schema] = $new[$schema]                                                                       # publication neuve (ajout)
      foreach ($e in $p8) { $want[$e.Path] = $e.Clone }                                                    # empreinte du clone (dérogation)
      if ($now.Count -ne $want.Count -or $now.Count -ne $old.Count + 1 -or @($want.Keys | Where-Object { $now[$_] -cne $want[$_] }).Count -ne 0) { throw 'Manifeste different de ancien + 4 chemins attendus.' }
      if (-not $now.ContainsKey('data/infra/server-diagnostics.bicep') -or -not (Test-Path -LiteralPath "$target/data/infra/server-diagnostics.bicep" -PathType Leaf)) { throw 'server-diagnostics.bicep perdu.' }
      if ($now.ContainsKey('.ifs/install/tests/IfsInstall.Tests.ps1') -or (Test-Path -LiteralPath "$target/.ifs/install/tests/IfsInstall.Tests.ps1")) { throw 'IfsInstall.Tests.ps1 ne doit pas etre ajoute.' }
      foreach ($c in 'core','data','platform','orders') { if ($after.units[$c].fingerprint -cne $oldUnits[$c]) { throw "Fingerprint d'unite change : $c" } }   # $oldUnits : pris avant Write-ProofManifest, qui modifie $before
      if ([int]$after.revision -ne $rev -or [string]$after.proofLineage -cne $lineage -or $after.commit -cne $ref.commit) { throw 'Revision, filiation ou commit incorrects.' }
      [void](Assert-ProofManifestWorkingTree -RepositoryPath $target -Manifest $after)
      Import-Module "$target/.ifs/templates/scripts/IfsRelease.psm1" -Force
      if (-not (Get-Command Write-IfsReleaseReport).Parameters.ContainsKey('ApprovedPreviewFingerprint')) { throw 'Module du clone sans ApprovedPreviewFingerprint.' }
      if ((& $hashOf "$target/.ifs/templates/scripts/IfsRelease.psm1") -cne $new['.ifs/templates/scripts/IfsRelease.psm1']) { throw 'IfsRelease.psm1 different de la publication neuve.' }
      rtk git -C $target status --short   # exactement 3 lignes : M manifest.json, M IfsRelease.psm1, ?? ifs-report.v2.schema.json
      rtk git -C $target diff -- .ifs/manifest.json   # seuls changent : commit, generatedAtUtc, sha256 de IfsRelease.psm1, sha256 des deux fichiers P8 (empreinte du manifeste → empreinte du clone), entrée du schéma v2
      Remove-TempCopy $fresh 'ifs-shop42-fresh'   # vérifie juste avant : chemin résolu = $env:TEMP/ifs-shop42-fresh, sous GetTempPath(), pas un lien
      ```

      Toute exception ou tout écart = **arrêt** : ne rien corriger à la main, ne pas éditer le manifeste, statut `BLOQUE` avec
      la sortie complète (cas typiques : état du clone ≠ `2/p3-role`, `HEAD` ≠ `91c022e`, troisième fichier géré divergent ou
      empreinte P8 autre que celles du tableau, écart P8 non additif, écart de la publication neuve autre que les quatre
      connus de l'étape 2, `units.*.fingerprint` différente de l'ancien manifeste après l'étape 3, `commit` lu par un pipeline,
      mauvais `-SqlFreeOffer`). La dérogation ne s'étend pas :
      on n'ajoute pas de chemin ni d'empreinte au tableau sans nouvel arbitrage de Claude.
   3. **Pousser dans Azure Repos, pas dans le dépôt IFS.** Dans `C:/src/shop42`, branche `fix/p08-approved-preview-fingerprint` :
      `rtk git -C C:/src/shop42 add .ifs/manifest.json .ifs/templates/scripts/IfsRelease.psm1 .ifs/templates/schemas/ifs-report.v2.schema.json`
      (ces trois chemins seulement), commit `rtk git -C C:/src/shop42 commit -m "fix(ifs): ifs-report/v2 avec l'empreinte d'aperçu approuvée (IFS <7 premiers caractères de $ref.commit>)"`
      avec en corps de message : « Mise à jour ciblée, pas une republication : IfsRelease.psm1 et le schéma v2 viennent de IFS
      <$ref.commit> ; server-diagnostics.bicep conservé ; IfsInstall.Tests.ps1 non ajouté ; src/api P8 conservé ; units.* inchangées »,
      `rtk git -C C:/src/shop42 push -u origin fix/p08-approved-preview-fingerprint`, PR vers `main` (même procédure que les PR #51/#52), fusion.
   4. **Avant de lancer le run P8** : `rtk git -C C:/src/shop42 fetch origin` ; `rtk git -C C:/src/shop42 diff --stat 91c022e origin/main` ne liste que ces trois chemins
      (sinon, `main` a bougé : arrêt) ; relire les `trigger`/`pr` des YAML du clone : si la fusion a déclenché un run, attendre sa fin
      (aucune release applicative ne doit être en cours ni démarrer entre l'aperçu et l'approbation). Le contrôle de la preuve
      est dans le rapport du nouveau run : `schema` = `ifs-report/v2`. Un rapport `ifs-report/v1` signifie que le run n'a pas
      utilisé le clone mis à jour : ne pas approuver, `BLOQUE`.
   5. **Dépôt IFS** : rien à fusionner vers `main` avant le run. Le commit de l'étape 0 bis reste sur `impl/preuves` ; le
      pousser (sans PR) est recommandé pour que `commit` du manifeste soit résolvable, mais le run ne le lit pas.
0 quater. **Diagnostic de la divergence What-If du run #748** (arbitrage Claude CLI du 2026-10-17). Aucun pipeline ni aucune
   release avant la fin du diagnostic et la définition des conditions de D3. Aucun changement au contrôle fail-closed.
   - **D1a — Luna, hors ligne.** Auditer les captures existantes et les artefacts téléchargés dans
     `%LOCALAPPDATA%\InfraFlowSculptor\P08\D1a\` : `infra` de la CI #746, `ifs-preview-dev` et `ifs-report-dev` de la release #748,
     logs de preview #748 (9) et de déploiement #748 (20). Consigner dans
     [`preuves/diagnostic-p8-748.md`](preuves/diagnostic-p8-748.md) les formats, empreintes, nombres de changements, métadonnées
     utiles et données absentes ; ne pas y copier les contenus bruts, GUID Azure ou valeurs de propriétés. L'aperçu publié est
     `ifs-preview/v1` ; le rapport v2 est `FailedBeforeMutation`, porte l'empreinte publiée et a `steps` vide. Le log 20 ne
     contient pas de JSON What-If recalculé identifiable. Tant que le JSON initial et le What-If recalculé du #748 ne sont pas
     tous deux récupérés, classer ce run **non diagnostiqué** ; ne lui attribuer aucune cause.
   - **Comparateur hors ligne.** Ajouter `tools/proofs/Compare-IfsPreviews.psm1` et
     `reference/release-module/tests/CompareIfsPreviews.Tests.ps1`. Le comparateur accepte des fichiers `ifs-preview/v1`,
     compare leur fingerprint et les changements par ressource, type et chemins de delta sans dépendre de l'ordre des tableaux.
     Il ne retire aucun champ d'effet et ne recalcule pas l'empreinte. Il distingue `Identique`, `Divergent` (ressources,
     type de changement ou delta de propriété) et `NonConcluant` (entrée absente, illisible ou schéma/contexte incompatible).
     `generatedAt` est consigné, mais ne participe pas à l'égalité. Les tests utilisent uniquement des fixtures synthétiques
     et couvrent les trois verdicts. Commit D1a : `feat(preuves): diagnostic What-If hors ligne`.
   - **D1b — utilisateur, lecture seule.** Depuis l'artefact `infra` de la CI #746, répéter cinq aperçus DEV avec le même
     template et les mêmes paramètres en appelant `Invoke-IfsPreview` dans la portée du module chargé. Le contrôle
     `servicePrincipalId` est dans le wrapper AzureCLI, hors de cette fonction ; ne pas définir ni simuler cette variable.
     Marquer chaque sortie `identity: personal-az-cli` et `servicePrincipalId: absent` : ces passes mesurent seulement la
     reproductibilité sous l'identité personnelle et n'expliquent pas le #748. Capturer cinq `ifs-preview/v1`, leur horodatage,
     versions d'outils, contexte et SHA-256 ; les sorties brutes restent hors du dépôt et sont vérifiées avant partage.
     Aucun pipeline, approbation, déploiement ou autre mutation.
   - **Sortie D1a.** Le protocole exact D1b et les résultats d'audit sont dans `diagnostic-p8-748.md`. Après D1a, mettre
     `NEXT.md` à `EN_ATTENTE_DE_RECETTE`. Quand l'utilisateur fournit les captures D1b, exécuter
     `python tools/plan/gate.py resume P-08`, comparer hors ligne, puis consigner le diagnostic en distinguant le #748
     (non diagnostiqué si son What-If recalculé manque) de la reproductibilité mesurée sous l'identité personnelle.
   - **Suite.** Garder P-08 `EN_COURS` après `resume`. D2 et D3 sont détaillés à l'étape 0 quinquies ci-dessous ; conserver
     le contrôle fail-closed. Il n'y a pas de revue intermédiaire via `gate.py` : la revue de Claude
     intervient au verrou final R-03 après P-08.
0 quinquies. **D2 et D3 : instrumenter la divergence, puis un essai unique** (arbitrage Claude du 2026-10-18). Dans cette étape,
   « D2a », « D2b » et « D3 » sont des phases du diagnostic ; l'« image D2 » reste l'image applicative livrée par #739.
   - **Décision sur #748 : option 1.** Le run #748 reste **définitivement non diagnostiqué** : son What-If recalculé n'a été écrit
     ni dans les logs 9 et 20, ni dans les captures, ni dans les artefacts (D1a), et Azure ne conserve pas le résultat d'un
     What-If. Aucune source connue n'existe, donc aucune récupération n'est tentée (option 2 écartée) ; relancer un pipeline pour
     « retrouver » l'original ne le retrouverait pas, puisque ce serait un autre calcul à un autre moment. Elle ne se rouvre que si
     l'utilisateur désigne une source **absente** de l'audit D1a (alors : question dans `NEXT.md`, pas d'initiative). D1b n'est ni
     un substitut ni une preuve de cause : elle établit seulement la répétabilité personnelle. Observation à consigner sans
     interprétation : l'empreinte publiée par #748 (`bec9d7c6…3460`, identité de la service connection, 2026-10-17 10:19Z) diffère de
     celle des cinq passes D1b (`77dc939f…87b1`, identité personnelle, 2026-10-17 soir) ; la cause (identité, moment, état Azure)
     n'est pas établie et ne doit être attribuée à aucune des trois.
   - **Objectif.** Obtenir la preuve positive de P8 (empreinte approuvée = empreinte appliquée, [DT-43](../technique/01-decisions.md#dt-43--empreinte-dapercu-approuvée-liée-à-lempreinte-appliquée))
     par **un seul** nouvel essai de release `orders`, rendu diagnostiquable : si la divergence se reproduit, le What-If recalculé
     est conservé et comparé. Le contrôle fail-closed n'est ni assoupli ni contourné.
   - **D2a — Luna, hors ligne, test d'abord.** Un test rouge par comportement, puis l'implémentation minimale. Fichiers, sans autre
     (hors la tâche de publication d'artefact ci-dessous) : `reference/pilot/bicep-azdo/.ifs/templates/scripts/IfsRelease.psm1`
     (`Invoke-IfsInfraDeploy`), `reference/release-module/tests/IfsRelease.Tests.ps1`, `reference/pilot/manifest.example.json`
     (recalculé par `reference/tools/Update-ManifestExample.ps1`).
     1. Comportement : quand `$current.fingerprint -ne $approved.fingerprint`, **avant** l'exception existante, l'aperçu
        recalculé (l'`ifs-preview/v1` que `Invoke-IfsPreview` vient de produire) est écrit octet pour octet sous le nom
        `ifs-preview-recalculated.json`, dans le répertoire d'`ifs-report.json`. Sans divergence, ce fichier n'existe pas. L'aperçu
        approuvé n'est jamais réécrit ni écrasé. Si l'écriture échoue, l'exception d'origine est quand même levée avec le même
        message (l'échec d'écriture ne remplace pas le refus). Ni schéma de rapport, ni champ de rapport, ni code d'erreur, ni
        paramètre n'est ajouté ; `result` reste `FailedBeforeMutation`, aucune opération du journal ne passe à `Started`/`Done`,
        `stack sub create` n'est pas appelé.
     2. Tests (`az` simulé, comme ceux de l'étape 0 bis) : (a) divergence : le fichier existe, ses octets égalent l'aperçu
        recalculé simulé, il valide le schéma d'aperçu v1 existant, sa `fingerprint` diffère de l'approuvée, l'aperçu approuvé est
        inchangé octet pour octet, exception et `result` inchangés, aucun `Started`/`Done`, pas d'appel à `stack sub create` ;
        (b) égalité : aucun `ifs-preview-recalculated.json` ; (c) écriture impossible : même exception qu'en (a) ; (d) tous les
        tests de l'étape 0 bis restent verts sans modification.
     3. Publication : lire dans `.ifs/templates/` la tâche qui publie l'artefact `ifs-report-<cible>`. Si son chemin couvre le
        répertoire du rapport ou un motif incluant le nouveau fichier, aucun YAML ne change. Sinon, ajouter ce seul fichier au
        chemin déjà publié par **cette même tâche** (pas de nouvelle tâche, pas de nouvel artefact), puis
        `Test-ReferencePipelines.ps1`. Tout autre besoin : `BLOQUE`.
     4. Vérification : Pester complet (`reference/release-module/tests`), PSScriptAnalyzer, `Test-Reference*.ps1`, build Bicep
        inchangés et verts ; `git status --porcelain` limité aux fichiers listés ci-dessus. Commit
        `fix(reference): conserver l'aperçu recalculé lors d'une divergence`, pied `Étape: P-08`, puis push de `impl/preuves`.
   - **D2b — Luna, mise à jour ciblée sur un clone de preuve indépendant, puis réconciliation avec `C:/Users/flori/RiderProjects/shop42` (même méthode que l'étape 0 ter, arbitrage GPT Sol du 2026-10-20).**
     *Pourquoi* : les releases utilisent le module **embarqué dans l'artefact `infra` de leur CI**, pas le dépôt ; la correction n'agit
     qu'après un nouveau CI `orders/infra` construit depuis un `main` qui la contient. Périmètre : `shop42` uniquement.
     1. Préalables : D2a commitée et poussée ; sources `reference/pilot/bicep-azdo samples/witness-app` propres. Préserver le clone
        original `C:/Users/flori/RiderProjects/shop42`, sa branche et `origin/fix/p08-keep-recalculated-preview` au commit `2875cf3` ;
        ne pas le réinitialiser ni le modifier pendant les deux passes. Créer seulement si le chemin est absent le clone de preuve
        indépendant `C:/Users/flori/RiderProjects/shop42-d2b-proof` depuis le même dépôt Azure Repos. Dans ce clone : `fetch origin`,
        vérifier `origin/main` = `3f5917588b83d11fc07d7f9838f5e895e037dd6d`, arbre propre, puis créer la branche locale
        `fix/p08-keep-recalculated-preview` depuis `origin/main` avec `--no-track` (ne pas suivre ni modifier la branche distante
        homonyme). Vérifier `revision` 2, `proofLineage` `p3-role`, projet `shop42` et `Assert-ProofManifestWorkingTree` réussi
        (le manifeste de la fusion de la PR #74 décrit l'arbre exactement, les deux fichiers P8 de `src/api` compris : la dérogation
        de 0 ter n'est plus nécessaire). Noter le hash complet de `origin/main` : c'est le point de départ de l'invariant 2.
     2. Répétition générale puis exécution réelle : reprendre le bloc de 0 ter.2 (mêmes `Remove-TempCopy`, mêmes noms de répertoires
        temporaires, mêmes arrêts) avec ces seules substitutions. Pour chaque passe, remplacer la valeur de `$target` du bloc par le
        chemin de sa cible : `$env:TEMP/ifs-shop42-rehearsal` pour la répétition, `C:/Users/flori/RiderProjects/shop42-d2b-proof`
        pour l'exécution réelle. La répétition se fait dans le clone temporaire créé depuis la branche propre du clone de preuve.
        Garder le clone original à `2875cf3` intact. Fixer le dépôt IFS à `893f2a3dda4d7c8461be9c93f208a9e84d317962` pendant les deux
        passes, utiliser `$ifsRoot = 'C:/Users/flori/RiderProjects/InfraFlowSculptor'`, conserver les sorties complètes dans
        `d2b-rehearsal-rerun-transcript.txt` et `d2b-execution-rerun-transcript.txt` sous `%LOCALAPPDATA%\InfraFlowSculptor\P08\D2b\`,
        et ne créer aucun commit avant la fin des sous-étapes 3.1 à 3.6 ci-dessous.
        Supprimer l'étape « 0. Dérogation P8 » et la remplacer par
        l'assertion d'arbre propre ci-dessus ; à l'étape 2, l'écart attendu de la publication neuve est **exactement** modifiés = les
        chemins gérés modifiés par le commit D2a (`rtk git diff --name-only <parent-de-D2a> <D2a> -- reference/pilot/bicep-azdo`,
        préfixe retiré) + `src/api/Program.cs` + `src/api/tests/WitnessAppEndpointTests.cs` (marqueur P8 absent de la publication
        neuve : `Program.cs` ne contient pas `[assembly: System.Reflection.AssemblyMetadata("ifs-proof", "p8")]` et
        `WitnessAppEndpointTests.cs` ne contient pas `Assembly_ContainsP8ImageMarker`) ; ajoutés =
        `.ifs/install/tests/IfsInstall.Tests.ps1` ; retirés = `data/infra/server-diagnostics.bicep` ; `core`, `platform`, `orders`
        identiques, `data` différente (valeurs de 0 ter conservées). À l'étape 3, capturer le `numstat` du module du clone vers la
        publication neuve avant copie, le comparer aux nombres issus du commit D2a, puis ne copier **que** les chemins modifiés par
        D2a ; l'ensemble des chemins gérés ne change pas (aucun ajout au manifeste), seul le manifeste est recalculé
        (`Write-ProofManifest`). À l'étape 4, le manifeste final égale le manifeste de départ sauf l'empreinte du module, `commit`
        et `generatedAtUtc` régénéré par l'outil ; les quatre `units.*.fingerprint` sont **égales** à celles de départ. Vérifier
        que les nombres capturés avant copie correspondent au commit D2a et que le module copié a l'empreinte logique `$new[$module]`.
        Toute exception ou tout écart : arrêt,
        rien corrigé à la main, `BLOQUE` avec la sortie complète.
     3. **Garde DEV, puis audit des déclencheurs — avant tout commit, push, PR ou fusion** (protocole GPT Sol du 2026-10-20,
        sous-étapes 3.1 à 3.6 ; l'option C du 2026-10-18 est historique). Dans cet ordre ; une vérification fausse ou invérifiable : ne rien
        commiter ni pousser, `BLOQUE`.
        1. *Garde présente.* L'environnement `shop42-dev` (ID 10, organisation `floriandrevet0332`, projet `shop42`) porte, sur sa
           page « Approvals and checks », un contrôle **Pre-check approvals** placé **avant** l'Exclusive Lock existant : approbateur
           `[shop42]\Shop42 Release Approvers` (un seul membre suffit), « Allow approvers to approve their own runs » **désactivé**,
           délai 30 jours. Luna, sous autorisation explicite de Florian, a configuré le check via l'interface le 2026-10-18 (14:43 UTC) ;
           la capture de la page et de la fiche est visible dans cette session (aucune capture locale enregistrée). Luna la cite et, si
           elle peut lire l'état en lecture seule, le recoupe ; hors cette configuration autorisée, elle ne crée, ne modifie et ne
           supprime jamais ce contrôle. Si la capture est indisponible, est antérieure à une modification de l'environnement connue ou
           diffère de cette description : arrêt.
        2. *Seul `Deploy_dev` écrit.* Lecture hors ligne de `.ifs/templates/infra-target-stages.yml` et de
           `orders/infra/pipelines/release.yml` **dans le clone mis à jour** : (a) `Preview_dev` est un `job` ordinaire, sans
           `environment`, dont la seule tâche Azure exécute `Invoke-IfsInfraPreview.ps1` (aperçu en lecture seule) ; (b) `Deploy_dev`
           est le seul `deployment` de la cible, lié à `environment: shop42-dev`, et la seule tâche qui exécute
           `Invoke-IfsInfraDeploy.ps1` (donc la seule qui écrit dans Azure) ; (c) aucun autre fichier de `.ifs/templates/` ni de
           `orders/` n'appelle `Invoke-IfsInfraDeploy`, `stack sub create` ou `az deployment` en écriture pour la cible dev ;
           (d) `Deploy_prd` dépend de `Deploy_dev` (un rejet de `Deploy_dev` ne laisse donc pas PRD s'exécuter). Un écart : arrêt.
         3. *Inventaire des définitions.* Florian relève en lecture seule les dix définitions : les cinq CI
            (core/infra, data/infra, orders/infra, platform/infra, orders/apps/api) et leurs cinq releases. Pour chacune, noter le nom,
            le chemin YAML, l'ID, la révision, le `queueStatus` initial et les déclencheurs YAML et configurés dans l'interface.
            Relever les exécutions en cours et en file. Une exécution en cours/en file, une définition inattendue, un déclencheur
            incompris ou un état illisible : arrêt, aucune modification de `queueStatus`, aucun commit/push/PR/merge.
         4. *Préconditions C9 et clone.* Florian a retenu le 2026-10-20 l'activation de l'auto-approbation après R0. Après le
            rejet de R0, il l'active lui-même et fournit une nouvelle capture avant D3 ; si l'activation échoue, D3 reste bloquée.
            Dans `C:/Users/flori/RiderProjects/shop42-d2b-proof`, vérifier la branche et le diff prévus, exactement `.ifs/manifest.json`
            et `.ifs/templates/scripts/IfsRelease.psm1` préparés par D2b, les filtres et les dix YAML contre l'inventaire, la garde
            `shop42-dev` et le fait que seul `Deploy_dev` écrit. Vérifier aussi que le clone original reste propre sur `2875cf3`.
            Clone absent/inaccessible, diff différent, garde non vérifiable ou choix C9 retiré : ne rien commiter ni pousser. Si
            l'activation C9 échoue après R0, bloquer uniquement le lancement D3.
         5. *Suspension temporaire par Florian.* Après réussite des sous-étapes 3.1 à 3.4 et avant tout commit/push/PR/merge, passer
            `queueStatus` à `disabled` pour exactement ces huit définitions collatérales : les CI et releases de core/infra, data/infra,
            platform/infra et orders/apps/api. Ne modifier que `queueStatus`, relever les états initiaux et les nouvelles révisions ;
            ne pas utiliser `paused`, qui accepte les mises en file. Laisser les deux définitions orders/infra à `enabled`. Une lecture
            de contrôle doit confirmer les huit `disabled`, les deux `enabled` et des files vides. Toute différence ou impossibilité
            de vérifier : arrêt.
         6. *Portée des déclencheurs pendant la fenêtre.* Les dix définitions peuvent avoir des déclencheurs supplémentaires : ils
            sont tous consignés et compris, mais aucune définition collatérale ne doit pouvoir démarrer, car les huit sont `disabled`.
            Après la suspension, seuls la validation de PR sans connexion Azure (P-04), le CI `orders/infra` N lancé manuellement
            une fois par Florian après la fusion sur le SHA de fusion, puis son unique release automatique R0 sont attendus. L'override
            UI « Disable continuous integration » observé sur la définition 80 est la raison de ce lancement manuel ; le déclencheur
            de ressource de la release reste attendu après la réussite de N. Aucun YAML du clone n'est modifié pour résoudre les
            déclencheurs. Toute exécution autre que N/R0, toute file non vide ou toute écriture Azure avant approbation : arrêt, `BLOQUE`.
     4. **Livraison limitée, seulement après vérification des sous-étapes 3.1 à 3.6.** Le commit `2875cf3`, poussé avant cette garde,
        reste un fait historique préservé. Aucun nouveau commit (même local) ni merge ne précède la confirmation des huit suspensions
        par Florian. Ensuite, Luna crée le commit de preuve dans le clone indépendant, réconcilie
        la branche distante existante sans réécrire son historique, pousse et ouvre la PR ; Florian fusionne ; il lance le CI N manuel
        unique sur le SHA fusionné, puis observe le run automatique R0.
        1. Après validation de 3.1 à 3.6, commiter dans le clone de preuve uniquement `.ifs/manifest.json` et
           `.ifs/templates/scripts/IfsRelease.psm1`, corps : « Mise à jour ciblée, pas une republication : <chemins> viennent de IFS
           <commit D2a> ; rien d'autre ne change ; units.* inchangées ». Conserver le commit de preuve et le clone comme justificatifs.
           Dans le clone original, vérifier que `origin/fix/p08-keep-recalculated-preview` pointe toujours sur `2875cf3` et que l'arbre
           est propre ; intégrer le commit de preuve par un merge `--no-ff`, avec `2875cf3` comme premier parent. En cas de conflit,
           seuls `.ifs/manifest.json` et `.ifs/templates/scripts/IfsRelease.psm1` peuvent être résolus en prenant exactement leur
           version du commit de preuve ; tout autre conflit impose l'arrêt. Vérifier que l'arbre du merge est identique à celui du
           commit de preuve (hash d'arbre égal et arbre de travail propre). Cette relation rend le push de la branche originale
           fast-forward : pousser sans `--force`, puis ouvrir une seule PR vers `main` (même procédure que #51/#52/#74). Florian
           effectue la fusion lui-même, les écritures Azure DevOps restant à sa charge selon `AGENTS.md`. Aucune autre branche ni PR.
        2. Après la fusion, Florian vérifie que la tête de `main` est toujours le SHA de fusion, puis lance manuellement **une seule fois**
           la définition CI `orders/infra` (ID 80) sur `refs/heads/main`. Noter N. Vérifier dans le run que `sourceBranch` =
           `refs/heads/main`, `sourceVersion` = SHA de fusion et `reason` = `manual` ; un SHA différent, un autre run ou une
           file non vide impose l'arrêt `BLOQUE` sans relancer. Vérifier ensuite :
           `rtk git -C C:/Users/flori/RiderProjects/shop42 diff --stat <origin/main de départ> origin/main` ne liste que les chemins attendus ; CI N réussi ;
           l'artefact `infra` de N contient `IfsRelease.psm1` de même empreinte logique que celui du clone, et `orders/infra/main.bicep` et
           `main.dev.bicepparam` de même SHA-256 que `inputSha256.template` et `inputSha256.parameters` des `metadata.json` D1b.
           Capturer : `Save-AdoRecordings.ps1 … -Scenario p8-d2b-ci-orders-run-<N>`.
        3. **Run de release automatique R0 (attendu).** La fin réussie de N déclenche `release.yml` par sa ressource pipeline. Ce run,
           noté **R0**, n'est **pas** l'essai unique de D3, ne compte pour aucune des conditions C1 à C9 et ne sert à aucune preuve.
           Aucune release n'est lancée à la main pour le remplacer. Si R0 n'apparaît pas dans les 10 minutes qui suivent la fin de N :
           le consigner tel quel (aucun R0, trigger de ressource non observé) et ne rien déclencher ; C3 s'applique alors sans
           exception. Le choix C9 de Florian prévoit une activation après R0 : en l'absence de R0, ne pas activer le réglage ni
           lancer D3 sans confirmation distincte de Florian.
        4. **Traitement de R0 : bloqué puis rejeté, jamais approuvé.** `Preview_dev` de R0 peut s'exécuter (lecture seule) et publier
           `ifs-preview-dev`. `Deploy_dev` doit ensuite rester **en attente du Pre-check approvals**, sans démarrer ses étapes.
           Florian **rejette** l'approbation de R0 dans l'interface, le run automatique connu seulement ; **personne n'approuve R0,
           ni pour « voir »**. Luna n'effectue aucune écriture Azure DevOps et ne touche à aucun autre run. Consigner dans
           `NEXT.md` : ID de R0, heure UTC de son apparition et du
           rejet, état final de chaque stage (`Preview_dev` terminé, `Deploy_dev` rejeté, `Deploy_prd` ignoré), preuve que
           `Deploy_dev` n'a jamais démarré (timeline : aucune tâche exécutée, pas d'artefact `ifs-report-dev`), capture
           (`p8-d2b-release-auto-run-<R0>`) et le fait que R0 n'est pas l'essai D3.
        5. **Arrêt immédiat si la garde ne tient pas** : `Deploy_dev` démarre ou exécute une tâche sans approbation, R0 est approuvé
           par erreur, ou un autre run de release apparaît (ID ≠ R0, en cours ou en file). Luna arrête et demande à Florian au lieu d'annuler
           le run en cause ; `BLOQUE` avec la timeline ; D3 ne reprend qu'après une nouvelle décision de conception consignée. L'état Azure est relevé
           par l'inventaire en lecture seule de C5.
      5. **Arbitrage du 2026-10-18 — option C (historique, remplacée le 2026-10-20).** La garde `shop42-dev` reste en place et
         empêche une écriture DEV sans approbation ; elle ne neutralise ni les CI ni les déclencheurs collatéraux. La voie branche
         seule est écartée par DEC-101 et les connexions installées. Le protocole actif est l'inventaire et la suspension temporaire
         des huit définitions de la sous-étape 3 ; aucun YAML du clone n'est changé pour résoudre les déclencheurs.
      6. **Restauration.** Après le run D3 terminal et seulement quand les dix pipelines n'ont aucune exécution en cours ou en file,
         Florian rétablit les `queueStatus` initiaux des huit définitions suspendues et vérifie chaque résultat par lecture.
         Consigner IDs, états avant/après, révisions, captures et absence de runs différés. Si une exécution inattendue existe ou si
         la restauration n'est pas confirmable, maintenir les définitions collatérales désactivées et arrêter pour arbitrage.
   - **D3 — conditions, puis essai unique.** Vérifier et consigner **C1 à C7 et C9 avant le lancement** dans `NEXT.md` (ligne
     « Ressources Azure créées… », avec l'heure UTC). **C8 se vérifie après la publication de `Preview_dev`, avant toute approbation
     de `Deploy_dev`** ; C3 (absence de run concurrent) est relue avant le lancement et au point d'approbation. Toute condition fausse
     ou invérifiable : ne pas lancer/ne pas approuver, `BLOQUE` avec la condition en cause.
     - **C1** D2a commitée et poussée, ses vérifications vertes (sortie citée).
     - **C2** D2b terminée selon le protocole : diff limité aux deux fichiers prévus, huit pipelines collatéraux `disabled`
        (états initiaux et nouvelles révisions consignés), fusion `main` effectuée par Florian, CI orders/infra N lancé manuellement
        une fois par Florian sur le SHA de fusion et réussi, et R0 rejeté par Florian selon les règles D2b (ou absence de R0 consignée
        après dix minutes).
     - **C3** Lecture seule : aucune exécution en cours ou en file parmi les dix définitions, aucun run inattendu depuis la fusion,
        et au plus R0 terminé et rejeté par Florian avant mutation. Les huit définitions collatérales restent `disabled`; CI et release
        orders/infra restent `enabled`. Aucun push ni nouveau CI orders/infra n'est autorisé jusqu'au terminal de l'essai D3.
     - **C4** État DEV inchangé : une passe du script D1b (même code, boucle ramenée à 1, `AZURE_CORE_ONLY_SHOW_ERRORS=true` dans le seul
       processus, artefact `infra` de N, dossier `D3-pre`) donne l'empreinte `77dc939ffd65b288a6534f5172191753ea05a06bb3d14bdad7d9626e112a87b1`.
       Un autre résultat : arrêt, Azure a changé sans mutation connue.
     - **C5** Inventaire en lecture seule égal à celui d'après #748 (163 ressources, 26 groupes, 7 piles, 57 rôles, 7 budgets, mêmes
       IDs, comparaison insensible à la casse) ; coût réel et prévision du jour relevés dans l'inventaire ; l'absence de plafond
       personnel décidée le 2026-10-17 est rappelée. Tout écart d'inventaire : arrêt.
     - **C6** Image applicative DEV et PRD = image D2 (digests consignés avant l'essai).
     - **C7** L'utilisateur, membre de « Shop42 Release Approvers », est disponible pour les approbations (Luna n'approuve jamais) :
       P-08 passe `EN_ATTENTE_DE_RECETTE` dès l'aperçu DEV publié, avec la consigne d'approbation ci-dessous.
     - **C8** Après la publication de `Preview_dev` de l'essai unique et avant d'approuver `Deploy_dev`, ses effets sont : 0 création,
       0 recréation, 0 suppression, 0 révocation, 0 opération en attente (le détail est relu par Florian). Sinon : ne pas approuver,
       consigner, arrêt. C8 n'est pas une précondition au lancement : elle dépend de l'aperçu produit par ce run.
     - **C9** (ajoutée le 2026-10-18) Une **approbation humaine précède toute écriture Azure du stage DEV** de l'essai. L'étape DEV du
       kit n'est pas protégée par défaut ([recette § 2](recettes/01-preuves.md#2-p1--première-installation-et-application-témoin) :
       seule PRD est approuvée) ; sans cette garde, la release pourrait commencer l'application avant que Florian puisse examiner C8
       et décider de l'approbation. Vérifié par lecture hors ligne de `infra-release.yml` (toute écriture Azure du stage DEV est dans un job de
       déploiement lié à l'environnement `shop42-dev`, l'aperçu n'écrit rien) **et** par la capture de la page de contrôles de cet
       environnement, visible dans la session. Mise en place : Luna, sous autorisation explicite de Florian, a configuré le check via
       l'interface le 2026-10-18 à 14:43 UTC ; hors cette configuration autorisée, Luna ne modifie
       jamais un environnement ni un contrôle Azure DevOps. *Point d'attention :* le groupe « Shop42 Release Approvers » ne comporte qu'**un
       membre, Florian DREVET** (constaté en lecture seule dans Project Settings > Permissions > Shop42 Release Approvers > Members).
       La capture initiale montre « Allow approvers to approve their own runs » désactivé, ce qui empêche l'approbation de R0.
       Si R0 apparaît, Florian le rejettera sans approbation, puis activera lui-même ce réglage avant D3 et fournira une nouvelle
       capture. Si R0 n'apparaît pas dans les dix minutes suivant N, le choix « après R0 » ne précise pas quand l'activer : ne pas
       modifier le réglage ni lancer D3 sans confirmation distincte de Florian. Avant le lancement D3, vérifier que le check
       **Pre-check approvals** reste en place avant l'Exclusive Lock, que le groupe est inchangé et que le réglage d'auto-approbation
       est activé. **D3 ne démarre pas** tant que ces conditions ne sont pas vérifiées. Luna
       n'active pas l'auto-approbation et n'ajoute personne au groupe. Si Florian ne peut pas appliquer ou vérifier ce réglage, ne
       rien lancer (`BLOQUE`). Les instructions du contrôle restent vraies : aucune approbation de R0 ; pour D3, approbation seulement
       après relecture de C8.

     **Essai unique** (le déploiement DEV modifiera réellement DEV : c'est le contenu normal de la release `orders`, le même que celui
     de l'aperçu #748). Ordre :
     0. Cet essai est **distinct de R0** (run automatique rejeté, sous-étape D2b.4.4) : R0 n'est ni réapprouvé, ni relancé, ni compté.
        L'essai unique est le premier et seul run de release lancé à la main après la vérification de C1 à C7 et C9.
     1. Lancer à la main `orders/infra/pipelines/release.yml` en fixant la ressource de CI sur **N** (version explicite). Si l'interface ne
        permet pas de fixer N : `BLOQUE`. Noter l'identifiant de run R (≠ R0) et l'heure.
     2. Aperçu DEV publié, `Deploy_dev` en attente du Pre-check approvals : capturer (`p8-d3-preview-dev-run-<R>`), vérifier C8.
        **Consigne d'approbation à l'utilisateur** : approuver `Deploy_dev` seulement si C8 est vrai et si aucune autre exécution n'a
        démarré ; sinon rejeter et prévenir. Luna n'approuve jamais.
     3. Résultat DEV, trois cas exclusifs :
        a. `Succeeded` : vérifier dans `ifs-report-dev` (`ifs-report/v2`) `approvedPreviewFingerprint` = `fingerprint` de l'aperçu publié.
           Passer à PRD : même procédure (aperçu PRD capturé, mêmes effets exigés qu'en C8, aucune livraison applicative entre aperçu
           et approbation), approbation par l'utilisateur, puis dans `ifs-report-prd` égalité des empreintes approuvée/appliquée, image D2
           conservée, `/health` = 200. Capturer `p8-d3-result-dev-run-<R>` et `p8-d3-result-prd-run-<R>`. Seul ce cas permet de consigner
           P8 **OK** dans `resultats.md`.
        b. `FailedBeforeMutation` par divergence d'empreinte : **arrêt**. Ne pas approuver PRD, ne pas relancer, ne rien forcer. Télécharger
           `ifs-report-dev` avec `ifs-preview-recalculated.json` et exécuter `Compare-IfsPreviews` entre l'aperçu publié de R et cet
           aperçu recalculé (même contexte, comparaison légitime : mêmes run et identité). Consigner verdict et écarts (clés hachées,
           jamais de valeurs) dans `diagnostic-p8-748.md` § D3 : cela diagnostique **R**, pas #748. P8 reste KO.
        c. Toute autre issue (échec d'une étape, rapport `v1`, fichier de rapport absent, approbation expirée) : arrêt, capture, constat.
     4. Dans les cas a et b, comparer ensuite l'aperçu publié de R à la passe `D3-pre` (identité personnelle) ; consigner le verdict comme
        **observation**, jamais comme cause. Refaire le contrôle de coût et d'inventaire, puis capturer l'état final.
     5. **Pas de second essai.** Après b ou c, `BLOQUE` avec question à Claude (verdict de comparaison, captures). Une nouvelle release
        exige une nouvelle décision de Claude. Le nettoyage de `shop42` reste suspendu jusqu'à la réussite de P8 (cas a) et des autres
        conditions de la procédure T.
1. Avant toute commande qui crée, déploie ou modifie des ressources Azure, appliquer la décision de Florian du 2026-10-17 :
   aucun plafond personnel n'est fixé et la poursuite est autorisée sous contrôle des coûts. Cette décision remplace l'exigence
   d'un plafond numérique ; elle ne transforme pas le budget préexistant en coupe-circuit. Avant chaque action Azure significative,
   actualiser dans `NEXT.md` le coût réel, la prévision disponible et l'estimation complète France Central, puis confirmer avec
   Florian que l'autorisation de poursuivre reste valable. Maintenir l'inventaire de référence et les contrôles au début/à la fin
   de chaque session, avant/après chaque action significative et après chaque nettoyage. Si les données ne sont pas disponibles ou si
   Florian retire son autorisation, ne pas créer ni déployer de ressources. Une fois ce contrôle effectué, exécuter la
   recette dans l'ordre P1, P9, P3, P8, P2, P4, P5, P7, avec un seul jeu Azure à la fois. Après chaque run, capturer les
   réponses Azure DevOps (étape 3 bis de P-07) et consigner son résultat. `P-08` reste `EN_COURS` jusqu'au dernier
   nettoyage ; ne pas demander à l'utilisateur de rejouer les preuves déjà exécutées par Luna.
2. Pour chaque KO observé pendant la recette (journal du run, message) : si la cause est une erreur de mise en œuvre de la sortie
   de référence (le comportement attendu est clair dans la spec et le plan), corriger, ajouter le test Pester
   qui l'aurait détectée, commiter `fix(reference): …` ; si la cause est une question de conception, ne rien
   corriger : question pour Claude.
3. Consigner chaque résultat exécuté dans `docs/plan/preuves/resultats.md`.
4. **Coût et propriété** : vérifier et consigner le coût au démarrage et à la fin de chaque session, avant et après chaque
   déploiement ou run Azure significatif, puis après chaque nettoyage. Si Florian retire son autorisation, arrêter les nouveaux runs
   et appliquer, sur sa demande, la procédure d'urgence aux seuls IDs créés par Luna consignés dans
   [`inventaires-azure.md`](preuves/inventaires-azure.md) ; ne jamais supprimer une ressource préexistante.

✅ **Vérification automatique.** Après chaque correction : `Test-Reference*.ps1` et Pester verts.

🧪 **Test de recette.** Luna déroule [`recettes/01-preuves.md`](recettes/01-preuves.md) en entier ; l'utilisateur intervient
uniquement si une étape requiert son authentification ou une action réellement manuelle.

🧠 **Mémoire.** `08-runtime-and-orchestration.md` : ce que les preuves ont appris (limites du what-if des piles,
délais de propagation RBAC observés, temps de chaque release).

📌 **Hors dépôt.** Décision de coût de Florian, estimation/coût actualisés et ressources Azure créées par les preuves : dans `NEXT.md`.
Ces ressources sont **supprimées après leur dernière preuve dépendante** ([DT-42](../technique/01-decisions.md#dt-42--coût-des-preuves-éphémères-azure),
[recette § 10](recettes/01-preuves.md#10-supprimer-les-ressources-azure-et-consigner-les-résultats)), plus « gardées pour J0 » :
le jalon 0 redéploie depuis des abonnements vides. Les preuves Azure DevOps et `resultats.md` restent jusqu'à R-03.

### 🔒 R-03 — Revue des preuves

**Périmètre** : `P-07`, `P-08`, et les résultats de `docs/plan/preuves/resultats.md`.

**Claude** : analyse chaque KO, décide des changements de conception (nouvelles `DT-nn`, corrections de la sortie
de référence en tâches `R-03-Cn`), décide du sort de `IFS_TEST_ABORT_AFTER` dans la sortie générée, met à jour le
statut des preuves dans [04 § 7.2](../specs/04-perimetre-et-lots.md) (« prototypée » si la preuve est passée sur la
sortie écrite à la main ; « vérifiée » viendra quand IFS la générera, au jalon 0), puis vérifie que le jalon 0
reste exécutable tel qu'écrit.

**Recette utilisateur** : [`recettes/01-preuves.md`](recettes/01-preuves.md) complète.

**Après approbation** : vous fusionnez ; branche suivante `impl/j0-acces`.
