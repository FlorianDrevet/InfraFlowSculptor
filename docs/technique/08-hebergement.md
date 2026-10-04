# 08 — Hébergement d'IFS dans Azure

> Détaillé à l'étape [J0-30](../plan/02-jalon-0-pilote.md) ; ce document fixe la cible.

## 1. Environnements d'IFS

| Environnement | Usage | Abonnement | Données |
|---|---|---|---|
| `dev` | Recette continue de l'équipe IFS | Abonnement « IFS dev » | Factices |
| `pilot` | Équipes pilotes du jalon 0 | Abonnement « IFS prod » (groupe séparé) | Réelles (pilote) |
| `prod` | Ouverture commerciale (après jalon 3) | Abonnement « IFS prod » | Réelles |

Région : `francecentral` ([DT-27](01-decisions.md#dt-27--hébergement-dans-azure-france-central)).

## 2. Ressources

| Ressource | SKU de départ (`dev` / `pilot`) | Remarque |
|---|---|---|
| Container Apps Environment (profil Consumption) | — | Journaux vers Log Analytics |
| Container App `api` | 0,5 vCPU / 1 Gi, 1–3 réplicas | Ingress externe derrière le domaine `api.<domaine>` |
| Container App `web` | 0,25 vCPU / 0,5 Gi, 1–2 réplicas | nginx, `config.json` régénéré au démarrage |
| Container App `worker` | 1 vCPU / 2 Gi, 1–3 réplicas | Image avec Bicep CLI, PowerShell, PSScriptAnalyzer ; sans ingress |
| Container Apps Job `migrate` | 0,5 vCPU | Bundle EF exécuté avant chaque déploiement de l'API |
| PostgreSQL serveur flexible 17 | `Standard_B2ms` / `Standard_D2ds_v5`, sauvegarde géo-redondante (prod) | Authentification Entra seule ; PITR 35 jours ([EXG-10](../specs/27-exigences-non-fonctionnelles.md)) |
| Service Bus | Standard | Files à sessions ([DT-32](01-decisions.md#dt-32--files-et-équité)) |
| Stockage | Standard_ZRS, versioning, suppression réversible | Révisions, exports, rapports |
| Key Vault | standard, RBAC, protection contre la purge | Jetons git de repli, clé de l'application GitHub, clé MediatR |
| Azure Managed Redis | Balanced_B0 | Cache, limites |
| Application Insights + Log Analytics | — | Alertes : échec de contrôle de sortie ([EXG-11](../specs/27-exigences-non-fonctionnelles.md)) |
| ACS + Email | — | Domaine d'envoi vérifié |
| Registre de conteneurs | Standard | Images `api`, `worker`, `web` |
| Identités managées | une par Container App | Aucun secret de connexion |

## 3. Livraison d'IFS

- `infra/` : Bicep avec AVM (IFS utilise ce qu'il génère), `infra/main.bicep` + `infra/parameters/<env>.bicepparam`.
- `.github/workflows/deploy.yml` : build et poussée des images, `what-if` puis déploiement de l'infrastructure,
  job de migration, mise à jour des Container Apps ; fédération OIDC GitHub → Azure ; approbation manuelle
  pour `pilot` et `prod` (environnements GitHub).
- Aucune opération Azure n'est faite par Luna : Luna écrit et valide (`az bicep build`, `what-if` en lecture si
  l'utilisateur fournit l'accès) ; l'utilisateur déclenche les déploiements.
