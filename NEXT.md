# NEXT — où en est l'implémentation

> **À lire en premier en arrivant, à mettre à jour en dernier en partant**, même en pleine étape.
> Ce fichier dit **où l'on est** et **ce que git ne sait pas** (Azure, Entra, Azure DevOps, tests manuels passés).
> Les étapes sont dans [`docs/plan/`](docs/plan/README.md) ; l'historique dans [`docs/plan/JOURNAL.md`](docs/plan/JOURNAL.md).
> Les lignes du tableau ci-dessous sont lues et écrites par `python tools/plan/gate.py` : ne pas renommer leurs libellés.

## En un coup d'œil

| | |
|---|---|
| **Étape courante** | [`S-01`](docs/plan/00-socle.md#s-01--prérequis-de-la-machine-et-garde-fous-du-plan) — Prérequis de la machine et garde-fous du plan |
| **Statut** | `A_FAIRE` |
| **Dernière étape terminée** | — |
| **Étape suivante** | `S-02` — Squelette backend généré depuis le template CQRS |
| **Verrou** | aucun |
| **Branche** | `impl/socle` (à créer depuis `origin/main` à S-01) |
| **Dernière mise à jour** | 2026-10-03 — Claude (plan initial) |

Statuts : `A_FAIRE` · `EN_COURS` · `EN_ATTENTE_DE_REVUE` (🔒 Luna s'arrête) · `CORRECTIONS_DEMANDEES` · `BLOQUE` (question ci-dessous).

## Décisions à confirmer avant leur étape

Luna ne commence pas l'étape tant que la colonne « Confirmée » ne vaut pas **Oui** (sinon : statut `BLOQUE`).

| Décision | Choix proposé | Avant l'étape | Confirmée |
|---|---|---|---|
| [DT-22](docs/technique/01-decisions.md#dt-22--mediatr-et-sa-licence) | MediatR 14 avec clé de licence (Community si CA < 5 M$) | S-03 | Non |
| [DT-21](docs/technique/01-decisions.md#dt-21--outillage-de-test) | AwesomeAssertions au lieu de FluentAssertions 8 (licence) | S-04 | Non |
| [DT-04](docs/technique/01-decisions.md#dt-04--postgresql-17) | PostgreSQL 17 au lieu de SQL Server | S-06 | Non |
| [DT-07](docs/technique/01-decisions.md#dt-07--authentification--oidc-entra-en-azure-keycloak-en-local) | OIDC générique : Entra en Azure, Keycloak en local | S-07 | Non |
| [DT-08](docs/technique/01-decisions.md#dt-08--worker--service-net-et-service-bus-pas-azure-functions) | Worker .NET + Service Bus à sessions, pas Azure Functions | S-08 | Non |
| [DT-30](docs/technique/01-decisions.md#dt-30--thème-clair) | Thème sombre seul tant que Strata n'a pas de thème clair | J3-08 | Non |

## Machine

| Outil | Version attendue | Posé ? |
|---|---|---|
| SDK .NET | `10.0.203` | Oui (constaté le 2026-10-03) |
| Node | `24.15.0` | Oui (constaté le 2026-10-03) |
| Docker | — | Oui (`29.4.3`) |
| CLI Aspire | `13.5.3` | Non |
| PowerShell 7, Azure CLI, Bicep, gh | voir `tools/versions.json` (S-01) | À vérifier |

## État hors dépôt 📌

| Élément | État | Depuis |
|---|---|---|
| Abonnements Azure de test (preuves) | — | — |
| Organisation / projet Azure DevOps de test | — | — |
| Groupes Entra (`sg-shop-sql-admins`) et Azure DevOps (« Shop Release Approvers ») | — | — |
| Inscriptions Entra d'IFS (`dev`) | — | — |
| Protection de la branche `main` sur GitHub | — | — |

## Tests manuels en attente de vous

Aucun. (Chaque étape terminée ajoute ici son 🧪 ; vous consignez le résultat dans
[`docs/plan/recettes/suivi.md`](docs/plan/recettes/suivi.md) puis retirez la ligne.)

## Questions pour Claude

Aucune.
