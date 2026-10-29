# NEXT — où en est l'implémentation

> **À lire en premier en arrivant, à mettre à jour en dernier en partant**, même en pleine étape.
> Ce fichier dit **où l'on est** et **ce que git ne sait pas** (Azure, Entra, Azure DevOps, tests manuels passés).
> Les étapes sont dans [`docs/plan/`](docs/plan/README.md) ; l'historique dans [`docs/plan/JOURNAL.md`](docs/plan/JOURNAL.md).
> Les lignes du tableau ci-dessous sont lues et écrites par `python tools/plan/gate.py` : ne pas renommer leurs libellés.

## En un coup d'œil

| | |
|---|---|
| **Étape courante** | [`J0-15`](docs/plan/02-jalon-0-pilote.md#j0-15--liaisons-identités-attributions-de-rôles-et-impact) — Liaisons, identités, attributions de rôles et impact |
| **Statut** | `EN_COURS` |
| **Dernière étape terminée** | [`J0-14`](docs/plan/02-jalon-0-pilote.md#j0-14--écrans-des-ressources--ajout-général-propriétés-existante-suppression) — Écrans des ressources : ajout, général, propriétés, existante, suppression (commit `407e564`) |
| **Étape suivante** | `J0-16` — Paramètres applicatifs et secrets de pipeline |
| **Verrou** | aucun |
| **Branche** | `impl/j0-modele` (créée depuis `origin/impl/j0-acces`, PR empilée après R-04) |
| **Dernière mise à jour** | 2026-10-29 — Codex (blocage résolu) |
Statuts : `A_FAIRE` · `EN_COURS` · `EN_ATTENTE_DE_REVUE` (auto-revue Codex à exécuter) · `EN_ATTENTE_DE_RECETTE` (recette
Codex à exécuter) · `CORRECTIONS_DEMANDEES` · `BLOQUE` (condition bloquante à résoudre et consigner).

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
| SDK .NET | `10.0.203` | Oui (`10.0.203`) |
| Node | `24.15.0` | Oui (`24.15.0`) |
| Docker | `27.0.0` | Oui (`29.4.3`, daemon répond ; prérequis vérifiés le 2026-10-27) |
| CLI Aspire | `13.5.x` | Oui (`13.5.4`, DCP `0.22.11`; packages AppHost épinglés à `13.5.3`) |
| Python | `3.11` | Oui (`3.14.4`) |
| PowerShell 7 | `7.5.0` | Oui (`7.6.5`) |
| Azure CLI | `2.90.0` | Oui (`2.91.0`, prérequis vérifiés le 2026-10-27) |
| Bicep CLI | `0.47.16` | Oui (`0.47.16`) |
| Git / GitHub CLI | présence | Oui (`2.45.1` / `2.92.0`) |

## État hors dépôt 📌

| Élément | État | Depuis |
|---|---|---|
| Abonnements Azure de test (preuves) | Souscription `fc4fa56f-e4e7-41e9-8a0d-559fcd27b827`, tenant `c833b396-517c-465d-bcce-b79702eaf932` ; `francecentral` et les fournisseurs/SKU requis disponibles ; utilisateur connecté Owner au scope souscription et User Access Administrator au scope tenant ; même souscription pour dev/prd/shared | 2026-10-15 |
| Décision de coût et estimation des preuves ([contrôle](docs/plan/recettes/01-preuves.md#01-surveillance-des-coûts-avant-toute-action-sur-les-ressources-azure)) | Le portail Cost Analysis avant #840 affichait 103,13 EUR MTD et 134,61 EUR projetés à 22:14Z. Après #840, l’API Cost Management donne 103,125364 EUR MTD (données jusqu’au 25/10), 30,254608 EUR de prévision restante du 25 au 31/10 et 133,379972 EUR projetés. Avec la réserve complète P4/P5/P7 de +22,15 EUR, l’estimation prudente est 155,529972 EUR HT, 44,470028 EUR sous l’alerte préexistante de 200 EUR, inchangée ; poursuite autorisée par Florian au profil minimal. Le rafraîchissement portail post-run a demandé une authentification ; l’API sert à cette vérification. C5 pré-run et post-run #840 : 174 ressources, 29 groupes, 7 piles, 63 rôles, 7 budgets, sans changement d’ID. Captures hors dépôt : %LOCALAPPDATA%/InfraFlowSculptor/P08/before-p4-rev4-ci839-20261025T221335Z/, %LOCALAPPDATA%/InfraFlowSculptor/P08/after-p4-rev4-run840-20261025T223927Z/ et %LOCALAPPDATA%/InfraFlowSculptor/P08/after-p4-rev4-run840-20261025T224325Z/. J+1 shop42 tenté le 2026-10-26, résultat inconclusif (données Cost Management disponibles jusqu’au 24/10). Préflight avant approbation PRD #843, après DEV, à 2026-10-25T23:15:51Z : ActualCost MTD 103,125364 EUR (données au 25/10), Forecast 27,045203 EUR du 26 au 31/10, projection 130,170566 EUR ; avec la réserve P4/P5/P7 de 22,15 EUR, total prudent 152,320566 EUR, soit 47,679434 EUR sous le budget indicatif de 200 EUR. C5 après DEV à 23:14:44Z : 174 ressources, 29 groupes, 7 piles, 63 rôles et 7 budgets, zéro ajout/retrait dans les cinq collections. Détails sous %LOCALAPPDATA%/InfraFlowSculptor/P08/after-p4-rev5-dev-20261025T231300Z/. Après #843, Cost Management API à 2026-10-25T23:30:13Z : ActualCost MTD 103,125364 EUR (usage jusqu’au 25/10), Forecast restant 27,045203 EUR du 26 au 31/10, projection 130,170566 EUR ; réserve complète P4/P5/P7 +22,15 EUR, estimation prudente 152,320566 EUR, marge 47,679434 EUR sous l’alerte non plafonnante de 200 EUR. Captures C5/coût : %LOCALAPPDATA%/InfraFlowSculptor/P08/after-p4-rev5-run843-20261025T232741Z/. | 2026-10-26 |
| Préflight coût avant P1/P7 shop44 | ActualCost relu à `2026-10-26T19:09:58Z` : `150,954272 EUR HT` (données jusqu'au 26/10) ; Forecast du 27 au 31/10 `23,049120 EUR`, projection `174,003392 EUR HT`. Deux appels précédents ont répondu 415/400 pendant la mise au point du format, puis un 429 ; la lecture corrigée a réussi et confirme les montants de `19:02:54Z`. Borne complète P1+P7 : `15,00 EUR HT` avec marge de 20 %, incluant SQL serverless `GP_S_Gen5_1` à 1 vCore maximum et 4 h actives par base ; ACR Basic 2 jours ; 2 Go Log Analytics sans crédit gratuit ; stockage, sauvegardes, Container Apps, Key Vault, stockage technique et egress. Total prudent `189,003392 EUR HT`, `10,996608 EUR` sous l'alerte non plafonnante de 200 EUR ; taxes contractuelles absentes de `PreTaxCost`. Prix Retail France Central vérifiés : SQL `0,5739 EUR/vCore-h`, ACR Basic `0,1466 EUR/jour`, stockage ACR `0,088 EUR/Go-mois`, Log Analytics `2,4287 EUR/Go`, rétention `0,1056 EUR/Go-mois`, stockage SQL `0,1265 EUR/Go-mois`. C5 à `19:07:23Z` : 168/29/5/58/7 ; C3 vide (0 build actif/en file, 0 PR active). Bases DEV et PRD observées `Paused` à `19:06:05Z` après le contrôle SQL PRD à `18:50:46Z`. Captures hors dépôt : `%LOCALAPPDATA%/InfraFlowSculptor/P08/shop44-p1-orders-preflight-20261026T190723Z/` et lectures de coût dans le même répertoire.
| Procédure T — suppression core-dev shop43 | Pré-C5 12:27:19Z = 159/24/2/51/7 ; post-C5 `2026-10-26T12:38:03Z` = 155/23/1/49/7 ; C3 zéro build en cours/en file, zéro release active, zéro PR active. Delta exact sans ajout : 4 ressources core-dev, son groupe, sa pile, et deux rôles créés par Luna retirés (`Role Based Access Control Administrator` `20281781-048b-5145-ac68-11f1abb88482`, `Key Vault Secrets Officer` `15f8e5c3-ed7e-599f-87fd-31ed9bec60e5`). Les deux IDs sont absents du C5 pré-shop43 ; le premier figure dans le diff d’installation, le second dans les IDs gérés du stack P1. Cost Management : dernier succès 11:50Z, projection 176,085340 EUR ; refresh corrigé 12:47Z HTTP 429. Estimation P7 + réserve complète 198,235340 EUR HT. Captures : `%LOCALAPPDATA%/InfraFlowSculptor/P08/after-core-dev-delete-20261026T123803Z/`. | 2026-10-26 |
| Procédure T — pré-suppression core-prd shop43 | Préflight `2026-10-26T13:03:13Z` : C5 155 ressources/23 groupes/1 pile/49 rôles/7 budgets ; C3 zéro build en cours/en file, release active et PR active. Pile `ifs-shop43-core-prd` `succeeded`, deny `none`, 7 gérées, 0 détachée, 0 échec ; IDs gérés identiques à l’inventaire antérieur. Groupe, 4 ressources directes et rôles `4cd92509-f1db-5594-aa7d-1f1ebf938ed0` et `1ff37b2e-e96a-5611-a20b-975f2f0e5329` correspondent à P1 et sont absents de la baseline pré-shop43. Le premier essai C3 avec l’URL d’organisation erronée a répondu 404 ; le relevé correct utilise `https://dev.azure.com/floriandrevet0332/shop42` et retourne zéro. Cost Management : dernier succès 11:50Z, projection prudente avec réserve P7 198,235340 EUR HT ; ActualCost refresh correctement formé à 12:47Z HTTP 429. Suppression `deleteAll` lancée après ce préflight. Captures : `%LOCALAPPDATA%/InfraFlowSculptor/P08/pre-core-prd-delete-20261026T130255Z/`. | 2026-10-26 |
| Ressources Azure créées pour les preuves ([inventaire](docs/plan/preuves/inventaires-azure.md)) | Shop42, shop43 et shop44 : les ressources de preuve actives, piles et attributions inventoriées ont été retirées. Contrôle Azure CLI du 2026-10-27 : aucun groupe actif ne correspond à shop42/43/44 et aucune ressource active ne porte ces noms. Six Key Vault standards sont en soft-delete, avec purge automatique du 22 au 25 janvier 2027 ; ils ne génèrent pas de frais d’usage. `rg-vpd-dev` reste préexistant et hors périmètre. Détails : [nettoyage résiduel shop42](docs/plan/preuves/nettoyage-residuel-shop42-20261027.md), [clôture shop44](docs/plan/revues/P-08-nettoyage-shop44-20261027.md). | 2026-10-27 |
L’omission initiale de `deploymentPrincipalId` avait détaché l’attribution Key Vault `1ff37b2e-e96a-5611-a20b-975f2f0e5329` du stack sans la supprimer. Le What-If complet avec la valeur runtime exacte a confirmé `NoChange` ; le modèle a été réappliqué avec ce paramètre pour rétablir les sept IDs gérés avant suppression. Détails dans la revue.
| Organisation / projet Azure DevOps de test | PR #77 fusionnée sur `main` au SHA `1abacadd605c0ca6c342ac305ff083be680d56c4` ; N #776 réussi, R0 #777 annulé avant écriture, reprise ciblée #778 réussie, D3 #779 échoué avant tâches et unique D3 corrigé #780 réussi en DEV/PRD. C3 post-nettoyage à 00:06:45Z : aucune build active/en file, release active ou PR active ; 17 définitions, 9 activées et 8 désactivées ; 80/82 activées et C9 restaurée à `requesterCannotBeApprover=true`. Capture hors dépôt : `%LOCALAPPDATA%/InfraFlowSculptor/P08/core-prd-reconcile/c3-after-delete-20261025T000645Z.json`. | 2026-10-25 |
| Jeux de preuve Azure DevOps shop42 / shop43 / shop44 | Shop42 : 17 définitions ; shop43/shop44 : 16 chacune. Au nettoyage R-03, toutes sont `disabled`, les groupes de variables sont supprimés et les historiques des runs sont conservés ; #933 et #960 restent consultables dans shop44. Les 6 connexions WIF de shop42 restent présentes, mais les identités managées auxquelles elles se référaient ont été supprimées le 2026-10-27 ; elles devront être reconfigurées avant une future recette Azure. Il ne reste aucun build actif/en file, PR active ou release classique active dans les trois projets. Pour le crochet P7, les 17 définitions shop42 et la définition P7 115 de shop44 ne portent aucune variable persistante `IFS_TEST_ABORT_AFTER`. Les groupes secrets shop44 ne sont plus présents ; aucune valeur n’a été lue. Captures anonymisées des preuves sous `src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/`. | 2026-10-27 |
| Droits Azure utilisés pour D1b | `Storage Blob Data Reader` personnel sur `ifs-operations` de `stifsshop42dev` a été révoqué le 2026-10-27 avec le nettoyage résiduel shop42 ; aucun lecteur temporaire ne reste. L’attribution `Key Vault Reader` sur `kv-shop42-main-dev` a disparu avec `core-dev` et ne figure plus dans le C5 final. Aucune valeur de secret n’a été lue | 2026-10-25 |
| Groupes Entra (`sg-shop42-sql-admins`) et Azure DevOps (« Shop42 Release Approvers ») | Créés dans le tenant fourni ; Florian DREVET est membre des deux groupes | 2026-10-15 |
| Résultat D3 — vérification C1–C9 | D3 #780 est terminé avec succès sur le CI #776 et le SHA main exact. Les aperçus DEV/PRD ont zéro opération pending, création, recréation, détachement/suppression ou révocation ; `modified=5` et `dataWrites=1` sont hors des prédicats C8. Les rapports `ifs-report/v2` DEV/PRD sont `Succeeded`, révision 2, commit `3533c9c…`, erreur vide ; leurs empreintes approuvées correspondent aux empreintes des aperçus publiés, sans détachement, révocation ou pare-feu temporaire. C3 après run : aucune exécution active/en file ni PR active ; C9 restaurée à `true`. C5 conserve les cinq collections d’IDs et les 21 IDs Shop42 suivis. C6 après run confirme les apps DEV/PRD Running, saines et HTTP 200 sur le digest D2. Captures #779–#780 dans `src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/` ; rapports et préflight C5/C6 hors dépôt sous `%LOCALAPPDATA%/InfraFlowSculptor/P08/D3-report-*-run780/`, `c5-after-D3-run780-20261024T195132Z.json` et `c6-after-D3-run780-20261024T195239Z.json`. [Analyse C8 et reprise](docs/plan/revues/P-08-C8-analyse-20261024.md). | 2026-10-24 |
| Inscriptions Entra d'IFS (`dev`) | — | — |
| Protection de la branche `main` sur GitHub | À activer par vous dans *Settings → Branches* | 2026-10-06 |
| Secret de dépôt GitHub Actions `IFS_E2E_PASSWORD` | Configuré (valeur masquée) | 2026-10-06 |

| Nettoyage résiduel Azure shop42 (demande de réduction des coûts) | Les trois groupes du kit encore présents ont été supprimés; C5 après opération : 135 ressources, 16 groupes, 0 pile, 30 rôles et 7 budgets. Recontrôle CLI du 2026-10-27 : 0 ressource correspondant aux noms/tags shop42/43/44 et 0 groupe de recette actif. Six coffres Key Vault standards restent en soft-delete jusqu’au 23–25 janvier 2027 ; ils ne génèrent pas de facturation d’usage en état supprimé. Voir [preuve](docs/plan/preuves/nettoyage-residuel-shop42-20261027.md). | 2026-10-27 |
| Stack local de recette | AppHost Aspire arrêté après les vérifications R-04 pour libérer les DLL ; relance : `cd src/backend; aspire run`. Les paramètres de développement restent dans User Secrets. Le PostgreSQL local garde les données des recettes, dont Emma membre de Contoso après une invitation précédente ; ne pas réinitialiser la base. Le scénario E2E « Emma sans organisation » échoue donc sur cet état persistant. Aucun secret n’est consigné. | 2026-10-27 |

## Recettes autonomes en attente de Codex

- S-02 — [ouvrir `src/backend/InfraFlowSculptor.slnx` dans Rider](docs/plan/00-socle.md#s-02--squelette-backend-généré-depuis-le-template-cqrs) : vérifier les cinq projets et l'absence de références à `Web.Template.CQRS`.
- S-03 — ouvrir `http://localhost:5257/v1/inexistant` dans un navigateur et vérifier le JSON 404 avec `traceId` ([étape](docs/plan/00-socle.md#s-03--moderniser-le-squelette-évolutions-de-vole-papillon-damour)); l'API a été vérifiée par HTTP, mais Chrome a bloqué la navigation directe.
- S-04 — ouvrir la solution dans Rider, vérifier les cinq projets de test puis lancer *Run All* ([étape](docs/plan/00-socle.md#s-04--projets-de-tests-et-règles-darchitecture)); `dotnet test` passe, mais aucune fenêtre native n'est exposée à l'automatisation.
- S-17 — sur un clone neuf, suivre [README — Démarrer](README.md#démarrer-windows) jusqu'à « Bonjour Alice Martin ». Les prérequis passent ; le démarrage du clone reste à vérifier quand les ports fixes 4200, 8080, 5257 et 3000 seront libres.
Codex exécute chaque 🧪, consigne le résultat dans [`docs/plan/recettes/suivi.md`](docs/plan/recettes/suivi.md), puis retire la ligne.

## Points ouverts et historique des décisions

### J0-09 — écrans du projet — 2026-10-28

**Décisions reçues le 2026-10-28** : favoris/récents stockés de façon relationnelle dans le profil ; JSONB exclu. Les compteurs composants/ressources/constats sont reportés à J0-10/J0-20 ; aucun zéro provisoire n'est affiché. Le blocage de conception est résolu et consigné dans [`J0-09-blocage-20261028.md`](docs/plan/revues/J0-09-blocage-20261028.md). Florian autorise l'authentification des personas de test via Keycloak local ; `project.spec.ts` et ses captures passent sur desktop et mobile. Les résultats de la suite complète et ses limites connues sont consignés dans [`suivi.md`](docs/plan/recettes/suivi.md).

### J0-11 — stockage des propriétés des ressources — 2026-10-28

**Décision reçue le 2026-10-28 :** Florian étend l'interdiction à tous les usages JSONB, existants compris. J0-11 couvre donc aussi la migration relationnelle des colonnes actuelles, l'actualisation des conventions et la précision des contrats `array`/`object` du catalogue. [Décision et vérifications](docs/plan/revues/J0-11-blocage-stockage-20261028.md).

### J0-12 — valeurs effectives, présences et tags — 2026-10-28

**Arbitrages reçus le 2026-10-28 :** RG-RES-07 sera couverte avec les liaisons en J0-15 et le constat `VAL-RES-PRESENCE` en J0-20 ; le DTO de la route `effective-values` est confirmé ; le nom API Key Vault est `purgeProtection` (la recette cite `enablePurgeProtection`, nom du paramètre Bicep). La route, ses tests et le client généré sont implémentés localement. La preuve des arbitrages est dans [le déblocage J0-12](docs/plan/revues/J0-12-deblocage-20261028.md).

**Arbitrage `requiredWhen:` résolu le 2026-10-28 :** sur demande de Florian, Claude CLI a recommandé de traiter `requiredWhen:` comme une condition d'applicabilité, comme `requires:`. Le moteur masque `capacityReservationLevel` et `administratorLogin` hors condition et les inclut lorsque celle-ci est vraie. Tests ciblés 11/11 après reproduction rouge. [Décision et preuve de déblocage](docs/plan/revues/J0-12-deblocage-requiredwhen-20261028.md).

### J0-13 — nommage — 2026-10-28

Sur délégation de Florian, Claude CLI recommande de reporter RG-NOM-10 à J0-28 : les noms publiés seront disponibles avec les révisions (J0-24), et l'impact par cible ainsi que la révocation des accès dépendent du déploiement. J0-13 conserve les verrous après publication, y compris pour les noms forcés, avec tests de non-régression ; la page rend les réglages publiés en lecture seule. Le tableau de l'étape et la dette ont été alignés sur ce périmètre. Les constats de collision globale sur ressources existantes et le parcours d'erreur avec template d'un projet voisin ont été vérifiés/corrigés ; l'échec fermé est conservé si un template voisin ne peut pas être évalué. Preuves dans [`suivi.md`](docs/plan/recettes/suivi.md).

### J0-15 — incompatibilité entre la recette Accès et le catalogue épinglé — 2026-10-29

Sur demande de Florian, l'avis de Claude CLI est retenu : conserver `2026.10-pilot` immuable et bloquer J0-15. La recette manuelle exige une liaison Accès vers `log main`, mais le descripteur publié de Log Analytics n'autorise que les liaisons `Logging` et `Diagnostics` comme cibles. DEC-107 impose une nouvelle version pour toute évolution du catalogue. Pour reprendre, il faut préciser la version de catalogue à publier et comment la cible de démonstration l'utilisera, ou autoriser une correction du scénario manuel vers une cible compatible. RG-LIA-24 reste reportée à J2 conformément au plan et n'est pas bloquante. Détails et vérifications dans [`J0-15-blocage-20261029.md`](docs/plan/revues/J0-15-blocage-20261029.md).

**Résolution — 2026-10-29 :** Florian a confié le choix à Codex après avis Claude. Le catalogue correctif `2026.10-pilot.1` autorise `Access` vers Log Analytics; seul le projet temporaire `shop15` l'utilise, tandis que `shop` garde la version publiée originale. La recette Scalar locale est passée et le projet temporaire a été archivé de façon récupérable. Voir [`J0-15-deblocage-20261029.md`](docs/plan/revues/J0-15-deblocage-20261029.md).

### R-04 — portée d’idempotence de création d’organisation — 2026-10-27

R-04 : arbitrage de Florian reçu le 2026-10-27, clé distincte par tenant et utilisateur retenue. La création d'organisation utilise un magasin et une migration dédiés ; les écritures avec organisation dérivent leur clé de stockage avec l'identité utilisateur. Réservation, écriture métier et réponse sont transactionnelles. Les tests de rejeu, de corps différent, d’isolation entre utilisateurs, de conflit PostgreSQL au savepoint et d'acceptation concurrente idempotente passent. A14 Scalar est passée avec Alice et Chloé. [PR #4](https://github.com/FlorianDrevet/InfraFlowSculptor/pull/4) est ouverte vers `impl/socle`. Avant l'appel de la purge d'audit par le Worker hébergé en J0-31, accorder `EXECUTE` à son identité. L'auto-validation est dans [la revue R-04](docs/plan/revues/R-04-autovalidation.md) ; le détail de l'arbitrage est dans [la résolution de l’idempotence](docs/plan/revues/R-04-blocage-idempotence.md).

### P-08 — réconciliation What-If et procédure T shop43 — 2026-10-26

Florian a délégué la règle de décision et demandé de finir la recette sans délai J+1, en maintenant le coût bas. La réconciliation est ciblée par propriété et ressource ; aucune tolérance générale n'est appliquée. P4 est terminée et nettoyée ; l'effet d'update observé sur SQL `data-prd` reste documenté dans `docs/plan/revues/P-08-blocage-whatif-shop43-20261026.md`.

P5 shop43 est exécutée : #860 finit `PartiallyApplied`, avec `deploy-unit=Done` et `secret-write=Started`; #861 reprend le même ID d'opération, termine `Succeeded` sans doublon sur CI #858 / SHA `3b3d0f409c204e4aa08961617428250d58f68b51`. Le secret original du groupe #9 n'a jamais été lu ou modifié ; le groupe temporaire #11 et son autorisation sont supprimés.

Le What-If core PRD refait à 12:44Z avec les trois fichiers exacts de CI #858 donne 4 `Modify`, 2 `NoChange`, 1 `Ignore`. Il détaille notamment l’omission non persistante des `retentionPolicy.days=0` et de l’entrée `audit` désactivée ; après #860, toutes ces valeurs sont restées live. Les changements `kind`, `Flow_Type`, `Request_Source`, destination App Insights et `createMode` restent également non persistants après l’artefact exact. Workspace `NoChange`, Smart Detector `Ignore`. C5 pré-update à 12:44:35Z : 155/23/1/49/7, pile et 7 IDs gérés identiques à P1, `denyDelete`, zéro échec ; C3 vide. Détails : `core-prd-whatif-summary.json` et `summary.json` sous `%LOCALAPPDATA%/InfraFlowSculptor/P08/pre-core-prd-update-20261026T124417Z/`.

C5 pré-suppression core-dev du 12:27:19Z = 159/24/2/51/7 ; C5 post-suppression à 12:38:03Z = 155/23/1/49/7 ; C3 est vide. Le delta entre les deux est exact et sans ajout : 4 ressources du groupe core-dev, le groupe, la pile, et les deux rôles `20281781-048b-5145-ac68-11f1abb88482` (RBAC Administrator, créé par l'installation shop43) et `15f8e5c3-ed7e-599f-87fd-31ed9bec60e5` (Key Vault Secrets Officer, géré par le stack P1). Les deux IDs étaient absents de la baseline pré-shop43. Preuves : `%LOCALAPPDATA%/InfraFlowSculptor/P08/after-core-dev-delete-20261026T123803Z/`.

Coût : dernier succès Cost Management 11:50Z — ActualCost 148,565572 EUR au 25/10, Forecast 27,519768 EUR, projection 176,085340 EUR. Le refresh ActualCost correct du 12:47Z a reçu HTTP 429. Estimation haute P7 +11,0748 EUR ; avec toute la réserve de 22,15 EUR, projection 198,235340 EUR HT sous l'alerte non plafonnante de 200 EUR. Profil minimal et nettoyage immédiat. Le contrôle J+1 shop42 reste inconclusif et ne retarde pas la recette.

P-08 est terminée ; après le commit des preuves et `gate.py done P-08`, poursuivre au verrou R-03.
### P-08 — clôture shop43/shop44 et procédure T

Les recettes P1 à P9 et les nettoyages Azure shop42, shop43 et shop44 sont exécutés. P1 shop44 inclut les releases core #904, data #906, platform #908 et orders API #929 ; les contrôles de santé DEV/PRD répondent 200. P7 : #933 a interrompu le processus après `deploy-unit`; après l’échec partiel #959, #960 termine la reprise rev3. Les opérations rev2 conservées finissent `Done`; le fait que `revoke` ait été journalisé en rev3 est consigné comme écart. P8 positif D3 #780 a réussi ; #748 reste non diagnostiqué conformément au plan.

La procédure T est terminée : les sept piles, 64 ressources, trois groupes du kit et 21 rôles shop44 sont absents ; aucun ID baseline n’a disparu. C5 final 144/19/0/40/7, C3 vide. Les 67 versions de journaux sont vérifiées ; les tombstones Key Vault sont conservés et non purgés. Les ressources VPD et le groupe de stockage `jetekf4xhml6iazscripts` restent hors périmètre.

Cost Management au 2026-10-27T02:53:50Z : réel 153,046179 EUR jusqu’au 26/10, forecast 31,936759 EUR, projection avec réserve 199,982937 EUR sous l’alerte de 200 EUR. La requête quotidienne par groupe a répondu HTTP 429 ; Florian a demandé de terminer sans attendre une nouvelle journée de publication. Aucune écriture Azure facturable supplémentaire n’est prévue. Captures et contrôle consolidé : `%LOCALAPPDATA%\InfraFlowSculptor\P08\shop44-p7-post959failure-20261027T001158Z\`. Le verrou P-08 passe à R-03 après son commit de preuves.
**Autorisation d'autonomie, 2026-10-23 :** Florian demande à Codex d'exécuter lui-même toutes les phases de tests, recettes,
validation et auto-revue, sans attente de Claude ou d'un modèle supérieur. Pour P-08, il autorise aussi les actions du projet
Azure DevOps de test `shop42` décrites dans l'amendement de [`P-08`](docs/plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement)
(états des dix définitions, N/R0, PR/fusion, C9 et D3). Les critères C1–C9, le refus de R0 et le fail-closed restent obligatoires. La décision de poursuite du 2026-10-24 autorise une seule nouvelle release D3 après résolution de C8, uniquement si C8 passe. Aucun déploiement facturable ne partira sans coût réel, prévision et estimation complète vérifiables.

**Décision de poursuite P-08, 2026-10-24 :** Florian demande à Codex de choisir la voie pertinente et d’avancer sur le plan.
Le critère C8 est conservé tel qu’écrit : il porte sur zéro création, recréation, suppression, révocation et opération en attente.
Le champ dataWrites affiché par l’aperçu n’est pas un critère C8. #761 échoue à C8 parce qu’une opération data-access
est encore Started. Son déploiement a été annulé avant toute tâche d’écriture.

La décision de poursuite du 2026-10-24 autorise une reprise idempotente ciblée de l’opération data-access, puis une seule
release manuelle D3 supplémentaire si C8 passe. La reprise pourrait réveiller la base DEV et nécessite donc un relevé récent
des coûts avant toute écriture. Avant cette release, Codex rafraîchit C1–C7 et C9 et confirme par lecture seule que le journal
est terminé et que le préflight ne prévoit plus d’opération en attente. L’aperçu publié doit ensuite satisfaire C8 avant toute
approbation. Si une condition échoue ou est illisible, annuler avant les tâches de déploiement ; aucune tentative supplémentaire
n’est autorisée après celle-ci. Cette autorisation a été consommée par la reprise DEV puis l’essai unique #762 ; C8 est passé en DEV et PRD, mais PRD est PartiallyApplied après un délai SQL post-login. P-08 reste BLOQUÉ et aucune reprise PRD n’est autorisée sans nouvelle décision.

**Nouvelle consigne de continuation P-08, 2026-10-24 :** après le résultat #762, Florian demande de choisir une solution, de résoudre les blocages et de poursuivre le plan aussi loin que possible. Cette décision remplace l’arrêt précédent pour la reprise PRD et autorise, si tous les contrôles et le coût le permettent, une reprise ciblée puis au plus un nouvel essai D3 après passage de C8. Le fail-closed C8, le refus de R0 et l’arrêt après cet essai restent obligatoires.

**Solution retenue — reprise PRD ciblée :** le pipeline existant `orders/infra` reçoit un mode manuel `recover-prd-762`, désactivé en mode `standard`. La reprise tourne sous la connexion `ifs-shop42-prd`, dans l’environnement protégé `shop42-prd`, avec verrou séquentiel et contrôle de souscription. Elle accepte seulement l’opération `36ab5a2dfdd9b34f84098f22274bb4a6c35c85fb62493d585df6fab21ba2a71f`, révision 2, commit `893f2a3dda4d7c8461be9c93f208a9e84d317962`, dans le journal `ifs-shop42-orders-prd`. Elle exige une unique opération pending, son état `Started`, la correspondance intégrale du détail data-access avec l’artefact PRD et l’exposition `Public`. Elle exécute le seul script SQL idempotent ; aucune modification de pare-feu, pile ARM, révocation ou écriture de secret n’est permise. Après succès SQL seulement, l’opération est marquée `Done` sous le bail.

**État historique de P-08 au préflight 2026-10-24T20:38Z :** PR #77 est fusionnée au SHA `1abacadd` ; N #776 a réussi, R0 #777 a été annulé avant écriture, la reprise PRD #778 a terminé l’opération exacte et le D3 unique #780 a réussi en DEV/PRD. C8 passe sur les deux aperçus ; les rapports sont `Succeeded`, C3 est vide, C9 est restaurée et C5 conserve les IDs suivis. Le préflight de nettoyage a révélé cinq modifications ; leur suite et l’écart What-If sont consignés dans la note de nettoyage liée ci-dessus.

**Décision active — P-08 / D2b :** Florian a délégué à GPT Sol une autorité d’arbitrage équivalente à Claude. Sol confirme que le résumé
PASS ne remplace pas les sorties complètes : le transcript de 09:35 s’arrête sur `Prepared branch manifest metadata differs from
rehearsal` et `d2b-local-verification.txt` n’est qu’un résumé après rafraîchissement. L’échec initial reste conservé comme preuve.
Son arbitrage du 2026-10-20 autorise un amendement ciblé de D2b.1/.2/.4 : rejouer dans le clone indépendant
`C:/Users/flori/RiderProjects/shop42-d2b-proof`, créé sur `origin/main` `3f59175`, avec la répétition sous `$TEMP/ifs-shop42-rehearsal` ;
préserver le clone original et sa branche `2875cf3`, puis réconcilier le résultat testé par merge et identité de tout l’arbre, sans
réécrire l’historique distant. L’IFS est fixé à `893f2a3dda4d7c8461be9c93f208a9e84d317962` pendant les deux passes. Les assertions
D2b.2 restent intactes et toute nouvelle exception impose `BLOQUE`. Aucun commit, merge ou push nouveau avant la validation de toutes
les sous-étapes D2b.3.1 à 3.6, y compris la confirmation par Florian des huit suspensions et des files vides. La décision précédente
sur le CI N manuel après fusion et l’observation du R0 reste en vigueur.

**Nouvelle exception et arbitrage Sol — 2026-10-20 :** la répétition a interrompu le runner avant publication sur `git rev-parse
origin/main` dans le clone temporaire. La sortie complète a été renommée sans modification et archivée sous
`%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-rehearsal-precondition-failure-20261020T130743Z-transcript.txt` (SHA-256
`91BD51AF4D30FA9F5C152D7FEC6DB598A0852F8FBEB58050146BBC812F6515FA`). La répétition a bien `HEAD=3f5917588…`, la branche attendue
et un arbre propre ; seul `origin/main` manque, ce qui est normal dans ce clone temporaire. Sol confirme que l'exception impose
`BLOQUE`, mais autorise un rejeu complet après suppression de cette exigence superflue pour le clone temporaire. La vérification
`origin/main` du clone de preuve reste obligatoire. Pour reprendre, après le commit de blocage, Luna transcrit cette décision du
propriétaire en passant explicitement le statut `BLOQUE` à `A_FAIRE` ; `gate.py` n'a pas de commande pour cette transition.

**Nouvelle exception du 2026-10-20T13:14:20Z :** après correction de la précondition précédente, le rejeu s'est arrêté à l'étape 0
par `CommandNotFoundException` : `Assert-ProofManifestWorkingTree` n'était pas disponible, car le runner n'avait pas importé
`tools/proofs/ProofTools.psm1`. Aucune publication neuve ni modification du clone de preuve n'a eu lieu. Transcript complet archivé
sous `%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-rehearsal-attempt2-proofmanifest-module-not-imported-20261020T131420Z-transcript.txt`,
SHA-256 `C0E40FB93D3A4B05D24EA845FBC492DBC6517EAEC0F0650A13B930C34F99D955`. Sol confirme que c'est une omission du runner et
autorise à importer explicitement `tools/proofs/ProofTools.psm1` avant l'étape 0, conformément au bloc 0 ter.2. Sous la délégation de
Florian, P-08 reprend en `A_FAIRE` après le commit de cette décision et `gate.py check` ; nettoyer d'abord la copie temporaire restante,
puis refaire les deux passes intégralement avec de nouveaux transcripts. Toute nouvelle exception rebloque immédiatement P-08.

**Nouvelle exception du 2026-10-20T13:16:17Z :** la répétition a passé les étapes 0 à 3 puis échoué à l'étape 4 sur l'assertion
« Le manifeste final diffère du manifeste initial hors module, commit et generatedAtUtc. » Le transcript complet est archivé sous
`%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-rehearsal-attempt3-final-manifest-mismatch-20261020T131617Z-transcript.txt`,
SHA-256 `791FE392BAEFBA26543C6B316AF691A3D1508C9DF545011CA254197030A1966B`. Aucun changement n'a atteint le clone de preuve ou le
clone original ; les deux répertoires temporaires contiennent des artefacts partiels et sont laissés intacts. Sol a diagnostiqué en
lecture seule une erreur du runner : `ConvertFrom-Json` conserve `generatedAtUtc` comme `DateTime`, puis le cast `[string]` le convertit
au format local `10/20/2026 13:16:22`, alors que le manifeste réel conserve `2026-10-20T13:16:22Z`. C'est le premier écart des JSON
canoniques (index 12059 ; longueurs 12540/12541). Sol confirme en lecture seule que les 99 chemins/empreintes sauf le module, les
quatre unités et le module copié correspondent à la publication. Il autorise uniquement le remplacement de
`$expectedManifest.generatedAtUtc = [string]$after.generatedAtUtc` par `$expectedManifest.generatedAtUtc = $after.generatedAtUtc`.
La tentative reste échouée. Le 2026-10-20T13:20:00Z, Luna a nettoyé les deux chemins exacts via `Remove-TempCopy` et confirmé leur
absence ; transcript du nettoyage `%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-attempt3-temp-cleanup-transcript.txt`, SHA-256
`30AEC9B23A446ABE5E0454F03921AF2A1848B5EC615979AFABEFF7F549139E77`. Selon la délégation de Florian et l'arbitrage de Sol, P-08
reprend formellement en `A_FAIRE` ; le `gate.py check` doit passer avant le rejeu complet des deux passes avec de nouveaux transcripts.

Nouveau blocage d'authentification — l'essai de clone Git HTTPS avec le PAT fourni a été refusé par Azure Repos. Le clone original
est resté propre ; son `origin/main` local vaut `3f5917588b83d11fc07d7f9838f5e895e037dd6d`, SHA également visible sur la page
Azure DevOps `shop42` dans le navigateur. Sur décision de GPT Sol du 2026-10-20, D2b.2 peut utiliser un clone indépendant
`--no-hardlinks` depuis le clone original, récupérer localement cette ref, conserver l'URL ADO en `origin` et la recouper avec la
lecture navigateur ; ce repli ne sera pas présenté comme un `fetch origin` réseau. La preuve est conservée dans
`%LOCALAPPDATA%\InfraFlowSculptor\P08\D2b\d2b-git-auth-fallback-evidence.txt`. Un accès Git valide reste obligatoire avant D2b.4 ;
aucun PAT neuf ne sera créé sans consentement spécifique.

L’interface Azure DevOps a été vérifiée en lecture seule le 2026-10-20 pour les dix définitions.
Elles sont toutes en révision 9 avec `queueStatus=enabled`. Pour les dix définitions, l’option UI « Override the YAML
continuous integration trigger from here » est cochée et « Disable continuous integration » est sélectionné ; aucun déclencheur
planifié ni de fin de build n’est configuré. En particulier, CI `orders/infra` (ID 80) a aussi `trigger: main` dans son YAML, mais
l’override UI désactive ce déclencheur : la fusion ne produira pas le run N automatique exigé par D2b.4. Microsoft documente que
l’override UI remplace le déclencheur CI YAML ([Build Azure Repos Git repositories](https://learn.microsoft.com/en-us/azure/devops/pipelines/repos/azure-repos-git?view=azure-devops)).
Les dix définitions n’avaient aucun run actif ou en file ; aucune PR n’existe pour `fix/p08-keep-recalculated-preview`. La garde
`shop42-dev` a été relue et correspond au plan. Aucune définition n’a été suspendue et aucune écriture Azure DevOps n’a été faite.

La définition 80 conserve son override UI désactivant CI ; les huit définitions collatérales ont été suspendues avant la livraison.
La PR #75 est fusionnée sur `main` au SHA `492dfa0df7f9c22f182f1fec443f90c041ed823d`. Le CI N #759 a réussi une seule fois,
`reason=manual` sur ce SHA ; la ressource pipeline a déclenché R0 #760 automatiquement selon l’interface Azure DevOps.
`Preview_dev` a réussi, puis l’approbation a été annulée avant toute tâche `Deploy_dev`, faute de pouvoir la rejeter avec l’auteur
unique bloqué. Aucun N ou R0 n’est relancé ; la suite D3 est suspendue jusqu’à résolution de cet écart et du contrôle C5.
Le protocole actif est détaillé dans [P-08, étape 0 quinquies](docs/plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement)
et la [recette § 5](docs/plan/recettes/01-preuves.md#5-p8--livraison-applicative-pendant-lattente-dinfrastructure).

- **Déclencheurs D2b :** livraison sur `main`. Avant le commit/push/PR/merge, Florian relève les dix définitions CI/releases et leurs
  files, puis désactive exactement les huit définitions collatérales (`core`, `data`, `platform`, `orders/apps/api`) avec
  `queueStatus=disabled`. Les deux définitions `orders/infra` restent `enabled`. Aucun YAML du clone n'est changé pour cela.
- **Acteurs :** Codex a préparé la preuve, poussé la branche, créé et fusionné la PR #75 après réussite des contrôles #754–#758.
  Codex a lancé N #759 une fois, puis a arrêté R0 #760 avant toute tâche `Deploy_dev` après le refus d’Azure de rejeter
  l’approbation avec l’unique approbateur bloqué. L’état final de l’approbation est `canceled`, pas `rejected`.
- **D3 — état au 2026-10-24T19:01:57Z :** #778 a résolu l’opération Started ; C1–C7 et C9 sont documentées dans la ligne de résultat ci-dessus.
  C3 vient d’être relu : 0 run actif/en file parmi 100, 0 PR active ; pipelines collatéraux 74/76/77/79/83/85/86/88 `disabled` révision 12,
  orders/infra 80/82 `enabled` révision 9. C8 reste à lire sur `Preview_dev` et `Preview_prd` avant leurs approbations. Le check Approval 138
  et ExclusiveLock 87 sont présents ; le groupe d’approbation est inchangé et `requesterCannotBeApprover=true`. Une seule D3 est autorisée,
  sur la ressource CI #776 et le SHA main `1abacadd605c0ca6c342ac305ff083be680d56c4`. Le réglage C9 sera basculé temporairement juste avant
  le lancement puis restauré à la fin du run ; aucune seconde tentative ne sera faite.
- **Coût :** relevé disponible avant D3 : ActualCost MTD 131,724309 EUR HT (18:42:56Z), Forecast restant 41,098486 EUR (18:50:29Z),
  projection 172,822795 EUR. Scénario conservateur incluant #770, #778, un D3 complet et 2 Go de logs : 185,323195 EUR HT, soit 14,676805 EUR
  sous l’alerte préexistante de 200 EUR. Les données de facturation peuvent être retardées ; ce montant n’est pas un plafond garanti.
  Une nouvelle lecture le 19:00Z a été limitée par Cost Management (HTTP 429) ; aucun coût ni aucune estimation n’est inventé, et aucun
  déploiement facturable n’est lancé tant qu’une lecture fraîche n’est pas obtenue.

- **Historique du statut :** après l’exception du transcript, P-08 est passé à `BLOQUE`. Florian a délégué l’arbitrage à GPT Sol, qui
  a autorisé le clone indépendant et le merge par identité d’arbre ; les amendements ciblés D2b.1/.2/.4 consignent cette méthode.
  La répétition du 2026-10-20T13:07:43Z a échoué avant publication : le clone temporaire possède le HEAD, la branche et l'arbre propres
  attendus, mais pas `origin/main`, car il a été cloné depuis le clone de preuve local. Le runner ajoutait à tort cette exigence à la
  répétition ; `git rev-parse origin/main` a échoué avant toute publication. Transcript complet archivé sous
  `%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-rehearsal-precondition-failure-20261020T130743Z-transcript.txt`, SHA-256
  `91BD51AF4D30FA9F5C152D7FEC6DB598A0852F8FBEB58050146BBC812F6515FA`. Aucun changement n'a été publié dans le clone de preuve.
  L'exception remet P-08 à `BLOQUE`. Sol autorise un nouveau rejeu complet après retrait de cette seule vérification pour le clone
  temporaire ; `origin/main` reste obligatoire pour le clone de preuve. La répétition conserve les assertions `HEAD`, branche et arbre
  propre, et doit repartir après une transition formelle vers `A_FAIRE`. Les commits, merge, push et PR restent interdits jusqu’à la fin
  de D2b.3.1–3.6. `gate.py` n'a ni commande `unblock` ni commande `start` ; `resume` reste réservé à `EN_ATTENTE_DE_RECETTE`.
  Après l'autorisation de reprise, une deuxième exception est survenue à l'étape 0 parce que `ProofTools.psm1` n'était pas chargé ;
  transcript archivé et P-08 rebloqué. Sol a ensuite confirmé l'omission, autorisé l'import explicite et le rejeu complet, puis P-08 a
  été rétabli à `A_FAIRE`. La troisième répétition a passé les étapes 0 à 3, puis échoué sur l'équivalence finale du manifeste ; Sol a
  localisé une conversion locale de `generatedAtUtc` dans le runner et autorisé un correctif d'une ligne. Luna a ensuite nettoyé et
  vérifié les deux copies temporaires ; P-08 revient à `A_FAIRE` sous cette autorisation, avant deux nouvelles passes complètes.

**Prérequis D2b vérifiés le 2026-10-20 :** le clone original est accessible dans `C:/Users/flori/RiderProjects/shop42` ; sa branche
locale et distante `fix/p08-keep-recalculated-preview` contient le commit de Florian `2875cf3` et l’arbre est propre. Le clone de preuve
est créé à `C:/Users/flori/RiderProjects/shop42-d2b-proof`, avec HEAD et `origin/main` égaux à `3f5917588b83d11fc07d7f9838f5e895e037dd6d`,
branche prévue, arbre propre, sans upstream. Les contrôles locaux du clone sont consignés sous
`%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-local-verification.txt` ; l’inventaire YAML local des dix CI/releases est sous
`%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-yaml-trigger-inventory.txt`. Le diff du précédent rafraîchissement de manifeste a été
archivé puis restauré ; les deux copies temporaires avaient été absentes au début du rejeu. Après la tentative du 2026-10-20T13:16:17Z,
`ifs-shop42-rehearsal` et `ifs-shop42-fresh` contenaient des artefacts partiels ; ils ont été supprimés par `Remove-TempCopy` et leur
absence vérifiée à 2026-10-20T13:20:00Z.

**Résultat du rejeu D2b.2 — 2026-10-20 :** répétition et exécution complètes terminées avec `PASS_OK`, IFS fixé au commit
`893f2a3dda4d7c8461be9c93f208a9e84d317962`. Transcripts complets conservés dans `%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/` :

- `d2b-rehearsal-rerun-transcript.txt` — SHA-256 `F2FDF29815BE5061649519C352308562CEDFC2EF275395B27598C184E66CB40E`.
- `d2b-execution-rerun-transcript.txt` — SHA-256 `39660364C48D244FD5DB9442822844F54CD628575A580AF8859738E1F40DC5A9`.
- Runner employé `d2b-replay-runner.ps1` — SHA-256 `ACA244751B3B8C1F041D082FD985FA2C331E26B7579D168F06E432A74647A91A`.

Les deux passes confirment la publication ciblée, les unités inchangées et l’égalité attendue du manifeste final. Les chemins
temporaires `C:/Users/flori/AppData/Local/Temp/ifs-shop42-rehearsal` et `C:/Users/flori/AppData/Local/Temp/ifs-shop42-fresh`
sont absents. Le clone original `shop42` reste propre sur `2875cf3cb3fbb0eef2c777dc6d4565d4b16612aa`. Le clone de preuve
`shop42-d2b-proof`, basé sur `origin/main` `3f5917588b83d11fc07d7f9838f5e895e037dd6d`, ne contient que deux fichiers modifiés :
`.ifs/manifest.json` et `.ifs/templates/scripts/IfsRelease.psm1` ; aucun commit n’y a été créé.

**Contrôles D2b.3 initiaux — 2026-10-20 :** la garde `shop42-dev` avait `Pre-check approvals` en ordre 1 avant `Exclusive Lock`
en ordre 2, groupe `[shop42]\Shop42 Release Approvers`, délai 30 jours et auto-approbation décochée. Les YAML locaux confirment
que `Preview_dev` est en lecture seule, que seul `Deploy_dev` écrit sous `shop42-dev`, et que `Deploy_prd` dépend de `Deploy_dev`.

**Résultat D2b.3.5 — 2026-10-23 20:30 UTC :** Codex a changé uniquement `queueStatus` dans l’interface Azure DevOps. Les huit
définitions initialement `enabled` révision 9 sont maintenant `disabled` révision 10 : 74, 76, 77, 79, 83, 85, 86 et 88. Les
deux `orders/infra` 80/82 restent `enabled` révision 9. Les cinq pipelines PR 75/78/81/84/87 et les définitions hors périmètre
73/89 restent inchangés. Lecture CLI après changement : aucune exécution `inProgress`, `notStarted` ou `postponed`, aucune PR active.
Les cinq CI YAML ciblent `main` avec des filtres `.ifs/**`; les cinq releases ont `trigger: none` et attendent leur CI sur `main`.
Les cinq PR YAML n’ont pas de déclencheur YAML ; les Build Validations bloquantes 9–13 sur `main` ciblent les définitions
75/78/81/84/87, avec `/.ifs/templates/**` dans leurs filtres. Ces YAML PR ne contiennent pas de tâche ni de connexion Azure.
Les définitions 73/89 ont `trigger: none`, `pr: none`. L’override UI du CI 80 désactive son déclenchement automatique sur `main` ;
la release 82 garde son déclencheur de ressource CI sur `main`.
La garde `shop42-dev` est toujours en ordre 1/2 avec le même groupe et le délai 30 jours ; le groupe contient seulement Florian,
et l’auto-approbation est toujours décochée. L’état est prouvé dans `docs/plan/revues/P-08-resolution.md`.

**Auto-revue D2b.3.1–3.6 — 2026-10-23 20:50 UTC :** verdict `VALIDÉ` pour la livraison D2b. Détails dans
[`P-08-D2b-autovalidation.md`](docs/plan/revues/P-08-D2b-autovalidation.md) ; C5 et C8 restent à vérifier avant toute écriture D3.

Le fetch Git authentifié a réussi ; avant livraison, `origin/main` était `3f5917588b83d11fc07d7f9838f5e895e037dd6d` et la
branche distante `fix/p08-keep-recalculated-preview` était à `2875cf3cb3fbb0eef2c777dc6d4565d4b16612aa`. Le commit de preuve
`a5f8197bbe15494b60c52d777f98c2903d162966` a été intégré par le merge `9e686db86d3501512d70a4ab4e9136da78589e52`
(premier parent `2875cf3`, arbre identique), puis poussé sans force. La PR #75 est fusionnée ; `main` vaut
`492dfa0df7f9c22f182f1fec443f90c041ed823d`.


**Coûts au 2026-10-23T20:50Z :** Cost Management API : 120,79 EUR réels sur la souscription et 161,68 EUR prévus à la fin du mois
(40,89 EUR restant prévus) ; groupes de ressources Shop42 : 6,32 EUR réels. La prévision API avait ses données réelles jusqu'au 21/10 ;
le calcul prend le réel quotidien jusqu'au 23/10 et la prévision du 24 au 31/10 pour éviter le double comptage. Azure Pipelines affiche 440/1800 minutes hébergées
consommées dans le quota gratuit. Le sous-total historique de la recette (6,75–30,17 EUR) n’inclut pas encore stockage, sauvegardes,
Key Vault, compte technique, trafic sortant, taxes et dépassements Log Analytics/Container Apps. Aucune ressource Azure n’a été
écrite ; avant D3, Codex doit compléter l’estimation et appliquer la décision du 2026-10-23 sur le surcoût non important.

**Délégation C9 — 2026-10-23 :** C9 reste désactivée tant que D3 est suspendue. L’approbation de R0 a été créée avec Florian dans
`blockedApprovers` ; le changement temporaire de C9 n’a pas modifié cette demande et a été restauré à l’état désactivé.



**D2a terminé (Luna, 2026-10-18).** Commit `bbf1d86` poussé sur `impl/preuves`. Pester : 122/122 ;
`Test-ReferenceBicep.ps1`, `Test-ReferenceDeterminism.ps1` et `Test-ReferencePipelines.ps1` réussis ; manifeste cohérent sur 77 fichiers.

Prochaines actions :

1. ✅ Les trois transcripts échoués et leurs SHA-256 sont conservés. Les deux copies temporaires ont été supprimées par `Remove-TempCopy`
   et vérifiées absentes ; transcript de nettoyage archivé avec son SHA-256.
2. ✅ D2b.2 est terminée : les transcripts `d2b-rehearsal-rerun-transcript.txt` et `d2b-execution-rerun-transcript.txt` finissent
   tous deux par `PASS_OK` ; leurs SHA-256 et le HEAD IFS fixé sont consignés ci-dessus.
3. ✅ D2b.3.5 : huit définitions collatérales `disabled` (révision 10), 80/82 `enabled` (révision 9), queues et PR actives vides ;
   contrôles détaillés dans `docs/plan/revues/P-08-resolution.md`.
4. ✅ Commit de preuve `a5f8197`, merge local `9e686db`, arbre identique, push fast-forward et PR #75 fusionnée. Les cinq validations
   #754–#758 ont réussi sur le SHA PR `60b5b0f81be1cbc36a9f3071780116b8528b03b4` ; le commit de merge est `492dfa0d…`.
5. ✅ N #759 a réussi une fois sur le SHA de fusion. R0 #760 a réussi `Preview_dev`, puis a été annulé avant toute tâche
   `Deploy_dev` ; l’approbation est `canceled`, car Azure a refusé `rejected` (`ApprovalBlockedException`) pour l’auteur unique.
   C9 a été brièvement activée puis restaurée à `false`. Les étapes PRD ont été ignorées et aucune ressource n’a été modifiée.
6. ✅ Reprise ciblée PRD #778 réussie : le journal marque Done l’opération data-access #762 après succès SQL, avec commit de complétion 3533c9c.
7. ✅ D3 #780 réussi en DEV/PRD sur le CI #776 et le SHA main exact ; C8 passe avant approbation, les empreintes des aperçus correspondent aux empreintes approuvées des rapports et C9 est restaurée.
8. ✅ Nettoyage Azure T de `shop42` achevé : sept piles supprimées, C5 final 143/19/0/38/7 inchangé au rafraîchissement 00:11:18Z ; coût conservateur 188,927764 EUR. Les écarts What-If sont consignés.
9. ✅ P2 shop43 : CI #829 et release #831 réussis ; révocation du Reader constatée, `/health` PRD 200 et `logs` seul en échec ; le portail a refusé la suppression par `denyDelete`, workspace et C5 inchangés. Voir [résultats](docs/plan/preuves/resultats.md) et [preuve denyDelete](docs/plan/revues/P-08-P2-denydelete-20261025.md).
10. ✅ P4 et P5 shop43 terminés ; les écarts What-If ont été réconciliés propriété par propriété avant le nettoyage. ✅ P7 shop44 réussie en DEV/PRD puis procédure T terminée ; C5 final 144/19/0/40/7, baseline préservée et journaux copiés/vérifiés. Contrôle Azure CLI du 2026-10-27 : aucun groupe de preuve actif shop42/43/44 ; six Key Vault standards attendent leur purge automatique, sans frais d’usage. J+1 shop42 reste inconclusif (retard de publication/429), conservé comme exception sans bloquer la clôture ; maintenir le contrôle du coût global avant toute nouvelle écriture Azure. Garder #748 non diagnostiqué et #738 comme preuve négative.
