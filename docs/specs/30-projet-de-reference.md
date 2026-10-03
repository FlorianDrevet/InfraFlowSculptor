# 30 — Projet de référence

Ce projet est la **spécification par l'exemple** du lot 1 et son **test d'acceptation**
([EXG-16](27-exigences-non-fonctionnelles.md)). Il exerce les deux modes de composant, les liaisons entre
composants, le câblage implicite, l'accès aux données, les secrets de pipeline, un environnement protégé
et le préréglage « infra et code séparés ». Toute évolution d'IFS doit continuer à produire, pour ce
modèle, une sortie conforme à ce document.

Le **modèle** (sections 1 et 2) est unique. La **sortie** (section 3) est décrite pour la variante de
référence (Bicep + Azure DevOps), puis pour les autres combinaisons (section 3.5). Les résultats de la
section 2 sont identiques dans toutes les variantes : c'est la parité exigée par [EXG-19](27-exigences-non-fonctionnelles.md).

## 1. Modèle

### 1.1 Organisation et projet

| Élément | Valeur |
|---|---|
| Organisation | Contoso |
| Projet | Boutique en ligne, code `shop` |
| Gabarit par défaut | `{abbr}-{project}-{name}-{env}` |
| Gabarit `ResourceGroup` | `rg-{project}-{component}-{name}-{env}` |
| Tags du projet | `costCenter=ecommerce` |
| Langage, plateforme (variante de référence) | Bicep, Azure DevOps Pipelines |

### 1.2 Environnements et cibles

| Cible | Code | Ordre | Abonnement | Région | Protégée | Approbateurs |
|---|---|---|---|---|---|---|
| Développement | `dev` | 1 | Abonnement A | `francecentral` | Non | — |
| Production | `prd` | 2 | Abonnement B | `francecentral` | Oui | Groupe Azure DevOps « Shop Release Approvers » |
| Cible propre de `platform` | `shared` | — | Abonnement C | `francecentral` | Oui | Groupe « Shop Release Approvers » |

### 1.3 Composants

**`platform`** — mode `Single`. Groupe de ressources `main`.

| Ressource | Type | Réglages |
|---|---|---|
| `main` | ContainerRegistry | SKU `Standard` |

**`core`** — mode `PerEnvironment` (dev, prd). Groupe de ressources `main`. Espace de journaux par
défaut : `log main`.

| Ressource | Type | Réglages (surcharges entre parenthèses) |
|---|---|---|
| `main` | LogAnalyticsWorkspace | Rétention 30 j (prd : 90 j) |
| `main` | ApplicationInsights | Journalisation → `log main` |
| `main` | KeyVault | Protection contre la purge oui (dev : non) |
| `main` | AppConfiguration | SKU `Standard` (dev : `Free`) |

**`orders`** — mode `PerEnvironment` (dev, prd). Groupe de ressources `main`. Espace de journaux par
défaut : `core / log main`.

| Ressource | Type | Réglages et liaisons |
|---|---|---|
| `api` | UserAssignedIdentity | — |
| `main` | ContainerAppsEnvironment | Journalisation → `core / log main` |
| `api` | ContainerApp | Hébergement → `cae main` ; CPU/mémoire 0,5/1Gi (prd : 1/2Gi) ; réplicas 0–2 (prd : 2–10) ; tirage d'image → `platform / cr main` par `id api` ; télémétrie → `core / appi main` ; lecture de configuration → `core / appcs main` par `id api` ; identité par défaut `id api` |
| `orders` | SqlServer | Administrateur Entra : groupe `sg-shop-sql-admins` (identifiant par environnement) |
| `orders` | SqlDatabase | Hébergement → `sql orders` ; SKU `GP_S_Gen5_1` (prd : `GP_Gen5_2`) |
| `orders` | ServiceBusNamespace | File `order-created` |

Liaisons explicites supplémentaires de `ca api` (identité `id api`) :
- Accès → `sbns orders`, rôle Azure Service Bus Data Sender ;
- Accès aux données → `sqldb orders`, niveau `LectureÉcriture`.

### 1.4 Paramètres applicatifs

| Destination | Nom / clé | Source |
|---|---|---|
| `ca api` | `Sql__Server` | Sortie `fullyQualifiedDomainName` de `sql orders` |
| `ca api` | `Sql__Database` | Sortie `name` de `sqldb orders` |
| `ca api` | `ServiceBus__Namespace` | Sortie `fullyQualifiedNamespace` de `sbns orders` |
| `ca api` | `Payments__ApiKey` | Secret `payments-api-key` de `core / kv main`, alimenté par secret de pipeline (variable `MAIN_PAYMENTS_API_KEY`), identité `id api` |
| `core / appcs main` | `orders:maxItemsPerOrder` (sans label) | Littérale `50` (dev : `500`) |

### 1.5 Application

| Champ | Valeur |
|---|---|
| Application | `api` (Container App) |
| Code source | `src/api` du dépôt `shop-app` |
| Dockerfile | `src/api/Dockerfile` |
| Dépôt d'image | `shop/orders/api` |
| Registre de build | `platform / cr main` (composant `Single` : pas de promotion) |
| Étapes après build | `build/tests.yml` (fourni par le client) |
| Contrôle de santé | `/health` |

### 1.6 Plan de publication

Préréglage **Infra et code séparés**, connexion Azure DevOps (principal de service).

| Partie | Destination |
|---|---|
| Infrastructure de `core`, `platform`, `orders` | Dépôt `shop-infra`, racine |
| Applications de `orders` | Dépôt `shop-app`, racine |
| Code source de `orders` | Dépôt `shop-app` |

## 2. Résultats attendus du calcul

### 2.1 Noms Azure

| Ressource | `dev` | `prd` | `shared` |
|---|---|---|---|
| Groupe `platform / main` | — | — | `rg-shop-platform-main-shared` |
| `platform / cr main` | — | — | `crshopmainshared` (tirets retirés par assainissement) |
| Groupe `core / main` | `rg-shop-core-main-dev` | `rg-shop-core-main-prd` | — |
| `log main` | `log-shop-main-dev` | `log-shop-main-prd` | — |
| `appi main` | `appi-shop-main-dev` | `appi-shop-main-prd` | — |
| `kv main` | `kv-shop-main-dev` | `kv-shop-main-prd` | — |
| `appcs main` | `appcs-shop-main-dev` | `appcs-shop-main-prd` | — |
| Groupe `orders / main` | `rg-shop-orders-main-dev` | `rg-shop-orders-main-prd` | — |
| `id api` | `id-shop-api-dev` | `id-shop-api-prd` | — |
| `cae main` | `cae-shop-main-dev` | `cae-shop-main-prd` | — |
| `ca api` | `ca-shop-api-dev` | `ca-shop-api-prd` | — |
| `sql orders` | `sql-shop-orders-dev` | `sql-shop-orders-prd` | — |
| `sqldb orders` | `sqldb-shop-orders-dev` | `sqldb-shop-orders-prd` | — |
| `sbns orders` | `sbns-shop-orders-dev` | `sbns-shop-orders-prd` | — |

### 2.2 Ordre de déploiement

1. `core` (aucune dépendance)
2. `platform` (aucune dépendance ; après `core` par ordre de code)
3. `orders` (dépend de `core` et `platform`)

### 2.3 Éléments implicites (par environnement, pour `id-shop-api-<env>`)

| Élément | Origine |
|---|---|
| Identité `id api` attachée à `ca api` | Liaisons utilisant `id api` |
| `AcrPull` sur `crshopmainshared` (portée externe : abonnement C) | Tirage d'image |
| App Configuration Data Reader sur `appcs-shop-main-<env>` | Lecture de configuration |
| Key Vault Secrets User sur `kv-shop-main-<env>` | Paramètre `Payments__ApiKey` |
| Paramètre `APPLICATIONINSIGHTS_CONNECTION_STRING` | Télémétrie |
| Paramètre `AZURE_APPCONFIG_ENDPOINT` | Lecture de configuration |
| Paramètre `AZURE_CLIENT_ID` | Identité par défaut |
| Paramètres de diagnostic vers `log-shop-main-<env>` pour `appi`, `kv`, `appcs`, `cae`, `ca`, `sql`, `sqldb`, `sbns` | Espace par défaut de `core` et `orders` |

Le registre `crshopmainshared` n'a pas de paramètre de diagnostic : `platform` est `Single` et ne peut
pas se lier à un espace `PerEnvironment` ([RG-CMP-01](12-composants-et-groupes-de-ressources.md)).

### 2.4 Constats attendus

| Code | Gravité | Objet |
|---|---|---|
| `VAL-LIA-PORTEE-EXTERNE` | Info | `AcrPull` attribué dans l'abonnement C par le déploiement de `orders` |
| `VAL-LIA-SQL-ADMIN` | Avertissement | Tant que l'adhésion des identités de déploiement au groupe `sg-shop-sql-admins` n'est pas confirmée |

Aucune erreur.

### 2.5 Droits calculés pour les identités de déploiement

| Identité | Droits |
|---|---|
| `id-ifs-deploy-shop-dev`, `-prd` | Contributor (abonnement A, B) ; RBAC Administrator conditionné à : AcrPull, App Configuration Data Reader, Key Vault Secrets User, Azure Service Bus Data Sender ; RBAC Administrator conditionné à AcrPull sur `crshopmainshared` (abonnement C) ; App Configuration Data Owner sur `appcs-shop-main-<env>` ; Key Vault Secrets Officer sur `kv-shop-main-<env>` (écriture du secret de pipeline) ; membre de `sg-shop-sql-admins` (<env>) |
| `id-ifs-deploy-shop-shared` | Contributor (abonnement C) |

### 2.6 Secrets de pipeline

| Groupe de variables | Variable | Utilisée par |
|---|---|---|
| `ifs-shop-dev` | `MAIN_PAYMENTS_API_KEY` | `Payments__ApiKey` de `ca api` |
| `ifs-shop-prd` | `MAIN_PAYMENTS_API_KEY` | idem |

## 3. Sortie attendue

### 3.1 Dépôt `shop-infra` (variante de référence)

```
.ifs/
  manifest.json
  templates/…
  install/
    azure-setup.ps1
    install.pipeline.yml
    SETUP.md
README.ifs.md
core/infra/
  main.bicep
  main.dev.bicepparam
  main.prd.bicepparam
  types.bicep
  pipelines/pr.yml, ci.yml, release.yml
platform/infra/
  main.bicep
  main.shared.bicepparam
  types.bicep
  pipelines/pr.yml, ci.yml, release.yml
orders/infra/
  main.bicep
  main.dev.bicepparam
  main.prd.bicepparam
  types.bicep
  scripts/data-access.sql
  pipelines/pr.yml, ci.yml, release.yml
```

### 3.2 Dépôt `shop-app` (variante de référence)

```
.ifs/
  manifest.json
  templates/…
README.ifs.md
orders/apps/api/pipelines/pr.yml, ci.yml, release.yml
```

Le code source (`src/api`) et `build/tests.yml` appartiennent au client et ne figurent pas au manifeste.

### 3.3 Extrait indicatif de `orders/infra/main.dev.bicepparam`

```bicep
// Généré par InfraFlowSculptor — projet shop, révision 1. Ne pas modifier.
using 'main.bicep'

param target = {
  code: 'dev'
  subscriptionId: '<abonnement A>'
  location: 'francecentral'
}

param resourceGroups = {
  main: {
    name: 'rg-shop-orders-main-dev'
    location: 'francecentral'
    tags: { costCenter: 'ecommerce', 'ifs-project': 'shop', 'ifs-component': 'orders', 'ifs-environment': 'dev', 'managed-by': 'infraflowsculptor' }
  }
}

param caApi = {
  deploy: true
  name: 'ca-shop-api-dev'
  cpu: '0.5'
  memory: '1Gi'
  minReplicas: 0
  maxReplicas: 2
  ingress: 'external'
  targetPort: 8080
}
```

La forme exacte des types est fixée par l'implémentation ; les invariants sont ceux de
[21 § 5](21-generation-et-revisions.md) : noms en clair, toutes les valeurs effectives, aucune valeur
propre à l'environnement dans `main.bicep`.

### 3.4 Pipelines Azure DevOps créés par le pipeline d'installation

`shop · core · infra · PR/CI/Release`, `shop · platform · infra · PR/CI/Release`,
`shop · orders · infra · PR/CI/Release`, `shop · orders · api · PR/CI/Release`, `shop · installation`.
Environnements `shop-dev`, `shop-prd` (approbation), `shop-shared` (approbation).

### 3.5 Autres variantes

**Bicep + GitHub Actions (lot 1).** Dépôts `shop-infra` et `shop-app` sur GitHub. Différences avec la
variante de référence :

```
shop-infra/
  .github/workflows/
    ifs-shop-core-infra-pr.yml, …-ci.yml, …-release.yml
    ifs-shop-platform-infra-pr.yml, …
    ifs-shop-orders-infra-pr.yml, …
  .ifs/actions/…                     (actions composites partagées)
  .ifs/install/azure-setup.ps1, github-setup.ps1, SETUP.md
  core/infra/, platform/infra/, orders/infra/   (sans dossier pipelines/)
shop-app/
  .github/workflows/ifs-shop-orders-api-pr.yml, …-ci.yml, …-release.yml
  .ifs/actions/…
```

Environnements GitHub `shop-dev`, `shop-prd` (relecteurs requis), `shop-shared` (relecteurs requis) dans
chaque dépôt qui déploie. Secret `MAIN_PAYMENTS_API_KEY` attendu dans les environnements `shop-dev` et
`shop-prd` du dépôt `shop-infra`. Identifiants fédérés : `repo:contoso/shop-infra:environment:shop-<cible>`
et `repo:contoso/shop-app:environment:shop-<cible>`.

**Terraform (lot 2).** Le dossier `infra/` de chaque composant devient :

```
orders/infra/
  versions.tf, providers.tf, main.tf, variables.tf, outputs.tf, removed.tf
  targets/dev.tfvars, targets/prd.tfvars
  targets/dev.backend.hcl, targets/prd.backend.hcl
  scripts/data-access.sql
```

Le kit crée en plus un stockage d'état par cible (`stifsshopdev`, `stifsshopprd`, `stifsshopshared`). Le
registre de l'abonnement C est déclaré par une source de données sur un fournisseur avec alias.

**Pulumi TypeScript (lot 3).** Le dossier `infra/` de chaque composant devient :

```
orders/infra/
  Pulumi.yaml, Pulumi.dev.yaml, Pulumi.prd.yaml
  index.ts, config.ts, package.json, tsconfig.json
  scripts/data-access.sql
```

Le kit crée en plus, par cible, le stockage d'état et la clé Key Vault de chiffrement des secrets.

## 4. Critères d'acceptation

1. La validation ne produit que les constats de 2.4.
2. La génération produit exactement l'arborescence de 3.1 et 3.2 ; compilation et linter sans erreur ni
   avertissement ; deux générations successives sont identiques octet pour octet.
3. Sur une organisation et des abonnements de test, après exécution du kit d'installation et saisie de
   `MAIN_PAYMENTS_API_KEY` : les releases d'infrastructure de `core`, `platform` puis `orders` réussissent
   en dev, puis en prd après approbation.
4. La CI de `api` construit l'image d'une application témoin et la pousse dans `crshopmainshared` ; la
   release la déploie en dev puis en prd ; `/health` répond 2xx.
5. L'application témoin expose `/health/dependencies` qui vérifie, avec son identité, la lecture du
   secret, la lecture de la clé App Configuration, l'envoi d'un message dans `order-created` et une
   requête SQL en lecture et écriture. Tous les contrôles réussissent dans les deux environnements.
6. Une deuxième révision qui ajoute une file Service Bus produit une pull request dont le résumé ne cite
   que cet ajout ; une modification manuelle préalable de `orders/infra/main.bicep` est détectée et
   présentée en diff avant publication.
7. Les critères 1 à 6 sont vérifiés pour chaque variante livrée (section 3.5), et la comparaison des
   ressources déployées entre variantes ne montre aucun écart non toléré ([EXG-19](27-exigences-non-fonctionnelles.md)).
8. Une troisième révision qui **retire** la file ajoutée au critère 6 la détache dans toutes les variantes :
   la file existe toujours dans Azure, et n'est plus gérée par la pile ou par l'état ([DEC-46](03-decisions.md)).
