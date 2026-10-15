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
| Abonnements Azure de test (preuves) | Souscription `fc4fa56f-e4e7-41e9-8a0d-559fcd27b827`, tenant `c833b396-517c-465d-bcce-b79702eaf932` ; `francecentral` et les fournisseurs/SKU requis disponibles ; utilisateur connecté Owner au scope souscription et User Access Administrator au scope tenant ; même souscription pour dev/prd/shared | 2026-10-15 |
| Plafond de dépense et estimation actualisée des preuves ([verrou](docs/plan/recettes/01-preuves.md#0-verrou-de-coût-et-préflight-bloquants)) | L’utilisateur ne fixe **aucun plafond de dépense** (confirmation du 2026-10-15) et autorise la poursuite. Au snapshot du 2026-10-15T21:17:43Z, l’API Usage affiche 73,5482 EUR réels MTD ; l’API Forecast projette 176,6738 EUR au 31/10 (73,5482 EUR réels + 103,1256 EUR prévus). L’objet du budget préexistant `budget-sandbox-monthly` affiche 0,430301 EUR réel et 235,7656 EUR prévisionnels : les vues divergent et servent au suivi, sans seuil d’arrêt utilisateur. Le budget Azure de 200 EUR/mois reste une alerte différée (80 % réel / 100 % prévision) ; six budgets de groupes à 200 EUR/mois ; aucun budget modifié. ACA représente 42,6953 EUR MTD ; aucun crédit gratuit ACA n’est supposé. Estimation catalogue France Central 6,75–30,17 EUR hors stockage SQL/sauvegardes, Key Vault, stockage technique, trafic et taxes | 2026-10-15 |
| Ressources Azure créées pour les preuves ([inventaire](docs/plan/preuves/inventaires-azure.md)) | Aucun déploiement pilote ni pile Azure. Inventaire de référence du 2026-10-15T12:29:09Z : 133 ressources, 16 groupes, 0 pile, 28 attributions de rôle, 7 budgets. Avant release, le snapshot du 21:17:43Z compte 141 ressources, 24 groupes, 0 pile, 44 attributions, 7 budgets : le kit a ajouté 9 ressources (6 identités managées, 3 comptes de stockage), 8 groupes et 16 attributions de rôle ; une ressource `Microsoft.Resources/deploymentScripts` préexistante présente au snapshot précédent n’apparaît plus. Aucune écriture Azure pilote effectuée par Luna. Snapshots et IDs conservés hors dépôt sous `%LOCALAPPDATA%\InfraFlowSculptor\P08\` | 2026-10-15 |
| Organisation / projet Azure DevOps de test | Organisation `floriandrevet0332`, projet privé `shop42` et dépôt `shop42`. PR #47 (templates) et PR #48 (chemins du contrôle) fusionnées ; CI 569, PR 570–574 et CI main 575 réussis ; finalizer 578 réussi. Releases core 577, 579 et 580 échouent au contrôle `ExtendsCheck`. Le run 581 passe ce contrôle avec `repositoryName=shop42` et `templatePath=.ifs/templates/infra-release.yml`, puis a été annulé pendant le What-If dev ; le déploiement dev est resté ignoré. Correctif du finalizer en cours, aucun déploiement pilote encore effectué | 2026-10-15 |
| Groupes Entra (`sg-shop42-sql-admins`) et Azure DevOps (« Shop42 Release Approvers ») | Créés dans le tenant fourni ; Florian DREVET est membre des deux groupes | 2026-10-15 |
| Inscriptions Entra d'IFS (`dev`) | — | — |
| Protection de la branche `main` sur GitHub | À activer par vous dans *Settings → Branches* | 2026-10-06 |
| Secret de dépôt GitHub Actions `IFS_E2E_PASSWORD` | Configuré (valeur masquée) | 2026-10-06 |

## Tests manuels en attente de vous

- S-02 — [ouvrir `src/backend/InfraFlowSculptor.slnx` dans Rider](docs/plan/00-socle.md#s-02--squelette-backend-généré-depuis-le-template-cqrs) : vérifier les cinq projets et l'absence de références à `Web.Template.CQRS`.
- S-03 — ouvrir `http://localhost:5257/v1/inexistant` dans un navigateur et vérifier le JSON 404 avec `traceId` ([étape](docs/plan/00-socle.md#s-03--moderniser-le-squelette-évolutions-de-vole-papillon-damour)); l'API a été vérifiée par HTTP, mais Chrome a bloqué la navigation directe.
- S-04 — ouvrir la solution dans Rider, vérifier les cinq projets de test puis lancer *Run All* ([étape](docs/plan/00-socle.md#s-04--projets-de-tests-et-règles-darchitecture)); `dotnet test` passe, mais aucune fenêtre native n'est exposée à l'automatisation.
- S-17 — sur un clone neuf, suivre [README — Démarrer](README.md#démarrer-windows) jusqu'à « Bonjour Alice Martin ». Les prérequis passent ; le démarrage du clone reste à vérifier quand les ports fixes 4200, 8080, 5257 et 3000 seront libres, car l'AppHost de travail reste actif.
- P-08 — recette en cours : coût et inventaire actualisés, absence de plafond personnel confirmée ; préflight France Central prêt. Le contrôle `ExtendsCheck` est validé avec le format exact `shop42` + `.ifs/templates/...`. Publier le correctif du finalizer, réaligner les six connexions via le finalizer, puis reprendre P1 par la release core (dev puis prd).
Chaque étape terminée ajoute ici son 🧪 ; vous consignez le résultat dans
[`docs/plan/recettes/suivi.md`](docs/plan/recettes/suivi.md) puis retirez la ligne.

## Questions pour Claude

Aucun arbitrage de conception n'est en attente. L'utilisateur a choisi `francecentral` pour toutes les ressources régionales et confirmé qu'il ne fixe pas de plafond de dépense ; les coûts sont suivis sans seuil d'arrêt. Les vues Cost Management divergent et restent consignées. Le préflight confirme la région, les fournisseurs et SKU, les droits Owner/User Access Administrator, les paramètres de zone redondante désactivée et l'absence d'offre SQL gratuite déjà utilisée. Le contrôle ADO échouait car le finalizer inscrivait `projet/dépôt` et `/chemin` alors que l’évaluation attend `dépôt` et `chemin` relatifs. Le run 581 a confirmé le passage du contrôle et a été annulé avant toute écriture Azure ; aucune pile ou ressource pilote n'a encore été déployée.
