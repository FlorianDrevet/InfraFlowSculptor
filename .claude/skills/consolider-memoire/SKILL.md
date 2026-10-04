---
name: consolider-memoire
description: "Use at every lock review, or when asked to consolidate memory. Reflective pass over MEMORY.md and .github/memory/: merge, correct, prune, keep it factual and short. Touches only memory files."
---

# Consolider la mémoire (« dream »)

Inspiré de l'agent `dream` de Vole-Papillon-Damour. Ne modifie **que** `MEMORY.md` et `.github/memory/`.

1. **Orienter** : lire `dream-state.md`, `MEMORY.md`, survoler chaque fichier thématique.
2. **Recueillir** : `changelog.md` depuis `lastConsolidation` ; `git log --since=<date> --name-only` ; `graphify-out/GRAPH_REPORT.md` s'il existe ; la demande et la revue du verrou.
3. **Consolider** : chaque fait au bon fichier (routage de `memory-management`), dates absolues, faits contredits
   supprimés, doublons fusionnés, faits vérifiés dans le code (pas de cible présentée comme existante).
4. **Élaguer** : chaque fichier < 150 lignes (sinon scinder et l'ajouter à l'index), `MEMORY.md` < 80 lignes, changelog >
   60 jours résumé en une ligne.
5. `dream-state.md` : `lastConsolidation` = aujourd'hui, `lastLock` = R-nn. Commit avec la revue.
