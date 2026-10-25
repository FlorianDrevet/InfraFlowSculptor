# Blocage P-08 — périmètre des preuves restantes — 2026-10-25

Blocage résolu : CONFIRMÉ

## Étape et condition initialement manquante

P-08 doit exécuter la recette complète dans l’ordre prévu. Le nettoyage de `shop42` est achevé, mais P2, P4 et P5 sont prévues avec le jeu isolé `shop43`, puis P7 avec `shop44` ([recette des preuves, sections 7 à 9](../recettes/01-preuves.md)). Aucune ligne de résultat ni capture `p2`, `p4`, `p5` ou `p7` n’est présente dans le dépôt.

Le 2026-10-25, Florian a explicitement répondu : « Oui, autoriser shop43 et shop44 ». Cette autorisation étend la portée de preuve aux projets/dépôts Azure DevOps et aux ressources de test `shop43` et `shop44` dans la souscription de test. Les projets, dépôts et groupes préparatoires ont depuis été créés selon la recette ; aucun pipeline ni ressource Azure n’a été créé pour ces jeux.

La procédure T § 7 impose aussi une vérification *Cost Analysis* le 2026-10-26, après la suppression `shop42`. Elle n’est pas encore due ni exécutée ; elle reste une condition distincte avant la clôture de P-08.

## Actualisation des coûts — 2026-10-25T00:28Z

- Cost Management (devise EUR, `PreTaxCost`) : réel MTD **133,281523 EUR** jusqu’au 24/10 ; prévision du 25 au 31/10 **38,118441 EUR** ; projection **171,399964 EUR**. En ajoutant la réserve prudente existante de **17,527800 EUR** pour 2 Go de logs, le scénario de contrôle est **188,927764 EUR**, soit **11,072236 EUR** sous l’alerte non plafonnante de 200 EUR avant les jeux restants.
- Coût réel regroupé par ressources `shop42` : **6,972212 EUR**, dont SQL (données stockées **2,792711 EUR**, vCore **2,425404 EUR**), registre ACR Basic **1,198257 EUR**, Container Apps **0,378320 EUR**, Log Analytics **0,173581 EUR**, puis Key Vault, stockage et trafic pour moins de 0,004 EUR cumulés. Les valeurs et requêtes sont conservées sous `%LOCALAPPDATA%/InfraFlowSculptor/P08/session-preflight-20261025T002809Z/` (`actual.json`, `forecast.json`, `actual-by-group-meter.json`).
- L’estimation France Central de la recette chiffre **6,75–30,17 EUR** pour les trois jeux avec les hypothèses listées, mais exclut les coûts annexes. Au prorata de deux jeux, la fourchette connue seule est **4,50–20,11 EUR** ; additionnée au scénario courant, elle donne **193,43–209,04 EUR** avant les postes encore non chiffrés (stockage/sauvegardes, dépassement Container Apps, trafic, taxes) et avant éventuel dépassement des heures actives estimées. Ce calcul sert au triage, pas d’estimation complète ni de plafond.
- Conclusion coût : l’alerte de 200 EUR peut être dépassée même dans la borne tarifaire connue seule, et les postes annexes restent incomplets. Avant une écriture Azure facturable, compléter l’estimation et obtenir le plafond précis demandé par la procédure § 0.1 si le coût demeure important.

## Actualisation du périmètre ADO — 2026-10-25T00:35Z

Les lectures `az devops project list` et `az repos list --project shop42` montrent respectivement les projets ADO `AI Catalog`, `shop42`, `DS - Magellan` et `Infra Flow Sculptor`, puis le seul dépôt `shop42` dans ce projet. `shop43` et `shop44` n’existent pas encore comme projets de test. Leur création est autorisée par la réponse explicite de Florian du 2026-10-25. Aucun projet, dépôt ou autre objet ADO n’a encore été créé ou modifié.

## Projets de preuve préparés — vérification 2026-10-25T09:46Z

- `shop43` : projet privé Basic/Git `ef568519-cbcc-4f87-8c69-a8af216b1b14`; dépôt Git `shop43` `dd5df33a-ac99-42cb-b4b2-3bf34b82bf32`, branche par défaut `main`, commit initial `cc81768b48985c54fb37bb9264c2efb082b8f342`, SHA distant égal au clone local `C:\Users\flori\RiderProjects\shop43`.
- `shop44` : projet privé Basic/Git `b526fffe-311a-4a5c-badc-29a60b64279b`; dépôt Git `shop44` `cb3f4b63-acaf-4907-bef2-0ae8e111d7ef`, branche par défaut `main`, commit initial `9925eec9e027fa9733850eff1386cd2d472b3271`, SHA distant égal au clone local `C:\Users\flori\RiderProjects\shop44`.
- Les groupes SQL sont `sg-shop43-sql-admins` (`7e27d920-aab1-4522-8544-da597c6421b8`) et `sg-shop44-sql-admins` (`a10d6bcc-7254-4be2-bb12-4d2c1c3cbdef`), chacun avec un membre, identique au groupe SQL Shop42.
- Les groupes ADO de portée projet `Shop43 Release Approvers` (`39e9d1cd-95f0-4e8c-8337-70068211ab92`) et `Shop44 Release Approvers` (`618a902e-4b85-4c93-8ef7-23fa283c4af9`) ont chacun un membre, identique au groupe d’approbation Shop42.
- `Publish-PilotReference.ps1` a validé puis publié 79 fichiers de référence et 22 fichiers témoins par projet. Les quatre compilations `main.bicep` et les sept compilations `.bicepparam` sont réussies pour chaque clone ; les paramètres SQL/Container Apps ont `zoneRedundant: false`, sans code Shop42 ni placeholder résiduel. `Test-ReferenceDeterminism.ps1` passe sur la référence pipeline (79 fichiers UTF-8 sans BOM, LF, saut final et en-tête attendu).
- Aucun pipeline, run, variable secrète, connexion de service ou ressource Azure n’a été créé pour `shop43` ou `shop44`.

## Résolution de la portée

L’option d’extension est choisie par l’autorisation explicite ci-dessus. P2/P4/P5 dans `shop43` et P7 dans `shop44` restent dans l’ordre et selon la recette écrite ; aucune adaptation du plan n’est nécessaire. L’autorisation ne lève pas le verrou de coût : aucune écriture Azure facturable ne sera faite avant le contrôle J+1 du 2026-10-26, une estimation complète et fraîche, et un plafond explicite si le surcoût demeure important.

## Vérifications effectuées

- `python tools/plan/gate.py check` autorisait P-08 au statut `EN_COURS` avant ce blocage ; le gate avait été rouvert à P-08 afin d’exécuter les preuves restantes avant R-03.
- La recette `docs/plan/recettes/01-preuves.md` assigne P2/P4/P5 à `shop43` et P7 à `shop44`.
- L’inventaire des sous-répertoires de `src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/` ne contient aucun dossier `p2`, `p4`, `p5` ou `p7` ; `docs/plan/preuves/resultats.md` ne contient aucune ligne pour ces preuves.
- `python tools/plan/gate.py lint` : plan cohérent, 105 étapes et 18 verrous.
- `Invoke-Pester -Path reference/release-module/tests -Output Minimal -PassThru` : 133 tests réussis, 0 échec, 0 ignoré.
- Avant leur création, `az devops project list` relu après l’autorisation ne montrait que le projet de test `shop42`, et `az repos list --project shop42` ne montrait que son dépôt `shop42`.
- Après leur création, `az devops project show`, `az repos show`, `az devops security group membership list`, `az ad group show`, `az ad group member list` et `git ls-remote origin refs/heads/main` confirment les IDs, les groupes et les branches/commits ci-dessus.
- Les prépublications `Publish-PilotReference.ps1 -WhatIf` ont confirmé 79 fichiers de référence et 22 fichiers témoins, sans modifier les clones ; les deux publications réelles ont passé leurs vérifications internes.
- Pour chaque clone, `az bicep build` a réussi sur core/data/orders/platform ; `az bicep build-params` a réussi sur les deux cibles core/data/orders et la cible shared platform. Aucun avertissement Bicep n’a été produit.
- `Test-ReferenceDeterminism.ps1 -PipelineRoot reference/pilot/bicep-azdo` : 79 fichiers validés. Une invocation exploratoire sur la racine entière du clone incluait aussi deux fichiers témoins écrits à la main, hors périmètre de ce test ; les sources n’ont pas été modifiées.
- Cost Management à 00:28:13Z : réel 133,281523 EUR, prévision 38,118441 EUR, réserve comprise 188,927764 EUR ; C5 à 00:28:36Z : 143 ressources, 19 groupes, 0 pile, 38 rôles, 7 budgets.
- Le contrôle de coût après suppression de `shop42` n’a pas été anticipé : il est dû le 2026-10-26 selon T § 7.

## Suite

Continuer les seuls préflights en lecture seule et la préparation locale/ADO. Le contrôle J+1 du 2026-10-26 reste requis. Avant toute écriture Azure facturable, actualiser les coûts et terminer l’estimation complète ; demander un plafond précis si le coût demeure important, puis exécuter seulement dans la portée et sous le plafond confirmé.
