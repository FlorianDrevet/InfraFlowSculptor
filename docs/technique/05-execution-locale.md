# 05 — Exécution locale (Aspire et émulateurs)

## 1. Une commande

```powershell
aspire run          # depuis src/backend (aspire.config.json pointe l'AppHost)
# ou
dotnet run --project src/backend/InfraFlowSculptor.AppHost
```

Le tableau de bord Aspire s'ouvre (URL affichée dans la console) : chaque ressource, ses journaux, ses
traces OpenTelemetry et ses métriques. Prérequis : Docker Desktop démarré ([S-01](../plan/00-socle.md)).

## 2. Ressources de l'AppHost

Tout service Azure qu'IFS utilise a, en local, soit l'**émulateur officiel**, soit le **conteneur équivalent** :

| Ressource Aspire | Remplace en local | Intégration Aspire | Interface d'observation |
|---|---|---|---|
| `postgres` + base `ifs` | Azure Database for PostgreSQL | `Aspire.Hosting.PostgreSQL` (`AddPostgres`, `WithDataVolume`, `WithPgWeb`) | pgweb (lien dans le tableau de bord) |
| `storage` → `blobs` | Stockage Azure (révisions, exports, rapports) | `Aspire.Hosting.Azure.Storage` (`RunAsEmulator` = Azurite) | Azure Storage Explorer sur `127.0.0.1:10000` |
| `servicebus` → files `generation`, `publication`, `tracking`, `notifications` (à sessions) | Azure Service Bus | `Aspire.Hosting.Azure.ServiceBus` (`RunAsEmulator`) | Journaux du worker |
| `redis` | Azure Managed Redis | `Aspire.Hosting.Redis` (`WithRedisInsight`) | RedisInsight |
| `mailpit` | Azure Communication Services Email | `CommunityToolkit.Aspire.Hosting.MailPit` | Interface MailPit (tous les e-mails envoyés) |
| `keycloak` (royaume `ifs`) | Entra ID | `Aspire.Hosting.Keycloak` (`WithRealmImport`) ; repli : conteneur `quay.io/keycloak/keycloak` | Console Keycloak |
| `gitea` | Azure Repos / GitHub (publication) | Conteneur `gitea/gitea` (`AddContainer`) | Interface Gitea : dépôts, branches, pull requests |
| `keyvault` | Azure Key Vault (jetons git de repli) | Émulateur communautaire si disponible pour Aspire 13, sinon `DevelopmentSecretStore` ([DT-12](01-decisions.md#dt-12--secrets-propres-à-ifs--key-vault)) | — |
| `api` | Container App `api` | `AddProject<InfraFlowSculptor_Api>` | Scalar `/scalar` |
| `worker` | Container App `worker` | `AddProject<InfraFlowSculptor_Worker>` | Journaux |
| `web` | Container App `web` | `Aspire.Hosting.JavaScript` (`AddJavaScriptApp`, script `start`, port fixe 4200) | http://localhost:4200 |

**Sans émulateur** (et donc hors exécution locale) : Azure DevOps Pipelines et l'exécution réelle des
pipelines générés, Azure Resource Manager (what-if, piles de déploiement), le DNS public des noms Azure.
Les tests les remplacent par des réponses enregistrées (WireMock.Net) ; la recette les exerce sur une vraie
organisation Azure DevOps et de vrais abonnements ([plan, phase P](../plan/01-preuves.md)).

**Chaînes de connexion et identité managée.** Aspire passe aux processus des chaînes de connexion vers les émulateurs
(`ConnectionStrings__<nom>`) ; en Azure, ils reçoivent à la place `Azure__<nom>__Endpoint` et se connectent par
identité managée. Une seule règle le décide : [DT-33](01-decisions.md#dt-33--identité-managée-partout--chaîne-de-connexion-seulement-en-local).

Ports fixes (les URI de redirection OIDC et l'émetteur des jetons en dépendent) : `web` 4200, `keycloak`
8080, `gitea` 3000. Les autres ports sont attribués par Aspire.

## 3. Utilisateurs de démonstration (royaume Keycloak `ifs`)

Guide complet de Keycloak (console, ajout d'utilisateurs, jetons, remise à zéro) : [09](09-keycloak.md).

Fichier : `src/backend/InfraFlowSculptor.AppHost/Realms/ifs-realm.json`. Mot de passe de tous les comptes :
`Ifs-Demo-2026!` (développement uniquement).

| Utilisateur | `tid` (simulé) | Rôle dans les tests | Particularité |
|---|---|---|---|
| `alice@contoso.example` | `11111111-1111-1111-1111-111111111111` (Contoso) | Administratrice de l'organisation Contoso, propriétaire du projet `shop` | — |
| `bob@contoso.example` | Contoso | Contributeur | — |
| `chloe@contoso.example` | Contoso | Lectrice | — |
| `david@fabrikam.example` | `22222222-2222-2222-2222-222222222222` (Fabrikam) | Administrateur d'une **autre** organisation | Tests d'isolation |
| `emma@outlook.example` | `9188040d-6c67-4c5b-b112-36a304b66dad` (comptes personnels) | Compte personnel | Exclu si l'organisation restreint ses tenants |
| `nina@contoso.example` | Contoso | Invitée non vérifiée | `email_verified = false` : l'acceptation d'invitation échoue |
| `ops@ifs.example` | `33333333-3333-3333-3333-333333333333` (tenant IFS) | Opérateur support, éditeur de catalogue | Rôles internes, `amr = ["pwd","mfa"]` |

Les clients du royaume : `ifs-web` (public, PKCE, redirections `http://localhost:4200/*`), `ifs-api`
(audience), `ifs-scalar` (public, PKCE, redirection `/scalar/*`). Les mappers ajoutent `oid` (attribut
utilisateur), `tid`, `email`, `email_verified`, `name`, `roles`, `amr`.

## 4. Données de démonstration

`dotnet run --project src/backend/InfraFlowSculptor.Api -- seed demo` (ou le bouton « Réinitialiser les
données de démonstration » de la galerie de développement) crée, de façon idempotente : l'organisation
Contoso (alice, bob, chloe), l'organisation Fabrikam (david), et, à partir de J0, le projet pilote de
référence complet ([`../plan/reference-pilote.md`](../plan/reference-pilote.md)). Les recettes partent de
cet état.

## 5. Gitea (émulateur git)

Initialisation, une fois par machine (idempotente) :

```powershell
pwsh tools/dev/gitea-init.ps1
```

Elle crée l'utilisateur administrateur `ifs-dev`, l'organisation `contoso`, les dépôts `shop`, `shop-infra`,
`shop-app` avec une branche `main`, et écrit le jeton de `ifs-dev` dans les secrets utilisateur de l'AppHost.
Une connexion git « Gitea (développement) » est alors proposée dans l'organisation Contoso.

## 6. Dépannage

| Symptôme | Cause probable | Action |
|---|---|---|
| L'émulateur Service Bus ne démarre pas | Docker sans assez de mémoire (il embarque SQL Edge) | Allouer 6 Go à Docker Desktop |
| `401` sur toutes les requêtes | Émetteur du jeton ≠ `http://localhost:8080/realms/ifs` | Ne pas changer le port de Keycloak ; vider le stockage local du navigateur |
| `web` démarre avant l'API et affiche une erreur | `config.json` écrit avant que l'URL de l'API soit connue | `WaitFor(api)` dans l'AppHost ; relancer la ressource `web` |
| Migrations non appliquées | Environnement différent de `Development` | Vérifier `ASPNETCORE_ENVIRONMENT` dans le tableau de bord |
