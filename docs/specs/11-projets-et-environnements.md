# 11 — Projets et environnements

## 1. Objectif

Le projet porte ce qui est commun à tous ses composants : l'équipe, la chaîne d'environnements, les
conventions (nommage, tags) et le plan de publication.

## 2. Projet

| Champ | Règles | Défaut |
|---|---|---|
| Nom | 3 à 80 caractères, unique dans l'organisation (insensible à la casse). | — |
| Code | `^[a-z][a-z0-9]{1,9}$`, unique dans l'organisation. Jeton `{project}` du nommage, tags système, noms de pipelines. | Dérivé du nom, modifiable |
| Description | 1 000 caractères max. | Vide |
| Nommage | Gabarit par défaut, gabarits par type, abréviations ([13](13-nommage.md)). | Voir [13 § 3](13-nommage.md) |
| Tags | Section 5. | Aucun |
| Tags système | Activés ou non ([DEC-33](03-decisions.md)). | Activés |
| Langage d'infrastructure | `Bicep` ; `Terraform` *(lot 2)* ; `Pulumi` *(lot 3)* ([DEC-43](03-decisions.md)). Changeable ([DEC-48](03-decisions.md)). | `Bicep` |
| Backend d'état Pulumi | `Azure Storage` (créé par le kit) ou `Pulumi Cloud` (+ organisation Pulumi). Pulumi uniquement. | `Azure Storage` |
| Plateforme CI | `AzureDevOps`, `GitHubActions` ; `GitLabCI` *(lot 3)* ([DEC-47](03-decisions.md)). Changeable ([DEC-48](03-decisions.md)). | — (obligatoire) |
| Exécuteurs par défaut | Selon la plateforme : nom d'un pool d'agents (Azure DevOps), liste de libellés de runners (GitHub), liste de tags (GitLab). Vide = exécuteurs hébergés Linux de la plateforme. | Vide |
| Relecteurs par défaut | Utilisateurs ou groupes du fournisseur git ajoutés aux pull requests ([24](24-depots-et-publication.md)). | Aucun |
| Plan de publication | [24](24-depots-et-publication.md). | Rempli par le préréglage choisi |

**RG-PRJ-01 — Code stable.** Changer le code d'un projet change les noms Azure de toutes les ressources
qui utilisent `{project}`. Si au moins une révision a été publiée, IFS affiche la liste des noms qui
changent et exige une confirmation explicite, car Azure recréera ces ressources.

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

**UC-PRJ-04 — Supprimer un projet** (propriétaire). L'utilisateur saisit le nom du projet pour
confirmer. Le projet est conservé 30 jours, restaurable (**UC-PRJ-05**), puis purgé. Ni les dépôts git
ni les ressources Azure ne sont touchés.

**UC-PRJ-06 — Changer de langage d'infrastructure ou de plateforme CI** (propriétaire). IFS affiche les
conséquences ([DEC-48](03-decisions.md)) : fichiers remplacés à la prochaine publication, kit à
réexécuter, approbateurs à ressaisir (changement de plateforme), migration assistée (changement de
langage après publication, *lot 2*). Le changement s'applique à la prochaine révision.

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

**RG-ENV-01 — Identifiant.** Toute donnée par environnement (surcharges, présence, valeurs de
paramètres, identifiants de ressources existantes, groupes Entra) est rattachée à l'identifiant de
l'environnement ([DEC-08](03-decisions.md)). Renommer un environnement ne casse rien.

**RG-ENV-02 — Ordre continu.** Insérer à la position *n* décale les suivants. Supprimer resserre
l'ordre. Déplacer réordonne.

**RG-ENV-03 — Changement d'impact.** Changer le code, l'abonnement ou la région d'un environnement
change les noms ou l'emplacement de ses ressources. Si une révision a été publiée, IFS liste les
ressources concernées et exige une confirmation explicite.

**UC-ENV-01 — Ajouter un environnement** (propriétaire). Les composants `PerEnvironment` qui suivent
« tous les environnements » le ciblent aussitôt. Les ressources y sont présentes avec leurs valeurs par
défaut ; les ressources existantes y sont **absentes** jusqu'à saisie de leur identifiant.

**UC-ENV-02 — Modifier un environnement** (propriétaire).

**UC-ENV-03 — Supprimer un environnement** (propriétaire). IFS liste ce qui sera supprimé (surcharges,
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
