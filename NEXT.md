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
| **Verrou** | aucun |
| **Branche** | `impl/preuves` (empilée sur `impl/socle` jusqu’à sa fusion) |
| **Dernière mise à jour** | 2026-10-25 — Codex (nettoyage P-08 terminé, C5 final rafraîchi) |
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
| Décision de coût et estimation des preuves ([contrôle](docs/plan/recettes/01-preuves.md#01-surveillance-des-coûts-avant-toute-action-sur-les-ressources-azure)) | ActualCost relu après `core-prd` à 00:07:01Z : 133,281523 EUR HT MTD. Forecast restant du 25 au 31/10 : 38,118441 EUR ; projection 171,399964 EUR. Avec la réserve de 17,527800 EUR (2 Go de logs inclus), estimation conservatrice 188,927764 EUR, soit 11,072236 EUR sous l’alerte non plafonnante de 200 EUR. Réponses : `%LOCALAPPDATA%/InfraFlowSculptor/P08/core-prd-reconcile/p08-cost-actual-after-delete-20261025T000701Z.json` et `p08-cost-forecast-after-delete-20261025T000701Z.json`. | 2026-10-25 |
| Ressources Azure créées pour les preuves ([inventaire](docs/plan/preuves/inventaires-azure.md)) | Les sept piles éphémères (`orders-dev/prd`, `data-dev/prd`, `platform-shared`, `core-dev/prd`) sont supprimées. C5 après `core-prd` à 00:06:45Z : 143 ressources, 19 groupes, 0 pile, 38 rôles et 7 budgets ; deltas exacts −4 ressources, −1 groupe, −1 pile, −3 rôles, 0 budget, aucun ajout. Rafraîchissement C5 à 00:11:18Z : cinq collections identiques. Preuve : `%LOCALAPPDATA%/InfraFlowSculptor/P08/final-c5-refresh-20261025T001110Z/c5-final.json`. | 2026-10-25 |
| Organisation / projet Azure DevOps de test | PR #77 fusionnée sur `main` au SHA `1abacadd605c0ca6c342ac305ff083be680d56c4` ; N #776 réussi, R0 #777 annulé avant écriture, reprise ciblée #778 réussie, D3 #779 échoué avant tâches et unique D3 corrigé #780 réussi en DEV/PRD. C3 post-nettoyage à 00:06:45Z : aucune build active/en file, release active ou PR active ; 17 définitions, 9 activées et 8 désactivées ; 80/82 activées et C9 restaurée à `requesterCannotBeApprover=true`. Capture hors dépôt : `%LOCALAPPDATA%/InfraFlowSculptor/P08/core-prd-reconcile/c3-after-delete-20261025T000645Z.json`. | 2026-10-25 |
| Droits Azure utilisés pour D1b | `Storage Blob Data Reader` au conteneur `ifs-operations` du compte `stifsshop42dev` et `Key Vault Reader` au coffre `kv-shop42-main-dev`, attribués à l’identité personnelle pour les lectures D1b. Les deux attributions restent en place ; aucune valeur de secret n’a été lue | 2026-10-17 |
| Groupes Entra (`sg-shop42-sql-admins`) et Azure DevOps (« Shop42 Release Approvers ») | Créés dans le tenant fourni ; Florian DREVET est membre des deux groupes | 2026-10-15 |
| Résultat D3 — vérification C1–C9 | D3 #780 est terminé avec succès sur le CI #776 et le SHA main exact. Les aperçus DEV/PRD ont zéro opération pending, création, recréation, détachement/suppression ou révocation ; `modified=5` et `dataWrites=1` sont hors des prédicats C8. Les rapports `ifs-report/v2` DEV/PRD sont `Succeeded`, révision 2, commit `3533c9c…`, erreur vide ; leurs empreintes approuvées correspondent aux empreintes des aperçus publiés, sans détachement, révocation ou pare-feu temporaire. C3 après run : aucune exécution active/en file ni PR active ; C9 restaurée à `true`. C5 conserve les cinq collections d’IDs et les 21 IDs Shop42 suivis. C6 après run confirme les apps DEV/PRD Running, saines et HTTP 200 sur le digest D2. Captures #779–#780 dans `src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/` ; rapports et préflight C5/C6 hors dépôt sous `%LOCALAPPDATA%/InfraFlowSculptor/P08/D3-report-*-run780/`, `c5-after-D3-run780-20261024T195132Z.json` et `c6-after-D3-run780-20261024T195239Z.json`. [Analyse C8 et reprise](docs/plan/revues/P-08-C8-analyse-20261024.md). | 2026-10-24 |
| Inscriptions Entra d'IFS (`dev`) | — | — |
| Protection de la branche `main` sur GitHub | À activer par vous dans *Settings → Branches* | 2026-10-06 |
| Secret de dépôt GitHub Actions `IFS_E2E_PASSWORD` | Configuré (valeur masquée) | 2026-10-06 |

## Recettes autonomes en attente de Codex

- S-02 — [ouvrir `src/backend/InfraFlowSculptor.slnx` dans Rider](docs/plan/00-socle.md#s-02--squelette-backend-généré-depuis-le-template-cqrs) : vérifier les cinq projets et l'absence de références à `Web.Template.CQRS`.
- S-03 — ouvrir `http://localhost:5257/v1/inexistant` dans un navigateur et vérifier le JSON 404 avec `traceId` ([étape](docs/plan/00-socle.md#s-03--moderniser-le-squelette-évolutions-de-vole-papillon-damour)); l'API a été vérifiée par HTTP, mais Chrome a bloqué la navigation directe.
- S-04 — ouvrir la solution dans Rider, vérifier les cinq projets de test puis lancer *Run All* ([étape](docs/plan/00-socle.md#s-04--projets-de-tests-et-règles-darchitecture)); `dotnet test` passe, mais aucune fenêtre native n'est exposée à l'automatisation.
- S-17 — sur un clone neuf, suivre [README — Démarrer](README.md#démarrer-windows) jusqu'à « Bonjour Alice Martin ». Les prérequis passent ; le démarrage du clone reste à vérifier quand les ports fixes 4200, 8080, 5257 et 3000 seront libres, car l'AppHost de travail reste actif.
Codex exécute chaque 🧪, consigne le résultat dans [`docs/plan/recettes/suivi.md`](docs/plan/recettes/suivi.md), puis retire la ligne.

## Points ouverts et historique des décisions

**État courant au 2026-10-25 :** le D3 #780 et le nettoyage T sont achevés. Les sept piles éphémères sont absentes ; le C5 final a été relu deux fois sans différence, C3 est vide et le scénario de coût conservateur reste sous l’alerte existante. Les écarts What-If (ordres PRD, ACR partagé et valeurs par défaut des piles core) sont conservés comme écarts, sans être qualifiés de conformes. Aucun point bloquant P-08 ne reste ouvert ; l’étape doit être consignée puis le verrou R-03 auto-revu.

### P-08 — suite du nettoyage des piles

Le blocage documenté au préflight de 20:38Z est historique ; la consigne de continuation de Florian du 2026-10-25
autorise à résoudre les blocages et à poursuivre la recette. La réconciliation ciblée `ifs-shop42-orders-prd-261024215yke9`
a réussi, `orders-dev` a été supprimé et `ifs-shop42-orders-prd-26102421h5csw` a terminé avec
`denySettings.mode = none`.

Le What-If exact conserve 5 `Modify`, 2 `NoChange` et 3 `Unsupported`. La divergence `Dedicated`/`null` des diagnostics
reste connue et n’est pas présentée comme résolue ; la mise à jour de pile a été appliquée sous la consigne de continuation,
puis contrôlée : 10 IDs présents, zéro ressource en échec, application Healthy/HTTP 200, C3 sans exécution active et C5 sans
changement de collections. C’est un écart à la garde What-If de la procédure T, consigné pour l’auto-revue.

Les suppressions `orders-dev`, `orders-prd`, `data-dev` et `data-prd` sont terminées. Le `deleteAll` de `ifs-shop42-data-prd` a fini à 23:07:29Z avec le code 0 ; son C5 confirme exactement −3 ressources, −1 groupe et −1 pile, sans changement des rôles/budgets ni ajout. Le C5 frais avant `platform-shared` (23:15:21Z) est identique sur les cinq collections : 152 ressources, 22 groupes, 3 piles, 49 rôles et 7 budgets. C3 à 23:10:25Z est sans build actif/en file, release active ou PR active.

Le préflight initial de `platform-shared` (23:15:59Z) a confirmé la pile `succeeded`, `denyDelete`, 3 IDs gérés et zéro échec. Le groupe contient uniquement le registre ACR ; le groupe, le registre, la pile et les quatre rôles de portée groupe/registre sont attribués à Luna et absents de la baseline. Le What-If exact annonce 1 `Modify`, 2 `NoChange`, 1 `NoEffect`, aucune création/suppression et aucun `Unsupported`. La différence ACR sur `encryption.status=disabled` et `roleAssignmentMode=LegacyRegistryPermissions` correspond aux valeurs live omises du modèle ; le modèle propose déjà `networkRuleBypassAllowedForTasks=false`, comme l’état live. Cet écart reste déclaré, le What-If n’est pas qualifié de propre. Le modèle exact a été réappliqué à 23:18:33Z ; l’update a fini à 23:19:57Z, état `succeeded`, `denySettings.mode=none`, 3 IDs gérés, zéro échec, actions `detach` et `resourcesWithoutDeleteSupport=fail` conservées. Le What-If après update garde la même empreinte et les mêmes écarts ; l’ACR live a conservé ses valeurs. Le C5 après update (23:20:39Z) confirme les cinq collections inchangées à 152/22/3/49/7. C3 à 23:20:38Z est vide. Après deux réponses HTTP 429, ActualCost a réussi à 23:23:09Z à 133,281523 EUR MTD ; Forecast réussi à 23:20:31Z : 38,511544 EUR. Projection conservatrice avec réserve : 189,320867 EUR, sous l’alerte non plafonnante de 200 EUR. Le préflight final `%LOCALAPPDATA%/InfraFlowSculptor/P08/platform-shared-before-delete-final-20261024T232341Z.json` passe ; le `deleteAll` est prêt.

Preuves : [`P-08-nettoyage-suite-20261024.md`](docs/plan/revues/P-08-nettoyage-suite-20261024.md) ; snapshot historique du
blocage : [`P-08-blocage-nettoyage-20261024.md`](docs/plan/revues/P-08-blocage-nettoyage-20261024.md) ; préflight complet
hors dépôt : `%LOCALAPPDATA%\InfraFlowSculptor\P08\shop42-cleanup-preflight-20261024T2002Z.json`.

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
8. ✅ Nettoyage T achevé le 2026-10-25 : les sept piles éphémères sont supprimées ; C5 final rafraîchi à 00:11:18Z = 143 ressources, 19 groupes, 0 pile, 38 rôles, 7 budgets, sans différence avec l’après-suppression. C3 est vide ; coût conservateur 188,927764 EUR, sous l’alerte. Les écarts What-If restent documentés dans `docs/plan/revues/P-08-nettoyage-suite-20261024.md`.
9. ▶ Commiter P-08 avec le message du plan, exécuter `gate.py done P-08`, puis effectuer et consigner l’auto-revue R-03. Garder #748 non diagnostiqué et #738 comme preuve négative.
