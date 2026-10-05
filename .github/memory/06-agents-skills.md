# 06 — Agents, skills et verrous

## Répartition
Claude (Opus) conçoit et relit ; Luna (Codex) exécute. Consignes : `CLAUDE.md`, `AGENTS.md`.

## Cycle
`NEXT.md` → `gate.py check` → étape → commit `Étape: ID` → `gate.py done ID` → … → verrou 🔒 `R-nn` :
`R-nn-demande.md` (Luna) → `gate.py request` → revue de Claude `R-nn-revue.md` (`Verdict : APPROUVÉ|CORRECTIONS`)
→ `gate.py approve|reject`. Pendant une revue, `gate.py check` renvoie 3 et le crochet `pre-commit` refuse le code.

## Skills
| Qui | Skill |
|---|---|
| Luna | `executer-etape`, `demander-revue`, `appliquer-corrections` (`.agents/skills/`) |
| Claude | `revue-verrou`, `detailler-jalon`, `exporter-design`, `consolider-memoire` (`.claude/skills/`) |
| Tous | `tdd-workflow`, `cqrs-feature`, `xunit-testing`, `angular-strata`, `memory-management` (`.github/skills/`) |

## Règles d'architecture vérifiées par tests
- `Domain` ne référence directement que `ErrorOr`, aucun autre projet du produit ni EF Core, ASP.NET Core ou Azure.
- `Application` ne dépend pas de `Infrastructure`.
- Les contrôleurs API sont statiques et ne dépendent pas de `Infrastructure`.
- Un fichier `.cs` de production déclare au plus un type public de premier niveau; le scan exclut `tests/`, `obj/`, `bin/` et `Migrations/`.
- Les types énumérés dans `Architecture.Tests/ExtensionKeys.Types` ne sont ni utilisés dans un `switch` ni comparés directement hors des registres et de leurs implémentations. La liste est vide au début du socle et chaque étape qui ajoute une clé doit la compléter.
