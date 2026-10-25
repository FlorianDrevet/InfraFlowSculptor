# Blocage P-08 — périmètre des preuves restantes — 2026-10-25

Blocage confirmé : CONFIRMÉ

## Étape et condition manquante

P-08 doit exécuter la recette complète dans l’ordre prévu. Le nettoyage de `shop42` est achevé, mais P2, P4 et P5 sont prévues avec le jeu isolé `shop43`, puis P7 avec `shop44` ([recette des preuves, sections 7 à 9](../recettes/01-preuves.md)). Aucune ligne de résultat ni capture `p2`, `p4`, `p5` ou `p7` n’est présente dans le dépôt.

Les consignes de Florian pour cette session autorisent les opérations de preuve dans la souscription Azure de test et le projet Azure DevOps `shop42`, avec ses dépôts associés, et interdisent les modifications hors de ce périmètre. Elles n’autorisent pas les écritures dans les projets/jeux `shop43` et `shop44`. Aucun déploiement, run ou changement n’a été effectué sur ces jeux.

La procédure T § 7 impose aussi une vérification *Cost Analysis* le 2026-10-26, après la suppression `shop42`. Elle n’est pas encore due ni exécutée ; elle reste une condition distincte avant la clôture de P-08.

## Actualisation des coûts — 2026-10-25T00:28Z

- Cost Management (devise EUR, `PreTaxCost`) : réel MTD **133,281523 EUR** jusqu’au 24/10 ; prévision du 25 au 31/10 **38,118441 EUR** ; projection **171,399964 EUR**. En ajoutant la réserve prudente existante de **17,527800 EUR** pour 2 Go de logs, le scénario de contrôle est **188,927764 EUR**, soit **11,072236 EUR** sous l’alerte non plafonnante de 200 EUR avant les jeux restants.
- Coût réel regroupé par ressources `shop42` : **6,972212 EUR**, dont SQL (données stockées **2,792711 EUR**, vCore **2,425404 EUR**), registre ACR Basic **1,198257 EUR**, Container Apps **0,378320 EUR**, Log Analytics **0,173581 EUR**, puis Key Vault, stockage et trafic pour moins de 0,004 EUR cumulés. Les valeurs et requêtes sont conservées sous `%LOCALAPPDATA%/InfraFlowSculptor/P08/session-preflight-20261025T002809Z/` (`actual.json`, `forecast.json`, `actual-by-group-meter.json`).
- L’estimation France Central de la recette chiffre **6,75–30,17 EUR** pour les trois jeux avec les hypothèses listées, mais exclut les coûts annexes. Au prorata de deux jeux, la fourchette connue seule est **4,50–20,11 EUR** ; additionnée au scénario courant, elle donne **193,43–209,04 EUR** avant les postes encore non chiffrés (stockage/sauvegardes, dépassement Container Apps, trafic, taxes) et avant éventuel dépassement des heures actives estimées. Ce calcul sert au triage, pas d’estimation complète ni de plafond.
- Conclusion coût : l’alerte de 200 EUR peut être dépassée même dans la borne tarifaire connue seule, et les postes annexes restent incomplets. Avant une écriture Azure facturable, compléter l’estimation et obtenir le plafond précis demandé par la procédure § 0.1 si le coût demeure important.

## Actualisation du périmètre ADO — 2026-10-25T00:35Z

Les lectures `az devops project list` et `az repos list --project shop42` montrent respectivement les projets ADO `AI Catalog`, `shop42`, `DS - Magellan` et `Infra Flow Sculptor`, puis le seul dépôt `shop42` dans le projet autorisé. `shop43` et `shop44` n’existent pas comme projets de test. Aucun projet, dépôt ou autre objet ADO n’a été créé ou modifié. L’alternative de dépôts séparés dans `shop42` resterait une adaptation à la recette et ne peut pas être choisie sans arbitrage documenté.

## Options ouvertes

1. Florian étend explicitement l’autorisation aux projets/dépôts et ressources de test `shop43` et `shop44` ; Codex exécute ensuite P2/P4/P5/P7 après le contrôle de coût J+1 et une estimation complète fraîche avant toute écriture.
2. Florian fournit des preuves existantes pour P2/P4/P5/P7 à examiner en lecture seule.
3. Le périmètre reste limité à `shop42` ; P-08 demeure bloquée et une décision explicite sur la modification du plan est nécessaire avant d’omettre ces scénarios.

Aucune option n’est choisie ici. Question précise : Florian autorise-t-il les écritures requises sur `shop43` et `shop44`, ou dispose-t-il de preuves existantes à examiner en lecture seule ?

## Vérifications effectuées

- `python tools/plan/gate.py check` autorisait P-08 au statut `EN_COURS` avant ce blocage ; le gate avait été rouvert à P-08 afin d’exécuter les preuves restantes avant R-03.
- La recette `docs/plan/recettes/01-preuves.md` assigne P2/P4/P5 à `shop43` et P7 à `shop44`.
- L’inventaire des sous-répertoires de `src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/` ne contient aucun dossier `p2`, `p4`, `p5` ou `p7` ; `docs/plan/preuves/resultats.md` ne contient aucune ligne pour ces preuves.
- `python tools/plan/gate.py lint` : plan cohérent, 105 étapes et 18 verrous.
- `Invoke-Pester -Path reference/release-module/tests -Output Minimal -PassThru` : 133 tests réussis, 0 échec, 0 ignoré.
- `az devops project list` : `shop42` existe ; aucun projet `shop43`/`shop44`. `az repos list --project shop42` : dépôt `shop42` seul.
- Cost Management à 00:28:13Z : réel 133,281523 EUR, prévision 38,118441 EUR, réserve comprise 188,927764 EUR ; C5 à 00:28:36Z : 143 ressources, 19 groupes, 0 pile, 38 rôles, 7 budgets.
- Le contrôle de coût après suppression de `shop42` n’a pas été anticipé : il est dû le 2026-10-26 selon T § 7.

## Suite

Après réponse de Florian, consigner l’extension de périmètre ou l’option d’adaptation, ainsi qu’un plafond de coût applicable. Le contrôle J+1 du 2026-10-26 reste requis. Ensuite, actualiser les coûts et terminer l’estimation complète avant toute écriture ; exécuter seulement dans la portée et sous le plafond confirmés.
