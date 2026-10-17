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
| **Verrou** | P-08 : arbitrage rendu (option B) ; reste le parcours positif P8 (nouvel aperçu infra après D2) |
| **Branche** | `impl/preuves` (empilée sur `impl/socle` jusqu’à sa fusion) |
| **Dernière mise à jour** | 2026-10-17 — Luna |
Statuts : `A_FAIRE` · `EN_COURS` · `EN_ATTENTE_DE_REVUE` (🔒 Luna s'arrête) · `EN_ATTENTE_DE_RECETTE` (Luna attend vos
résultats) · `CORRECTIONS_DEMANDEES` · `BLOQUE` (question d'arbitrage en attente).

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
| Abonnements Azure de test (preuves) | Souscription `fc4fa56f-e4e7-41e9-8a0d-559fcd27b827`, tenant `c833b396-517c-465d-bcce-b79702eaf932` ; `francecentral` et les fournisseurs/SKU requis disponibles ; utilisateur connecté Owner au scope souscription et User Access Administrator au scope tenant ; même souscription pour dev/prd/shared | 2026-10-15 |
| Plafond de dépense et estimation actualisée des preuves ([verrou](docs/plan/recettes/01-preuves.md#0-verrou-de-coût-et-préflight-bloquants)) | L’utilisateur ne fixe **aucun plafond de dépense** et autorise la poursuite. Au contrôle Cost Management du 2026-10-16T22:37Z : 80,945803 EUR réels MTD ; 106,655714 EUR prévus du 17 au 31/10, soit 187,601517 EUR projetés pour octobre. Le budget préexistant de 200 EUR/mois et les six budgets de groupes restent inchangés ; aucun budget n’a été créé ou modifié. Les valeurs sont suivies sans seuil d’arrêt utilisateur | 2026-10-16 |
| Ressources Azure créées pour les preuves ([inventaire](docs/plan/preuves/inventaires-azure.md)) | Jeu Shop42 déployé en France Central. Baseline du 2026-10-15 : 133 ressources, 16 groupes, 28 rôles et 7 budgets. Après les runs #739/#738 : 163 ressources, 26 groupes, 7 piles, 57 rôles et 7 budgets ; aucun ID ajouté ou retiré depuis le snapshot après API #737. D2 reste saine en DEV/PRD. P8 est KO et le nettoyage est suspendu. IDs exacts hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\after-infra-738-20261016T223549Z\inventory.json` | 2026-10-16 |
| Organisation / projet Azure DevOps de test | Organisation `floriandrevet0332`, projet privé `shop42` et dépôt `shop42`. CI API #733 (D2) et release applicative #739 réussis en DEV/PRD. Le run infra #738 a été approuvé après D2, puis bloqué en PRD avec `FailedBeforeMutation` car les effets avaient changé depuis l’aperçu approuvé. Captures anonymisées conservées dans `Recordings/` ; décisions Entra et groupes restent en place | 2026-10-16 |
| Groupes Entra (`sg-shop42-sql-admins`) et Azure DevOps (« Shop42 Release Approvers ») | Créés dans le tenant fourni ; Florian DREVET est membre des deux groupes | 2026-10-15 |
| Inscriptions Entra d'IFS (`dev`) | — | — |
| Protection de la branche `main` sur GitHub | À activer par vous dans *Settings → Branches* | 2026-10-06 |
| Secret de dépôt GitHub Actions `IFS_E2E_PASSWORD` | Configuré (valeur masquée) | 2026-10-06 |

## Tests manuels en attente de vous

- S-02 — [ouvrir `src/backend/InfraFlowSculptor.slnx` dans Rider](docs/plan/00-socle.md#s-02--squelette-backend-généré-depuis-le-template-cqrs) : vérifier les cinq projets et l'absence de références à `Web.Template.CQRS`.
- S-03 — ouvrir `http://localhost:5257/v1/inexistant` dans un navigateur et vérifier le JSON 404 avec `traceId` ([étape](docs/plan/00-socle.md#s-03--moderniser-le-squelette-évolutions-de-vole-papillon-damour)); l'API a été vérifiée par HTTP, mais Chrome a bloqué la navigation directe.
- S-04 — ouvrir la solution dans Rider, vérifier les cinq projets de test puis lancer *Run All* ([étape](docs/plan/00-socle.md#s-04--projets-de-tests-et-règles-darchitecture)); `dotnet test` passe, mais aucune fenêtre native n'est exposée à l'automatisation.
- S-17 — sur un clone neuf, suivre [README — Démarrer](README.md#démarrer-windows) jusqu'à « Bonjour Alice Martin ». Les prérequis passent ; le démarrage du clone reste à vérifier quand les ports fixes 4200, 8080, 5257 et 3000 seront libres, car l'AppHost de travail reste actif.
- P-08 — P1, P9, P3a et P3b sont OK et consignées. P8 reste KO : #738 est conservé comme preuve négative `FailedBeforeMutation` après l’intercalation de D2 par #739 ; ne pas le rejouer. Le run #740 (infra PRD après D2) est fonctionnellement réussi (D2 conservée, `/health` = 200) mais **incomplet comme preuve** : son rapport ne porte pas `approvedPreviewFingerprint` ([DT-43](docs/technique/01-decisions.md#dt-43--empreinte-dapercu-approuvée-liée-à-lempreinte-appliquée)). Requis : d’abord le changement contrat/test (étape 0 bis de P-08, test d’abord : nouveau schéma `ifs-report.v2.schema.json` ajouté, schémas v1 et captures existantes inchangés, `Write-IfsReleaseReport` dans `IfsRelease.psm1` émet v2, test dans `reference/release-module/tests/IfsRelease.Tests.ps1` ; le contrôle d’empreinte existant et son message sont conservés ; ni chargeur ni migration), puis un **nouveau** run infra PRD après #739, sans release applicative entre l’aperçu et l’approbation ; vérifier D2 et `approvedPreviewFingerprint` = `preview.fingerprint`. Ne pas rejouer #738 ni réapprouver #740. Consigner le résultat ; le nettoyage Shop42 reste suspendu jusque-là.
Chaque étape terminée ajoute ici son 🧪 ; vous consignez le résultat dans
[`docs/plan/recettes/suivi.md`](docs/plan/recettes/suivi.md) puis retirez la ligne.

## Questions pour Claude

Aucune question en attente.

**Décision P-08 (Claude, 2026-10-17) — option B.** Le refus fail-closed est conservé (aucun changement de code). La recette P8 ([§ 5](docs/plan/recettes/01-preuves.md#5-p8--livraison-applicative-pendant-lattente-dinfrastructure)) est révisée : #738 est le cas négatif conservé (`FailedBeforeMutation` attendu), ni forcé ni rejoué. Aucune nouvelle preuve n’a été exécutée.

Prochaines actions :
1. Luna : exécuter l’accompagnement du parcours positif P8 — nouvelle release infra orders PRD à partir de D2 (#739), sans release applicative entre l’aperçu et l’approbation, puis vérifier D2 conservée et empreinte appliquée = approuvée ; capturer `p8-infra-preview-d2` et `p8-infra-result-d2`.
2. Vous : consigner P8 dans `resultats.md` après cette preuve ; P8 reste KO d’ici là.
3. Nettoyage Shop42 suspendu jusqu’à la réussite de P8 et des autres conditions de la procédure T.
