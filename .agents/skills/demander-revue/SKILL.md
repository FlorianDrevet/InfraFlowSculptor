---
name: demander-revue
description: "Use when the current step of NEXT.md is a lock 🔒 R-nn (gate.py done announced it). Writes the review request, opens the segment pull request, sets the lock status, and STOPS until Claude reviews."
---

# Demander la revue d'un verrou

1. `python tools/plan/gate.py status` : l'étape courante est bien `R-nn`.
2. Relance toutes les vérifications du segment (backend, frontend, e2e, `gate.py lint`) et note les résultats exacts.
3. Écris `docs/plan/revues/R-nn-demande.md` selon le modèle de `docs/plan/revues/README.md` : étapes et commits du
   segment (`git log --oneline origin/main..HEAD`), résultats, **tous** les écarts au plan, dette de test ouverte, tests
   manuels préparés et section de recette, captures (`docs/plan/revues/captures/R-nn/`), points fragiles.
4. `python tools/plan/gate.py request R-nn`.
5. Commit `docs(plan): demande de revue R-nn` (demande, captures, NEXT.md, JOURNAL.md), `git push`.
6. Ouvre (ou mets à jour) la pull request `impl/<segment>` → `main` : titre `[R-nn] <titre du verrou>`, description =
   contenu de la demande. `gh pr create --base main --title "[R-nn] …" --body-file docs/plan/revues/R-nn-demande.md`.
7. **Arrête-toi.** Dis à l'utilisateur : la revue R-nn est demandée, le lien de la pull request, la recette à exécuter
   (`docs/plan/recettes/…`, section), et que Claude doit relire avant toute suite. Ne commence aucune autre étape, même
   « pour gagner du temps ».
