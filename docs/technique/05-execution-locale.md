# 05 — Exécution locale (Aspire et émulateurs)

## 1. Une commande

```powershell
aspire start        # depuis src/backend (aspire.config.json pointe l'AppHost)
```

Le tableau de bord Aspire s'ouvre (URL affichée dans la console) : chaque ressource, ses journaux, ses
traces OpenTelemetry et ses métriques. Prérequis : Docker Desktop démarré ([S-01](../plan/00-socle.md)).

## 2. Ressources de l'AppHost

Tout service Azure qu'IFS utilise a, en local, soit l'**émulateur officiel**, soit le **conteneur équivalent** :

| Ressource Aspire | Remplace en local | Intégration Aspire | Interface d'observation |
|---|---|---|---|
| `postgres` + base `ifs` | Azure Database for PostgreSQL | `Aspire.Hosting.PostgreSQL` (`AddPostgres`, `WithDataVolume`, `WithPgWeb`) | pgweb (lien dans le tableau de bord) |
| `storage` → `blobs` | Stockage Azure (révisions, exports, rapports) | `Aspire.Hosting.Azure.Storage` (`RunAsEmulator` = Azurite) | Azure Storage Explorer sur `127.0.0.1:10000` |
| `servicebus` → files `generation`, `publication`, `tracking`, `notifications` (à sessions) | Azure Service Bus | `Aspire.Hosting.Azure.ServiceBus` 13.5.3 (`RunAsEmulator`, namespace `sbemulatorns`, lifetime persistant partagé avec le sidecar SQL) | Journaux du worker |
| `redis` | Azure Managed Redis | `Aspire.Hosting.Redis` (`WithRedisInsight`, conteneur Redis persistant) | RedisInsight |
| `mailpit` | Azure Communication Services Email | `CommunityToolkit.Aspire.Hosting.MailPit` (conteneur persistant) | Interface MailPit (tous les e-mails envoyés) |
| `keycloak` (royaume `ifs`) | Entra ID | `Aspire.Hosting.Keycloak` 13.5.3-preview.1.26425.3 (`WithRealmImport`, HTTPS, conteneur et volume persistants) ; repli : conteneur `quay.io/keycloak/keycloak` | Console Keycloak |
| `gitea` | Azure Repos / GitHub (publication) | Conteneur `gitea/gitea` (`AddContainer`, volume et lifetime persistants) | Interface Gitea : dépôts, branches, pull requests |
| `keyvault` | Azure Key Vault (jetons git de repli) | `AzureKeyVaultEmulator.Aspire.Hosting` 3.1.3 ([DT-12](01-decisions.md#dt-12--secrets-propres-à-ifs--key-vault)) | — |
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
8080 en HTTPS, l'API 5257 en HTTP et 7246 en HTTPS (profil de lancement .NET), `gitea` 3000. Les ports des
autres ressources sont attribués par Aspire.

Les points de terminaison de démonstration `/v1/dev/*` sont mappés en `Development` et `Testing` afin que les
recettes d'acceptation puissent exercer le worker via l'AppHost. En `Testing`, l'AppHost fournit une clé de
signature éphémère réservée aux tests ; Keycloak et les ressources de démonstration ne sont pas démarrés.

Le Worker renouvelle son bail de tâche planifiée toutes les 20 secondes (bail d'une minute), puis programme la
prochaine exécution à l'intervalle prévu. Un échec applique un backoff exponentiel plafonné à cinq minutes. Les
envois outbox partagent un sender par file et sont bornés à 15 secondes par lot ; après dix échecs, le message est
marqué en échec définitif et reste consultable dans `outbox_messages.failed_at`. L'idempotence des messages bus
repose sur `processed_jobs`, car la détection de doublons Service Bus n'est pas activée. Quand le délai global est
atteint, le lot s'arrête ; les messages restants n'ont pas consommé de tentative et seront repris au lot suivant.

### Durée de vie entre les lancements

PostgreSQL, Azurite, Service Bus et son sidecar SQL, Redis, MailPit, Keycloak et Gitea utilisent
`ContainerLifetime.Persistent` : leurs conteneurs restent démarrés quand l'AppHost s'arrête et Aspire peut les
réutiliser au prochain `aspire start` si leur configuration n'a pas changé. Docker doit rester démarré. L'API,
le Worker, le Web et les ressources d'observation restent à durée de vie de session.

La durée persistante du conteneur ne protège pas les données si Aspire doit le recréer. Les volumes de PostgreSQL,
Azurite, Keycloak et Gitea protègent leurs données ; Redis et MailPit n'ont pas de volume, leur état peut donc être
perdu lors d'une recréation. L'émulateur Key Vault et les conteneurs d'observation restent à durée de vie de session.

Le premier démarrage à froid du Service Bus peut dépasser une minute pendant que son sidecar SQL démarre. Sur la
machine de développement, le premier état sain est arrivé après environ 70 secondes ; après `aspire stop` puis
`aspire start`, les deux conteneurs persistants étaient déjà sains en moins de cinq secondes. Pour un démarrage à
froid, attendre jusqu'à deux minutes avec `aspire wait servicebus --timeout 120`.

L'émulateur Service Bus réserve le nom d'espace de noms `sbemulatorns`. L'AppHost le configure dans son
`UserConfig` avant de lancer le conteneur ; le nom de ressource Aspire reste `servicebus`. Les quatre files
exigent une session.

## 3. Utilisateurs de démonstration (royaume Keycloak `ifs`)

Guide complet de Keycloak (console, ajout d'utilisateurs, jetons, remise à zéro) : [09](09-keycloak.md).

Fichier : `src/backend/InfraFlowSculptor.AppHost/Realms/ifs-realm.json`. Mot de passe de tous les comptes :
`Ifs-Demo-2026!` (développement uniquement).

| Utilisateur | `oid` (stable) | `tid` (simulé) | Rôle dans les tests | Particularité |
|---|---|---|---|---|
| `alice@contoso.example` | `aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa` | `11111111-1111-1111-1111-111111111111` (Contoso) | Administratrice de l'organisation Contoso, propriétaire du projet `shop` | — |
| `bob@contoso.example` | `bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb` | Contoso | Contributeur | — |
| `chloe@contoso.example` | `cccccccc-cccc-4ccc-8ccc-cccccccccccc` | Contoso | Lectrice | — |
| `david@fabrikam.example` | `dddddddd-dddd-4ddd-8ddd-dddddddddddd` | `22222222-2222-2222-2222-222222222222` (Fabrikam) | Administrateur d'une **autre** organisation | Tests d'isolation |
| `emma@outlook.example` | `eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee` | `9188040d-6c67-4c5b-b112-36a304b66dad` (comptes personnels) | Compte personnel | Exclu si l'organisation restreint ses tenants |
| `nina@contoso.example` | `ffffffff-ffff-4fff-8fff-ffffffffffff` | Contoso | Invitée non vérifiée | `email_verified = false` : l'acceptation d'invitation échoue |
| `ops@ifs.example` | `99999999-9999-4999-8999-999999999999` | `33333333-3333-3333-3333-333333333333` (tenant IFS) | Opérateur support, éditeur de catalogue | Rôles internes, `amr = ["pwd","mfa"]` |

Les clients du royaume : `ifs-web` (public, PKCE, redirections `http://localhost:4200/*`), `ifs-api`
(audience), `ifs-scalar` (public, PKCE, redirection `/scalar/*`). Les comptes de démonstration reçoivent aussi les
rôles Keycloak intégrés `account/manage-account` et `account/view-profile` pour ouvrir la console de compte. Les
mappers ajoutent `oid` (attribut
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
| L'émulateur Service Bus ne démarre pas | Le sidecar SQL n'est pas encore sain ou Aspire garde un ancien état de ressource | Vérifier `servicebus-mssql` ; si le redémarrage échoue pendant la suppression du conteneur, arrêter puis relancer tout l'AppHost et consulter les journaux des deux ressources |
| `worker` signale « did not have any active links in the past 300000 milliseconds » pendant `AcceptSession` | Une fermeture de lien inactif est rapportée pour l'émulateur local. Le dépôt de l'émulateur documente un symptôme voisin sur Windows amd64, mais pendant `RenewLock` ; la source diffère du journal IFS | Le code classe en `Warning` uniquement cette signature exacte, transitoire, de `AcceptSession`, si l'AppHost indique l'émulateur ou si le Worker `Development` se connecte à un endpoint loopback. Vérifier qu'un job de ping soumis ensuite est traité. Toute autre source, raison ou message reste une erreur à investiguer ([issue 142](https://github.com/Azure/azure-service-bus-emulator-installer/issues/142)) |
| `Invalid parameter: redirect_uri` ou 401 de la console de compte | Le royaume existe déjà et `WithRealmImport` ignore son import ; les données du volume peuvent être plus anciennes que `ifs-realm.json` | Mettre à jour le client ou les rôles précis dans la console d'administration Keycloak ; ne supprimer le volume que si les données locales peuvent être perdues |
| `invalid_scope` pour `phone` ou `address` | Ces scopes optionnels ne sont pas inclus dans le jeu défini par `ifs-realm.json` | Ajouter le scope nécessaire au JSON et vérifier son import ; `offline_access` est un scope intégré optionnel, réservé aux consommateurs hors ligne, et n'est pas demandé par l'application Web (voir `09-keycloak.md` § 9) |
| Journal Keycloak `Offline tokens not allowed for the user or client` | Un client demande explicitement `offline_access`, mais le scope intégré ou le rôle utilisateur manque | Pour l'application Web, vérifier que `/config.json` ne demande plus `offline_access`. Pour un client hors ligne, lier le scope intégré en *Optional* et ajouter le rôle dans *Users* → *Role mapping* (voir `09-keycloak.md` § 9) |
| `invalid_grant` sur la console d'administration Keycloak | Le volume persistant garde un ancien mot de passe administrateur ; le secret Aspire n'est lu qu'à la création | [09, § 8](09-keycloak.md#8-récupérer-laccès-administrateur-invalid_grant) : ajouter un administrateur temporaire, sans supprimer le volume |
| `401` sur toutes les requêtes | Émetteur du jeton ≠ `https://localhost:8080/realms/ifs` | Ne pas changer le port de Keycloak ; vider le stockage local du navigateur |
| `web` démarre avant l'API et affiche une erreur | `config.json` écrit avant que l'URL de l'API soit connue | `WaitFor(api)` dans l'AppHost ; relancer la ressource `web` |
| Migrations non appliquées | Environnement différent de `Development` | Vérifier `ASPNETCORE_ENVIRONMENT` dans le tableau de bord |
