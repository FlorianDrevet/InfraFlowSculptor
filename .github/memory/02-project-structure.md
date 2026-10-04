# 02 — Structure du dépôt

## Existant (2026-10-04)
| Chemin | Rôle |
|---|---|
| `docs/specs/` | Spécifications fonctionnelles v1 (ne pas modifier sans Claude) |
| `docs/reviews/` | Revues fonctionnelles des specs |
| `docs/technique/` | Conception technique, décisions `DT-nn` |
| `docs/plan/` | Plan d'exécution (`NN-*.md`), revues de verrou, recettes, journal |
| `docs/design/` | Maquette v1 et Strata exportés en HTML statique + zip |
| `tools/plan/gate.py` | Garde-fou du plan (étape courante, verrous) |
| `tools/design/export_design.py` | Export statique de la maquette et du design system |
| `.agents/skills/`, `.claude/skills/`, `.github/skills/` | Skills de Luna, de Claude, partagées |
| `.github/memory/` | Cette mémoire |

## Cible
Arborescence de `docs/technique/00-vue-d-ensemble.md` § 4 : `src/backend`, `src/frontend/ifs-web`, `catalog/`,
`reference/`, `samples/witness-app`, `infra/`.
