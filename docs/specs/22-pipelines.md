# 22 — Pipelines

## 1. Objectif

Produire, pour la plateforme CI du projet ([DEC-47](03-decisions.md)), les pipelines qui valident,
construisent et déploient chaque composant et chaque application, dans l'ordre de la chaîne de
promotion, avec un aperçu des changements avant chaque déploiement.

Les pipelines sont d'abord décrits de façon **logique** (étapes, déclencheurs, cibles) dans le plan de
déploiement ([21 § 3](21-generation-et-revisions.md)). L'émetteur de la plateforme les traduit
(section 6).

## 2. Règles communes

**RG-PIP-01 — Autonomie par destination.** Les pipelines d'une destination n'utilisent que des modèles
présents dans la même destination (`.ifs/`). Aucun pipeline ne dépend d'un autre dépôt
([DEC-21](03-decisions.md)).

**RG-PIP-02 — Exécuteurs.** Pour un stage de cible *E* : exécuteurs de *E*, sinon ceux du projet, sinon
les exécuteurs hébergés de la plateforme (Linux). Les jobs sans cible (PR, CI) utilisent ceux du projet.

**RG-PIP-03 — Connexion à Azure.** Chaque stage d'une cible s'authentifie par la connexion de
déploiement de cette cible, en fédération d'identité (OIDC). Aucun secret de principal de service.

**RG-PIP-04 — Environnements.** Chaque stage de déploiement s'exécute dans l'environnement
`<projet>-<cible>` de la plateforme. Les approbations des cibles protégées s'y appliquent
([DEC-39](03-decisions.md)).

**RG-PIP-05 — Un déploiement à la fois.** Deux déploiements d'un même composant (ou d'une même
application) dans une même cible ne s'exécutent jamais en parallèle : le second attend. Cela protège
l'état Terraform ou Pulumi et la pile Bicep.

**RG-PIP-06 — Noms.**

| Pipeline | Nom |
|---|---|
| Infrastructure | `<projet> · <composant> · infra · PR` / `CI` / `Release` |
| Application | `<projet> · <composant> · <application> · PR` / `CI` / `Release` |
| Installation (Azure DevOps) | `<projet> · installation` |

**RG-PIP-07 — Lisibilité.** YAML formaté, commenté par étape. Toute étape non triviale est un modèle
partagé nommé (modèle Azure DevOps, action composite GitHub).

## 3. Pipelines d'infrastructure (par composant)

| Pipeline | Déclenchement | Étapes |
|---|---|---|
| **PR** | Pull request vers la branche par défaut, sur les fichiers d'infrastructure du composant et les modèles partagés. | Contrôles du langage ([21 § 6](21-generation-et-revisions.md)) ; aperçu des changements sur la première cible non protégée, publié en commentaire de la pull request quand la plateforme le permet. |
| **CI** | Commit sur la branche par défaut, mêmes chemins. | Contrôles du langage ; publication de l'artefact `infra`. |
| **Release** | Fin réussie de la CI. | Un stage par cible, dans l'ordre (section 3.1). |

### 3.1 Stage de release d'infrastructure (une cible)

1. **Vérification des dépendances.** Pour chaque composant dont celui-ci dépend
   ([RG-CMP-05](12-composants-et-groupes-de-ressources.md)), les ressources ciblées existent dans la
   cible. Sinon échec : « déployez d'abord <composant> en <cible> » ([DEC-40](03-decisions.md)).
2. **Vérification des secrets** ([RG-PAR-14](17-parametres-applicatifs-et-secrets.md)), puis lecture ou
   génération des mots de passe d'administration ([RG-PAR-17](17-parametres-applicatifs-et-secrets.md)).
3. **Lecture des images en service** des applications en mode Container
   ([RG-APP-02](19-applications-build-et-deploiement.md)).
4. **Aperçu** des changements ; résumé publié dans le rapport du run.
5. **Approbation** si la cible est protégée ; l'approbateur voit l'aperçu.
6. **Déploiement.**
7. **Écriture des secrets de pipeline** et des identifiants admin de registre dans leurs Key Vaults ; redémarrage des applications dont une
   référence de secret a changé de valeur ([RG-PAR-15](17-parametres-applicatifs-et-secrets.md)).
8. **Scripts post-déploiement** : accès aux données ([16 § 6](16-liaisons-identites-et-acces.md)), avec
   ouverture et fermeture temporaires du pare-feu si nécessaire.
9. **Domaines personnalisés** *(lot 2)* : vérification DNS puis liaison ([RG-NET-11](18-reseau-et-exposition.md)).

### 3.2 Étapes propres au langage

| Étape | Bicep | Terraform, OpenTofu *(lots 3, 4)* | Pulumi *(lot 4)* |
|---|---|---|---|
| Préparation | — | `terraform init` avec `targets/<cible>.backend.hcl` | `npm ci`, connexion au backend d'état, sélection de la pile |
| Aperçu | `what-if` de la pile | `terraform plan -out` ; le plan est conservé comme artefact du run | `pulumi preview --diff` |
| Déploiement | Création ou mise à jour de la pile de déploiement | `terraform apply` **du plan conservé** : ce qui est approuvé est exactement ce qui est appliqué | `pulumi up` ; tout écart avec l'aperçu approuvé est signalé dans le rapport |

**RG-PIP-08 — Enchaînement.** Les cibles non protégées se déploient automatiquement l'une après l'autre.
Une cible protégée attend son approbation. Un échec arrête les stages suivants.

## 4. Pipelines applicatifs (par application)

| Pipeline | Déclenchement | Étapes |
|---|---|---|
| **PR** | Pull request, sur le code source de l'application. | Étapes « avant build » du client ; build ; étapes « après build » du client. Pas de publication d'artefact. |
| **CI** | Commit sur la branche par défaut, mêmes chemins. | Étapes « avant build » ; build ([19 § 3](19-applications-build-et-deploiement.md)) ou image ([19 § 4](19-applications-build-et-deploiement.md)) avec scan ; étapes « après build » ; publication de l'artefact ou poussée de l'image. |
| **Release** | Fin réussie de la CI. | Un stage par environnement de présence : vérification préalable, promotion d'image si nécessaire, approbation si protégé, déploiement selon la stratégie choisie (directe, slot et bascule, bleu/vert, progressive, [19 § 9](19-applications-build-et-deploiement.md)), contrôle de santé, tests post-déploiement. |

### 4.1 Autres pipelines

| Pipeline | Déclenchement | Rôle |
|---|---|---|
| **Dérive** *(lot 2)* | Planifié, chaque jour | Aperçu de la dernière révision déployée de chaque composant dans chaque cible, résumé publié pour IFS ([33 § 5](33-gouvernance-couts-et-supervision.md)). |
| **Installation** (Azure DevOps) | Manuel | Kit d'installation ([23 § 3.1](23-kit-installation.md)). |

**RG-PIP-10 — Fenêtres et délais** *(lot 2)*. Les fenêtres de déploiement et délais d'attente des cibles
([DEC-77](03-decisions.md)) sont appliqués par la plateforme : contrôle « heures ouvrées » des environnements
Azure DevOps ; règle de délai d'attente des environnements GitHub. Un déploiement hors fenêtre attend, il
n'échoue pas.

**RG-PIP-09 — Chemins de déclenchement.** La CI se déclenche sur le code source, le Dockerfile, le
contexte de build et les modèles d'extension. Jamais sur les fichiers d'infrastructure.

## 5. Ce qui n'est pas généré

- Aucune étape de test, de couverture, de Sonar ou de lint en dur ([DEC-25](03-decisions.md)) : elles
  passent par les points d'extension.
- Aucune notification : les plateformes les gèrent nativement.
- Aucune option cachée : tout ce que les pipelines font découle du modèle et est décrit ici.

## 6. Traduction par plateforme

| Élément logique | Azure DevOps (lot 1) | GitHub Actions (lot 2) | GitLab CI (lot 3) |
|---|---|---|---|
| Emplacement des fichiers | `<composant>/infra/pipelines/`, `<composant>/apps/<app>/pipelines/` | `.github/workflows/ifs-<projet>-<composant>[-<app>]-<pr\|ci\|release>.yml` à la racine du dépôt | Fichiers sous `.ifs/gitlab/`, inclus depuis `.gitlab-ci.yml` ([RG-PUB-18](24-depots-et-publication.md)) |
| Modèles partagés | `.ifs/templates/` (modèles YAML) | `.ifs/actions/` (actions composites) | `.ifs/gitlab/templates/` |
| Enchaînement CI → Release | Ressource de pipeline | Événement `workflow_run` | Pipeline enfant / `needs` |
| Stage de cible | Job de déploiement sur l'environnement | Job avec `environment:` | Job avec `environment:` |
| Approbation | Contrôle d'approbation de l'environnement | Relecteurs requis de l'environnement | Approbations d'environnement protégé |
| Exclusivité (RG-PIP-05) | Contrôle de verrou exclusif | `concurrency` par composant × cible | `resource_group` |
| Connexion à Azure | Service connection ARM fédérée | `azure/login` en OIDC, avec identifiants non secrets en variables d'environnement | `id_tokens` + `az login --federated-token` |
| Secrets | Groupe de variables de la cible | Secrets de l'environnement | Variables CI/CD limitées à l'environnement |
| Définition des pipelines | Créée par le pipeline d'installation | Automatique (fichiers du dossier `.github/workflows`) | Automatique |
| Commentaire de PR (aperçu) | API Azure Repos ou GitHub selon le dépôt | Commentaire de pull request | Note de merge request |
