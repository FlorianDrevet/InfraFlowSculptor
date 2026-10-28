# 06 — Agents, skills et verrous

## Répartition
Les specs, décisions, plan et maquettes portent la conception. Luna (Codex) exécute les étapes, recettes et auto-revues.
À chaque verrou `R-nn`, Claude CLI relit le code du segment en lecture seule ; Codex vérifie et traite ses constats puis garde
la responsabilité du verdict et du gate. Si le plan ne suffit pas pour un choix de conception, Luna demande ce point à Florian.
Consignes : `AGENTS.md`.

## Cycle
`NEXT.md` → `gate.py check` → étape → commit `Étape: ID` → `gate.py done ID` → … → verrou 🔒 `R-nn` :
Claude CLI relit le code du segment, Codex vérifie ses constats, exécute les recettes, crée `R-nn-autovalidation.md` puis
lance `gate.py auto-review`.
`gate.py check` ne bloque pas une recette ou auto-revue en cours ; un blocage réel exige une preuve et `gate.py unblock`.

## Skills
| Qui | Skill |
|---|---|
| Luna | `executer-etape` (`.agents/skills/`) |
| Claude CLI | `demander-revue` (`.agents/skills/`) — constats de code aux verrous, vérifiés par Codex |
| Tous | `tdd-workflow`, `cqrs-feature`, `xunit-testing`, `angular-strata`, `memory-management` (`.github/skills/`) |

## Règles d'architecture vérifiées par tests
- `Domain` ne référence directement que `ErrorOr`, aucun autre projet du produit ni EF Core, ASP.NET Core ou Azure.
- `Application` ne dépend pas de `Infrastructure`.
- Les contrôleurs API sont statiques et ne dépendent pas de `Infrastructure`.
- Un fichier `.cs` de production déclare au plus un type public de premier niveau; le scan exclut `tests/`, `obj/`, `bin/` et `Migrations/`.
- Les types énumérés dans `Architecture.Tests/ExtensionKeys.Types` ne sont ni utilisés dans un `switch` ni comparés directement hors des registres et de leurs implémentations. La liste est vide au début du socle et chaque étape qui ajoute une clé doit la compléter.
