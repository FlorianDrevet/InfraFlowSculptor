# P3b — Dérive temporaire de rétention

- Cible : `log-shop42-main-prd`, groupe `rg-shop42-core-main-prd`, région `francecentral`.
- Avant le run #713 : rétention `90` jours, vérifiée en lecture seule.
- Run #713 en attente PRD : aperçu capturé dans `p3b-core-pending-run-713/`.
- Changement temporaire : `90` → `120` jours. L’activité Azure indique l’opération commencée à `2026-10-16T12:01:52Z` et terminée à `2026-10-16T12:02:12Z`; Azure CLI a relu `retentionInDays=120`.
- Après approbation du run #713 : le rapport PRD porte `FailedBeforeMutation` et indique que les effets ont changé depuis l’aperçu approuvé. Voir `p3b-core-drift-run-713/artifacts/ifs-report-prd/ifs-report.json`.
- Restauration : `120` → `90` jours. L’activité Azure indique l’opération commencée à `2026-10-16T12:11:01Z` et terminée à `2026-10-16T12:11:22Z`; Azure CLI a relu `retentionInDays=90` et `location=francecentral`.
- La preuve de réglage est constituée des réponses Azure CLI et des événements Azure Activity Log; aucun changement de région ou de SKU n’a été demandé.
