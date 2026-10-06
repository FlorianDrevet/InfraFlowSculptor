# NEXT — où en est l'implémentation

> **À lire en premier en arrivant, à mettre à jour en dernier en partant**, même en pleine étape.
> Ce fichier dit **où l'on est** et **ce que git ne sait pas** (Azure, Entra, Azure DevOps, tests manuels passés).
> Les étapes sont dans [`docs/plan/`](docs/plan/README.md) ; l'historique dans [`docs/plan/JOURNAL.md`](docs/plan/JOURNAL.md).
> Les lignes du tableau ci-dessous sont lues et écrites par `python tools/plan/gate.py` : ne pas renommer leurs libellés.

## En un coup d'œil

| | |
|---|---|
| **Étape courante** | [`S-12`](docs/plan/00-socle.md#s-12--composants-produit-strata-et-motifs) — Composants produit Strata et motifs |
| **Statut** | `A_FAIRE` |
| **Dernière étape terminée** | [`S-11`](docs/plan/00-socle.md#s-11--composants-strata-de-base) — Composants Strata de base (commit `4879ded`) |
| **Étape suivante** | `S-13` — Coquille de l'application, connexion et Playwright |
| **Verrou** | aucun |
| **Branche** | `impl/socle` (créée depuis `origin/main` à S-01) |
| **Dernière mise à jour** | 2026-10-06 — Luna |
Statuts : `A_FAIRE` · `EN_COURS` · `EN_ATTENTE_DE_REVUE` (🔒 Luna s'arrête) · `EN_ATTENTE_DE_RECETTE` (Luna attend vos
résultats) · `CORRECTIONS_DEMANDEES` · `BLOQUE` (question ci-dessous).

## Décisions à confirmer avant leur étape

Toutes confirmées le 2026-10-04. Règle pour les suivantes : Luna ne commence pas l'étape tant que la colonne « Confirmée » ne vaut pas **Oui** (sinon : statut `BLOQUE`).

| Décision | Choix proposé | Avant l'étape | Confirmée |
|---|---|---|---|
| [DT-22](docs/technique/01-decisions.md#dt-22--mediator-à-la-place-de-mediatr) | `Mediator` (MIT, génération de source) à la place de MediatR | S-03 | Oui (2026-10-04) |
| [DT-21](docs/technique/01-decisions.md#dt-21--outillage-de-test) | AwesomeAssertions au lieu de FluentAssertions 8 (licence) | S-04 | Oui (2026-10-04) |
| [DT-04](docs/technique/01-decisions.md#dt-04--postgresql-17) | PostgreSQL 17 au lieu de SQL Server | S-06 | Oui (2026-10-04) |
| [DT-07](docs/technique/01-decisions.md#dt-07--authentification--oidc-entra-en-azure-keycloak-en-local) | OIDC générique : Entra en Azure, Keycloak en local ([guide](docs/technique/09-keycloak.md)) | S-07 | Oui (2026-10-04) |
| [DT-08](docs/technique/01-decisions.md#dt-08--worker--service-net-et-service-bus-pas-azure-functions) | Worker .NET + Service Bus à sessions, pas Azure Functions | S-08 | Oui (2026-10-04) |
| [DT-30](docs/technique/01-decisions.md#dt-30--thème-sombre-seul-changement-de-thème-prêt) | Thème sombre seul ; sélecteur de thème prêt et caché tant qu'il n'y a qu'un thème | S-10 | Oui (2026-10-04) |

## Machine

| Outil | Version attendue | Posé ? |
|---|---|---|
| SDK .NET | `10.0.203` | Oui (`10.0.301`) |
| Node | `24.15.0` | Oui (`24.19.0`) |
| Docker | `27.0.0` | Oui (`29.5.3`, daemon répond) |
| CLI Aspire | `13.5.3` | Oui (`13.6.0`) |
| Python | `3.11` | Oui (`3.14.2`) |
| PowerShell 7 | `7.5.0` | Oui (`7.6.6`) |
| Azure CLI | `2.90.0` | Non (`2.88.0`; mise à jour MSI interrompue, version inchangée) |
| Bicep CLI | `0.47.16` | Oui (`0.47.16`) |
| Git / GitHub CLI | présence | Oui (`2.52.0` / `2.88.1`) |

## État hors dépôt 📌

| Élément | État | Depuis |
|---|---|---|
| Abonnements Azure de test (preuves) | — | — |
| Organisation / projet Azure DevOps de test | — | — |
| Groupes Entra (`sg-shop-sql-admins`) et Azure DevOps (« Shop Release Approvers ») | — | — |
| Inscriptions Entra d'IFS (`dev`) | — | — |
| Protection de la branche `main` sur GitHub | — | — |

## Tests manuels en attente de vous

- S-11 — comparer les neuf sections de `/dev/design-system` aux fichiers `docs/design/strata/rendered/*.html`; Chrome bloque ces références locales en automatisation (`file://`). Les interactions clavier, le focus et les dimensions des boutons ont été vérifiés.
- S-02 — [ouvrir `src/backend/InfraFlowSculptor.slnx` dans Rider](docs/plan/00-socle.md#s-02--squelette-backend-généré-depuis-le-template-cqrs) : vérifier les cinq projets et l'absence de références à `Web.Template.CQRS`.
- S-03 — ouvrir `http://localhost:5257/v1/inexistant` dans un navigateur et vérifier le JSON 404 avec `traceId` ([étape](docs/plan/00-socle.md#s-03--moderniser-le-squelette-évolutions-de-vole-papillon-damour)); l'API a été vérifiée par HTTP, mais Chrome a bloqué la navigation directe.
- S-04 — ouvrir la solution dans Rider, vérifier les cinq projets de test puis lancer *Run All* ([étape](docs/plan/00-socle.md#s-04--projets-de-tests-et-règles-darchitecture)); `dotnet test` passe, mais aucune fenêtre native n'est exposée à l'automatisation.
Chaque étape terminée ajoute ici son 🧪 ; vous consignez le résultat dans
[`docs/plan/recettes/suivi.md`](docs/plan/recettes/suivi.md) puis retirez la ligne.

## Questions pour Claude

Aucune.
