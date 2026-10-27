# Clôture P-08 — nettoyage shop44 — 2026-10-27

## Décision de nettoyage

Florian a délégué le choix du mécanisme de réconciliation What-If des piles protégées. La règle appliquée est exacte et locale à chaque propriété : comparaison avec le modèle et les paramètres effectivement déployés, preuve des écarts, puis mise à jour de la pile à partir du contenu exact et de ses paramètres effectifs pour retirer `denyDelete` avant `deleteAll`. Aucun seuil général de tolérance n’a été appliqué. Les différences What-If sont conservées dans les diagnostics comme telles.

## Résultat Azure

- Les 7 piles de preuve shop44, les 64 ressources inscrites à l’inventaire, les 3 groupes du kit (9 ressources) et les 21 affectations de rôle inventoriées sont absents.
- Le C5 final compte 144 ressources, 19 groupes, 0 pile, 40 rôles et 7 budgets. Tous les IDs de la baseline restent présents. Les seuls ajouts depuis la baseline sont deux ressources VPD et leurs deux affectations de rôle ; elles sont hors périmètre et conservées. Le stockage et le container group `jetekf4xhml6iazscripts` ont également été préservés.
- C3 rapporte zéro build active/en file, zéro release classique active et zéro PR active dans `shop42`.
- Les 67 versions des blobs de journal, dont 7 versions courantes, ont été copiées hors Azure puis vérifiées par SHA-256. Les lecteurs temporaires DEV/Shared ont été révoqués et relus absents.
- Les tombstones `kv-shop44-main-dev` et `kv-shop44-main-prd` sont conservés en soft-delete ; purge prévue le 2027-01-25, aucune purge manuelle.

## Contrôle du coût et écart de recette

À `2026-10-27T02:53:50Z`, Cost Management retourne `153,046179 EUR` d’ActualCost jusqu’au 26/10 et `31,936759 EUR` de prévision pour le 27–31/10. Avec la réserve de `15 EUR`, le total prudent est `199,982937 EUR`, soit `0,017063 EUR` sous l’alerte non plafonnante de `200 EUR`. Les valeurs sont `PreTaxCost`; les taxes contractuelles n’y figurent pas. Une requête supplémentaire groupée par nom de groupe a reçu HTTP 429 ; elle n’a pas été relancée. Florian a demandé de ne pas attendre une nouvelle journée de publication et de privilégier la limite de coût global. La mesure globale est postérieure au nettoyage de shop42 ; aucune écriture Azure facturable supplémentaire n’est nécessaire.

## Résultat P7 et écart consigné

Le run #933 a utilisé `IFS_TEST_ABORT_AFTER=deploy-unit` pour interrompre le processus après la mutation de pile. Sa capture montre `deploy-unit` rev2 à `Started` et `data-access` rev2 à `ToDo`. Le journal ne contenait pas encore d’opération `revoke`, contrairement à la séquence attendue dans la recette ; la révocation a été introduite par rev3. Le run #959 a ensuite terminé en `PartiallyApplied` sur `AuthorizationFailed` pendant cette révocation. Le run #960, après approbation PRD, a réussi en DEV/PRD sur le CI #956 et le SHA `3a8550f1c3ede8008d62f3739d1e038180b8b24d`. La dernière version de journal préservée montre les IDs initiaux rev2 `deploy-unit` et `data-access` en `Done`, sans doublon, et l’opération `revoke` rev3 en `Done`.

La preuve démontre la reprise des opérations effectivement enregistrées à travers une révision plus récente. L’absence initiale de `revoke` est une différence d’exécution consignée ; elle n’a pas été masquée ni transformée en comportement attendu. Les rapports #960 DEV/PRD sont `ifs-report/v2`, `Succeeded`, révision 3, avec les empreintes approuvées concordantes.

## Emplacement des preuves

- Captures ADO : `src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/p7-aborted-run-933/`, `p7-psgallery-main-ci-run956/`, `p7-psgallery-main-release-run959-prd-failed/` et `p7-psgallery-main-release-run960/`.
- Vérification consolidée et Cost Management : `%LOCALAPPDATA%\InfraFlowSculptor\P08\shop44-p7-post959failure-20261027T001158Z\shop44-final-cleanup-verification.json` et les fichiers `cost-*-post-cleanup.json` du même répertoire.
- C5/C3, comparaison à la baseline, tombstones et copies des versions : `%LOCALAPPDATA%\InfraFlowSculptor\P08\shop44-p7-post959failure-20261027T001158Z\cleanup-preflight-20261027T0145Z\`.

## Conclusion

La procédure T shop44 est terminée : les IDs créés pour la preuve sont supprimés et la baseline est préservée. La vérification détaillée du coût quotidien par groupe reste indisponible (HTTP 429) ; cet écart est explicite, la mesure globale reste disponible et aucune attente ou écriture facturable supplémentaire n’est engagée.