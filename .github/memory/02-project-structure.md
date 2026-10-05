# 02 — Structure du dépôt

## Existant (2026-10-05)
| Chemin | Rôle |
|---|---|
| `docs/specs/` | Spécifications fonctionnelles v1 (ne pas modifier sans Claude) |
| `docs/reviews/` | Revues fonctionnelles des specs |
| `docs/technique/` | Conception technique, décisions `DT-nn` |
| `docs/plan/` | Plan d'exécution (`NN-*.md`), revues de verrou, recettes, journal |
| `docs/design/` | Maquette v1 et Strata exportés en HTML statique + zip |
| `tools/plan/gate.py` | Garde-fou du plan (étape courante, verrous) |
| `tools/plan/tests/test_gate.py` | Tests `unittest` du lint, des transitions, des verrous et du crochet |
| `tools/versions.json`, `tools/dev/check-prereqs.ps1` | Minimums d'outils et contrôle de prérequis local |
| `tools/dev/tests/test_check_prereqs.py` | Teste le contrôle avec des commandes Windows simulées |
| `tools/design/export_design.py` | Export statique de la maquette et du design system |
| `.githooks/pre-commit` | Appelle `tools/plan/gate.py precommit` avant chaque commit |
| `.editorconfig`, `.gitattributes`, `.gitignore`, `.nvmrc` | Fins de ligne, indentations, fichiers ignorés et version Node |
| `src/backend/` | Solution .NET 10 : Api, Application, Contracts, Domain, Infrastructure, ServiceDefaults ; test Application dans `tests/` |
| `src/backend/Directory.Build.props` | Framework commun, nullable, analyse recommandée, erreurs sur avertissements, globalisation invariante |
| `.agents/skills/`, `.claude/skills/`, `.github/skills/` | Skills de Luna, de Claude, partagées |
| `.github/memory/` | Cette mémoire |

## Cible
Arborescence de `docs/technique/00-vue-d-ensemble.md` § 4 : `src/backend`, `src/frontend/ifs-web`, `catalog/`,
`reference/`, `samples/witness-app`, `infra/`.
