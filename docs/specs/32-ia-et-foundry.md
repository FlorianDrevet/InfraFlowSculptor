# 32 — Applications d'IA : Microsoft Foundry *(lot 2, vague B)*

## 1. Objectif

Permettre de modéliser une application d'IA générative complète, sécurisée et privatisable : modèles
déployés, projets, agents, recherche vectorielle, stockage des conversations, et applications qui les
consomment par identité ([DEC-67](03-decisions.md)).

> Le domaine évolue vite. Les types, rôles, modèles et SKU ci-dessous sont la version initiale du
> catalogue ; ils sont revus à chaque version du catalogue ([40 § 3](40-exploitation-ifs.md)).

## 2. Types

### 2.1 Compte Foundry (`FoundryAccount`)

Ressource Azure AI Services (`Microsoft.CognitiveServices/accounts`, type `AIServices`, gestion de projets
activée). Abréviation `aif` · nom 2–64, lettres, chiffres, tirets · unicité globale. Le sous-domaine
personnalisé est égal au nom Azure (obligatoire pour l'authentification Entra).

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| SKU | `S0` | `S0` | — |
| Désactiver l'authentification locale | Booléen ; `Non` est **D** ([DEC-51](03-decisions.md)) | Oui | S |
| Réseau des agents | `Géré par Microsoft` ou injection dans un subnet délégué `Microsoft.App/environments` | `Géré par Microsoft` | V |
| Exposition | Publique, restreinte, privée (`groupId` `account`, zones `privatelink.cognitiveservices.azure.com`, `privatelink.openai.azure.com`, `privatelink.services.ai.azure.com`) | Publique | S |

Enfants : **déploiements de modèles** (section 2.3).
Identité : système (utilisée par les connexions).
Rôles cible : Azure AI User, Azure AI Developer, Azure AI Project Manager, Cognitive Services OpenAI User,
Cognitive Services OpenAI Contributor, Cognitive Services User, Reader.
Sorties : `endpoint`, `openAiEndpoint`, `name`, `id`.

### 2.2 Projet Foundry (`FoundryProject`)

Projet d'un compte Foundry (`accounts/projects`). Abréviation `aifp`. Un projet isole les accès, les
données et les coûts d'une équipe ou d'une application.

| Propriété | Valeurs | Défaut |
|---|---|---|
| Compte | Liaison **hébergement** obligatoire vers un `FoundryAccount` (même composant) | — |
| Nom affiché, description | Texte | Nom logique |
| Stockage des agents | `Géré par Microsoft` (configuration de base) ou `Ressources du client` (configuration standard, section 4) | `Géré par Microsoft` |

Identité : système.
Rôles cible : Azure AI User, Azure AI Developer, Azure AI Project Manager.
Sorties : `projectEndpoint`, `name`, `id`.

### 2.3 Déploiement de modèle (enfant d'un compte)

| Champ | Valeurs | Attributs |
|---|---|---|
| Nom | 2 à 64 caractères, unique dans le compte ; c'est le nom que l'application utilise | V |
| Modèle | Fournisseur, nom et version, choisis dans la liste du catalogue des modèles disponibles dans la région (exemple : famille GPT, modèles d'embeddings) | V (version modifiable) |
| Type de déploiement | `GlobalStandard`, `DataZoneStandard`, `Standard`, `GlobalProvisionedManaged`, `DataZoneProvisionedManaged`, `ProvisionedManaged`, selon le modèle et la région | S |
| Capacité | Milliers de jetons par minute (standard) ou unités provisionnées (PTU) | S |
| Montée de version | `Dès qu'une nouvelle version par défaut existe`, `À l'expiration de la version`, `Jamais` | — |
| Filtre de contenu | Politique par défaut, ou politique enfant du compte *(lot 3)* | — |

**RG-IA-01 — Résidence des données.** `DataZone*` garde le traitement dans la zone (UE, États-Unis) ;
`Global*` peut traiter dans toute région. L'écran l'explique. Une politique d'organisation peut interdire
`Global*` ([DEC-71](03-decisions.md)).

**RG-IA-02 — Quotas.** La capacité est limitée par le quota de l'abonnement, par modèle et par région.
- Sans connexion Azure : constat `Info` qui rappelle de vérifier le quota.
- Avec la connexion Azure en lecture (lot 2) : IFS lit l'usage et le quota, et produit
  `VAL-IA-QUOTA` (`Erreur` si la somme des capacités du projet dans l'abonnement dépasse le quota).

**RG-IA-03 — Disponibilité.** Un modèle ou un type de déploiement indisponible dans la région effective est
une erreur `VAL-CAT-REGION`. Un modèle dont la date de retrait approche suit le cycle de dépréciation
([DEC-41](03-decisions.md)).

### 2.4 AI Search (`SearchService`)

Abréviation `srch` · nom 2–60, minuscules, chiffres, tirets · unicité globale.

| Propriété | Valeurs | Défaut | Attributs |
|---|---|---|---|
| SKU | `free`, `basic`, `standard`, `standard2`, `standard3`, `storage_optimized_l1`, `storage_optimized_l2` | `basic` | S (changement de niveau soumis aux règles Azure) |
| Réplicas, partitions | Entiers, bornes selon SKU | 1, 1 | S |
| Classement sémantique | `Désactivé`, `Gratuit`, `Standard` | `Gratuit` | S |
| Authentification | `RBAC seul` (recommandé) ou `RBAC et clés` (**D**) | `RBAC seul` | S |
| Exposition | Publique, restreinte, privée (`searchService`) | Publique | S |

Rôles : Search Index Data Reader, Search Index Data Contributor, Search Service Contributor.
Sorties : `endpoint`, `name`, `id`.

### 2.5 Cosmos DB

Le type Cosmos DB (API NoSQL) est décrit en [15 § 4](15-catalogue.md). Il est livré avec l'IA (vague B),
car les agents en configuration standard en ont besoin.

## 3. Consommer un modèle depuis une application

**UC-IA-01 — Liaison « utilisation d'IA »** d'une Web App, Function App ou Container App vers un compte ou
un projet Foundry.

| Paramètre | Contenu |
|---|---|
| Identité | Identité de l'application utilisée. |
| Niveau | `Inférence` (appeler les modèles) ou `Agents` (créer et exécuter des agents dans le projet). |
| Déploiements | Déploiements de modèles utilisés, avec le nom de variable pour chacun. |

**RG-IA-04 — Ce qu'IFS déduit.**
- Rôle implicite : Cognitive Services OpenAI User (inférence sur le compte) ou Azure AI User (agents sur le
  projet).
- Paramètres implicites : adresse du compte ou du projet (`AZURE_AI_PROJECT_ENDPOINT`,
  `AZURE_OPENAI_ENDPOINT`, noms modifiables), nom de chaque déploiement utilisé
  (`AZURE_OPENAI_DEPLOYMENT_<NOM>`).
- Aucune clé : l'authentification locale du compte est désactivée par défaut.

## 4. Agents en configuration standard

**RG-IA-05 — Ressources du client.** Un projet en configuration standard stocke les données de ses agents
dans les ressources du client. Il exige trois liaisons **connexion Foundry** du projet vers :

| Ressource | Usage | Contrainte |
|---|---|---|
| Compte de stockage | Fichiers téléversés | — |
| AI Search | Index vectoriels | — |
| Cosmos DB (NoSQL) | Fils de conversation et métadonnées | Débit total d'au moins 3 000 RU/s, provisionné ou serverless (`VAL-IA-COSMOS`) |

IFS génère les connexions du projet (authentification par identité), l'hôte de capacités des agents et les
rôles implicites de l'identité du projet sur chaque ressource : Storage Blob Data Contributor, Search Index
Data Contributor et Search Service Contributor, rôle de données Cosmos DB intégré « Data Contributor ».

**RG-IA-06 — Réseau privé de bout en bout.** Avec le réseau des agents injecté dans un subnet, le compte et
les trois ressources doivent être privés, avec leurs points de terminaison et leurs zones DNS. Azure ne
crée pas ces points de terminaison : IFS les génère comme pour toute ressource privée
([18 § 8](18-reseau-et-exposition.md)). Sinon `VAL-NET-CONSOMMATEUR`.

## 5. Autres connexions

**UC-IA-02 — Connexion Foundry** d'un projet ou d'un compte vers Application Insights (traçage des agents et
des appels), un Key Vault, un stockage ou un AI Search pour les données des applications. Authentification
par identité, rôle implicite selon la cible.

## 6. Hors périmètre

Le contenu des agents, les index, les prompts, les évaluations et les flux ne sont pas de l'infrastructure :
ils relèvent du code de l'application ou du portail Foundry. Les projets « hub » de l'ancienne génération ne
sont pas modélisés ; un hub existant peut être référencé comme ressource existante.
