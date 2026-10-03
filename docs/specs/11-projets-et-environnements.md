# 11 — Projets et environnements

## 1. Objectif

Le projet porte ce qui est commun à tous ses composants : l'équipe, la chaîne d'environnements, les
conventions (nommage, tags) et le plan de publication.

## 2. Projet

| Champ | Règles | Défaut |
|---|---|---|
| Nom | 3 à 80 caractères, unique dans l'organisation (insensible à la casse). | — |
| Code | `^[a-z][a-z0-9]{1,9}$`, unique dans l'organisation. Jeton `{project}` du nommage, tags système, noms de pipelines, d'identités techniques et d'unités de déploiement. Verrouillé après la première publication (RG-PRJ-01). | Dérivé du nom, modifiable jusqu'à la première publication |
| Tenant Entra | Identifiant du tenant de tous les abonnements du projet. Un projet vise un seul tenant ; le kit vérifie chaque abonnement. | Tenant de la connexion Azure de l'organisation, sinon saisi |
| Description | 1 000 caractères max. | Vide |
| Nommage | Gabarit par défaut, gabarits par type, abréviations ([13](13-nommage.md)). | Voir [13 § 3](13-nommage.md) |
| Tags | Section 5. | Aucun |
| Tags système | Activés ou non ([DEC-33](03-decisions.md)). | Activés |
| Langage d'infrastructure | `Bicep` ; `Terraform` *(lot 3)* ; `OpenTofu`, `Pulumi` avec son langage (TypeScript, C#, Python, Go, Java, YAML) *(lot 4)* ([DEC-43](03-decisions.md), [DEC-62](03-decisions.md), [DEC-63](03-decisions.md)). Changeable ([DEC-48](03-decisions.md)). | `Bicep` |
| Source des modules | `AVM registre public` ; `AVM embarqués`, `Modules IFS` *(lot 2)* ; `Modules du client` *(lot 3)* ; pour Pulumi, `Ressources directes` ou `Composants IFS` *(lot 4)*. Surcharge possible par type ([DEC-54](03-decisions.md), [21 § 6.5](21-generation-et-revisions.md)). | `AVM registre public` |
| Version du catalogue | Version figée du projet ; une nouvelle version est proposée, la montée est explicite ([DEC-92](03-decisions.md), [40 § 3](40-exploitation-ifs.md)). | Dernière publiée à la création |
| Backend d'état Pulumi | `Azure Storage` (créé par le kit) ou `Pulumi Cloud` (+ organisation Pulumi). Pulumi uniquement. | `Azure Storage` |
| Plateforme CI | `AzureDevOps` ; `GitHubActions` *(lot 2)* ; `GitLabCI` *(lot 3)* ([DEC-47](03-decisions.md), [DEC-62](03-decisions.md)). Changeable ([DEC-48](03-decisions.md)). | — (obligatoire) |
| Exécuteurs par défaut | Selon la plateforme : nom d'un pool d'agents (Azure DevOps), liste de libellés de runners (GitHub), liste de tags (GitLab). Vide = exécuteurs hébergés Linux de la plateforme. | Vide |
| Relecteurs par défaut | Utilisateurs ou groupes du fournisseur git ajoutés aux pull requests ([24](24-depots-et-publication.md)). | Aucun |
| Plan de publication | [24](24-depots-et-publication.md). | Rempli par le préréglage choisi |

**RG-PRJ-01 — Code verrouillé.** Le code d'un projet est modifiable jusqu'à sa première publication, puis
verrouillé. Il nomme les ressources, les unités de déploiement (piles, états), les identités techniques,
les environnements de la plateforme CI et les pipelines : le changer après publication créerait une
seconde chaîne de déploiement à côté de l'ancienne, toujours active. Renommer un projet publié revient à
créer un nouveau projet par export et import, puis à retirer l'ancien.

## 3. Création

**UC-PRJ-01 — Créer un projet avec l'assistant.** Un seul parcours ([DEC-36](03-decisions.md)) :

| Étape | Saisie | Contrôles |
|---|---|---|
| 1. Identité | Nom, code, description. | RG de la section 2. |
| 2. Outils | Langage d'infrastructure, plateforme CI. L'écran explique ce que chaque choix produit et ce qui est compatible (dépôts, lots). | Seuls les choix livrés sont proposés ([P9](01-principes.md)). |
| 3. Environnements | Au moins un environnement (section 4). L'assistant propose `dev` puis `prod`, sans région ni abonnement présaisis. | Section 4. |
| 4. Nommage | Gabarit par défaut (proposition [13 § 3](13-nommage.md)), aperçu sur des exemples. | [13](13-nommage.md). |
| 5. Publication | Préréglage (mono-dépôt, infra et code séparés, un dépôt par composant) et dépôts choisis dans les connexions de l'organisation. **Étape facultative**, complétable plus tard. | Accès vérifié pour chaque dépôt choisi ; compatibilité avec la plateforme CI ([RG-PUB-19](24-depots-et-publication.md)). |
| 6. Récapitulatif | Relecture, création. | — |

**RG-PRJ-02 — Brouillon serveur.** Le brouillon de l'assistant est enregistré côté serveur pour
l'utilisateur et l'organisation, 30 jours. Il reprend sur n'importe quel poste.

**RG-PRJ-03 — Création atomique.** Le projet est créé entièrement ou pas du tout. Aucune action externe
(Key Vault, fournisseur git) n'a lieu pendant la création : les connexions git existent déjà au niveau
organisation.

**RG-PRJ-04 — Créateur propriétaire.** Le créateur devient propriétaire du projet.

**UC-PRJ-02 — Lister ses projets** : nom, code, nombre de composants et de ressources, nombre de
constats par gravité, dernière révision, dernière publication, favoris d'abord.

**UC-PRJ-03 — Consulter un projet** : composants et leur ordre de déploiement, environnements, nommage,
tags, plan de publication, membres, constats, révisions.

**UC-PRJ-04 — Supprimer un projet** (`projet.administrer`). L'utilisateur saisit le nom du projet pour
confirmer. Le projet est conservé 30 jours, restaurable (**UC-PRJ-05**), puis purgé. Ni les dépôts git
ni les ressources Azure ne sont touchés.

**UC-PRJ-06 — Changer de langage d'infrastructure ou de plateforme CI** (`projet.administrer`). IFS affiche les
conséquences ([DEC-48](03-decisions.md)) : fichiers remplacés à la prochaine publication, kit à
réexécuter, approbateurs à ressaisir (changement de plateforme), migration assistée (changement de
langage après publication, *lot 3*). Le changement s'applique à la prochaine révision.

## 4. Environnements

Un environnement est une étape de la chaîne de promotion et une **cible de déploiement** pour les
composants `PerEnvironment` ([12](12-composants-et-groupes-de-ressources.md)).

| Champ | Règles | Défaut |
|---|---|---|
| Nom | 1 à 50 caractères, unique dans le projet. | — |
| Code | `^[a-z][a-z0-9]{1,7}$`, unique dans le projet. Jeton `{env}`, noms des fichiers de valeurs (`main.<code>.bicepparam`, `<code>.tfvars`, `Pulumi.<code>.yaml`), environnements de la plateforme CI. | — |
| Ordre | Position dans la chaîne de promotion, unique et continue (1, 2, 3…). | En dernier |
| Abonnement | GUID d'abonnement Azure. | — (obligatoire) |
| Région | Une région du catalogue ([15 § 5](15-catalogue.md)). | — (obligatoire) |
| Connexion de déploiement | Azure DevOps : nom de la service connection Azure Resource Manager. GitHub, GitLab : déduite (environnement `<projet>-<code>` et identifiant fédéré), non saisie. | `ifs-<projet>-<code>` (créée par le kit d'installation) |
| Protégé | Booléen. | Non |
| Approbateurs | Obligatoire si protégé : au moins une identité de la plateforme CI (Azure DevOps : utilisateur par adresse e-mail ou groupe ; GitHub : utilisateur ou équipe ; GitLab : utilisateur ou groupe). Changer de plateforme exige de les ressaisir. | — |
| Exécuteurs | Surcharge des exécuteurs du projet pour cet environnement. | Ceux du projet |
| Tags | Section 5. | Aucun |
| Description | 500 caractères max. | Vide |
| Fenêtres de déploiement *(lot 2)* | Plages de jours et d'heures, fuseau horaire ; un déploiement hors fenêtre attend la suivante ([DEC-77](03-decisions.md)). | Aucune |
| Délai d'attente *(lot 2)* | Minutes d'attente obligatoire avant tout déploiement dans la cible (0 à 1440). | 0 |
| Stratégie DNS privée *(lot 2)* | [18 § 8.2](18-reseau-et-exposition.md). | — |

**RG-ENV-01 — Identifiant.** Toute donnée par environnement (surcharges, présence, valeurs de
paramètres, identifiants de ressources existantes, groupes Entra) est rattachée à l'identifiant de
l'environnement ([DEC-08](03-decisions.md)). Renommer un environnement ne casse rien.

**RG-ENV-02 — Ordre continu.** Insérer à la position *n* décale les suivants. Supprimer resserre
l'ordre. Déplacer réordonne.

**RG-ENV-03 — Changement d'impact.** Le code d'un environnement est verrouillé après la première
publication qui le cible, pour la même raison que RG-PRJ-01. Changer l'abonnement ou la région change
l'emplacement de ses ressources : si une révision a été publiée, IFS liste les ressources concernées et
exige une confirmation explicite.

**UC-ENV-01 — Ajouter un environnement** (`environnements.gerer` ; `environnements.proteges.gerer` s'il est protégé). Les composants `PerEnvironment` qui suivent
« tous les environnements » le ciblent aussitôt. Les ressources y sont présentes avec leurs valeurs par
défaut ; les ressources existantes y sont **absentes** jusqu'à saisie de leur identifiant.

**UC-ENV-02 — Modifier un environnement** (`environnements.gerer` ; `environnements.proteges.gerer` pour une cible protégée ou pour changer la protection).

**UC-ENV-03 — Supprimer un environnement** (`environnements.gerer` ; `environnements.proteges.gerer` s'il est protégé). IFS liste ce qui sera supprimé (surcharges,
valeurs de paramètres, présences, identifiants de ressources existantes, ciblage par les composants).
Après confirmation, tout est supprimé. Un composant `PerEnvironment` qui ne cible plus aucun
environnement produit une erreur de validation.

## 5. Tags

**RG-PRJ-05 — Niveaux et priorité.** Des tags se déclarent sur le projet, l'environnement, le composant,
le groupe de ressources et la ressource. Les tags effectifs d'une ressource sont l'union, avec cette
priorité en cas de clé commune : ressource > groupe de ressources > composant > environnement > projet.

**RG-PRJ-06 — Tags système.** Quand ils sont activés, `ifs-project` (code du projet), `ifs-component`
(code du composant), `ifs-environment` (code de la cible) et `managed-by` (`infraflowsculptor`) sont
ajoutés à chaque groupe de ressources et ressource. Ils priment sur tout tag utilisateur de même clé ;
déclarer un tag utilisateur avec l'une de ces clés est refusé.

**RG-PRJ-07 — Limites Azure.**
- Clé : 1 à 512 caractères, sans `< > % & \ ? /`.
- Valeur : 0 à 256 caractères.
- 50 tags effectifs maximum par ressource. Un dépassement est une erreur de validation.

**RG-PRJ-08 — Généré.** Les tags effectifs sont écrits en clair dans le fichier de paramètres de chaque
environnement.

## 6. Export, import et modèles ([DEC-56](03-decisions.md))

**UC-PRJ-07 — Exporter un projet** (`projet.administrer`). IFS produit un document JSON versionné
(`ifs-project/v1`) qui contient tout le modèle :
- projet, environnements, composants, groupes de ressources, ressources ;
- liaisons, paramètres applicatifs, nommage, tags, plan de publication ;
- étapes de pipeline.

Il ne contient ni membres, ni révisions, ni secret (IFS n'en a pas). L'historique du modèle (jeux de
modifications) n'y figure que si l'export est demandé **avec l'historique** ([RG-HIS-03](31-historique-et-versions.md)).
Les identifiants d'abonnement et de ressources existantes y figurent : l'export est journalisé.

**UC-PRJ-08 — Importer un projet** (droit de créer des projets dans l'organisation). L'import crée un
nouveau projet :
- les codes en conflit sont proposés à la modification ;
- les connexions git sont choisies parmi celles de l'organisation ;
- la validation s'exécute aussitôt.

Un document d'une version de schéma antérieure est migré automatiquement ; une version plus récente
qu'IFS ne connaît pas est refusée.

**RG-PRJ-09 — L'import crée une copie isolée** ([DEC-108](03-decisions.md)). Un projet importé est une copie,
jamais la reprise d'un projet existant : il reçoit un nouveau code, donc d'autres unités de déploiement et
d'autres identités. Un nouveau code ne garantit pas de nouveaux noms Azure (noms forcés, gabarits sans
`{project}`) : IFS recalcule les noms effectifs de chaque cible et les compare aux ressources gérées par
tous les projets de l'organisation. Chaque collision est une erreur à résoudre avant la première
publication : renommer, ou transformer la ressource en ressource existante. Une ressource existante déjà
référencée par un autre projet est signalée. Reprendre la gestion d'un déploiement existant passe par
l'import d'infrastructure ([29](29-import.md)).

**UC-PRJ-09 — Modèles de projet et de composant** *(lot 2)*. Un administrateur d'organisation enregistre un
projet ou un composant comme **modèle** : abonnements et identifiants retirés, valeurs à saisir marquées.
Créer un projet ou un composant depuis un modèle passe par l'assistant, qui demande les valeurs
marquées. IFS fournit aussi des modèles de départ (« API conteneur + base SQL », « Function App + file
Service Bus », « site statique + API »).
