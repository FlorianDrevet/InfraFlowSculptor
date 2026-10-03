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

**RG-PIP-03 — Connexion à Azure.** Chaque stage d'une cible s'authentifie en fédération d'identité (OIDC),
sans aucun secret de principal de service, par la connexion qui correspond à son rôle
([DEC-88](03-decisions.md)) : la connexion d'infrastructure pour les pipelines d'infrastructure, la
connexion applicative pour les pipelines applicatifs. Les pipelines de pull request n'ont aucune connexion
Azure.

**RG-PIP-04 — Environnements.** Chaque stage de déploiement s'exécute dans l'environnement
`<projet>-<cible>` de la plateforme. Les approbations des cibles protégées s'y appliquent
([DEC-39](03-decisions.md)).

**RG-PIP-05 — Un déploiement à la fois par cible.** Tous les stages qui modifient une cible
(infrastructure de tous ses composants et applications) partagent le verrou exclusif de son
environnement, en mode **séquentiel** : le second attend, aucune exécution n'est abandonnée
([DEC-87](03-decisions.md)). Cela protège la pile, l'état et le journal d'opérations. Le stage Aperçu
n'est pas sous ce verrou : une livraison d'application peut avoir lieu entre l'aperçu et le déploiement.
Elle est alors conservée, car le stage Déploiement relit sous le verrou l'image et le trafic en service
avant de les reconduire, et elle n'invalide pas l'approbation ([DEC-102](03-decisions.md)). Le verrou porte
sur la cible entière : deux composants d'une même cible ne se déploient pas en parallèle ; l'attente et
le débit sont mesurés pendant le pilote.

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
| **PR** | Pull request vers la branche par défaut, sur les fichiers d'infrastructure du composant et les modèles partagés. | Contrôles du langage ([21 § 6](21-generation-et-revisions.md)), sans connexion Azure. Le résumé des changements de la révision est déjà dans la description de la pull request ([24](24-depots-et-publication.md)). |
| **CI** | Commit sur la branche par défaut, mêmes chemins. | Contrôles du langage ; publication de l'artefact `infra`. |
| **Release** | Fin réussie de la CI. | Pour chaque cible, dans l'ordre : un stage **Aperçu** puis un stage **Déploiement** (section 3.1). |

### 3.1 Release d'infrastructure d'une cible : Aperçu, puis Déploiement

L'ordre des étapes est fixe ([DEC-86](03-decisions.md), [DEC-87](03-decisions.md), [DEC-100](03-decisions.md),
[DEC-102](03-decisions.md)). Chaque étape est idempotente, et le **journal d'opérations** de l'unité, tenu
chez le client (RG-PIP-12), permet de reprendre une release interrompue à tout moment. Les
scénarios de reprise et de concurrence sont dans [91](91-scenarios-critiques.md).

**Stage Aperçu** — il n'utilise pas l'environnement protégé, donc ne demande aucune approbation. Il
s'authentifie par la connexion d'infrastructure de la cible, que seuls les pipelines IFS de la branche par
défaut peuvent utiliser (RG-PIP-13). Il ne modifie rien.

1. **Lecture du journal.** Les opérations `à faire` ou `commencées` d'une exécution précédente de l'unité
   sont reprises : elles font partie de l'aperçu et seront terminées en premier.
2. **Vérification des dépendances.** Pour chaque composant dont celui-ci dépend
   ([RG-CMP-05](12-composants-et-groupes-de-ressources.md)), les ressources ciblées existent dans la
   cible. Sinon échec : « déployez d'abord <composant> en <cible> » ([DEC-40](03-decisions.md)).
3. **Vérification des secrets.** Chaque secret de pipeline attendu a une valeur
   ([RG-PAR-14](17-parametres-applicatifs-et-secrets.md)) ; chaque mot de passe que le composant doit lire
   existe dans son Key Vault, ou sera écrit à l'étape 8 depuis une variable de pipeline non vide
   ([RG-PAR-17](17-parametres-applicatifs-et-secrets.md)). Sinon échec, avec la liste et l'action à faire.
4. **Aperçu** (section 3.2). Le résumé publié dans le run, lisible par l'approbateur, sépare : opérations
   reprises du journal ; ressources créées, modifiées, recréées ; ressources qui seront détachées ou
   supprimées ; **accès révoqués** ([DEC-85](03-decisions.md)) ; écritures de plan de données prévues
   (secrets, clés, utilisateurs de base, par leur nom) ; limites de l'aperçu. Il produit l'**empreinte**
   des effets ([DEC-102](03-decisions.md)).

**Stage Déploiement** — sur l'environnement `<projet>-<cible>`, après l'approbation si la cible est
protégée, sous le verrou séquentiel de la cible (RG-PIP-05).

5. **Relecture sous verrou.** L'image, la répartition du trafic et le routage en service sont relus et
   seront reconduits tels qu'ils sont maintenant ([RG-APP-02](19-applications-build-et-deploiement.md)).
   L'aperçu est recalculé ; si l'empreinte des effets diffère de celle approuvée, le stage s'arrête sans
   rien modifier et une nouvelle approbation est nécessaire.
6. **Journal.** Toutes les opérations de mutation prévues sont écrites au journal à l'état `à faire`,
   **avant** la première d'entre elles : reprises, secrets vers coffres existants, déploiement de l'unité,
   révocations, suppressions, écritures de plan de données.
7. **Opérations reprises** du journal, terminées en premier.
8. **Secrets vers des coffres existants.** Les secrets dont ce composant est l'écrivain dans un Key Vault
   qui n'est pas géré par le projet sont écrits avant le déploiement ([RG-PAR-15](17-parametres-applicatifs-et-secrets.md)).
9. **Déploiement** de l'unité (pile, état). Le code d'infrastructure ordonne chaque attribution de rôle
   avant la ressource qui en a besoin. En Bicep, la pile détache ce qui sort du modèle (`detachAll`) et, en
   cible protégée, applique le refus de suppression ([DEC-99](03-decisions.md)).
10. **Révocations et suppressions.** Les objets d'autorisation et de configuration sortis de l'unité sont
    supprimés, même en cible protégée ([DEC-85](03-decisions.md)) ; les ressources à données d'un
    composant en « supprimer » le sont hors cible protégée. Les ressources détachées sont inscrites au
    rapport.
11. **Écritures de plan de données**, en attendant par réessais (10 minutes au plus) les droits tout juste
    attribués ; toute autre erreur arrête la release :
    - secrets des Key Vaults du composant dont il est l'écrivain : secrets de pipeline, mots de passe
      générés absents ([RG-PAR-17](17-parametres-applicatifs-et-secrets.md)), identifiants admin de
      registre ; puis redémarrage des applications dont une référence de secret a changé de valeur ;
    - clés des magasins App Configuration du composant : synchronisation
      ([RG-PAR-23](17-parametres-applicatifs-et-secrets.md)) ;
    - accès aux données des liaisons **sortantes** du composant ([16 § 6](16-liaisons-identites-et-acces.md),
      [DEC-103](03-decisions.md)), avec ouverture et fermeture temporaires du pare-feu si nécessaire.
12. **Domaines personnalisés** *(lot 2)* : vérification DNS puis liaison ([RG-NET-17](18-reseau-et-exposition.md)).
13. **Rapport de la release** (`ifs-report.json`, [DEC-91](03-decisions.md)) : copie du journal, ressources
    détachées, accès révoqués, résultat. Il est produit à chaque fin, même en échec. Si l'agent disparaît
    avant, le journal reste la référence de la release suivante.

Chaque opération passe au journal de `à faire` à `commencée`, puis à `faite`. L'état affiché par IFS en
découle ([28 § 3.1](28-suivi-des-deploiements.md)) : **partiellement appliquée** dès qu'une opération de
mutation a commencé sans se terminer, déploiement de l'unité compris ; **en échec avant modification** si
aucune n'a commencé ; **indéterminée** si ni le rapport ni le journal ne sont lisibles.

**RG-PIP-11 — Coffres avant consommateurs.** Les étapes 8 et 11 supposent qu'un Key Vault alimenté par la
release est déployé avant les consommateurs de ses secrets : c'est la règle `VAL-SEC-ORDRE`
([DEC-86](03-decisions.md)). Elle garantit qu'un premier déploiement réussit depuis un abonnement vide, en
une seule exécution par composant, dans l'ordre de déploiement.

**RG-PIP-12 — Journal d'opérations** ([DEC-100](03-decisions.md)). Un blob par unité de déploiement et par cible
dans le conteneur `ifs-operations` du compte de stockage technique de la cible, écrit par l'identité de
déploiement sous un bail exclusif. Chaque opération a un identifiant stable (unité, cible, nature, objet
Azure visé) : une reprise ne la duplique pas. Une opération reprise est terminée même si la release
déploie une révision plus récente ; si elle n'a plus d'objet (accès déjà supprimé), elle est marquée
`faite` avec la mention « déjà absent ».

**RG-PIP-13 — Frontière de confiance** ([DEC-101](03-decisions.md)). Le contenu de la branche par défaut des
dépôts de pipelines est de confiance ; sa modification passe par une pull request relue. Les connexions
d'infrastructure et applicatives de chaque cible portent les contrôles « contrôle de branche » (branche par
défaut) et « modèle requis » (modèle de release IFS du dépôt) ; la connexion applicative d'une cible
protégée porte en plus son contrôle d'approbation. Les pipelines d'infrastructure n'ont aucun point
d'extension de type étape ; les étapes du client ne s'exécutent qu'en CI applicative, avec la connexion
applicative de la cible du registre de build.

### 3.2 Étapes propres au langage

| Étape | Bicep | Terraform, OpenTofu *(lots 3, 4)* | Pulumi *(lot 4)* |
|---|---|---|---|
| Préparation | — | `terraform init` avec `targets/<cible>.backend.hcl` | `npm ci`, connexion au backend d'état, sélection de la pile |
| Aperçu | What-if ARM du modèle à la portée de l'abonnement, plus comparaison entre les ressources gérées par la pile et celles du modèle (le what-if n'existe pas pour les piles) | `terraform plan -out` ; le plan est conservé comme artefact du run | `pulumi preview --diff` |
| Déploiement | Mise à jour de la pile (`detachAll` ; refus de suppression `denyDelete` en cible protégée, [DEC-99](03-decisions.md)), puis révocations et suppressions explicites (étape 10) | `terraform apply` **du plan conservé** : Terraform refuse un plan devenu périmé | `pulumi up`, après le contrôle d'empreinte de l'étape 5 |

**RG-PIP-08 — Enchaînement.** Les cibles non protégées se déploient automatiquement l'une après l'autre.
Une cible protégée attend son approbation. Un échec arrête les stages suivants.

## 4. Pipelines applicatifs (par application)

| Pipeline | Déclenchement | Étapes |
|---|---|---|
| **PR** | Pull request, sur le code source de l'application. | Étapes « avant build » du client ; build ; étapes « après build » du client. Pas de publication d'artefact, aucune connexion Azure. |
| **CI** | Commit sur la branche par défaut, mêmes chemins. | Étapes « avant build » ; build ([19 § 3](19-applications-build-et-deploiement.md)) ou image ([19 § 4](19-applications-build-et-deploiement.md)) avec scan ; étapes « après build » ; publication de l'artefact ou poussée de l'image avec l'identité applicative de la cible du registre de build. |
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
| Stage Aperçu | Job sans environnement ; la connexion d'infrastructure porte les contrôles de branche et de modèle requis | Job dans l'environnement `<projet>-<cible>-apercu`, limité à la branche par défaut, sans relecteurs ; l'identité d'infrastructure a un identifiant fédéré pour cet environnement ([DEC-101](03-decisions.md)) | Job sans `environment:`, sur la branche par défaut protégée |
| Stage Déploiement | Job de déploiement sur l'environnement | Job avec `environment:` | Job avec `environment:` |
| Approbation | Contrôle d'approbation de l'environnement, évalué avant le stage Déploiement | Relecteurs requis de l'environnement, évalués avant le job | Approbations d'environnement protégé |
| Exclusivité (RG-PIP-05) | Contrôle de verrou exclusif de l'environnement, `lockBehavior: sequential` | `concurrency` par cible, `cancel-in-progress: false` | `resource_group` par cible |
| Connexion à Azure | Deux service connections ARM fédérées par cible : infrastructure et applications | `azure/login` en OIDC, avec les identifiants non secrets de l'identité du pipeline en variables d'environnement | `id_tokens` + `az login --federated-token` |
| Secrets | Groupe de variables de la cible | Secrets de l'environnement | Variables CI/CD limitées à l'environnement |
| Définition des pipelines | Créée par le pipeline d'installation | Automatique (fichiers du dossier `.github/workflows`) | Automatique |
| Rapport de release | Artefact du run, lu par IFS | Artefact du workflow, lu par IFS | Artefact du job, lu par IFS |
