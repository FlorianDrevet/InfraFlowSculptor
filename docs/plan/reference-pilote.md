# Projet pilote de référence

> Décision : [DT-29](../technique/01-decisions.md#dt-29--projet-pilote-de-référence). Ce projet est le **test
> d'acceptation du jalon 0** et le support des preuves P1–P5, P7–P9 ([04 § 7.2](../specs/04-perimetre-et-lots.md)).
> C'est un sous-ensemble du projet de référence ([90](../specs/90-projet-de-reference.md)) limité au catalogue
> du pilote ([04 § 2.0](../specs/04-perimetre-et-lots.md)). Au jalon 1, le projet de référence complet le remplace
> comme test d'acceptation, et celui-ci reste comme test de non-régression.

## 1. Modèle

### 1.1 Organisation et projet

| Élément | Valeur |
|---|---|
| Organisation | Contoso |
| Projet | Boutique en ligne, code `shop` |
| Langage, plateforme | Bicep, Azure DevOps Pipelines |
| Source des modules | AVM registre public |
| Gabarit par défaut | `{abbr}-{project}-{name}-{env}` (préréglage Compact) |
| Gabarit `ResourceGroup` | `rg-{project}-{component}-{name}-{env}` |
| Tags du projet | `costCenter=ecommerce` ; tags système activés |
| Plan de publication | **Mono-dépôt** : dépôt Azure Repos `shop`, racine |

### 1.2 Cibles

| Cible | Code | Ordre | Abonnement | Région | Protégée | Approbateurs |
|---|---|---|---|---|---|---|
| Développement | `dev` | 1 | A | `northeurope` | Non | — |
| Production | `prd` | 2 | B | `northeurope` | Oui | Groupe Azure DevOps « Shop Release Approvers » |
| Cible propre de `platform` | `shared` | — | C | `northeurope` | Oui | « Shop Release Approvers » |

Pour les preuves, A, B et C sont **le même abonnement** ([DT-42](../technique/01-decisions.md#dt-42--coût-des-preuves-éphémères-azure)) :
les noms diffèrent par cible. Région : `northeurope` uniquement ; si région, SKU ou offre est indisponible, la recette
s'arrête ([recette § 0](recettes/01-preuves.md#0-verrou-de-coût-et-préflight-bloquants)), sans repli.

### 1.3 Composants

**`core`** — `PerEnvironment` (dev, prd). Groupe `main`. Espace de journaux par défaut : `log main`.

| Ressource | Type | Réglages |
|---|---|---|
| `main` | LogAnalyticsWorkspace | Rétention 30 j (prd : 90 j) |
| `main` | ApplicationInsights | Journalisation → `log main` |
| `main` | KeyVault | Protection contre la purge oui (dev : non) |

**`data`** — `PerEnvironment` (dev, prd). Groupe `main`. Espace de journaux par défaut : `core / log main`.

| Ressource | Type | Réglages |
|---|---|---|
| `orders` | SqlServer | Administrateur Entra : groupe `sg-shop-sql-admins` (identifiant par environnement) ; authentification `EntraSeule` ; exposition publique |
| `orders` | SqlDatabase | Hébergement → `sql orders` ; SKU `GP_S_Gen5_1` (prd : `GP_Gen5_2`) |

**`platform`** — `Single`, cible `shared`. Groupe `main`.

| Ressource | Type | Réglages |
|---|---|---|
| `main` | ContainerRegistry | SKU `Standard` |

**`orders`** — `PerEnvironment` (dev, prd). Groupe `main`. Espace de journaux par défaut : `core / log main`.

| Ressource | Type | Réglages et liaisons |
|---|---|---|
| `api` | UserAssignedIdentity | — |
| `main` | ContainerAppsEnvironment | Journalisation → `core / log main` |
| `api` | ContainerApp | Hébergement → `cae main` ; CPU/mémoire 0,5/1Gi (prd : 1/2Gi) ; réplicas 0–2 (prd : 1–3) ; ingress externe, port 8080 ; tirage d'image → `platform / cr main` par `id api` ; télémétrie → `core / appi main` ; identité système **activée** ; identité par défaut `id api` |

Liaisons explicites supplémentaires de `ca api` :
- **Accès** → `core / log main`, rôle Log Analytics Reader, identité `id api` (la liaison que P2 retire) ;
- **Accès aux données** → `data / sqldb orders`, niveau `LectureÉcriture`, identité **`Système`** (P9 : base
  dans un autre composant, identité système).

### 1.4 Paramètres applicatifs de `ca api`

| Nom | Source |
|---|---|
| `Sql__Server` | Sortie `fullyQualifiedDomainName` de `data / sql orders` (calculable depuis le nom) |
| `Sql__Database` | Sortie `name` de `data / sqldb orders` |
| `LogAnalytics__WorkspaceId` | Sortie `customerId` de `core / log main` |
| `Payments__ApiKey` | Secret `payments-api-key` de `core / kv main`, alimentation **secret de pipeline** (variable `MAIN_PAYMENTS_API_KEY`), identité `id api` |

### 1.5 Application

| Champ | Valeur |
|---|---|
| Application | `api` (Container App), livraison « Pipelines IFS » |
| Code source | `src/api` du dépôt `shop` (copie de [`samples/witness-app`](../../samples/witness-app)) |
| Dockerfile | `src/api/Dockerfile` |
| Dépôt d'image | `shop/orders/api` |
| Registre de build | `platform / cr main` (composant `Single` : pas de promotion) |
| Étapes | Cache des dépendances ; scan de l'image (Trivy, bloquant à `CRITICAL`) |
| Contrôle de santé | `/health` |
| Stratégie | Directe |

### 1.6 Profil de coût des preuves

> [DT-42](../technique/01-decisions.md#dt-42--coût-des-preuves-éphémères-azure). Valable **uniquement** pour les
> exécutions de la [recette](recettes/01-preuves.md) (P1–P9) ; le modèle des § 1.3–1.4 reste celui que les émetteurs
> du jalon 0 reproduisent. Le profil est appliqué à la publication dans le clone `shopNN`, dev **et** prd. Il ne change ni
> noms, ni liaisons, ni rôles, ni secrets, ni la rétention Log Analytics.

| Ressource | Modèle (§ 1.3) | Profil des preuves | Raison |
|---|---|---|---|
| `cr main` | `Standard` | `Basic` | Une image témoin et quelques tirages tiennent dans Basic ; si le préflight échoue, la recette s'arrête |
| `sqldb orders` SKU | dev `GP_S_Gen5_1`, prd `GP_Gen5_2` | `GP_S_Gen5_1` partout | Pas de calcul facturé en pause ; l'assertion SQL de P1/P9 ne dépend pas du SKU |
| `sqldb orders` capacité | — | `minCapacity` 0,5 ; au plus 1 vCore | Plancher du serverless |
| `sqldb orders` pause | — | `autoPauseDelay` 15 min (minimum GP) | Réduit le temps de calcul facturé |
| `sqldb orders` zone | AVM : `true` si omis | `zoneRedundant: false` **explicite** ; `databaseAvailabilityZone` reste `-1` | Surcharge de zone redondante inutile |
| `sqldb orders` offre gratuite | — | **prd seulement** : `useFreeLimit: true`, `freeLimitExhaustionBehavior: 'AutoPause'` si préflight OK ; dev reste serverless payant | Respecte la limite d'une base `useFreeLimit` du schéma AVM local |
| `cae main` | `zoneRedundant: true` | `false` | Aucune preuve ne teste la redondance de zone |
| `ca api` réplicas | dev 0–2, prd 1–3 | **0–1** partout | Mise à zéro ; un seul réplica suffit aux preuves |
| `appi main` rétention | 365 j | 90 j | Aucune preuve ne lit l'historique d'`appi` |
| `log main` rétention | dev 30 j, prd 90 j | **inchangée** | P3b : 90 → 120 → 90 |
| `log main` plafond quotidien | dev `-1` | `dailyQuotaGb` 1 (dev et prd) | Borne le coût d'ingestion (garde-fou, pas un budget) |
| `cpu`/`memory` de `ca api` | dev 0,5/1Gi, prd 1/2Gi | inchangés | Hors périmètre de la décision |

Conséquences à connaître : le premier appel après pause SQL ou après mise à zéro de `ca api` est lent ou échoue ;
la recette fournit des contrôles manuels bornés ([§ 0.4](recettes/01-preuves.md#04-réveil-sql-et-démarrage-à-froid)).
`useFreeLimit` n'est jamais appliqué avant confirmation par le préflight ([§ 0.3](recettes/01-preuves.md#03-préflight-bloquant-avant-toute-création)).

## 2. Résultats attendus du calcul

### 2.1 Noms Azure

| Ressource | `dev` | `prd` | `shared` |
|---|---|---|---|
| Groupe `core / main` | `rg-shop-core-main-dev` | `rg-shop-core-main-prd` | — |
| `log main` | `log-shop-main-dev` | `log-shop-main-prd` | — |
| `appi main` | `appi-shop-main-dev` | `appi-shop-main-prd` | — |
| `kv main` | `kv-shop-main-dev` | `kv-shop-main-prd` | — |
| Groupe `data / main` | `rg-shop-data-main-dev` | `rg-shop-data-main-prd` | — |
| `sql orders` | `sql-shop-orders-dev` | `sql-shop-orders-prd` | — |
| `sqldb orders` | `sqldb-shop-orders-dev` | `sqldb-shop-orders-prd` | — |
| Groupe `platform / main` | — | — | `rg-shop-platform-main-shared` |
| `cr main` | — | — | `crshopmainshared` |
| Groupe `orders / main` | `rg-shop-orders-main-dev` | `rg-shop-orders-main-prd` | — |
| `id api` | `id-shop-api-dev` | `id-shop-api-prd` | — |
| `cae main` | `cae-shop-main-dev` | `cae-shop-main-prd` | — |
| `ca api` | `ca-shop-api-dev` | `ca-shop-api-prd` | — |

Les noms globaux (`kv-shop-main-*`, `sql-shop-orders-*`, `crshopmainshared`) peuvent être déjà pris dans
Azure. Pour les preuves, le code projet peut devenir `shopNN` (deux chiffres de votre choix, consignés dans
`NEXT.md`) : tous les noms suivent.

### 2.2 Ordre de déploiement

1. `core` (aucune dépendance)
2. `data` (dépend de `core` : paramètres de diagnostic vers `log main`, dépendance de création selon [DT-39](../technique/01-decisions.md#dt-39--journalisation-et-diagnostics-sont-des-dépendances-de-création))
3. `platform` (aucune dépendance ; après `data` par ordre de code)
4. `orders` (dépend de `core`, `data` — accès aux données —, `platform`)

### 2.3 Éléments implicites (par environnement)

| Élément | Origine |
|---|---|
| `id api` attachée à `ca api` | Liaisons utilisant `id api` |
| `AcrPull` pour `id-shop-api-<env>` sur `crshopmainshared` (portée externe : abonnement C) | Tirage d'image |
| Key Vault Secrets User pour `id-shop-api-<env>` sur `kv-shop-main-<env>` | Paramètre `Payments__ApiKey` |
| Log Analytics Reader pour `id-shop-api-<env>` sur `log-shop-main-<env>` | Liaison d'accès explicite |
| Paramètres `APPLICATIONINSIGHTS_CONNECTION_STRING`, `AZURE_CLIENT_ID` | Télémétrie, identité par défaut |
| Paramètres de diagnostic vers `log-shop-main-<env>` pour `appi`, `kv`, `sql`, `sqldb`, `cae`, `ca` | Espaces par défaut de `core`, `data`, `orders` |
| Utilisateur SQL de l'identité **système** de `ca-shop-api-<env>` dans `sqldb-shop-orders-<env>`, rôles `db_datareader`, `db_datawriter` | Accès aux données (script de la release d'`orders`) |

### 2.4 Constats attendus

| Code | Gravité | Objet |
|---|---|---|
| `VAL-LIA-PORTEE-EXTERNE` | Info | `AcrPull` attribué dans l'abonnement C par le déploiement d'`orders` |
| `VAL-LIA-SQL-ADMIN` | Avertissement | Tant que l'adhésion des identités de déploiement à `sg-shop-sql-admins` n'est pas confirmée |
| `VAL-NOM-DISPONIBILITE` | Info | Pour chaque nom global déjà résolu dans le DNS public, le cas échéant |

Aucune erreur.

### 2.5 Secrets de pipeline

| Groupe de variables | Variable | Utilisée par |
|---|---|---|
| `ifs-shop-dev` | `MAIN_PAYMENTS_API_KEY` | `Payments__ApiKey` de `ca api` |
| `ifs-shop-prd` | `MAIN_PAYMENTS_API_KEY` | idem |

## 3. Sortie attendue (dépôt `shop`)

```
.ifs/
  install/
    azure-setup.ps1
    install.pipeline.yml
    SETUP.md
  templates/
    infra-pr.yml
    infra-ci.yml
    infra-release.yml
    infra-target-stages.yml
    app-pr.yml
    app-ci.yml
    app-release.yml
    app-target-stage.yml
    scripts/
      IfsRelease.psm1
      Invoke-IfsInfraPreview.ps1
      Invoke-IfsInfraDeploy.ps1
      Invoke-IfsAppDeploy.ps1
README.ifs.md
core/infra/
  main.bicep
  types.bicep
  main.dev.bicepparam
  main.prd.bicepparam
  release.dev.json
  release.prd.json
  pipelines/pr.yml, ci.yml, release.yml
data/infra/
  main.bicep, types.bicep, main.dev.bicepparam, main.prd.bicepparam, release.dev.json, release.prd.json
  pipelines/pr.yml, ci.yml, release.yml
platform/infra/
  main.bicep, types.bicep, main.shared.bicepparam, release.shared.json
  pipelines/pr.yml, ci.yml, release.yml
orders/infra/
  main.bicep, types.bicep, main.dev.bicepparam, main.prd.bicepparam, release.dev.json, release.prd.json
  scripts/data-access.sql
  pipelines/pr.yml, ci.yml, release.yml
orders/apps/api/pipelines/pr.yml, ci.yml, release.yml
```

Le manifeste `.ifs/manifest.json` est écrit par la publication, pas par la génération : il figure dans la
sortie de référence sous `reference/pilot/manifest.example.json` (hors du dossier comparé) pour la recette, avec une
empreinte par fichier. Le code de l'application (`src/api`) appartient au client.

## 4. Critères d'acceptation du jalon 0

Ceux de [90 § 4](../specs/90-projet-de-reference.md) qui s'appliquent au catalogue du pilote, transposés :

1. La validation ne produit que les constats de 2.4.
2. La génération produit exactement l'arborescence de 3 ; contrôles sans erreur ni avertissement ; deux
   générations successives sont identiques octet pour octet ; le contenu est **identique** à
   `reference/pilot/bicep-azdo/`.
3. Après le kit d'installation et la saisie de `MAIN_PAYMENTS_API_KEY` : releases de `core`, `data`,
   `platform`, `orders` réussies en dev, puis en prd après approbation, **depuis des abonnements vides, une
   exécution par composant** (P1).
4. La CI de `api` construit l'image témoin, la pousse dans `crshopmainshared`, la release la déploie en dev
   puis en prd ; `/health` répond 2xx ; l'image est référencée par son empreinte.
5. Après exécution de `witness-schema.sql` par un administrateur SQL, `/health/dependencies` réussit dans les deux
   environnements : secret lu, requête Log Analytics, lecture et écriture SQL avec les seuls rôles
   `db_datareader`/`db_datawriter`, et création de table **refusée** (`sql-least-privilege`).
6. Une révision 2 qui change la rétention prd de `log main` (90 → 120 jours) produit une pull request qui ne
   modifie que `core/infra/main.prd.bicepparam` ; une modification manuelle préalable de
   `orders/infra/main.bicep` est détectée et présentée en diff avant publication.
7. Le suivi affiche « révision 1 déployée » pour les quatre composants dans toutes leurs cibles ; la liste de
   contrôle est entièrement cochée.
8. **Révocation (P2)** : la révision qui retire la liaison d'accès de `ca api` vers `log main`, déployée en prd
   protégée par `denyDelete`, supprime l'attribution ; le rapport la cite ; `/health/dependencies` constate
   l'échec de la requête Log Analytics après propagation ; l'espace et ses données restent ; une tentative de
   suppression de `log-shop-main-prd` par un propriétaire de l'abonnement est refusée.
9. **Aperçu et approbation (P3)** : l'aperçu prd est lisible dans le run avant approbation, puis deux cas, chacun sur
   une release en attente d'approbation : (a) une **révision plus récente qui change un rôle d'`orders`** est fusionnée
   pendant l'attente → après approbation, la release s'arrête à l'étape 5 sans rien modifier, et la release déclenchée
   par la fusion demande sa propre approbation ; (b) **Azure change** pendant l'attente (rétention de `log-shop-main-prd`
   modifiée à la main dans le portail) → après approbation, l'empreinte recalculée sur l'artefact figé diffère : arrêt
   sans rien modifier.
10. **Cible en retard (P4)** : révision 3 ajoute l'identité `id extra` dans `orders` et est **déployée en dev et en
    prd** (constater `id-shop-extra-prd` gérée par la pile `ifs-shop-orders-prd`) ; révision 4 la retire et n'est
    déployée **qu'en dev** (approbation prd refusée) ; révision 5 (autre changement) déployée en dev et prd : prd
    saute la révision 4, `id-shop-extra-prd` est retirée de la pile prd (toujours présente dans Azure) et figure à
    l'inventaire des ressources détachées de prd.
11. **Échec partiel (P5)** : `MAIN_PAYMENTS_API_KEY` vidée après l'approbation de `core` en prd : « partiellement
    appliquée » avec l'étape en échec ; après saisie, la relance termine sans doublon.
12. **Interruption brutale (P7)** : l'agent est interrompu après la mise à jour de la pile et avant les
    révocations (variable de test `IFS_TEST_ABORT_AFTER=deploy-unit`) ; la release suivante — même pour une
    révision plus récente — reprend la révocation du journal en premier.
13. **Livraison pendant une release (P8)** : une image livrée en prd pendant l'attente d'approbation de la
    release d'infrastructure d'`orders` est conservée ; l'empreinte approuvée égale l'empreinte appliquée.
14. **Accès aux données entre composants (P9)** : le premier déploiement de `data` puis `orders` réussit ; le
    journal d'`orders` contient l'opération d'accès aux données, celui de `data` aucune.
