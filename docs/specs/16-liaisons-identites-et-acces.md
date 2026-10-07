# 16 — Liaisons, identités et accès

## 1. Objectif

Décrire toutes les relations entre ressources avec **un seul mécanisme**, la liaison
([DEC-16](03-decisions.md)), et en déduire automatiquement les identités, les rôles et les paramètres
nécessaires ([DEC-17](03-decisions.md)). Les accès se font par identité managée, jamais par secret.

## 2. Liaison

| Champ | Règles |
|---|---|
| Source | Une ressource non existante du projet. |
| Cible | Une ressource du projet : même composant, autre composant, ou ressource existante. |
| Type | Un type de liaison (section 3) accepté par le descripteur de la source et compatible avec le type de la cible. |
| Paramètres | Selon le type : identité utilisée, rôles, portée, niveau d'accès… |
| Origine | `Explicite` (créée par l'utilisateur) ou `Implicite` (déduite d'un paramètre applicatif, d'une autre liaison ou d'un réglage de composant). |

**RG-LIA-01 — Même projet.** Source et cible appartiennent au même projet. Une cible d'un autre projet
est refusée comme introuvable ([RG-ORG-13](10-organisations-et-acces.md)).

**RG-LIA-02 — Pas sur soi-même.** Une ressource ne peut pas être sa propre cible.

**RG-LIA-03 — Entre composants.** Une liaison vers une ressource d'un autre composant est permise ; la
génération déclare la cible comme référence externe à partir de son nom calculé ([RG-GEN-11](21-generation-et-revisions.md)). Elle suit
[RG-CMP-01](12-composants-et-groupes-de-ressources.md) (sens `PerEnvironment` → `Single`) et
[RG-CMP-08](12-composants-et-groupes-de-ressources.md) (environnements cohérents).

**RG-LIA-24 — Accès vers un autre composant.** Une liaison qui produit un accès (accès, accès aux données,
lecture de secret, lecture de configuration, tirage d'image, stockage hôte, usage d'IA) vers une
ressource d'un autre composant exige `modele.modifier` sur le composant cible. Sans cette permission, elle
devient une **demande d'accès** à double consentement ([DEC-104](03-decisions.md)) : le demandeur a
`modele.modifier` sur le composant source, l'approbateur l'a sur le composant cible, aucun n'a besoin de
droits sur l'autre composant. Les deux droits sont revérifiés à l'application ; si la destination, le rôle
ou l'identité changent, l'approbation tombe. Seule la liaison demandée et ses éléments implicites sont
appliqués. Les éléments implicites qui produiraient un tel accès suivent la même règle.
*(Lot 2 : un composant peut ouvrir ses liaisons entrantes sans demande, à tous ou à une liste.)*

**RG-LIA-04 — Cardinalité et obligation.** Le descripteur fixe, par type de liaison, le nombre de
liaisons permises (0..1, 1, 0..n) et si elle est obligatoire. Une liaison obligatoire absente est une
erreur `VAL-LIA-OBLIGATOIRE`.

**RG-LIA-05 — Contraintes de placement.** Certains types exigent que source et cible soient dans le
même abonnement et la même région (exemple : une Web App et son plan). Une violation est une erreur
`VAL-LIA-PLACEMENT`, évaluée pour chaque environnement.

**RG-LIA-06 — Implicite.** Un élément implicite (liaison, rôle, identité, paramètre) affiche son
origine. Il ne se modifie ni ne se supprime directement : il disparaît quand son origine disparaît.

## 3. Types de liaison

| Type | Source → cible | Card. | Paramètres | Ce qu'IFS en déduit |
|---|---|---|---|---|
| **Hébergement** | WebApp, FunctionApp → AppServicePlan ; ContainerApp → ContainerAppsEnvironment ; SqlDatabase → SqlServer | 1, obligatoire | — | Placement : même abonnement et même région. La base SQL est déployée dans le groupe de ressources du serveur. |
| **Journalisation** | ApplicationInsights → LogAnalytics (obligatoire) ; ContainerAppsEnvironment → LogAnalytics | 0..1 ou 1 | — | Configuration de la ressource pour écrire dans l'espace. |
| **Diagnostics** | Toute ressource qui supporte les diagnostics → LogAnalytics | 0..1 | Exclusion possible | Paramètre de diagnostic « tous les journaux, toutes les métriques ». Implicite depuis l'espace par défaut du composant ([DEC-42](03-decisions.md)). |
| **Télémétrie** | WebApp, FunctionApp, ContainerApp → ApplicationInsights | 0..1 | Identité (si authentification locale désactivée) | Paramètre implicite `APPLICATIONINSIGHTS_CONNECTION_STRING`. Si l'authentification locale de la cible est désactivée : rôle Monitoring Metrics Publisher et paramètre `APPLICATIONINSIGHTS_AUTHENTICATION_STRING`. |
| **Tirage d'image** | WebApp, FunctionApp (mode Container), ContainerApp → ContainerRegistry | 1 en mode Container | Identité affectée (obligatoire) | Rôle `AcrPull` pour cette identité sur le registre (Container Registry Repository Reader limité au dépôt d'image si le registre est en mode de permissions par dépôt) ; configuration du tirage par cette identité. |
| **Stockage hôte** | FunctionApp → StorageAccount | 1, obligatoire | Identité | Rôles fixés par le descripteur (Storage Blob Data Owner, Queue Data Contributor, Table Data Contributor) ; paramètres implicites `AzureWebJobsStorage__*` par identité ; conteneur de déploiement implicite pour `FC1`. |
| **Lecture de configuration** | WebApp, FunctionApp, ContainerApp → AppConfiguration | 0..n | Identité ; nom de la variable d'adresse (défaut `AZURE_APPCONFIG_ENDPOINT`) | Rôle App Configuration Data Reader ; paramètre implicite portant l'adresse ; rôle Key Vault Secrets User sur chaque Key Vault référencé par des clés de ce magasin. |
| **Accès** | Ressource dotée d'une identité, ou UserAssignedIdentity → toute ressource non enfant | 0..n | Rôles (≥ 1), portée, identité utilisée | Attributions de rôles (section 5). Vers Azure Managed Redis : attribution d'une politique d'accès Redis à la base, pour l'identité, au lieu d'un rôle Azure. |
| **Accès aux données** | Ressource dotée d'une identité, ou UserAssignedIdentity → SqlDatabase, base enfant d'un PostgreSQL | 0..n | Niveau : `Lecture`, `LectureÉcriture`, `Schéma` ; identité utilisée | Script post-déploiement (section 6). |
| **Dépendance de valeur** | Destinataire d'un paramètre applicatif → ressource dont il lit une sortie | implicite | — | Ordre de déploiement, sauf si la sortie est calculable depuis le nom ([DEC-98](03-decisions.md)) ; référence externe si autre composant. |
| **Lecture de secret** | Destinataire d'un paramètre applicatif → KeyVault | implicite | Identité | Rôle Key Vault Secrets User ([17](17-parametres-applicatifs-et-secrets.md)). |
| **Point de terminaison privé** *(lot 2)* | Ressource exposée en privé → subnet | 1 par `groupId` | `groupId` | Point de terminaison privé, enregistrements DNS ([18](18-reseau-et-exposition.md)). |
| **Intégration sortante** *(lot 2)* | WebApp, FunctionApp, ContainerAppsEnvironment → subnet délégué | 0..1 | — | Intégration réseau, routage de tout le trafic sortant. |

## 4. Identités

**RG-LIA-07 — Capacités.** Le descripteur indique si un type supporte l'identité système et combien
d'identités affectées il accepte. Une liaison qui exige une identité ne peut partir que d'un type qui
en supporte une.

**RG-LIA-08 — Identité utilisée.** Chaque liaison qui agit au nom de la source (accès, tirage d'image,
stockage hôte, lecture de configuration, télémétrie, accès aux données, lecture de secret) précise
l'identité utilisée : `Système` ou une UserAssignedIdentity du projet.
- Choisir `Système` active implicitement l'identité système de la source.
- Choisir une identité affectée l'attache implicitement à la source.

**RG-LIA-09 — Pourquoi une identité affectée pour le tirage d'image.** L'identité système d'une
application n'existe qu'après sa création ; elle ne peut donc pas avoir `AcrPull` au moment du premier
tirage. Une identité affectée existe avant l'application et reçoit son rôle avant le premier tirage.
La règle s'applique à tous les types d'applications, par cohérence.

**RG-LIA-10 — Identité par défaut.** Une application peut désigner une de ses identités affectées comme
identité par défaut : IFS ajoute le paramètre implicite `AZURE_CLIENT_ID`, que les SDK Azure utilisent
pour s'authentifier.

**RG-LIA-11 — Détacher une identité.** Retirer une identité affectée d'une ressource, ou désactiver son
identité système, est refusé tant qu'une liaison l'utilise. IFS liste ces liaisons.

**RG-LIA-12 — Identité partagée.** Une UserAssignedIdentity peut servir à plusieurs ressources **de son
composant**. L'utiliser depuis un autre composant est une erreur `VAL-LIA-IDENTITE-PARTAGEE` : deux unités
de déploiement géreraient alors les mêmes attributions ([DEC-98](03-decisions.md)). L'écran de l'identité
liste ses utilisateurs et ses rôles.

## 5. Attributions de rôles

| Champ | Règles |
|---|---|
| Principal | Identité système d'une ressource, UserAssignedIdentity, ou groupe Entra du projet (section 5.2). |
| Rôle | Rôle intégré du catalogue du type de la cible ([15](15-catalogue.md)). |
| Portée | La ressource cible, ou son groupe de ressources. *(lot 2 : un enfant — conteneur, file, topic.)* |

**RG-LIA-13 — Rôles permis.** Seuls les rôles du descripteur de la cible sont proposés. Owner, User
Access Administrator et Role Based Access Control Administrator ne le sont jamais
([DEC-38](03-decisions.md)).

**RG-LIA-14 — Déduplication.** Une attribution (principal, rôle, portée) n'est générée qu'une fois,
même si plusieurs origines l'impliquent. Elle liste toutes ses origines et ne disparaît qu'avec la
dernière. Elle est déployée par un seul composant : celui du principal ([DEC-98](03-decisions.md)) ;
pour les identités de déploiement et de livraison, celui de la ressource visée.

**RG-LIA-15 — Équivalences.** Un besoin implicite est satisfait par tout rôle du même groupe
d'équivalence déjà attribué explicitement (exemple : « lecture de secrets » satisfaite par Secrets
Officer). Aucun rôle supplémentaire n'est alors ajouté.

### 5.1 Génération

**RG-LIA-16 — Forme.** Chaque attribution est générée avec un nom déterministe (dérivé de la portée, du
principal et du rôle) et le type de principal déclaré (`ServicePrincipal` ou `Group`), pour éviter les
erreurs de réplication d'Entra ID au premier déploiement.

**RG-LIA-17 — Portée externe.** Une attribution dont la cible est dans un autre composant ou est une
ressource existante est déployée à la portée de la cible (son groupe de ressources, éventuellement dans
un autre abonnement). Le kit d'installation en déduit les droits nécessaires à l'identité de
déploiement ([23](23-kit-installation.md)).

**RG-LIA-18 — Droits de l'identité de déploiement.** IFS calcule, par cible de déploiement, la liste des
rôles que la release doit attribuer ou retirer. Le kit d'installation donne à l'identité de déploiement le
rôle Role Based Access Control Administrator, **limité par condition** à ces rôles, en écriture et en
suppression. La liste comprend les droits de plan de données de l'identité de déploiement elle-même
(Key Vault Secrets Officer sur les coffres qu'elle alimente, App Configuration Data Owner sur les magasins
dont elle synchronise les clés) et ceux de l'identité applicative ([DEC-88](03-decisions.md)) : ils sont
attribués par le déploiement, à la portée de la ressource, au moment où celle-ci existe.

### 5.2 Groupes Entra

| Champ | Règles |
|---|---|
| Nom | Unique dans le projet, 1 à 80 caractères. |
| Identifiant d'objet | Par environnement (et par cible propre de composant `Single`), facultatif. |

**UC-LIA-01 — Déclarer un groupe Entra** (`conventions.gerer`) et **lui attribuer des rôles** sur des
ressources (exemple : « Développeurs » lecteur en dev, rien en prod).

**RG-LIA-19 — Environnement sans identifiant.** Si le groupe n'a pas d'identifiant pour un environnement,
ses attributions n'y sont pas générées, et l'écran l'indique explicitement (« non attribué en prod »).

## 6. Accès aux données (SQL, PostgreSQL)

Les droits dans une base Azure SQL ou PostgreSQL ne s'expriment pas en RBAC Azure : ils se créent par
une commande exécutée dans la base. IFS les génère pour qu'aucun accès ne soit à créer à la main.

**RG-LIA-20 — Niveaux.**

| Niveau | Azure SQL | PostgreSQL |
|---|---|---|
| `Lecture` | `db_datareader` | `SELECT` sur les tables du schéma `public` (y compris futures) |
| `LectureÉcriture` | `db_datareader`, `db_datawriter` | `SELECT, INSERT, UPDATE, DELETE` |
| `Schéma` | `db_datareader`, `db_datawriter`, `db_ddladmin` | Propriétaire du schéma `public` |

**RG-LIA-21 — Exécution.** L'accès aux données appartient au composant **source** de la liaison
([DEC-103](03-decisions.md)) : la base est déployée avant lui, et son identité existe une fois son propre
déploiement fait. Après ce déploiement, sa release exécute un script idempotent ([DEC-90](03-decisions.md)) :
- il crée l'utilisateur Entra de chaque identité s'il n'existe pas, **sans consulter l'annuaire** :
  `CREATE USER [<nom>] WITH SID = <Object ID du principal converti en SID>, TYPE = E` en Azure SQL,
  `pgaadauth_create_principal_with_oid` avec l'Object ID en PostgreSQL ; les identifiants sont
  des sorties du déploiement ;
- il ajuste ses droits au niveau voulu ;
- il marque les utilisateurs qu'il crée, pour ne jamais toucher un utilisateur créé par quelqu'un
  d'autre.

Le script s'exécute avec l'identité de déploiement, qui doit être membre du groupe administrateur Entra
du serveur. Aucune permission d'annuaire n'est demandée à l'identité du serveur.

**RG-LIA-22 — Joignabilité.**
- Exposition publique : la règle « services Azure » permet aux agents hébergés de joindre le serveur.
- Exposition restreinte : la release ajoute une règle temporaire pour l'adresse de l'agent et la retire
  à la fin, même en cas d'échec. La règle porte un nom `ifs-temp-<identifiant du run>` ; chaque release
  commence par supprimer les règles `ifs-temp-*` de plus de 2 heures, laissées par un exécuteur
  interrompu ([RG-NET-03](18-reseau-et-exposition.md)).
- Exposition privée *(lot 2)* : l'environnement doit utiliser des exécuteurs qui atteignent le réseau
  privé, sinon erreur `VAL-NET-AGENT`.

**RG-LIA-23 — Retrait.** Retirer un accès aux données retire ses rôles **et** supprime l'utilisateur créé
par IFS ([DEC-85](03-decisions.md), [DEC-90](03-decisions.md)). Les objets que cet utilisateur possède
(schéma, tables) bloquent la suppression : la release échoue alors en le disant, sans rien forcer, et la
liste de contrôle donne la commande pour transférer la propriété.

## 7. Analyse d'impact

**UC-LIA-02 — Voir l'impact d'un retrait.** Avant de retirer une liaison, un rôle, une identité ou une
ressource, IFS liste depuis le graphe ce qui disparaît ou casse :
- rôles et paramètres implicites retirés, avec la mention « accès révoqué dans Azure au prochain
  déploiement, y compris en production » ;
- applications qui perdent l'accès à une ressource utilisée par leurs paramètres ;
- liaisons obligatoires rompues (bloquant).

Gravité : `Bloquant`, `Critique` (une application perd l'accès à une dépendance), `Information`.

**UC-LIA-03 — Voir le graphe.** Vue graphe du projet ou d'un composant : ressources (nœuds), liaisons
(arêtes typées, explicites en trait plein, implicites en pointillé), filtrable par type de liaison et
par environnement.
