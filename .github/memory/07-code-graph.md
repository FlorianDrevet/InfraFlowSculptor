# 07 — Graphe de code

## Moteur choisi
- `codeGraphEngine` : graphify (comme Vole-Papillon-Damour).
- Artefacts locaux, ignorés par git : `graphify-out/graph.json`, `graphify-out/GRAPH_REPORT.md`, `graphify-out/wiki/`.
- Exclusions : `.graphifyignore`.

## Usage
- Lire `graphify-out/GRAPH_REPORT.md` avant une exploration large.
- `python -m graphify query "concept"`, `path`, `explain`.
- `python -m graphify update .` après une étape qui modifie du code.

## Amorçage (à faire à S-17)
```powershell
python -c "from pathlib import Path; from graphify.watch import _rebuild_code; import sys; sys.exit(0 if _rebuild_code(Path('.')) else 1)"
python -m graphify update .
```
Installation : à consigner ici à S-17.
