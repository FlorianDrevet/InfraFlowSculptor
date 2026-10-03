# 24 — Dépôts et publication

## 1. Objectif

Écrire une révision dans les dépôts git du client, à l'endroit choisi, sous forme de pull request
relisible, sans jamais écraser en silence une retouche manuelle ([DEC-01](03-decisions.md),
[DEC-22](03-decisions.md)).

## 2. Connexions git (niveau organisation)

| Type | Mise en place | Secret conservé par IFS |
|---|---|---|
| **Application GitHub** | Un administrateur de l'organisation installe l'application GitHub d'IFS sur un compte ou une organisation GitHub et choisit les dépôts accessibles. | Aucun (jetons d'installation temporaires). |
| **Azure DevOps (principal de service)** | Un administrateur ajoute le principal de service d'IFS à son organisation Azure DevOps et lui donne, sur les dépôts concernés, les droits de contribution, de création de branche et de contribution aux pull requests. IFS doit être consenti dans le tenant Entra de l'organisation Azure DevOps. | Aucun. |
| **GitLab** *(lot 3)* | Application OAuth de groupe GitLab, ou jeton de groupe en repli. | Selon le mode. |
| **Jeton personnel (repli)** | Jeton GitHub (fine-grained) ou Azure DevOps, avec sa **date d'expiration obligatoire**. | Le jeton, dans un coffre dédié, supprimé avec la connexion. |

Pour GitHub Actions, l'application GitHub d'IFS demande en plus la permission d'écriture sur les
workflows (`workflows`), sans laquelle GitHub refuse l'écriture dans `.github/workflows`.

**RG-PUB-01 — Test.** Une connexion est testée à l'ajout et à chaque modification : la liste des dépôts
accessibles est lue. Un échec empêche l'enregistrement.

**RG-PUB-02 — Expiration.** Pour un jeton de repli : alerte e-mail aux administrateurs 15 et 3 jours
avant l'expiration ; constat `VAL-PUB-CONNEXION` dans les projets qui l'utilisent.

**RG-PUB-03 — Droits.** Les connexions sont gérées par les administrateurs d'organisation. Un projet
choisit ses dépôts parmi ceux des connexions de son organisation.

**UC-PUB-01 — Ajouter, tester, modifier, retirer une connexion.** Le retrait est refusé tant qu'un plan
de publication l'utilise ; IFS liste les projets concernés.

## 3. Dépôts et destinations

**Destination** = connexion + dépôt + **chemin de base** (racine par défaut). La branche par défaut est
lue chez le fournisseur.

**RG-PUB-04 — Chemin de base.** Relatif, sans `..`, 200 caractères max. Toute écriture se fait sous ce
chemin ([DEC-21](03-decisions.md)), sauf les fichiers que la plateforme CI impose à la racine du dépôt
(RG-PUB-17, RG-PUB-18).

**RG-PUB-17 — Workflows GitHub à la racine.** GitHub n'exécute que les workflows de `.github/workflows`
à la racine du dépôt. Les workflows IFS y sont donc écrits quel que soit le chemin de base, avec des noms
préfixés `ifs-<projet>-` pour ne jamais entrer en collision avec ceux du client ni d'un autre projet. Ils
figurent au manifeste de leur destination. Les actions composites qu'ils appellent restent sous le
chemin de base (`.ifs/actions/`).

**RG-PUB-18 — Point d'entrée GitLab** *(lot 3)*. GitLab n'exécute qu'un fichier `.gitlab-ci.yml` à la
racine. IFS écrit ses pipelines sous `.ifs/gitlab/` ; si `.gitlab-ci.yml` n'existe pas, IFS le crée (et le
gère) avec les `include` nécessaires ; s'il existe et n'est pas géré, IFS ne le modifie pas et la liste de
contrôle donne les lignes `include` à ajouter.

**RG-PUB-19 — Compatibilité.** Chaque dépôt du plan de publication doit être compatible avec la plateforme
CI du projet ([DEC-47](03-decisions.md)) : GitHub Actions exige des dépôts GitHub ; Azure DevOps Pipelines
accepte Azure Repos et GitHub ; GitLab CI exige GitLab. Sinon erreur `VAL-PIP-COMPATIBILITE`.

## 4. Plan de publication (niveau projet)

Pour chaque composant, le plan désigne :

| Partie | Destination | Sous-dossier dans la destination |
|---|---|---|
| Infrastructure | Obligatoire pour publier | `<composant>/infra` |
| Applications | Obligatoire si le composant a des applications | `<composant>/apps` |
| Code source des applications | Dépôt (et branche par défaut) où vit le code, pour la navigation et les déclencheurs | — |

Le commun (`.ifs/`, `README.ifs.md`) est écrit à la racine de chaque destination utilisée.

### 4.1 Préréglages

À la création du projet (et à tout moment), un préréglage remplit le plan :

| Préréglage | Remplissage |
|---|---|
| **Mono-dépôt** | Un dépôt : infrastructure, applications et code de tous les composants. |
| **Infra et code séparés** | Un dépôt d'infrastructure pour toutes les infrastructures ; un dépôt de code pour les applications et leur code. |
| **Un dépôt par composant** | Pour chaque composant, un dépôt qui reçoit son infrastructure et ses applications. |

**RG-PUB-05 — Règle par défaut.** Le plan garde la règle de son préréglage pour les **nouveaux
composants** : un composant créé par un contributeur reçoit aussitôt sa destination. Pour « un dépôt par
composant », la destination d'un nouveau composant reste à désigner par un propriétaire
(`VAL-PUB-DESTINATION`).

**RG-PUB-06 — Pas de chevauchement.** Deux parties ne peuvent pas avoir le même dossier effectif dans un
même dépôt. Deux destinations d'un même dépôt ne peuvent pas avoir des chemins de base imbriqués.

**RG-PUB-07 — Modifier le plan.** Le propriétaire modifie toute ligne. Un changement de destination
d'une partie déjà publiée affiche l'effet : la prochaine publication écrira au nouvel endroit, et les
fichiers de l'ancien endroit seront retirés par une publication vers l'ancienne destination si
l'utilisateur la demande. Rien n'est supprimé implicitement.

## 5. Manifeste

**RG-PUB-08 — Contenu.** Chaque destination contient `.ifs/manifest.json` : projet, révision publiée,
date, liste des fichiers gérés de cette destination avec leur empreinte SHA-256.

**RG-PUB-09 — Fichiers gérés.** Seuls les fichiers listés dans le manifeste sont gérés. IFS ne crée,
modifie ou supprime **que** des fichiers gérés, plus le manifeste lui-même.

## 6. Publication

**UC-PUB-02 — Publier une révision** (contributeur, ou jeton `publish`). Saisie : révision (dernière par
défaut), destinations (toutes par défaut), mode (pull request par défaut, ou écriture directe sur une
branche), message.

**RG-PUB-10 — Révision périmée.** Si la révision est périmée ([RG-GEN-02](21-generation-et-revisions.md)),
l'écran propose de générer d'abord. Publier une révision périmée reste possible, avec confirmation
explicite.

**RG-PUB-11 — Préparation par destination.** Pour chaque destination, IFS lit la branche par défaut
(ou la branche cible) et calcule :

| Situation | Traitement |
|---|---|
| Fichier géré inchangé depuis la dernière publication et modifié par la révision | Mis à jour. |
| Fichier géré **modifié à la main** depuis la dernière publication (empreinte différente du manifeste) | Présenté en diff (modification manuelle → contenu de la révision). La publication de la destination exige une confirmation explicite. |
| Fichier géré qui n'est plus produit | Supprimé. |
| Nouveau fichier produit, déjà présent dans le dépôt mais non géré | Présenté comme conflit ; la publication de la destination exige une confirmation pour en prendre la propriété. |
| Fichier non géré | Jamais touché. |

**RG-PUB-12 — Mode pull request.** Les destinations d'un même dépôt sont publiées ensemble. Pour chaque
**dépôt**, IFS crée ou met à jour la branche `ifs/<projet>/revision-<n>` depuis la branche par défaut,
y fait **un seul commit** (toutes ses destinations), et ouvre une pull request :
- titre : « IFS révision <n> — <projet> » ;
- description : résumé des changements de la révision limité à ce dépôt, constats, nouveautés de la
  liste de contrôle ;
- relecteurs par défaut du projet.

S'il existe déjà une pull request IFS ouverte pour ce projet dans ce dépôt, sa branche est remplacée par
la nouvelle révision et la description mise à jour, au lieu d'ouvrir une deuxième pull request.

**RG-PUB-13 — Mode direct.** Commit sur une branche choisie (créée depuis la branche par défaut si
absente). Une branche protégée qui refuse l'écriture fait échouer la destination avec le message du
fournisseur.

**RG-PUB-14 — Plusieurs dépôts.** Tous les dépôts sont préparés et vérifiés avant la première écriture.
Les écritures sont indépendantes d'un dépôt à l'autre : le résultat est donné par dépôt et par
destination (succès, échec, motif). Un dépôt en échec peut être republié seul.

**RG-PUB-15 — Vérifications de contenu.** Avant écriture : modèles d'extension et code d'infrastructure additionnel
référencés présents dans la branche cible ([RG-APP-12](19-applications-build-et-deploiement.md),
[RG-GEN-17](21-generation-et-revisions.md)).

**RG-PUB-16 — Message.** 1 à 500 caractères ; par défaut « IFS révision <n> ». Le commit est signé par
l'identité de la connexion, avec l'auteur IFS indiqué dans le message (`Published-by: <utilisateur>`).

**UC-PUB-03 — Historique des publications** par destination : révision, date, auteur, commit, lien vers
la pull request ou la branche, résultat. *(Lot 2 : état de la pull request — ouverte, fusionnée,
fermée.)*

**UC-PUB-04 — Télécharger une révision** (archive) pour un usage hors publication. Le manifeste n'y est
pas inclus.

## 7. Navigation dans un dépôt de code

**UC-PUB-05 — Parcourir un dépôt** (lecture, contributeur) : branches, arborescence, recherche par nom,
lecture d'un fichier texte de moins de 1 Mo. Sert aux chemins des applications ([19 § 7](19-applications-build-et-deploiement.md)).
