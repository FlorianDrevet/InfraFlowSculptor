# Nettoyage résiduel du kit shop42 — 2026-10-27

## Préflight

À la demande de Florian, l’inventaire Azure de la souscription de test a été contrôlé avant suppression. Les groupes `rg-ifs-shop42-dev`, `rg-ifs-shop42-prd` et `rg-ifs-shop42-shared` contenaient chacun exactement deux identités managées et un compte de stockage. Les neuf ressources portaient les tags `managed-by=infraflowsculptor` et `ifs-kit-revision=2` et correspondaient aux identifiants du snapshot local reconstruit `baseline-20261015T1229Z-reconstructed-20261024.json`. Aucun verrou ni déploiement de groupe n’était présent. L’étape J0-01, consacrée au code local des profils utilisateurs et organisations, n’a pas besoin de ces ressources.

L’inventaire avant suppression était de 144 ressources, 19 groupes, 0 pile, 40 rôles et 7 budgets.

## Actions

- Les trois groupes ci-dessus ont été supprimés et leur absence confirmée par Azure.
- Cela retire neuf ressources : six identités managées et trois comptes de stockage.
- Dix affectations de rôles de recette ont été supprimées : les neuf encore présentes du snapshot, plus l’affectation personnelle `Storage Blob Data Reader` de D1b sur le conteneur DEV.
- Les trois fichiers JSON du conteneur DEV, soit 5 487 octets, ont été copiés localement avant la suppression. Le manifeste et leurs empreintes SHA-256 sont dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\shop42-residual-cleanup-20261027T074136Z\manifest.json`.
- Aucun autre groupe n’a été ciblé. Le snapshot reconstruit liste 30 ressources et 10 groupes créés pour le kit ; aucun de ces identifiants de ressource ou de groupe ne reste actif. Aucun des 29 identifiants d’affectation de rôle du snapshot ni des deux affectations personnelles D1b n’est encore présent.

## Vérification après suppression

À `2026-10-27T07:48Z`, C5 donne 135 ressources, 16 groupes, 0 pile, 30 affectations de rôle et 7 budgets. Les trois groupes `rg-ifs-shop42-*` ciblés sont absents. Les sept groupes de preuve shop42 déjà retirés avant cette opération restent absents.

Le snapshot reconstruit référence aussi `swa-sainte-fly` dans `rg-sainte-fly`, mais ce groupe était déjà vide à l’inventaire avant l’opération; `az resource show` retourne `ResourceNotFound`. La présente suppression n’a ciblé ni ce groupe ni cette ressource. Le snapshot indique `originalSnapshotFound=false`; cet écart doit être rapproché séparément avant de réutiliser cette référence comme baseline exhaustive.

Six coffres Key Vault standards des recettes shop42/shop43/shop44 (`dev` et `prd`) sont encore en état soft-delete, sans coffre actif correspondant. Ils suivent leur échéance de purge Azure entre le 23 et le 25 janvier 2027. Ils n’ont pas été purgés manuellement : [Microsoft précise qu’un coffre standard en état supprimé n’occasionne pas de facturation d’usage](https://learn.microsoft.com/azure/key-vault/general/soft-delete-change) (les opérations de purge et de récupération sont facturées).

## Coûts et suite Azure DevOps

Cost Management global, relevé après suppression, donne `153,839034 EUR` d’ActualCost jusqu’au 26/10 et `31,936776 EUR` de prévision du 27 au 31/10. Avec la réserve documentée de `15 EUR`, l’estimation prudente est `200,775810 EUR`, soit `0,775810 EUR` au-dessus de l’alerte non bloquante de `200 EUR`. Ces valeurs `PreTaxCost` sont globales, hors taxes, et le relevé n’a pas changé immédiatement après la suppression. La ventilation par groupe n’a pas été réessayée après le `429` antérieur; aucune économie précise ne peut donc être attribuée à ces trois comptes.

Les six connexions de service fédérées WIF du projet Azure DevOps `shop42` restent présentes, mais leurs identités managées associées ont été supprimées. Elles devront être recréées avant une future recette qui déploie dans Azure; l’étape J0-01 n’en dépend pas.
