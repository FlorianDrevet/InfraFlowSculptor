# Suivi des recettes et tests manuels

> Rempli par vous (ou par Luna sous votre dictée). Une ligne par test manuel 🧪 ou section de recette exécutée.
> Claude le lit à chaque verrou : un verrou n'est approuvé que si la recette de son segment est « OK » ou
> explicitement reportée par vous.

| Date | Étape / section | Résultat | Remarques (écart, capture, référence du run) |
|---|---|---|---|
| 2026-10-05 | S-01 — Prérequis de la machine et garde-fous du plan | Partiel, écarts consignés dans `NEXT.md` | `check-prereqs.ps1` : Node 24.13.0 et Azure CLI 2.88.0 sous les minimums; Bicep mis à jour en 0.47.16. Les mises à jour Node/Azure CLI n'ont pas abouti. `gate.py status` et `git config core.hooksPath` conformes. |
| 2026-10-05 | S-02 — Squelette backend | Partiel | `dotnet build src/backend/InfraFlowSculptor.slnx` : 6 projets, 0 erreur, 31 avertissements tolérés par S-02. `dotnet sln list` confirme les cinq projets et aucune référence au nom du template; ouverture dans Rider non vérifiée, le canal d'automatisation n'expose aucune fenêtre native. Suivi dans `NEXT.md`. |
| 2026-10-05 | S-03 — Moderniser le squelette | Partiel | Build : 0 avertissement/erreur; `dotnet test` : 1 test Registry vert. `/alive`, `/v1/version`, `/openapi/v1.json` → 200; route inconnue → 404 `application/problem+json` avec `traceId`. Scalar affiche « InfraFlowSculptor API v1 » et `GetVersion`; l'exécution depuis Scalar renvoie 200 avec `environment: Development`. Chrome bloque la navigation directe vers la route inconnue (`ERR_BLOCKED_BY_CLIENT`); son corps HTTP a été vérifié avec curl. Une vérification visuelle de cette erreur reste dans `NEXT.md`. |
| 2026-10-05 | S-04 — Projets de tests et règles d'architecture | Partiel | `dotnet test` : 13 tests verts, 0 avertissement; `dotnet build` : 0 erreur, 0 avertissement. L'ajout temporaire d'une référence `Microsoft.EntityFrameworkCore` au Domain a fait échouer `LayerDependencyTests` en nommant cette dépendance; référence retirée. `dotnet sln list` présente les cinq projets de test. Vérification du panneau Unit Tests dans Rider non possible : aucune fenêtre native n'est exposée à l'automatisation. |
