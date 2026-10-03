# 19 — Applications : build et déploiement

## 1. Objectif

Pour chaque **application** (WebApp, FunctionApp, ContainerApp non existante), produire les pipelines
qui construisent le code une fois, puis déploient ce même artefact environnement par environnement, en
respectant la chaîne de promotion et ses approbations.

IFS gère le **build minimal** et le **déploiement**. Les tests, l'analyse de qualité et la sécurité du
code sont des étapes du client, branchées par des points d'extension ([DEC-25](03-decisions.md)).

## 2. Données d'une application

| Champ | Applicable | Règles | Défaut |
|---|---|---|---|
| Nom d'application | Tous | `^[a-z][a-z0-9-]{1,39}$`, unique dans le composant. Dossier et noms des pipelines applicatifs. | Nom logique de la ressource |
| Code source | Tous | Chemin relatif dans le dépôt de code du composant ([24](24-depots-et-publication.md)). Déclencheur de la CI. | `src/<nom d'application>` |
| Mode | WebApp, FunctionApp | `Code` ou `Container` (propriété de la ressource). ContainerApp : `Container`. | — |
| Dockerfile | Container | Chemin relatif. | `<code source>/Dockerfile` |
| Contexte de build | Container | Chemin relatif. | `<code source>` |
| Arguments de build | Container | Paires clé/valeur non secrètes. | Aucun |
| Dépôt d'image | Container | Chemin dans le registre, `^[a-z0-9]+([._/-][a-z0-9]+)*$`. | `<projet>/<composant>/<nom d'application>` |
| Registre de build | Container | Section 4. | Section 4 |
| Scan de l'image | Container | Booléen. | Oui |
| Profil de build | Code | Déduit de la pile de la ressource (section 3). | — |
| Commande de build | Code | Remplace la commande du profil. | Celle du profil |
| Dossier de sortie | Code | Dossier à empaqueter. | Celui du profil |
| Étapes avant build | Tous | Chemin d'étapes **du client** dans le dépôt de code, au format de la plateforme (section 6). | Aucun |
| Étapes après build | Tous | Idem. | Aucun |
| Contrôle de santé | Tous | Chemin HTTP appelé après déploiement ; succès = réponse 2xx sous 5 minutes. | Chemin de contrôle de santé de la ressource, s'il existe |

## 3. Build en mode Code

| Profil (pile) | Build par défaut | Sortie |
|---|---|---|
| .NET | `dotnet publish <code source> -c Release -o <sortie>` | Dossier publié |
| Node | Installation selon le fichier de verrouillage présent (`npm ci`, `pnpm install --frozen-lockfile`, `yarn install --immutable`), puis `run build` si le script existe | Dossier du code source |
| Python | `pip install -r requirements.txt --target <sortie>/.python_packages/lib/site-packages` (Functions) ou paquet avec `requirements.txt` (Web App, build à distance) | Dossier du code source |
| Java | `mvn -B package -DskipTests` ou `gradle build -x test` selon le fichier de build | `target/*.jar` ou `build/libs/*.jar` |
| PHP | Composer si `composer.json` | Dossier du code source |
| PowerShell (Functions) | Aucun | Dossier du code source |

**RG-APP-01 — Version de pile.** Le build installe la version de pile de la ressource. La CI échoue si
la version n'est pas disponible sur l'agent.

## 4. Build et promotion en mode Container

**RG-APP-02 — Une image, construite une fois.** La CI construit l'image (`buildx`), la scanne si l'option
est activée, et la pousse dans le **registre de build** avec un tag immuable
`<numéro de build>-<sha court>`. La release déploie ce tag, inchangé, dans chaque environnement.

**RG-APP-03 — Registre de build et promotion.** Ils se déduisent des liaisons « tirage d'image » de
l'application :

| Situation | Registre de build | Promotion |
|---|---|---|
| Le registre appartient à un composant `Single` | Ce registre | Aucune : chaque environnement tire du même registre. |
| Un registre par environnement (composant `PerEnvironment`) | Registre du **premier environnement de la chaîne**, affiché et modifiable | À chaque stage, `az acr import` du tag vers le registre de l'environnement. |
| Registre existant | Le registre existant de l'environnement choisi comme build | Comme ci-dessus. |

**RG-APP-04 — Droits de la CI.** L'identité de la cible du registre de build reçoit `AcrPush` sur ce
registre (rôle implicite de déploiement, [23](23-kit-installation.md)).

## 5. Déploiement

**RG-APP-05 — Stages.** Un stage par environnement où l'application est présente, dans l'ordre de la
chaîne. Un environnement protégé exige l'approbation de ses approbateurs (environnement de la plateforme CI).

**RG-APP-06 — Vérification préalable.** Chaque stage vérifie d'abord que l'application existe dans
l'environnement (infrastructure déployée). Sinon il échoue avec « déployez d'abord l'infrastructure du
composant <code> en <env> ».

**RG-APP-07 — Actions.**

| Type | Code | Container |
|---|---|---|
| WebApp | Déploiement du paquet (zip) | Mise à jour de l'image |
| FunctionApp | Déploiement du paquet (zip, ou paquet Flex sur `FC1`) | Mise à jour de l'image |
| ContainerApp | — | Nouvelle révision avec la nouvelle image |

**RG-APP-08 — Contrôle de santé.** Si un chemin est défini, le stage appelle l'adresse de l'application et
échoue sans réponse 2xx en 5 minutes.

**RG-APP-09 — Le pipeline applicatif ne touche pas à la configuration.** Il change uniquement le code ou
l'image. Paramètres, identités, mise à l'échelle et réseau appartiennent au déploiement
d'infrastructure ([RG-PAR-11](17-parametres-applicatifs-et-secrets.md)).

**RG-APP-10 — L'infrastructure ne régresse pas l'image.** Le déploiement d'infrastructure d'une
application en mode Container lit l'image actuellement déployée et la reconduit. Au tout premier
déploiement, il utilise une image publique de démarrage, fixée par le descripteur. Redéployer
l'infrastructure ne remplace donc jamais l'image livrée par le pipeline applicatif.

## 6. Points d'extension

**RG-APP-11 — Étapes du client.** Les étapes « avant build » et « après build » sont écrites et
maintenues par le client hors des fichiers gérés, au format de la plateforme CI du projet :

| Plateforme | Format |
|---|---|
| Azure DevOps | Modèle d'étapes YAML (`steps`) |
| GitHub Actions | Action composite (`action.yml`) |
| GitLab CI *(lot 3)* | Modèle de job inclus (`include`) |

Les pipelines générés les appellent avec des paramètres documentés, identiques sur toutes les plateformes :
chemin du code source, configuration, dossier de sortie.
C'est l'emplacement prévu pour les tests, la couverture, l'analyse de qualité et l'analyse des
dépendances.

**RG-APP-12 — Présence vérifiée.** À la publication, IFS vérifie que les modèles référencés existent dans
la branche cible du dépôt de code. S'ils manquent : avertissement `VAL-APP-EXTENSION` avec un modèle
vide proposé en téléchargement.

## 7. Navigation dans le code

**UC-APP-01 — Choisir un chemin.** Pour renseigner le code source, le Dockerfile ou un modèle
d'extension, l'utilisateur parcourt le dépôt de code du composant (branche par défaut) : arborescence
et recherche par nom.

**UC-APP-02 — Suggestions.** À la saisie du code source, IFS propose les valeurs déductibles des
fichiers présents : Dockerfile, fichier de verrouillage Node, `*.csproj`, `pom.xml`. L'utilisateur
accepte ou non. Aucune autre détection n'est faite.

## 8. Slots de déploiement *(lot 2)*

Une WebApp ou FunctionApp sur un plan qui le permet peut déployer dans un slot `staging`, contrôler sa
santé, puis basculer. Les paramètres marqués « propres au slot » ne basculent pas.
