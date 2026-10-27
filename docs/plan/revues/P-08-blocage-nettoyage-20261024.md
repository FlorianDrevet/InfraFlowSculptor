# P-08 — blocage du nettoyage après le D3 #780

> **Instantané historique au 2026-10-24T20:38Z.** Les constats « aucun update » et « aucune suppression » ci-dessous étaient exacts à cette heure. La réconciliation, la suppression de `orders-dev` et la mise à jour de protection de `orders-prd` sont documentées dans [`P-08-nettoyage-suite-20261024.md`](P-08-nettoyage-suite-20261024.md).

## Blocage confirmé : CONFIRMÉ

Le D3 unique autorisé après la reprise PRD a été consommé par le run Azure DevOps #780. Le pipeline 82 a terminé `succeeded`
sur la ressource CI #776 et le commit `1abacadd605c0ca6c342ac305ff083be680d56c4`. Les aperçus DEV et PRD ont zéro création,
recréation, détachement/suppression, révocation et opération en attente ; `modified=5` et `dataWrites=1` ne font pas partie
des prédicats C8. Les rapports `ifs-report/v2` des deux cibles sont `Succeeded`, révision 2, commit `3533c9cca75f286976ab3e3ee46183e2f5c405b6`, sans erreur, détachement, révocation ou règle de pare-feu temporaire. Les empreintes approuvées égalent celles appliquées.

Le préflight T du nettoyage a ensuite exporté le modèle de la pile existante `ifs-shop42-orders-prd`, a réutilisé son commit
source et ses paramètres sans secret, puis a exécuté un What-If avec `FullResourcePayloads`. Le déploiement de pile courant
est `ifs-shop42-orders-prd-26102419cuo3w`. Le What-If rejoué le 2026-10-24T20:38:01Z confirme 5 ressources `Modify` et
2 `NoChange` (le groupe de ressources et une attribution de rôle) ; une propriété `principalType` de cette attribution est
marquée `Unsupported`. La sortie complète est conservée hors dépôt dans
`%LOCALAPPDATA%\InfraFlowSculptor\P08\whatif-orders-prd-cleanup-rerun-20261024T203801Z.txt`, SHA-256
`DA29CB67068AD9627EB25E94708D6AD8FE81C235B39E3EFDECB386956569609B`. Les cinq ressources
annoncées sont l’Container App `ca-shop42-api-prd`, son paramètre de diagnostic, l’environnement `cae-shop42-main-prd`,
son paramètre de diagnostic et l’identité managée `id-shop42-api-prd`. Les propriétés concernées incluent la configuration
d’identité, de trafic, d’images/secrets et d’échelle de l’application, la configuration des diagnostics, l’authentification
pair-à-pair et le réseau virtuel de l’environnement, ainsi que les propriétés de l’identité. Aucune valeur de secret n’est
consignée. La comparaison de payloads établit pour plusieurs propriétés scalaires que la valeur live correspond au côté
`before` du What-If, pas à la valeur proposée.

La recette [P-08, étape T, § 2](../recettes/01-preuves.md#procédure-t--suppression-dun-jeu-ou-dune-partie)
exige l’arrêt avant toute mise à jour de pile si le What-If annonce une modification de ressource. L’update `az stack sub create`
est un redéploiement ; poursuivre ferait donc courir un risque concret de modification de la configuration PRD. Le préflight
des IDs et de la baseline est complet, mais ne neutralise pas ce risque. Aucun update, changement de protection, `deleteAll`
ou suppression individuelle n’a été exécuté.

Le contrôle Cost Management du 2026-10-24T20:26:55Z donne 131,724309 EUR HT réels MTD, 41,098486 EUR de prévision restante,
172,822795 EUR projetés fin octobre et 185,323195 EUR dans le scénario conservateur incluant les runs réalisés, le D3 et
2 Go de logs. Le budget préexistant de 200 EUR est une alerte, pas un plafond ; aucun seuil d’urgence n’est actuellement
constaté. L’exception d’urgence de la recette T ne s’applique donc pas. L’autorisation d’un seul D3 a été consommée ; une
réconciliation supplémentaire nécessiterait un nouvel arbitrage sur les changements PRD à accepter.

## Vérifications effectuées

- Run #780 et ses quatre étapes de preuve conservés dans `src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/p8-d3-run-780/` ; le run #779 échoué avant les tâches est également archivé.
- Rapports DEV/PRD téléchargés hors dépôt sous `%LOCALAPPDATA%\InfraFlowSculptor\P08\D3-report-dev-run780\` et `D3-report-prd-run780\` ; les deux contiennent `ifs-report/v2`, résultat `Succeeded`, révision 2, commit identique et erreur vide.
- C3 après #780 : aucun run actif/en file ni PR active ; les définitions collatérales restent désactivées, les définitions 80/82 restent activées. C9 est restaurée à `requesterCannotBeApprover=true`. Capture hors dépôt : `%LOCALAPPDATA%\InfraFlowSculptor\P08\c3-c9-after-D3-run780-20261024T195251Z.json`.
- C5 après #780 : 164 ressources, 26 groupes, 7 piles, 59 attributions de rôle et 7 budgets ; les cinq collections restent identiques au relevé post-#778, les 21 IDs Shop42 suivis sont présents. Le préflight reconstitue la baseline du 2026-10-15 à partir des IDs historiques : 30 ressources Shop42 et 10 groupes attribués à Luna, tous retrouvés ; aucun ID manquant. Captures hors dépôt : `%LOCALAPPDATA%\InfraFlowSculptor\P08\c5-after-D3-run780-20261024T195132Z.json`, `shop42-cleanup-preflight-20261024T2002Z.json` et `baseline-20261015T1229Z-reconstructed-20261024.json`.
- Les paramètres de `orders-prd` ont été comparés aux paramètres consignés par la pile et par son déploiement courant : mêmes noms et valeurs. Le modèle exporté de la pile a été comparé au Bicep du commit exact `1abacadd…` ; les deux prévoient les mêmes cinq ressources `Modify`. Le What-If exact, son SHA-256 et le préflight sont hors dépôt sous `%LOCALAPPDATA%\InfraFlowSculptor\P08\whatif-orders-prd-cleanup-rerun-20261024T203801Z.txt`, `shop42-cleanup-preflight-20261024T2002Z.json`, `template-from-stack-ifs-shop42-orders-prd.json` et `whatif-parameters\ifs-shop42-orders-prd.json`.
- Cost Management actualisé après #780 à 20:26:55Z : 131,724309 EUR HT réels, 41,098486 EUR prévus et projection 172,822795 EUR. Réponses hors dépôt sous `%LOCALAPPDATA%\InfraFlowSculptor\P08\p08-cost-actual-refresh-20261024T202655Z.json` et `p08-cost-forecast-refresh-20261024T202655Z.json`.
- À l’heure de cet instantané, aucun contrôle n’autorisait l’update de la pile et aucune ressource Azure n’avait été supprimée. La suite des opérations et l’écart What-If sont consignés dans la note liée ci-dessus.
