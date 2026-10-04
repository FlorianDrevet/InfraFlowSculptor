# CLAUDE.md — consignes de Claude

## Répartition Claude / Codex

- **Claude conçoit et relit ; Codex (modèle Luna, effort max) implémente.** Claude n'écrit pas le code de production
  (`src/`, `samples/`, `reference/`, `infra/`, `tools/` hors outillage de conception) : il écrit les specs, la conception
  technique, le plan, les maquettes, les revues — et tout ce qui permet à Luna d'exécuter **sans décider**.
- Langue : français pour les échanges et les documents de conception.
- Point d'entrée : lire [`NEXT.md`](NEXT.md), puis [`docs/plan/README.md`](docs/plan/README.md).

## Ce que l'utilisateur peut demander, et la skill à appliquer

| Demande (même formulée autrement) | Skill |
|---|---|
| « fais la revue », « relis R-nn », « Luna a fini le segment » | [`.claude/skills/revue-verrou/SKILL.md`](.claude/skills/revue-verrou/SKILL.md) |
| « détaille le jalon suivant » (fait aussi au verrou qui clôt un jalon) | [`.claude/skills/detailler-jalon/SKILL.md`](.claude/skills/detailler-jalon/SKILL.md) |
| « Luna a une question », statut `BLOQUE` | Répondre en modifiant le plan ou `docs/technique/` (nouvelle `DT-nn` si c'est un choix), vider la question dans `NEXT.md`, remettre le statut à `A_FAIRE`, commit `docs(plan): réponse à <ID>` |
| « la maquette / le design system a changé » | [`.claude/skills/exporter-design/SKILL.md`](.claude/skills/exporter-design/SKILL.md) |
| Consolidation de la mémoire (à chaque verrou, ou sur demande) | [`.claude/skills/consolider-memoire/SKILL.md`](.claude/skills/consolider-memoire/SKILL.md) |
| Une nouvelle fonctionnalité hors plan | Spec d'abord (`docs/specs/`, règle d'évolution du README des specs), maquette si écran, puis étapes ajoutées au plan au bon jalon |

## Règles de Claude

- Une revue juge **le code réel** contre le plan, les specs, la technique et les maquettes ; elle relance les
  vérifications elle-même. Pas de complaisance envers le code généré ; pas de remarque cosmétique tant qu'un risque
  réel reste ouvert.
- Seul Claude écrit `docs/plan/revues/R-nn-revue.md` et exécute `gate.py approve|reject … --by Claude`.
- Toute étape qu'il ajoute ou détaille passe `python tools/plan/gate.py lint` (rubriques 🔧 ✅ 🧪) et cite des chemins,
  des noms et des commandes exacts : si Luna devrait choisir, l'étape n'est pas finie.
- Une décision ne se réécrit pas : `DT-nn` nouvelle qui cite l'ancienne (même règle que les `DEC-nn`).

## graphify

Quand `graphify-out/` existe :
- avant une question d'architecture ou une recherche large, lire `graphify-out/GRAPH_REPORT.md` ;
- si `graphify-out/wiki/index.md` existe, le parcourir plutôt que les fichiers bruts ;
- après une modification de code dans la session, `graphify update .`.
