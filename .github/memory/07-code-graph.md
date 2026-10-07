# 07 — Graphe de code

## Moteur choisi
- `codeGraphEngine` : graphify (comme Vole-Papillon-Damour).
- Le paquet Python `graphifyy` était déjà installé sur la machine au début de S-17 : `python -m graphify --version` retourne `0.7.16`. Aucune installation ni modification des configurations globales des agents n'est nécessaire.
- Artefacts locaux, ignorés par git : `graphify-out/graph.json`, `graphify-out/GRAPH_REPORT.md`, `graphify-out/wiki/`.
- Exclusions : `.graphifyignore`.

## Usage
- Lire `graphify-out/GRAPH_REPORT.md` avant une exploration large.
- `python -m graphify query "concept"`, `path`, `explain`.
- `python -m graphify update .` après une étape qui modifie du code.

## Construction locale sans LLM

La commande CLI `python -m graphify update .` refait l'extraction AST, les regroupements et le rapport sans appel LLM.
Le constructeur limite automatiquement la génération de `graph.html` aux graphes de taille prise en charge ; le JSON et
le rapport restent les sorties de référence. Le dépôt contient 278 fichiers de code et 152 documents détectés avant
l'amorçage S-17. La génération S-17 a extrait 370 fichiers et produit le graphe, son rapport et `graph.html` ; consulter
`graphify-out/GRAPH_REPORT.md` pour les compteurs du dernier build. Les fichiers `graphify-out/` sont ignorés par Git.

```powershell
python -m graphify update .
```

L'appel Python interne ci-dessous est conservé comme diagnostic de bas niveau, pas comme commande courante :

```powershell
python -c "from pathlib import Path; from graphify.watch import _rebuild_code; import sys; sys.exit(0 if _rebuild_code(Path('.')) else 1)"
```
