# 19 — Applications : build, qualité et déploiement

## 1. Objectif

Pour chaque **application** (WebApp, FunctionApp, ContainerApp non existante), produire les pipelines
qui construisent le code une fois, le vérifient, puis déploient ce même artefact environnement par
environnement, en respectant la chaîne de promotion et ses approbations.

IFS gère le **build**, les **étapes de qualité et de sécurité** de son catalogue d'étapes
([DEC-50](03-decisions.md)) et le **déploiement**. Ce que le catalogue ne couvre pas passe par des
**points d'extension**.

## 2. Qui fait quoi : infrastructure ou application ([DEC-53](03-decisions.md))

Une application est touchée par deux pipelines. Pour qu'ils ne s'écrasent jamais l'un l'autre, chaque
élément a un seul propriétaire :

| Élément | Propriétaire | Comment le changer |
|---|---|---|
| Existence de l'application, plan, SKU, mise à l'échelle | Pipeline d'infrastructure | Modifier le modèle, publier, déployer l'infrastructure |
| Identités, rôles, réseau, domaines | Pipeline d'infrastructure | Idem |
| Paramètres applicatifs : variables d'environnement, références Key Vault, clés App Configuration | Pipeline d'infrastructure | Idem |
| Code (paquet) ou image en service | Pipeline applicatif | Pousser du code sur la branche par défaut |
| Répartition du trafic entre révisions, contenu des slots, bascules | Pipeline applicatif ([DEC-70](03-decisions.md)) | Stratégie de déploiement (section 9) |
| Existence des slots, mode de révision | Pipeline d'infrastructure | Modifier le modèle |
| Valeurs à changer sans aucun déploiement (feature flags, réglages fonctionnels) | App Configuration, lue à l'exécution | Modifier la valeur dans App Configuration (hors IFS) ou, si la clé est gérée par IFS, par le modèle |

**RG-APP-01 — Le pipeline applicatif ne touche pas à la configuration.** Il change uniquement le code ou
l'image.

**RG-APP-02 — L'infrastructure ne régresse pas la livraison.** Le déploiement d'infrastructure lit et
reconduit ce qui appartient au pipeline applicatif :
- l'image en service (mode Container) ;
- la répartition du trafic et les étiquettes des révisions (Container Apps) ;
- la règle de routage vers un slot.

Au tout premier déploiement, il utilise une image publique de démarrage fixée par le descripteur.

**RG-APP-03 — Ordre quand le code attend un nouveau paramètre.** Le résumé d'une révision signale les
paramètres ajoutés à une application : « à déployer avant la prochaine livraison de `<application>` ».
Un paramètre ajouté est sans effet sur l'ancien code ; le déployer d'abord est toujours sûr.

**RG-APP-04 — Modification manuelle.** Un paramètre modifié à la main dans le portail Azure est remplacé
au déploiement d'infrastructure suivant. L'aperçu de la release (what-if, plan) le montre avant
l'approbation. L'écran des paramètres de l'application le rappelle.

## 3. Données d'une application

| Champ | Applicable | Règles | Défaut |
|---|---|---|---|
| Nom d'application | Tous | `^[a-z][a-z0-9-]{1,39}$`, unique dans le composant. Dossier et noms des pipelines applicatifs. | Nom logique de la ressource |
| Code source | Tous | Chemin relatif dans le dépôt de code du composant ([24](24-depots-et-publication.md)). Déclencheur de la CI. | `src/<nom d'application>` |
| Mode | WebApp, FunctionApp | `Code` ou `Container` (propriété de la ressource). ContainerApp : `Container`. | — |
| Profil de build | Code | Déduit de la pile de la ressource (section 4). | — |
| Commande de build | Code | Remplace la commande du profil. | Celle du profil |
| Dossier de sortie | Code | Dossier à empaqueter. | Celui du profil |
| Dockerfile | Container | Chemin relatif. | `<code source>/Dockerfile` |
| Contexte de build | Container | Chemin relatif. | `<code source>` |
| Arguments de build | Container | Paires clé/valeur non secrètes. | Aucun |
| Dépôt d'image | Container | Chemin dans le registre, `^[a-z0-9]+([._/-][a-z0-9]+)*$`. | `<projet>/<composant>/<nom d'application>` |
| Registre de build | Container | Section 5. | Section 5 |
| Étapes | Tous | Étapes du catalogue activées et leurs paramètres (section 6). | Selon le profil |
| Étapes du client | Tous | Points d'extension (section 7). | Aucune |
| Contrôle de santé | Tous | Chemin HTTP appelé après déploiement ; succès = réponse 2xx sous 5 minutes. | Chemin de contrôle de santé de la ressource, s'il existe |

## 4. Build en mode Code

| Profil (pile) | Build par défaut | Sortie |
|---|---|---|
| .NET | `dotnet publish <code source> -c Release -o <sortie>` | Dossier publié |
| Node | Installation selon le fichier de verrouillage (`npm ci`, `pnpm install --frozen-lockfile`, `yarn install --immutable`), puis `run build` si le script existe | Dossier du code source |
| Angular (variante Node) | Installation, puis `ng build --configuration production` | `dist/<projet>` |
| Python | `pip install -r requirements.txt --target <sortie>/.python_packages/lib/site-packages` (Functions) ou paquet avec `requirements.txt` (Web App, build distant) | Dossier du code source |
| Java | `mvn -B package -DskipTests` ou `gradle build -x test` selon le fichier de build | `target/*.jar` ou `build/libs/*.jar` |
| PHP | `composer install --no-dev` si `composer.json` | Dossier du code source |
| PowerShell (Functions) | Aucun | Dossier du code source |

**RG-APP-05 — Version de pile.** Le build installe la version de pile de la ressource. La CI échoue si la
version n'est pas disponible sur l'exécuteur.

## 5. Build et promotion en mode Container

**RG-APP-06 — Une image, construite une fois.** La CI construit l'image (`buildx`), exécute les étapes
d'image activées (scan, SBOM) et la pousse dans le **registre de build** avec un tag immuable
`<numéro de build>-<sha court>`. La release déploie ce tag, inchangé, dans chaque environnement.

**RG-APP-07 — Registre de build et promotion.** Ils se déduisent des liaisons « tirage d'image » :

| Situation | Registre de build | Promotion |
|---|---|---|
| Registre d'un composant `Single` | Ce registre | Aucune : chaque environnement tire du même registre. |
| Un registre par environnement | Registre du **premier environnement de la chaîne**, affiché et modifiable | À chaque stage, import du tag dans le registre de l'environnement. |
| Registre existant | Le registre existant de l'environnement choisi comme build | Comme ci-dessus. |

**RG-APP-08 — Droits de la CI.** L'identité de la cible du registre de build reçoit le droit de pousser
sur ce registre : `AcrPush`, ou Container Registry Repository Writer limité au dépôt d'image si le
registre est en mode de permissions par dépôt ([15 § 3.9](15-catalogue.md)).

## 6. Catalogue d'étapes ([DEC-50](03-decisions.md))

Les étapes de qualité et de sécurité sont décrites comme les types de ressources : un **descripteur
d'étape** versionné déclare ses paramètres, ses valeurs par défaut **par pile**, et sa traduction **par
plateforme CI**.

**RG-APP-09 — Commande d'abord, plateforme pour publier.** Une étape exécute un outil en ligne de
commande de la pile. Les fonctions de la plateforme ne servent qu'à publier les résultats : onglet de
tests et de couverture (Azure DevOps), résumé de job et artefacts (GitHub). Ajouter une plateforme ne
réécrit donc pas les étapes, seulement leur publication.

**RG-APP-10 — Paramètres communs.** Toute étape a : activée (oui/non), **bloquante** (un échec arrête le
pipeline) ou **informative** (un échec est signalé sans arrêter), position (avant le build, après le
build, après le déploiement).

### 6.1 Étapes

| Étape | Paramètres propres | Lot |
|---|---|---|
| **Cache des dépendances** | — | 1 |
| **Scan de l'image** | Outil (Trivy), gravité bloquante minimale (`CRITICAL`, `HIGH`…), exceptions | 1 |
| **Tests unitaires** | Commande, motif des projets de test (.NET), format des résultats (JUnit, TRX), publication | 2 |
| **Couverture de code** | Format (Cobertura, JaCoCo), publication, **seuil minimal** en % | 2 |
| **Lint et format** | Commande | 2 |
| **Analyse des dépendances** | Outil, gravité bloquante minimale | 2 |
| **SonarQube / SonarCloud** | Instance (URL ou SonarCloud), organisation, clé de projet, attente de la quality gate (bloquante ou non) ; jeton dans le magasin de secrets CI (`SONAR_TOKEN`) ou connexion de service Sonar (Azure DevOps) | 2 |
| **Analyse des secrets du code** | Outil (gitleaks), chemins exclus | 2 |
| **SBOM** | Outil (Syft), format (SPDX, CycloneDX), publication comme artefact | 2 |
| **Tests post-déploiement** | Commande ou URL, environnements concernés | 2 |
| **Migration de schéma** | Outil (EF Core, Flyway, Liquibase, Alembic, ou commande), base cible ; exécutée avant la bascule du trafic, avec l'identité de déploiement et un accès aux données de niveau `Schéma` | 2 |

### 6.2 Valeurs par défaut par pile (lot 2)

| Étape | .NET | Node / Angular | Python | Java |
|---|---|---|---|---|
| Tests unitaires | `dotnet test` sur `**/*Tests.csproj`, TRX | `npm test` (Angular : `ng test --watch=false --browsers=ChromeHeadless`), JUnit | `pytest --junitxml` | `mvn test` / `gradle test`, JUnit |
| Couverture | Cobertura (coverlet) | Cobertura (istanbul) | Cobertura (coverage.py) | JaCoCo |
| Lint et format | `dotnet format --verify-no-changes` | `npm run lint` | `ruff check` | Checkstyle (si configuré) |
| Analyse des dépendances | `dotnet list package --vulnerable --include-transitive` | `npm audit` | `pip-audit` | OWASP Dependency-Check |

PHP (`phpunit`, `composer audit`) et Go (`go test`, `govulncheck`) arrivent au lot 3.

**RG-APP-11 — Étape non applicable.** Une étape sans valeur par défaut pour la pile exige une commande
saisie, sinon erreur `VAL-APP-ETAPE`.

**RG-APP-12 — Secrets d'étape.** Les jetons d'outils (Sonar…) vivent dans le magasin de secrets CI, jamais
dans IFS. La liste de contrôle les mentionne comme secrets de pipeline à saisir.

### 6.3 Détection *(lot 2)*

**UC-APP-01 — Proposer les étapes depuis le dépôt.** IFS lit le dépôt de code (branche par défaut, chemin
du code source) et propose des valeurs :
- projets de test et frameworks ;
- fichiers de configuration du lint (`.editorconfig`, `eslint.config.*`, `ruff.toml`…) ;
- `sonar-project.properties` (clé de projet) ;
- fichier de verrouillage, Dockerfile.

L'utilisateur accepte chaque proposition ou non. La détection ne lit que le chemin du code source.

## 7. Points d'extension

**RG-APP-13 — Étapes du client.** Pour ce que le catalogue ne couvre pas, des étapes écrites et
maintenues par le client, hors des fichiers gérés, au format de la plateforme :

| Plateforme | Format |
|---|---|
| Azure DevOps | Modèle d'étapes YAML (`steps`) |
| GitHub Actions | Action composite (`action.yml`) |
| GitLab CI *(lot 3)* | Modèle de job inclus (`include`) |

Elles se placent avant le build, après le build ou après le déploiement, et reçoivent des paramètres
documentés, identiques sur toutes les plateformes : chemin du code source, configuration, dossier de
sortie, environnement (après déploiement).

**RG-APP-14 — Présence vérifiée.** À la publication, IFS vérifie que les étapes référencées existent dans
la branche cible du dépôt de code. S'ils manquent : avertissement `VAL-APP-EXTENSION`, avec un modèle vide
proposé en téléchargement.

## 8. Déploiement

**RG-APP-15 — Stages.** Un stage par environnement où l'application est présente, dans l'ordre de la
chaîne. Un environnement protégé exige l'approbation de ses approbateurs (environnement de la plateforme
CI).

**RG-APP-16 — Vérification préalable.** Chaque stage vérifie d'abord que l'application existe dans
l'environnement. Sinon il échoue avec « déployez d'abord l'infrastructure du composant <code> en <env> ».

**RG-APP-17 — Actions.**

| Type | Code | Container |
|---|---|---|
| WebApp | Déploiement du paquet (zip) | Mise à jour de l'image |
| FunctionApp | Déploiement du paquet (zip, ou paquet Flex sur `FC1`) | Mise à jour de l'image |
| ContainerApp | — | Nouvelle révision avec la nouvelle image |

**RG-APP-18 — Contrôle de santé et tests post-déploiement.** Si un chemin de santé est défini, le stage
appelle l'application et échoue sans réponse 2xx en 5 minutes. Les tests post-déploiement activés
s'exécutent ensuite.

## 9. Stratégies de déploiement ([DEC-70](03-decisions.md)) *(lot 2, vague C sauf « directe »)*

**RG-APP-19 — Choix.** Chaque application choisit une stratégie, éventuellement différente par
environnement (exemple : directe en dev, bleu/vert en prod).

| Stratégie | Types | Déroulé d'un stage | Retour arrière |
|---|---|---|---|
| **Directe** (défaut, lot 1) | Tous | Déploiement, contrôle de santé. Container Apps en mode de révision unique : la nouvelle révision ne reçoit le trafic qu'une fois ses sondes de disponibilité au vert, donc sans coupure si les sondes sont définies. | Relancer la release du build précédent. |
| **Mise à jour progressive des instances** | Function App sur `FC1` | Propriété d'infrastructure « stratégie de mise à jour » = `RollingUpdate` : les instances sont vidées et remplacées par lots, sans interrompre les exécutions en cours. | Relancer la release du build précédent. |
| **Slot et bascule** | Web App, Function App sur un plan qui a des slots (Standard, Premium, Premium élastique) | 1. Déploiement dans le slot `staging` (créé par l'infrastructure, avec ses paramètres propres au slot). 2. Préchauffage et contrôle de santé sur l'adresse du slot. 3. Tests post-déploiement sur le slot. 4. Approbation de bascule si demandée. 5. Échange `staging` ↔ production. | Nouvel échange : l'ancienne version est restée dans le slot `staging`. |
| **Bleu/vert** | Container App | 1. Nouvelle révision au suffixe déterministe (numéro de build), étiquette `green`, 0 % du trafic. 2. Contrôle de santé et tests sur l'adresse d'étiquette. 3. Approbation de bascule si demandée. 4. Trafic 100 % vers `green` ; l'ancienne révision devient `blue`. 5. Après le délai de conservation, les anciennes révisions sont désactivées. | Remettre 100 % du trafic sur `blue`. |
| **Progressive (canary)** | Container App ; Web App et Function App par routage de trafic vers le slot | Paliers de trafic (par défaut 10 %, 50 %, 100 %), chacun suivi d'une durée d'observation et d'un contrôle de santé, voire d'une requête d'alerte ([33 § 4](33-gouvernance-couts-et-supervision.md)). Un palier en échec ramène le trafic à 0 % sur la nouvelle version et fait échouer le stage. | Automatique en cas d'échec ; sinon remettre le trafic sur l'ancienne version. |

| Paramètre | Stratégies | Défaut |
|---|---|---|
| Approbation avant bascule | Slot, bleu/vert, progressive | Oui en cible protégée, non ailleurs |
| Paliers et durée d'observation | Progressive | 10 %, 50 %, 100 % ; 10 minutes |
| Délai de conservation de l'ancienne version | Slot, bleu/vert | 24 heures |
| Paramètres propres au slot | Slot | Aucun ; à choisir parmi les paramètres de l'application |

**RG-APP-20 — Prérequis d'infrastructure.** La stratégie implique des réglages d'infrastructure, ajoutés
implicitement :
- slot `staging` (avec les mêmes identités, liaisons et paramètres, sauf ceux propres au slot) ;
- mode de révision `Multiple` (Container Apps) ;
- stratégie de mise à jour `RollingUpdate` (Flex).

Un plan sans slots (Free, Basic, Flex) rend « slot et bascule » impossible : `VAL-APP-STRATEGIE`.

**RG-APP-21 — Compatibilité des données.** Pendant une bascule, deux versions coexistent. L'écran rappelle que
les migrations de schéma doivent rester compatibles avec la version précédente (migrations en deux temps)
et que les orchestrations Durable Functions doivent fixer leur version.

**UC-APP-03 — Revenir à la version précédente.** Depuis le suivi des déploiements
([28](28-suivi-des-deploiements.md)), IFS affiche, pour chaque application et chaque environnement, la
version en service et la précédente, avec l'action de retour arrière propre à la stratégie (lien vers le
pipeline ou commande exacte). IFS ne l'exécute pas lui-même ([RG-SUI-08](28-suivi-des-deploiements.md)).

## 10. Navigation dans le code

**UC-APP-02 — Choisir un chemin.** Pour renseigner le code source, le Dockerfile ou une étape du client,
l'utilisateur parcourt le dépôt de code du composant (branche par défaut) : arborescence et recherche
par nom.
