# 23 — Kit d'installation

## 1. Objectif

Réduire la mise en route à quelques actions humaines : **exécuter le script Azure**, **exécuter la
partie plateforme**, **saisir les valeurs des secrets**. Tout le reste est produit par le kit
([DEC-30](03-decisions.md)) : identités, droits, connexions de déploiement, stockage d'état,
environnements, approbations, magasins de secrets, définitions de pipelines.

Le kit fait partie de chaque révision (`.ifs/install/`). Il est **réexécutable** : chaque exécution
réconcilie l'existant avec la révision. Il a une partie **Azure**, commune à toutes les plateformes, et
une partie **plateforme CI** ([DEC-47](03-decisions.md)).

## 2. Script Azure (`azure-setup.ps1`) — commun

PowerShell + Azure CLI, à exécuter par une personne qui a les droits d'attribution de rôles sur les
abonnements (Owner ou User Access Administrator).

Pour chaque cible de déploiement (environnement, cible propre de chaque composant `Single`, et chaque
couple environnement × abonnement surchargé par un composant, [DEC-69](03-decisions.md)) :

| Étape | Effet | Langage / plateforme |
|---|---|---|
| 1. Groupe de ressources technique | `rg-ifs-<projet>-<cible>`. | Tous |
| 2. Identités | Identité d'infrastructure `id-ifs-deploy-<projet>-<cible>` et identité applicative `id-ifs-app-<projet>-<cible>` ([DEC-88](03-decisions.md)). Vérification que l'abonnement appartient au tenant du projet. | Tous |
| 3. Identifiants fédérés | Un par sujet autorisé, pour chaque identité : service connection d'infrastructure et service connection applicative (Azure DevOps) ; `repo:<propriétaire>/<dépôt>:environment:<projet>-<cible>` pour chaque dépôt qui déploie dans la cible (GitHub) ; sujet du jeton GitLab *(lot 3)*. Un identifiant qui ne correspond plus à aucun sujet attendu est **supprimé** (RG-INS-05). | Selon plateforme |
| 4. Droits | Identité d'infrastructure : `Contributor` sur l'abonnement ; `Role Based Access Control Administrator` **limité par condition**, en écriture et en suppression, aux rôles attribués ou retirés par la release ([RG-LIA-18](16-liaisons-identites-et-acces.md)) ; droits sur les portées externes (zones DNS, ressources existantes, registre d'un autre abonnement). Les droits de plan de données sur les ressources du projet (Key Vault Secrets Officer, App Configuration Data Owner) ne sont **pas** donnés par le kit : les ressources n'existent pas encore ; le déploiement les attribue ([DEC-86](03-decisions.md)). Identité applicative : aucun droit à l'abonnement ; ses droits sont attribués par le déploiement ([RG-APP-08](19-applications-build-et-deploiement.md)). | Tous |
| 5. Groupes administrateurs SQL et PostgreSQL | Ajout de l'identité au groupe Entra administrateur de chaque serveur. Si l'opérateur n'en a pas le droit : étape « à faire » dans la liste de contrôle, avec la commande exacte. | Tous |
| 6. Stockage d'état | Compte de stockage `stifs<projet><cible>` dans le groupe technique : authentification par clé partagée désactivée, accès public anonyme désactivé, versioning et suppression réversible des blobs activés, conteneur `tfstate` ou `pulumi`. L'identité de déploiement reçoit Storage Blob Data Contributor sur le conteneur. | Terraform, Pulumi (backend Azure) |
| 7. Clé de chiffrement des secrets d'état | Key Vault technique `kv-ifs-<projet>-<cible>` et clé RSA ; l'identité reçoit Key Vault Crypto User. | Pulumi |
| 8. Service connections | `ifs-<projet>-<cible>` (infrastructure) et `ifs-<projet>-<cible>-app` (applications), fédérées chacune avec son identité. | Azure DevOps |
| 9. Portées réseau externes *(lot 2)* | Droits sur le VNet du hub existant (appairage distant) et sur les zones DNS privées existantes ; si l'opérateur n'a pas ces droits, étape « à faire » destinée à l'équipe du hub, avec les commandes exactes. | Tous |
| 10. Pools d'exécuteurs privés *(lot 2)* | Inscription du fournisseur `Microsoft.DevOpsInfrastructure` ; Reader et Network Contributor sur le VNet du pool pour le principal de service du fournisseur, résolu dans le tenant. | Azure DevOps |

**RG-INS-01 — Paramètres.** Le script demande seulement ce qu'il ne peut pas déduire du plan de
publication (organisation Azure DevOps, identifiant du tenant). Il affiche ce qu'il va faire et demande
confirmation (mode `-WhatIf` disponible).

**RG-INS-02 — Rapport.** Le script termine par un rapport, par cible, de ce qui a été créé, modifié,
laissé tel quel ou n'a pas pu être fait.

**RG-INS-03 — Noms techniques.** Les noms des objets techniques suivent le nommage du projet quand c'est
possible. Ils passent par le même assainissement ([13 § 5](13-nommage.md)). En cas de dépassement de
longueur, et **seulement pour ces objets techniques**, IFS raccourcit le nom de façon déterministe
(troncature suivie d'une empreinte de 4 caractères) et l'indique dans le rapport : ces noms ne sont
jamais saisis ni référencés par l'utilisateur, la règle de [DEC-07](03-decisions.md) ne s'y applique pas.

## 3. Partie plateforme

### 3.1 Azure DevOps — pipeline d'installation (`install.pipeline.yml`)

Pipeline à déclenchement manuel, exécuté avec le jeton du pipeline. La liste de contrôle indique les
permissions à donner une fois au compte de service de build du projet.

| Étape | Effet |
|---|---|
| 1. Service connections | Chaque service connection attendue existe ; sinon échec, avec renvoi vers le script Azure. |
| 2. Environnements | Création ou mise à jour des environnements `<projet>-<cible>`. |
| 3. Approbations | Contrôle d'approbation de chaque cible protégée avec ses approbateurs (adresse e-mail ou nom de groupe). Approbateur introuvable : échec. Les approbateurs sont **réalignés** sur le modèle. Contrôle de verrou exclusif sur chaque environnement ([RG-PIP-05](22-pipelines.md)). |
| 4. Groupes de variables | Un groupe `ifs-<projet>-<cible>` par cible, avec une variable secrète par secret de pipeline attendu ([17 § 6](17-parametres-applicatifs-et-secrets.md)). Variable absente : créée vide. Variable existante : **jamais modifiée**. Variable plus attendue : signalée, pas supprimée. |
| 5. Définitions de pipelines | Création ou mise à jour de chaque définition ([RG-PIP-06](22-pipelines.md)) : nom, dossier `\<projet>\<composant>[\<application>]`, chemin YAML, dépôt, branche par défaut. |
| 6. Autorisations | Autorise chaque pipeline généré à utiliser **ses seules** ressources : la connexion d'infrastructure pour les pipelines d'infrastructure, la connexion applicative pour les pipelines applicatifs, aucune pour les pipelines de pull request ; environnements et groupes de variables. |
| 6 bis. Validation des pull requests | Politique de branche de validation de build sur la branche par défaut de chaque dépôt Azure Repos : le pipeline PR du composant ou de l'application concerné, filtré sur ses chemins. Un déclencheur `pr` en YAML ne suffit pas sur Azure Repos. Les dépôts GitHub utilisent le déclencheur YAML. |
| 7. Fenêtres et délais *(lot 2)* | Contrôle « heures ouvrées » et délai sur les environnements des cibles qui en déclarent. |
| 8. Rapport | Créé, mis à jour, laissé, orphelins détectés. |

Le pipeline d'installation est publié dans la destination de l'infrastructure du **premier composant**
dans l'ordre de déploiement. Il crée les définitions de tous les pipelines du projet, quel que soit le
dépôt qui les héberge.

### 3.2 GitHub Actions — script `github-setup.ps1`

Script PowerShell utilisant la CLI `gh`, à exécuter par un administrateur des dépôts concernés. Les
workflows n'ont pas de définition à créer : ils existent dès qu'ils sont dans `.github/workflows`.

| Étape | Effet, pour chaque dépôt qui héberge des workflows de déploiement |
|---|---|
| 1. Environnements | Création ou mise à jour des environnements `<projet>-<cible>` utilisés par les workflows du dépôt. |
| 2. Protection | Cibles protégées : relecteurs requis (utilisateurs ou équipes), déploiement limité à la branche par défaut. Réalignés sur le modèle. |
| 3. Variables d'environnement | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (non secrètes) de la cible. |
| 4. Secrets | GitHub ne permet pas de créer un secret vide : le script **liste** les secrets attendus absents de chaque environnement, avec la commande `gh secret set` correspondante. Il ne lit ni n'écrit aucune valeur. |
| 5. Délais *(lot 2)* | Règle de délai d'attente des environnements des cibles qui en déclarent. |
| 6. Rapport | Créé, mis à jour, laissé, manquant. |

*(Lot 2 : configuration des environnements directement par l'application GitHub d'IFS, si le client lui
accorde les permissions « environnements » et « variables ».)*

### 3.3 GitLab CI *(lot 3)*

Script équivalent utilisant l'API GitLab : environnements protégés, approbations, variables CI/CD
limitées à l'environnement.

### 3.4 Règles communes aux parties plateforme

**RG-INS-04 — Propriété.** IFS ne modifie que les objets qu'il a créés, reconnus par leur nom et une
marque de propriété (`managed-by: infraflowsculptor` dans la description quand la plateforme le permet).
Un objet de même nom sans cette marque n'est pas modifié : l'étape échoue en l'expliquant.

**RG-INS-05 — Révoquer les accès, ne jamais supprimer de données.** Le kit révoque ce qui donne un accès
et ne correspond plus au modèle : identifiants fédérés, attributions de rôle de ses identités, autorisations
de pipelines, approbateurs (réalignés). Il ne supprime jamais un objet qui porte des données ou de l'état :
groupes de variables, stockages d'état, identités et service connections orphelins (composant supprimé,
environnement retiré, ancienne plateforme après un changement [DEC-48](03-decisions.md)) sont listés avec
la commande pour les supprimer ; leurs fédérations sont déjà révoquées.

**RG-INS-07 — Version du kit.** Chaque exécution inscrit la révision du kit sur les objets qu'elle gère
(tag du groupe technique, description des objets de la plateforme). Un kit plus ancien que la dernière
version appliquée refuse de s'exécuter : il rétablirait d'anciens accès ou d'anciennes protections.

## 4. Liste de contrôle (`SETUP.md` et écran)

La liste de contrôle est générée dans la révision et affichée de façon interactive dans IFS. Chaque
étape porte un état : **automatique** (faite par le kit), **à faire** (action humaine), **fait** (cochée
par l'utilisateur, journalisée).

Contenu, dans l'ordre :
1. Prérequis de la plateforme : permissions du compte de build (Azure DevOps : environnements, groupes de
   variables, définitions de pipelines, politiques de branche), droits d'administration des dépôts
   (GitHub).
2. Exécution du script Azure, avec la commande exacte.
3. Étapes du script qui n'ont pas pu être faites (exemple : adhésion à un groupe Entra).
4. Exécution de la partie plateforme.
5. Saisie des secrets de pipeline : par cible, emplacement (groupe de variables ou environnement et
   dépôt), nom de chaque secret, paramètre applicatif qui l'utilise.
6. Secrets « gérés hors IFS » qui doivent exister dans leurs Key Vaults.
7. Premier déploiement : ordre des composants ([RG-CMP-07](12-composants-et-groupes-de-ressources.md)),
   puis applications.
8. Points d'extension référencés mais absents.
9. Après une migration de langage ou de plateforme : étapes de bascule et objets orphelins.

**RG-INS-06 — Différentiel.** Pour une révision qui n'est pas la première, la liste de contrôle met en
avant ce qui est nouveau depuis la révision précédente : nouvelle cible, nouveau secret, nouveau
composant, nouveau dépôt.
