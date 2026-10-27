# P-08 — blocage P1 shop44 : secret de test absent — 2026-10-26

Blocage confirmé : CONFIRMÉ

La release core #901 est déclenchée par la CI #900 fusionnée sur `main` au SHA `c4a6e2ef3758a03e83f6a2dac1395d40cd47c652`. Le pré-aperçu DEV s'arrête avant de publier `ifs-preview-dev` : `Test-IfsSecretVariable` constate que `MAIN_PAYMENTS_API_KEY` est vide dans le contexte d'exécution du groupe `ifs-shop44-dev`. Les stages `Deploy_dev`, `Preview_prd` et `Deploy_prd` sont ignorés. Aucun déploiement ni aucune écriture de ressource Azure n'a eu lieu.

## Vérifications effectuées

- CI #900 réussie sur le SHA de `main` ; `triggerInfo` de la release #901 confirme la ressource CI #900, la branche `main` et le SHA attendu.
- Journaux anonymisés de #901 : [timeline](../../../src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/p1-shop44-core-release-run901/timeline.json), [échec du contrôle de variable](../../../src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/p1-shop44-core-release-run901/logs/log-9.txt), [run](../../../src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/p1-shop44-core-release-run901/build.json). Aucun artefact de preview ou rapport d'application n'a été publié.
- Les groupes de variables ADO `ifs-shop44-dev` (ID 12) et `ifs-shop44-prd` (ID 13) déclarent chacun `MAIN_PAYMENTS_API_KEY`. Aucun contenu secret n'a été lu. Le journal d'exécution DEV prouve que la valeur effective est vide.
- C5 avant #900 et après #901 : mêmes IDs, exactement `153 ressources / 27 groupes / 0 pile / 55 rôles / 7 budgets`. Comparaison détaillée hors dépôt sous `%LOCALAPPDATA%/InfraFlowSculptor/P08/predeploy-shop44-20261026T172310Z/` et `%LOCALAPPDATA%/InfraFlowSculptor/P08/post-core-run901-20261026T171924Z/`.
- C3 après #901 : aucune exécution active ou en file, aucune PR active.
- Cost Management après #901 : ActualCost MTD `149,768872 EUR HT` (données jusqu'au 26/10), Forecast du 27 au 31/10 `23,049078 EUR`, projection `172,817950 EUR HT`. Avec la borne restante P1+P7 de `15,00 EUR`, projection prudente `187,817950 EUR HT`, sous l'alerte préexistante de 200 EUR. Ce seuil est une alerte, pas un plafond.

## Point ouvert

Rétablir dans les deux groupes ADO `ifs-shop44-dev` et `ifs-shop44-prd` une valeur de test approuvée pour `MAIN_PAYMENTS_API_KEY`, sans transmettre la valeur dans cette conversation, le dépôt, un journal ou la mémoire. Si aucune valeur approuvée n'est disponible, préciser quel mécanisme éphémère est autorisé ; la consigne du dépôt interdit de créer une nouvelle clé persistante. Après confirmation que les groupes sont prêts, refaire le contrôle de présence puis reprendre P1 depuis la CI core. Aucun déploiement n'a été lancé depuis l'échec #901.
