# Suivi des recettes et tests manuels

> Rempli par vous (ou par Luna sous votre dictée). Une ligne par test manuel 🧪 ou section de recette exécutée.
> Claude le lit à chaque verrou : un verrou n'est approuvé que si la recette de son segment est « OK » ou
> explicitement reportée par vous.

| Date | Étape / section | Résultat | Remarques (écart, capture, référence du run) |
|---|---|---|---|
| 2026-10-05 | S-01 — Prérequis de la machine et garde-fous du plan | Partiel, écarts consignés dans `NEXT.md` | `check-prereqs.ps1` : Node 24.13.0 et Azure CLI 2.88.0 sous les minimums; Bicep mis à jour en 0.47.16. Les mises à jour Node/Azure CLI n'ont pas abouti. `gate.py status` et `git config core.hooksPath` conformes. |
