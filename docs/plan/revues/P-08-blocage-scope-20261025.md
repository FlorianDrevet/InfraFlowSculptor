# Blocage P-08 — périmètre des preuves restantes — 2026-10-25

Blocage confirmé : CONFIRMÉ

## Étape et condition manquante

P-08 doit exécuter la recette complète dans l’ordre prévu. Le nettoyage de `shop42` est achevé, mais P2, P4 et P5 sont prévues avec le jeu isolé `shop43`, puis P7 avec `shop44` ([recette des preuves, sections 7 à 9](../recettes/01-preuves.md)). Aucune ligne de résultat ni capture `p2`, `p4`, `p5` ou `p7` n’est présente dans le dépôt.

Les consignes de Florian pour cette session autorisent les opérations de preuve dans la souscription Azure de test et le projet Azure DevOps `shop42`, avec ses dépôts associés, et interdisent les modifications hors de ce périmètre. Elles n’autorisent pas les écritures dans les projets/jeux `shop43` et `shop44`. Aucun déploiement, run ou changement n’a été effectué sur ces jeux.

La procédure T § 7 impose aussi une vérification *Cost Analysis* le 2026-10-26, après la suppression `shop42`. Elle n’est pas encore due ni exécutée ; elle reste une condition distincte avant la clôture de P-08.

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
- Le contrôle de coût après suppression de `shop42` n’a pas été anticipé : il est dû le 2026-10-26 selon T § 7.

## Suite

Après réponse de Florian, consigner l’autorisation ou les preuves reçues. Si la portée est étendue, relever coûts et prévision, établir l’estimation complète, puis exécuter les preuves sans dépasser cette portée. Si la portée reste limitée, ne pas créer de ressource et attendre une décision explicite de plan.
