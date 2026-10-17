# NEXT — où en est l'implémentation

> **À lire en premier en arrivant, à mettre à jour en dernier en partant**, même en pleine étape.
> Ce fichier dit **où l'on est** et **ce que git ne sait pas** (Azure, Entra, Azure DevOps, tests manuels passés).
> Les étapes sont dans [`docs/plan/`](docs/plan/README.md) ; l'historique dans [`docs/plan/JOURNAL.md`](docs/plan/JOURNAL.md).
> Les lignes du tableau ci-dessous sont lues et écrites par `python tools/plan/gate.py` : ne pas renommer leurs libellés.

## En un coup d'œil

| | |
|---|---|
| **Étape courante** | [`P-08`](docs/plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement) — Exécution des preuves (accompagnement) |
| **Statut** | `BLOQUE` |
| **Dernière étape terminée** | [`P-07`](docs/plan/01-preuves.md#p-07--outillage-des-preuves-et-recette-pas-à-pas) — Outillage des preuves et recette pas à pas (commit `1d6f1be`) |
| **Étape suivante** | `R-03` — Revue des preuves |
| **Verrou** | P-08 : arbitrage requis sur l'écart d'empreinte What-If du run #748 ; aucun nouveau run avant diagnostic |
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
| Plafond de dépense et estimation actualisée des preuves ([verrou](docs/plan/recettes/01-preuves.md#0-verrou-de-coût-et-préflight-bloquants)) | L’utilisateur ne fixe **aucun plafond personnel** et autorise la poursuite. Au contrôle Cost Management du 2026-10-17T10:36Z : 84,648182 EUR réels MTD ; prévision REST de 87,427283 EUR du 18 au 31/10, soit 172,075465 EUR projetés. L’API du budget préexistant de 200 EUR/mois indique toutefois 235,765571 EUR prévus et 0,430301 EUR dépensés ; cette divergence est conservée. Les six budgets de groupes et le budget de souscription sont inchangés | 2026-10-17 |
| Ressources Azure créées pour les preuves ([inventaire](docs/plan/preuves/inventaires-azure.md)) | Shop42 en France Central. Après #748 : 163 ressources, 26 groupes, 7 piles, 57 rôles et 7 budgets ; aucun ID ajouté ou retiré par rapport au préflight juste avant le run. D2 reste connue saine après #740 ; #748 n’a pas atteint PRD. P8 est KO et le nettoyage suspendu. IDs exacts hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\after-infra-748-20261017T103510Z.json` | 2026-10-17 |
| Organisation / projet Azure DevOps de test | Organisation `floriandrevet0332`, projet privé `shop42` et dépôt `shop42`. PR #74 fusionnée ; CI infra orders #746 réussi. La release infra #748 est arrêtée en DEV avec `FailedBeforeMutation` après divergence entre aperçu et recalcul sous verrou ; PRD ignorée. Aucune release applicative n’est intervenue entre les deux aperçus. Le cas négatif #738 reste conservé ; décisions Entra et groupes restent en place | 2026-10-17 |
| Groupes Entra (`sg-shop42-sql-admins`) et Azure DevOps (« Shop42 Release Approvers ») | Créés dans le tenant fourni ; Florian DREVET est membre des deux groupes | 2026-10-15 |
| Inscriptions Entra d'IFS (`dev`) | — | — |
| Protection de la branche `main` sur GitHub | À activer par vous dans *Settings → Branches* | 2026-10-06 |
| Secret de dépôt GitHub Actions `IFS_E2E_PASSWORD` | Configuré (valeur masquée) | 2026-10-06 |

## Tests manuels en attente de vous

- S-02 — [ouvrir `src/backend/InfraFlowSculptor.slnx` dans Rider](docs/plan/00-socle.md#s-02--squelette-backend-généré-depuis-le-template-cqrs) : vérifier les cinq projets et l'absence de références à `Web.Template.CQRS`.
- S-03 — ouvrir `http://localhost:5257/v1/inexistant` dans un navigateur et vérifier le JSON 404 avec `traceId` ([étape](docs/plan/00-socle.md#s-03--moderniser-le-squelette-évolutions-de-vole-papillon-damour)); l'API a été vérifiée par HTTP, mais Chrome a bloqué la navigation directe.
- S-04 — ouvrir la solution dans Rider, vérifier les cinq projets de test puis lancer *Run All* ([étape](docs/plan/00-socle.md#s-04--projets-de-tests-et-règles-darchitecture)); `dotnet test` passe, mais aucune fenêtre native n'est exposée à l'automatisation.
- S-17 — sur un clone neuf, suivre [README — Démarrer](README.md#démarrer-windows) jusqu'à « Bonjour Alice Martin ». Les prérequis passent ; le démarrage du clone reste à vérifier quand les ports fixes 4200, 8080, 5257 et 3000 seront libres, car l'AppHost de travail reste actif.
- P-08 — P1, P9, P3a et P3b sont OK et consignées. P8 reste KO : #738 est conservé comme preuve négative ; #740 est fonctionnellement réussi mais incomplet, car son rapport v1 ne relie pas l’aperçu approuvé. La PR #74 a livré le rapport v2 et le CI #746 a réussi. Au run #748, `approvedPreviewFingerprint` correspond au preview publié, mais le recalcul sous verrou a divergé en DEV : `FailedBeforeMutation`, aucune étape exécutée, PRD ignorée. Aucun déploiement applicatif n’a eu lieu entre les deux aperçus. Capture locale : `p8-infra-dev-failed-run-748`. Ne pas relancer avant arbitrage et diagnostic ; le nettoyage Shop42 reste suspendu.
Chaque étape terminée ajoute ici son 🧪 ; vous consignez le résultat dans
[`docs/plan/recettes/suivi.md`](docs/plan/recettes/suivi.md) puis retirez la ligne.

## Questions pour Claude

**P-08 — divergence d’empreinte What-If au run #748.** Le CI orders/infra #746 a réussi sur le commit `3f591758`. La release #748 a publié l’aperçu DEV `bec9d7c6492eb864e0fb7973427066f7ca2412b2a83faaf1fe3a24d8627b3460`, puis le recalcul sous verrou, cinq minutes plus tard, a produit une autre empreinte. Le rapport v2 est `FailedBeforeMutation`, avec `approvedPreviewFingerprint` égal à l’empreinte publiée, `steps` vide et sans hash recalculé ni diff. Aucun run applicatif ou autre release infra n’est intervenu ; le What-If annonce des changements de Container App, Managed Environment, diagnostics et identité managée.

**Question :** avant toute nouvelle mutation Azure, faut-il ajouter au plan un diagnostic What-If en lecture seule (3 à 5 répétitions sur les mêmes entrées, JSON brut et normalisé, empreintes et diff des chemins, journaux Activity/Policy), puis une étape séparée d’observabilité du rapport ; ou choisir une autre séquence ? Le contrôle fail-closed reste inchangé tant qu’une décision de conception distincte ne l’a pas révisé. Options à examiner sans trancher : diagnostic d’abord, ou nouvel aperçu/run avec le contrôle inchangé après vérification de l’état Azure. Le statut reste `BLOQUE` jusqu’à la réponse de Claude.

**Décision P-08 (Claude, 2026-10-17) — option B.** Le refus fail-closed est conservé (aucun changement de code). La recette P8 ([§ 5](docs/plan/recettes/01-preuves.md#5-p8--livraison-applicative-pendant-lattente-dinfrastructure)) est révisée : #738 est le cas négatif conservé (`FailedBeforeMutation` attendu), ni forcé ni rejoué.

Prochaines actions :
1. Claude : préciser le diagnostic de #748 et l’étape de plan requise ; Luna ne relance pas de release avant cette réponse.
2. Après arbitrage : reprendre P-08 selon le plan actualisé, conserver le contrôle fail-closed sauf nouvelle décision explicite, puis consigner la preuve P8.
3. Nettoyage Shop42 suspendu jusqu’à la réussite de P8 et des autres conditions de la procédure T.
