---
name: demander-revue
description: "Deprecated: historical Claude review workflow. Locks are now self-validated by Codex through executer-etape and gate.py auto-review."
---

# Skill retirée — revue autonome

Cette skill reste dans le dépôt pour préserver l'historique des instructions. Ne pas l'invoquer pour une nouvelle étape.
Suivre `.agents/skills/executer-etape/SKILL.md` : exécuter la recette, rédiger `docs/plan/revues/R-nn-autovalidation.md`,
puis appeler `python tools/plan/gate.py auto-review R-nn` après avoir vérifié les quatre sections requises.
