# 12 — Composants et groupes de ressources

## 1. Objectif

Un **composant** est une unité de déploiement : un ensemble de groupes de ressources et de ressources
déployés ensemble, par leurs propres pipelines. Exemples : le socle partagé d'un projet (journaux,
Key Vault), un service applicatif (« commandes ») avec sa base et ses files, un registre de conteneurs
commun à tous les environnements.

## 2. Données d'un composant

| Champ | Règles | Défaut |
|---|---|---|
| Nom | 1 à 60 caractères, unique dans le projet. | — |
| Code | `^[a-z][a-z0-9-]{1,19}$`, sans tiret final, unique dans le projet. Nom de dossier, jeton `{component}`, noms de pipelines. | Dérivé du nom |
| Description | 1 000 caractères max. | Vide |
| Mode de déploiement | `PerEnvironment` ou `Single`. Verrouillé après la première publication. | `PerEnvironment` |
| Environnements ciblés | `PerEnvironment` : « tous » ou une liste d'au moins un environnement. | Tous |
| Abonnement par environnement | `PerEnvironment` : pour chaque environnement, surcharge facultative de l'abonnement et de la connexion de déploiement ([DEC-69](03-decisions.md)) ; la région reste celle de l'environnement. Usage type : composant de connectivité dans l'abonnement de connectivité de chaque environnement. Le kit d'installation crée une identité et une connexion par couple (environnement, abonnement). | Ceux de l'environnement |
| Cible propre | `Single` uniquement : section 3. | — |
| Nommage | Surcharges facultatives du nommage du projet ([13](13-nommage.md)). | Aucune |
| Tags | [11 § 5](11-projets-et-environnements.md). | Aucun |
| Espace de journaux par défaut | Ressource Log Analytics ([DEC-42](03-decisions.md)). | Aucun |
| Ressources retirées | `Détacher` ou `Supprimer` ([DEC-46](03-decisions.md)). `Supprimer` ne s'applique jamais à une cible protégée. | `Détacher` |
| Verrou de suppression | Pose un verrou `CanNotDelete` sur les groupes de ressources dans les cibles protégées. | Activé |
| Code d'infrastructure additionnel | Point d'extension ([21 § 7](21-generation-et-revisions.md)) : chemin d'un code écrit par le client, dans le langage du projet. | Aucun |

## 3. Composant `Single` et sa cible propre

Un composant `Single` est déployé **une fois**, quel que soit le nombre d'environnements. Il sert aux
ressources partagées par toute la chaîne de promotion : registre de conteneurs commun, zone DNS,
espace de journaux central.

| Champ de la cible propre | Règles | Défaut |
|---|---|---|
| Code | `^[a-z][a-z0-9]{1,7}$`. Remplace `{env}` dans les noms. Différent des codes d'environnement du projet. | `shared` |
| Abonnement | GUID. | — |
| Région | Région du catalogue. | — |
| Connexion de déploiement | Comme un environnement. | `ifs-<projet>-<code>` |
| Protégée, approbateurs | Comme un environnement. | Protégée, avec les approbateurs du dernier environnement de la chaîne |
| Exécuteurs | Surcharge. | Ceux du projet |

**RG-CMP-01 — Sens des liaisons.** Une ressource d'un composant `PerEnvironment` peut être liée à une
ressource d'un composant `Single` : elle vise la même instance dans tous les environnements. Une
ressource d'un composant `Single` ne peut pas être liée à une ressource d'un composant
`PerEnvironment` (quel environnement viserait-elle ?) : erreur `VAL-CMP-SENS`.

**RG-CMP-02 — Présence.** Dans un composant `Single`, la présence par environnement n'existe pas : la
ressource est déployée ou retirée du modèle.

## 4. Cycle de vie

**UC-CMP-01 — Créer un composant** (`composants.gerer`). Saisie : nom, code, mode, environnements ou cible
propre. Le plan de publication attribue une destination selon sa règle par défaut
([24 § 4](24-depots-et-publication.md)). Un composant peut donc être créé et livré sans
intervention de la personne qui gère le plan de publication.

**UC-CMP-02 — Modifier un composant** (`composants.gerer`). Changer le code après publication suit la même
confirmation d'impact que [RG-PRJ-01](11-projets-et-environnements.md), et déplace les fichiers dans
les destinations (les anciens sont supprimés à la publication suivante via le manifeste).

**UC-CMP-03 — Dupliquer un composant** (`composants.gerer`). Nouveau nom et code obligatoires. Sont copiés :
groupes de ressources, ressources, surcharges, présences, enfants, liaisons internes (vers les copies),
liaisons externes (vers les mêmes cibles), paramètres applicatifs. Ne sont pas copiés : les
identifiants des ressources existantes, l'historique.

**UC-CMP-04 — Supprimer un composant** (`composants.gerer`). Refusé tant que des ressources d'autres
composants ont des liaisons vers ses ressources ; IFS les liste. Après suppression, la publication
suivante retire ses fichiers des destinations. Les ressources Azure suivent la règle « ressources
retirées » de leur dernière unité de déploiement (pile ou état) : rien n'est supprimé dans Azure par IFS,
l'unité reste en place jusqu'à suppression manuelle (indiqué dans la liste de contrôle).

## 5. Groupes de ressources

| Champ | Règles | Défaut |
|---|---|---|
| Nom logique | `^[a-z][a-z0-9-]{0,39}$`, unique dans le composant. Le nom Azure est calculé avec le type `ResourceGroup` ([13](13-nommage.md)). | — |
| Région | « Région de la cible » ou une région fixe. | Région de la cible |
| Tags | Voir [11 § 5](11-projets-et-environnements.md). | Aucun |
| Nom forcé | Par environnement, facultatif ([13 § 4](13-nommage.md)). | Aucun |

**RG-CMP-03 — Région des ressources.** Toute ressource d'un groupe de ressources est déployée dans la
région du groupe, sauf type global ([DEC-09](03-decisions.md)).

**RG-CMP-04 — Déploiement.** Un groupe de ressources est créé dans chaque cible du composant où au
moins une de ses ressources est présente.

**UC-CMP-05 — Créer, modifier, supprimer un groupe de ressources** (`modele.modifier`). La suppression est
refusée s'il contient des ressources.

**UC-CMP-06 — Déplacer une ressource** vers un autre groupe de ressources **du même composant**. IFS
avertit qu'Azure recréera la ressource dans le nouveau groupe (l'ancienne est détachée ou supprimée
selon la règle du composant) et affiche les liaisons concernées.

## 6. Dépendances et ordre de déploiement

**RG-CMP-05 — Dépendances déduites.** Le composant A dépend du composant B si au moins une ressource de
A a une liaison vers une ressource de B ([DEC-40](03-decisions.md)). Les paramètres applicatifs dont la
source est une sortie d'une ressource de B comptent comme des liaisons.

**RG-CMP-06 — Pas de cycle.** Un cycle de dépendances entre composants est une erreur
(`VAL-CMP-CYCLE`). Le constat indique les liaisons qui forment le cycle.

**RG-CMP-07 — Ordre.** L'ordre de déploiement est un tri topologique des composants (les composants
`Single` avant ceux qui en dépendent ; à égalité, par code). Il est affiché sur l'écran du projet et
dans la liste de contrôle.

**RG-CMP-08 — Cohérence des environnements.** Une liaison d'une ressource de A vers une ressource d'un
composant `PerEnvironment` B exige que B cible chaque environnement où la ressource source est
présente, et que la cible y soit présente (`VAL-CMP-ENVIRONNEMENT`, `VAL-RES-PRESENCE`).

**UC-CMP-07 — Voir les dépendances** d'un composant : « dépend de » et « utilisé par », avec les
liaisons en cause.
