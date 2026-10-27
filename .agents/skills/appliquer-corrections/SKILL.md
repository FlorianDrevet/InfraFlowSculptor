---
name: appliquer-corrections
description: "Deprecated: historical Claude correction workflow. Codex resolves its own verified findings during autonomous validation."
---

# Skill retirée — corrections autonomes

Cette skill reste dans le dépôt pour préserver l'historique des instructions. Ne pas l'invoquer dans le flux courant.
Lors de son auto-revue, Codex corrige tout défaut de mise en œuvre démontré par un test, puis relance les vérifications
complètes avant `gate.py auto-review`. Une ambiguïté de conception ou un écart non tranchable reste un vrai blocage et
doit être exposé précisément à Florian.
