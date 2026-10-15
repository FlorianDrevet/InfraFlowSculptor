# NEXT — où en est l'implémentation

> **À lire en premier en arrivant, à mettre à jour en dernier en partant**, même en pleine étape.
> Ce fichier dit **où l'on est** et **ce que git ne sait pas** (Azure, Entra, Azure DevOps, tests manuels passés).
> Les étapes sont dans [`docs/plan/`](docs/plan/README.md) ; l'historique dans [`docs/plan/JOURNAL.md`](docs/plan/JOURNAL.md).
> Les lignes du tableau ci-dessous sont lues et écrites par `python tools/plan/gate.py` : ne pas renommer leurs libellés.

## En un coup d'œil

| | |
|---|---|
| **Étape courante** | [`P-08`](docs/plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement) — Exécution des preuves (accompagnement) |
| **Statut** | `EN_COURS` |
| **Dernière étape terminée** | [`P-07`](docs/plan/01-preuves.md#p-07--outillage-des-preuves-et-recette-pas-à-pas) — Outillage des preuves et recette pas à pas (commit `1d6f1be`) |
| **Étape suivante** | `R-03` — Revue des preuves |
| **Verrou** | Aucun — disponibilité France Central vérifiée le 2026-10-15 ; exécution de la recette restante |
| **Branche** | `impl/preuves` (empilée sur `impl/socle` jusqu’à sa fusion) |
| **Dernière mise à jour** | 2026-10-15 — Luna |
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
| Azure CLI | `2.90.0` | Oui (`2.90.0`, vérifié par `pwsh tools/dev/check-prereqs.ps1` le 2026-10-06) |
| Bicep CLI | `0.47.16` | Oui (`0.47.16`) |
| Git / GitHub CLI | présence | Oui (`2.52.0` / `2.88.1`) |

## État hors dépôt 📌

| Élément | État | Depuis |
|---|---|---|
| Abonnements Azure de test (preuves) | — (souscription CLI par défaut identique au snapshot du 2026-10-08 ; contrôles région, fournisseurs et SKU France Central passés le 2026-10-15 ; contrôle d'accès restant) | 2026-10-15 |
| Plafond de dépense et estimation actualisée des preuves ([verrou](docs/plan/recettes/01-preuves.md#0-verrou-de-coût-et-préflight-bloquants)) | Plafond utilisateur confirmé : 200 EUR/mois. Le sous-total France Central du pilote est 6,75–30,17 EUR, hors coûts annexes. Le 2026-10-15, l’API Usage affiche 70,8406 EUR au 14/10 et l’API Forecast projette 174,6759 EUR au 31/10 ; l’objet budget affiche 0,430301 EUR réel et 235,7656 EUR prévision. Ces vues divergent. En ajoutant 30,17 EUR, on obtient 204,85 EUR selon Usage/Forecast ou 265,94 EUR selon l’objet budget, sans les postes annexes. Seuil dépassé dans les projections ; arrêt avant toute création Azure. Alertes existantes : 80 % réel / 100 % prévision ; six budgets de groupes à 200 EUR/mois ; aucun budget modifié par Luna | 2026-10-15 |
| Ressources Azure créées pour les preuves ([inventaire](docs/plan/preuves/inventaires-azure.md)) | Aucune ressource Azure Resource Manager créée par Luna. Inventaire de référence capturé le 2026-10-15T12:29:09Z (133 ressources, 16 groupes, 0 pile, 28 attributions de rôle, 7 budgets). IDs conservés hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\baseline-20261015T1229Z.json` | 2026-10-15 |
| Organisation / projet Azure DevOps de test | Organisation `floriandrevet0332`, projet privé `shop42` et dépôt Git `shop42` créés ; branche `main` contient la référence publiée (commit `225cb57`). Le pipeline d’installation n’a pas été lancé, car le verrou de coût bloque les écritures Azure | 2026-10-15 |
| Groupes Entra (`sg-shop42-sql-admins`) et Azure DevOps (« Shop42 Release Approvers ») | Créés dans le tenant fourni ; Florian DREVET est membre des deux groupes | 2026-10-15 |
| Inscriptions Entra d'IFS (`dev`) | — | — |
| Protection de la branche `main` sur GitHub | À activer par vous dans *Settings → Branches* | 2026-10-06 |
| Secret de dépôt GitHub Actions `IFS_E2E_PASSWORD` | Configuré (valeur masquée) | 2026-10-06 |

## Tests manuels en attente de vous

- S-02 — [ouvrir `src/backend/InfraFlowSculptor.slnx` dans Rider](docs/plan/00-socle.md#s-02--squelette-backend-généré-depuis-le-template-cqrs) : vérifier les cinq projets et l'absence de références à `Web.Template.CQRS`.
- S-03 — ouvrir `http://localhost:5257/v1/inexistant` dans un navigateur et vérifier le JSON 404 avec `traceId` ([étape](docs/plan/00-socle.md#s-03--moderniser-le-squelette-évolutions-de-vole-papillon-damour)); l'API a été vérifiée par HTTP, mais Chrome a bloqué la navigation directe.
- S-04 — ouvrir la solution dans Rider, vérifier les cinq projets de test puis lancer *Run All* ([étape](docs/plan/00-socle.md#s-04--projets-de-tests-et-règles-darchitecture)); `dotnet test` passe, mais aucune fenêtre native n'est exposée à l'automatisation.
- S-17 — sur un clone neuf, suivre [README — Démarrer](README.md#démarrer-windows) jusqu'à « Bonjour Alice Martin ». Les prérequis passent ; le démarrage du clone reste à vérifier quand les ports fixes 4200, 8080, 5257 et 3000 seront libres, car l'AppHost de travail reste actif.
- P-08 — reprendre la [recette des preuves](docs/plan/recettes/01-preuves.md) après résolution du plafond : les prévisions Cost Management du 2026-10-15 dépassent le plafond de 200 EUR/mois et les vues de coût divergent. Le dépôt `shop42` et les groupes sont prêts ; aucun déploiement Azure n’a été effectué.
Chaque étape terminée ajoute ici son 🧪 ; vous consignez le résultat dans
[`docs/plan/recettes/suivi.md`](docs/plan/recettes/suivi.md) puis retirez la ligne.

## Questions pour Claude

Le préflight du 2026-10-08 a retourné une restriction de provisionnement pour SQL serverless `GP_S_Gen5_1` en North Europe ; aucune ressource n'a été déployée. Le 2026-10-15, l'utilisateur a fixé France Central (`francecentral`) et confirmé le plafond préexistant de 200 EUR/mois. Les contrôles région, huit fournisseurs/types, SKU, noms globaux et accès réussissent ; le clone `shop42` est préparé. L’API Cost Management affiche 70,8406 EUR réels au 14/10 et 174,6759 EUR projetés au 31/10, alors que l’objet budget affiche 0,430301 EUR réel et 235,7656 EUR projetés. Avec le haut du sous-total pilote (30,17 EUR), les projections atteignent 204,85 EUR selon Usage/Forecast ou 265,94 EUR selon le budget, hors coûts annexes. Ce conflit bloque toute création Azure ; aucune ressource Azure Resource Manager n’a été créée par Luna. Reprendre après réconciliation des vues et confirmation d’un plafond compatible.
