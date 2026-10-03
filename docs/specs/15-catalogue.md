# 15 — Catalogue des types de ressources

> Les valeurs de ce document constituent la **version initiale du catalogue (2026-10)**. Elles vivent
> dans les descripteurs ([DEC-13](03-decisions.md)) et sont revues à chaque mise à jour du catalogue,
> en particulier les listes de versions, de SKU et de régions. Avant l'implémentation de chaque type,
> elles sont revérifiées contre la documentation Microsoft et contre les modules et fournisseurs épinglés
> de chaque langage ([DEC-45](03-decisions.md)).

## 1. Le descripteur

Chaque type est décrit par un descripteur versionné qui contient :

| Section | Contenu |
|---|---|
| Identité | Nom technique, libellés fr/en, catégorie, icône, type ARM, lot de disponibilité. |
| Nommage | Abréviation, caractères autorisés, casse, séparateurs, longueur min/max, premier/dernier caractère, portée d'unicité (`global`, `groupe de ressources`, `parent`), domaine DNS de disponibilité. |
| Propriétés | Liste des propriétés avec les attributs de [14 § 3](14-modele-des-ressources.md). |
| Valeurs fixes | Réglages imposés par IFS, non modifiables, affichés en lecture (exemple : TLS 1.2). |
| Enfants | Types d'enfants, leurs propriétés et leurs règles de nom. |
| Identités | Supporte identité système, identités affectées (nombre max). |
| Liaisons | Types de liaison acceptés en source, obligatoires ou non ([16](16-liaisons-identites-et-acces.md)). |
| Sorties | Nom, description, sensible ou non, condition. |
| Rôles | Rôles Azure intégrés applicables quand le type est **cible** d'une attribution : nom, identifiant, description, lien vers la documentation. Groupes de rôles équivalents (exemple : « lecture de secrets » = Secrets User, Secrets Officer, Administrator). |
| Accès aux données | Rôles de données hors RBAC (bases de données), le cas échéant. |
| Exposition réseau | Modes supportés, `groupIds` de point de terminaison privé, zone DNS privée, conditions (SKU). |
| Diagnostics | Supporte les paramètres de diagnostic. |
| Paramètres applicatifs | Destinataire de variables d'environnement, ou de clés. |
| Prise en charge par langage | Pour Bicep, Terraform et Pulumi : pris en charge ou non (type entier, propriété ou valeur) ([DEC-43](03-decisions.md)). |
| Génération par langage | Bicep : module AVM et version. Terraform : module AVM Terraform ou ressource `azurerm`/`azapi`, versions. Pulumi : ressource Azure Native. Pour chacun : correspondance propriété → entrée du module ou de la ressource ([DEC-45](03-decisions.md)). |
| Dépréciations | Valeurs dépréciées, date de dépréciation, date de refus. |

## 2. Conventions des tableaux

- **S** : surchargeable par environnement.
- **V** : verrouillée après publication.
- **I** : irréversible (le sens autorisé est indiqué).
- *Fixe* : valeur imposée par IFS, non modifiable.

Les champs communs ([14 § 2](14-modele-des-ressources.md)) ne sont pas répétés.

## 3. Types du lot 1

| Catégorie | Types |
|---|---|
| Observabilité | Log Analytics, Application Insights |
| Sécurité | Key Vault, identité managée |
| Données | Compte de stockage, serveur Azure SQL, base Azure SQL, PostgreSQL serveur flexible |
| Calcul | Plan App Service, Web App, Function App, environnement Container Apps, Container App |
| Plateforme | Registre de conteneurs, App Configuration |
| Messagerie | Service Bus |

### 3.1 LogAnalyticsWorkspace — espace Log Analytics

Abréviation `log` · nom 4–63, lettres, chiffres, tirets, commence et finit par un alphanumérique · unicité : groupe de ressources.

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| Tarification | `PerGB2018`, `CapacityReservation` | `PerGB2018` | S |
| Niveau de réservation (Go/jour) | 100, 200, 300, 400, 500, 1000, 2000, 5000 ; si `CapacityReservation` | 100 | S |
| Rétention (jours) | 30 à 730 | 30 | S |
| Quota journalier (Go) | Aucun, ou décimal > 0 | Aucun | S |

Sorties : `id`, `customerId`.
Rôles : Log Analytics Reader, Log Analytics Contributor, Monitoring Reader.

### 3.2 ApplicationInsights

Abréviation `appi` · nom 1–260, sans `% & \ ? /` · unicité : groupe de ressources.

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| Espace Log Analytics | Liaison **Journalisation** obligatoire | — | — |
| Type d'application | `web`, `other` | `web` | V |
| Échantillonnage à l'ingestion (%) | 0,1 à 100 | 100 | S |
| Désactiver l'authentification locale | Booléen | Non | S |

Si l'authentification locale est désactivée, les applications liées par **Télémétrie** reçoivent le rôle
implicite Monitoring Metrics Publisher ([16](16-liaisons-identites-et-acces.md)).

Sorties : `connectionString` (non sensible), `id`.
Rôles : Monitoring Metrics Publisher, Application Insights Component Contributor, Monitoring Reader.

### 3.3 KeyVault

Abréviation `kv` · nom 3–24, lettres, chiffres, tirets, commence par une lettre, pas de tirets consécutifs · unicité : globale (`vault.azure.net`).

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| SKU | `standard`, `premium` | `standard` | S |
| Protection contre la purge | Booléen | Oui | S, I (non → oui) |
| Rétention après suppression (jours) | 7 à 90 | 90 | V |

*Fixe* : autorisation RBAC (pas de stratégies d'accès), suppression réversible (imposée par Azure).

Sorties : `vaultUri`, `name`, `id`.
Rôles : Key Vault Administrator, Secrets Officer, Secrets User, Certificates Officer, Certificate User,
Crypto Officer, Crypto User, Crypto Service Encryption User, Key Vault Reader. Groupe « lecture de
secrets » : Secrets User, Secrets Officer, Administrator.
Exposition : publique, restreinte, privée *(lot 2, `vault`)*.

### 3.4 UserAssignedIdentity — identité managée

Abréviation `id` · nom 3–128, lettres, chiffres, tirets, soulignés, commence par un alphanumérique · unicité : groupe de ressources.

Aucune propriété. Sorties : `principalId`, `clientId`, `id`. Rôle applicable : Managed Identity Operator.

### 3.5 StorageAccount — compte de stockage

Abréviation `st` · nom 3–24, minuscules et chiffres uniquement · unicité : globale (`blob.core.windows.net`).

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| Redondance | `Standard_LRS`, `Standard_ZRS`, `Standard_GRS`, `Standard_GZRS`, `Standard_RAGRS`, `Standard_RAGZRS` | `Standard_LRS` | S |
| Niveau d'accès par défaut | `Hot`, `Cool`, `Cold` | `Hot` | S |
| Accès par clé partagée | Booléen | Non | S |
| Espace de noms hiérarchique (Data Lake) | Booléen | Non | V |
| Suppression réversible des blobs (jours) | 0 (désactivée) ou 1 à 365 | 7 | S |
| Suppression réversible des conteneurs (jours) | 0 ou 1 à 365 | 7 | S |
| Versioning des blobs | Booléen | Non | S |

*Fixe* : type `StorageV2`, HTTPS seul, TLS 1.2, accès public anonyme aux blobs désactivé.

| Enfant | Champs | Règles |
|---|---|---|
| Conteneur blob | Nom | 3–63, minuscules, chiffres, tirets. |
| File d'attente | Nom | 3–63, minuscules, chiffres, tirets. |
| Table | Nom | 3–63, alphanumérique, commence par une lettre. |
| Partage de fichiers | Nom, quota (Go, S) | 3–63, minuscules, chiffres, tirets. |
| Règle de cycle de vie | Nom, préfixes ciblés, jours avant `Cool`, avant `Cold`, avant suppression (S) | Au moins une action. |
| Règle CORS | Service (`Blob`, `Queue`, `Table`, `File`), origines, méthodes, en-têtes autorisés et exposés, durée de cache | — |

Sorties : `name`, `id`, `blobEndpoint`, `queueEndpoint`, `tableEndpoint`, `fileEndpoint`,
`dfsEndpoint` (si Data Lake). Sortie sensible : `connectionString`, disponible seulement si l'accès par
clé partagée est activé.
Rôles : Storage Blob Data Owner / Contributor / Reader, Storage Queue Data Contributor / Reader /
Message Sender / Message Processor, Storage Table Data Contributor / Reader, Storage Account
Contributor, Reader.
Exposition : publique, restreinte, privée *(lot 2, `blob`, `queue`, `table`, `file`, `dfs`, `web`)*.

### 3.6 AppServicePlan — plan App Service

Abréviation `asp` · nom 1–60, lettres, chiffres, tirets · unicité : groupe de ressources.

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| Système | `Linux`, `Windows` | `Linux` | V |
| SKU | Dédiés : `B1`–`B3`, `S1`–`S3`, `P0v3`–`P3v3`, `P1mv3`–`P5mv3`. Functions : `EP1`–`EP3` (Premium élastique), `FC1` (Flex Consumption, Linux) | `P0v3` | S |
| Nombre d'instances | 1 à 30 ; non applicable à `FC1` | 1 | S |
| Redondance de zone | Booléen ; SKU Premium v3 ou `EP*` ; nombre minimal d'instances fixé par le descripteur | Non | V |

Contraintes : `EP*` et `FC1` n'hébergent que des Function Apps ; `FC1` n'accepte qu'un système Linux.
Sorties : `id`, `name`. Rôles : Website Contributor, Reader.

### 3.7 WebApp

Abréviation `app` · nom 2–60, lettres, chiffres, tirets, sans tiret en bord · unicité : globale (`azurewebsites.net`).

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| Plan | Liaison **Hébergement** obligatoire vers un plan dédié | — | — |
| Mode | `Code`, `Container` | `Code` | — |
| Pile et version (mode Code) | .NET 8, 9, 10 ; Node 20, 22 ; Python 3.11, 3.12, 3.13 (Linux) ; Java 17, 21 ; PHP 8.3, 8.4 (Linux) | .NET 10 | — |
| Always On | Booléen ; refusé sur `F1` | Oui | S |
| Chemin du contrôle de santé | Texte commençant par `/` | Vide | S |
| Affinité de session (ARR) | Booléen | Non | S |

*Fixe* : HTTPS seul, TLS 1.2, FTP désactivé, authentification de base de publication désactivée, HTTP/2.

Sorties : `defaultHostName`, `name`, `id`. Rôles : Website Contributor, Reader.
Destinataire de variables d'environnement. Application ([19](19-applications-build-et-deploiement.md)).
Exposition : publique, restreinte, privée *(lot 2, `sites`)*.

### 3.8 FunctionApp

Abréviation `func` · mêmes règles de nom que WebApp.

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| Plan | Liaison **Hébergement** obligatoire | — | — |
| Stockage hôte | Liaison **Stockage hôte** obligatoire vers un compte de stockage | — | — |
| Mode | `Code`, `Container` (refusé sur `FC1`) | `Code` | — |
| Pile et version | .NET isolé 8, 9, 10 ; Node 20, 22 ; Python 3.11, 3.12 ; Java 17, 21 ; PowerShell 7.4 (sous-ensemble sur `FC1` fixé par le descripteur) | .NET isolé 10 | — |
| Instances maximales (`FC1`) | 40 à 1000 | 100 | S |
| Mémoire par instance (`FC1`, Mo) | 2048, 4096 | 2048 | S |
| Always On (plan dédié) | Booléen | Oui | S |

*Fixe* : comme WebApp.
Sorties, rôles, exposition : comme WebApp.

### 3.9 ContainerRegistry — registre de conteneurs

Abréviation `cr` · nom 5–50, alphanumérique uniquement · unicité : globale (`azurecr.io`).

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| SKU | `Basic`, `Standard`, `Premium` | `Standard` | S |
| Redondance de zone | Booléen ; `Premium` | Non | V |
| Rétention des manifestes non tagués (jours) | 0 (désactivée) à 365 ; `Premium` | 0 | S |

*Fixe* : utilisateur admin désactivé, tirage anonyme désactivé ([DEC-26](03-decisions.md)).
Sorties : `loginServer`, `name`, `id`. Rôles : AcrPull, AcrPush, AcrDelete, Reader.
Exposition : publique, restreinte et privée réservées au SKU `Premium` (privée *lot 2*, `registry`).

### 3.10 ContainerAppsEnvironment — environnement Container Apps

Abréviation `cae` · nom 2–32, minuscules, chiffres, tirets, commence par une lettre · unicité : groupe de ressources.

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| Journaux | Liaison **Journalisation** facultative vers un Log Analytics | — | — |
| Profils de charge dédiés | Liste : nom, type (`D4`, `D8`, `D16`, `D32`, `E4`, `E8`, `E16`, `E32`), instances min et max (S). Le profil `Consumption` existe toujours. | Aucun | — |
| Redondance de zone | Booléen ; exige un réseau intégré *(lot 2)* | Non | V |

Sorties : `id`, `defaultDomain`, `staticIp`. Rôles : Contributor, Reader.

### 3.11 ContainerApp

Abréviation `ca` · nom 2–32, minuscules, chiffres, tirets, commence par une lettre, sans `--` · unicité : groupe de ressources.

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| Environnement | Liaison **Hébergement** obligatoire | — | — |
| Profil de charge | `Consumption` ou un profil dédié de l'environnement | `Consumption` | — |
| CPU / mémoire | Couples autorisés par le descripteur (en `Consumption` : 0,25/0,5Gi à 4/8Gi, rapport 1:2) | 0,5 / 1Gi | S |
| Réplicas min / max | min 0 à max ; max ≤ limite du descripteur | 0 / 10 | S |
| Ingress | `Aucun`, `Interne`, `Externe` | `Externe` | S |
| Port cible | 1 à 65535 | 8080 | — |
| Transport | `auto`, `http`, `http2`, `tcp` | `auto` | — |
| Mise à l'échelle HTTP (requêtes simultanées) | 1 à 1000 | 10 | S |
| Sondes `liveness`, `readiness`, `startup` | Chemin (commence par `/`), port, délai initial, période | Aucune | S |
| Mode de révision | `Single`, `Multiple` | `Single` | — |

Le tirage d'image se fait toujours par une identité affectée ([DEC-26](03-decisions.md)).
Sorties : `fqdn`, `name`, `id`. Rôles : Contributor, Reader. Destinataire de variables d'environnement.
Application.

### 3.12 SqlServer — serveur Azure SQL

Abréviation `sql` · nom 1–63, minuscules, chiffres, tirets, sans tiret en bord · unicité : globale (`database.windows.net`).

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| Administrateur Entra | Groupe Entra : nom affiché et identifiant d'objet | — (obligatoire) | S |

*Fixe* : authentification Entra seule ([DEC-26](03-decisions.md)), TLS 1.2.
Le kit d'installation ajoute l'identité de déploiement de chaque cible au groupe administrateur, ou
l'inscrit dans la liste de contrôle ([23](23-kit-installation.md)) : elle en a besoin pour créer les
accès aux données.

Sorties : `fullyQualifiedDomainName`, `name`, `id`. Rôles : SQL Server Contributor, SQL Security
Manager, Reader.
Exposition : publique (règle « services Azure »), restreinte (adresses IP), privée *(lot 2, `sqlServer`)*.

### 3.13 SqlDatabase — base Azure SQL

Abréviation `sqldb` · nom 1–128, sans `< > * % & : \ / ?`, sans point final · unicité : parent (serveur).

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| Serveur | Liaison **Hébergement** obligatoire vers un serveur SQL du même composant ; la base est déployée dans le groupe de ressources du serveur | — | — |
| SKU | `Basic`, `S0`–`S3`, `GP_S_Gen5_1`, `GP_S_Gen5_2`, `GP_S_Gen5_4` (serverless), `GP_Gen5_2`, `GP_Gen5_4`, `GP_Gen5_8`, `HS_Gen5_2`, `HS_Gen5_4` | `GP_S_Gen5_1` | S |
| Taille max (Go) | Bornes selon SKU | 32 | S |
| Pause automatique (min, serverless) | Désactivée, ou 15 à 10080 | 60 | S |
| Capacité minimale (vCore, serverless) | Selon SKU | 0,5 | S |
| Redondance de zone | Booléen | Non | S |
| Redondance des sauvegardes | `Local`, `Zone`, `Geo` | `Geo` | S |
| Classement | Texte | `SQL_Latin1_General_CP1_CI_AS` | V |

Sorties : `name`, `id`. Rôles : SQL DB Contributor, Reader. Cible d'**accès aux données**
([16 § 6](16-liaisons-identites-et-acces.md)).

### 3.14 PostgreSqlFlexibleServer — PostgreSQL serveur flexible

Abréviation `psql` · nom 3–63, minuscules, chiffres, tirets · unicité : globale (`postgres.database.azure.com`).

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| Version | 16, 17 | 17 | V |
| SKU | Burstable : `Standard_B1ms`, `Standard_B2s`, `Standard_B2ms` ; General Purpose : `Standard_D2ds_v5`, `Standard_D4ds_v5`, `Standard_D8ds_v5` ; Memory Optimized : `Standard_E2ds_v5`, `Standard_E4ds_v5` | `Standard_B1ms` | S |
| Stockage (Go) | 32, 64, 128, 256, 512, 1024, 2048 | 32 | S, I (croissant) |
| Rétention des sauvegardes (jours) | 7 à 35 | 7 | S |
| Sauvegarde géo-redondante | Booléen | Non | V |
| Haute disponibilité | `Désactivée`, `RedondanteEnZone`, `MêmeZone` ; refusée en Burstable | `Désactivée` | S |
| Administrateur Entra | Groupe Entra | — (obligatoire) | S |

*Fixe* : authentification par mot de passe désactivée, TLS 1.2.

| Enfant | Champs | Règles |
|---|---|---|
| Base de données | Nom ; jeu de caractères `UTF8` ; classement `en_US.utf8` | Nom 1–63, minuscules, chiffres, soulignés. |

Sorties : `fullyQualifiedDomainName`, `name`, `id`. Rôles : Contributor, Reader. Cible d'accès aux
données (sur une base enfant). Exposition : publique, restreinte, privée *(lot 2)*.

### 3.15 ServiceBusNamespace — Service Bus

Abréviation `sbns` · nom 6–50, lettres, chiffres, tirets, commence par une lettre, finit par une lettre ou un chiffre · unicité : globale (`servicebus.windows.net`).

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| SKU | `Basic`, `Standard`, `Premium` ; `Basic` refusé s'il existe des topics | `Standard` | S |
| Unités de messagerie (`Premium`) | 1, 2, 4, 8, 16 | 1 | S |
| Désactiver l'authentification locale | Booléen | Oui | S |

*Fixe* : TLS 1.2.

| Enfant | Champs (S sauf mention) | Règles |
|---|---|---|
| File | Taille max, nombre max de livraisons (1–2000, défaut 10), durée de verrouillage (30 s à 5 min, défaut 1 min), durée de vie des messages, lettres mortes à l'expiration, sessions (V), détection des doublons (V) | Nom 1–260, unique dans le namespace (avec les topics). |
| Topic | Taille max, durée de vie des messages, détection des doublons (V) | Idem. |
| Abonnement (enfant d'un topic) | Nombre max de livraisons, durée de verrouillage, sessions (V), filtre SQL facultatif | Nom 1–50, unique dans le topic. |

Sorties : `endpoint`, `fullyQualifiedNamespace`, `name`, `id`. Sortie sensible : `connectionString`,
seulement si l'authentification locale est activée.
Rôles : Azure Service Bus Data Owner, Data Sender, Data Receiver, Reader.
Exposition : publique, restreinte, privée *(lot 2, `Premium`, `namespace`)*.

### 3.16 AppConfiguration — App Configuration

Abréviation `appcs` · nom 5–50, lettres, chiffres, tirets · unicité : globale (`azconfig.io`).

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| SKU | `Free`, `Standard`, `Premium` | `Standard` | S |
| Désactiver l'authentification locale | Booléen | Oui | S |
| Protection contre la purge | Booléen ; hors `Free` | Non | S, I (non → oui) |
| Rétention après suppression (jours) | 1 à 7 ; hors `Free` | 7 | V |

Les clés sont des paramètres applicatifs ([17](17-parametres-applicatifs-et-secrets.md)). Pour qu'elles
puissent être déployées alors que l'authentification locale est désactivée, l'identité de déploiement
reçoit le rôle App Configuration Data Owner ([23](23-kit-installation.md)). En Bicep, qui passe par ARM,
l'émetteur active en plus le mode d'accès « pass-through » au plan de données ; Terraform et Pulumi
écrivent les clés directement par le plan de données.

Sorties : `endpoint`, `name`, `id`. Rôles : App Configuration Data Reader, App Configuration Data Owner,
Reader. Exposition : publique, privée *(lot 2, `configurationStores`)*.

## 4. Types des lots suivants

| Type | Lot | Points clés |
|---|---|---|
| VirtualNetwork (`vnet`) + subnets (`snet`, enfants) | 2 | Espaces d'adressage et préfixes surchargeables par environnement, délégations, contrôles CIDR ([18](18-reseau-et-exposition.md)). |
| NetworkSecurityGroup (`nsg`) | 2 | Règles (priorité, sens, accès, protocole, ports, préfixes ou tags de service). |
| PrivateDnsZone | 2 | Nom fixé par le descripteur (`privatelink.*`), type global, liens VNet. |
| CosmosDbAccount (`cosmos`) | 2 | API NoSQL uniquement (V) ; bases et conteneurs comme enfants (clé de partition V) ; débit serverless, provisionné ou autoscale ; accès aux données par rôles SQL Cosmos (hors RBAC ARM). |
| EventHubsNamespace (`evhns`) | 2 | Event hubs et groupes de consommateurs comme enfants ; authentification locale désactivée par défaut. |
| ManagedRedis (`amr`) | 2 | Azure Managed Redis (Azure Cache for Redis est en fin de vie) ; authentification Entra. |
| AIServices (`ais`) | 2 | Compte Azure AI Services ; sous-domaine personnalisé = nom Azure (requis pour l'authentification Entra) ; authentification locale désactivée par défaut. |
| FrontDoor, ApiManagement, StaticWebApp, ContainerInstance | 3 | — |

## 5. Régions (version initiale)

Le code court alimente le jeton `{region}`. Une région s'ajoute au catalogue, jamais dans un projet.

| Région | Code | Région | Code |
|---|---|---|---|
| `francecentral` | `frc` | `uksouth` | `uks` |
| `francesouth` | `frs` | `eastus` | `eus` |
| `westeurope` | `weu` | `eastus2` | `eus2` |
| `northeurope` | `neu` | `centralus` | `cus` |
| `germanywestcentral` | `gwc` | `westus2` | `wus2` |
| `switzerlandnorth` | `szn` | `westus3` | `wus3` |
| `swedencentral` | `sdc` | `canadacentral` | `cac` |
| `norwayeast` | `nwe` | `brazilsouth` | `brs` |
| `polandcentral` | `plc` | `japaneast` | `jpe` |
| `italynorth` | `itn` | `southeastasia` | `sea` |
| `spaincentral` | `spc` | `australiaeast` | `aue` |
| `belgiumcentral` | `bec` | `centralindia` | `inc` |

**RG-CAT-01 — Disponibilité régionale.** Le descripteur peut exclure un type ou une valeur (SKU,
redondance de zone) d'une région. Un choix indisponible dans la région effective est une erreur
`VAL-CAT-REGION`.

## 6. Objets générés (non modélisés directement)

| Objet | Produit par |
|---|---|
| Groupe de ressources | Les groupes de ressources du composant. |
| Attribution de rôle | Liaisons d'accès explicites et rôles implicites ([16](16-liaisons-identites-et-acces.md)). |
| Paramètre de diagnostic | Liaisons de journalisation ([DEC-42](03-decisions.md)). |
| Verrou `CanNotDelete` | Option du composant, cibles protégées. |
| Unité de déploiement (pile Bicep, état Terraform, pile Pulumi) | Chaque composant × cible ([DEC-46](03-decisions.md)). |
| Point de terminaison privé, groupe de zones DNS | Exposition privée *(lot 2)*. |
| Secret Key Vault | Paramètres applicatifs alimentés par une sortie sensible ou un secret de pipeline ([17](17-parametres-applicatifs-et-secrets.md)). |
| Clé App Configuration | Paramètres applicatifs à destination d'une App Configuration. |
