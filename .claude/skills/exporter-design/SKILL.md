---
name: exporter-design
description: "Use when the InfraFlowSculptor mockup (Claude Design canvas) or the Strata design system changed, or the user asks to re-export them as static HTML/zip. Reads the published artifacts, runs tools/design/export_design.py, updates docs/design/README.md and flags affected plan steps."
---

# Réexporter la maquette et Strata

Sources : maquette `https://claude.ai/artifact/8Vqk73EgXnLj9yzcn3uMAT`, Strata `https://claude.ai/artifact/4M9UorSE1agmiyxSa3vuqJ`.

1. `Artifact` `action: "list"`, `scope: "files"` sur chaque artefact (noter la version).
2. `Artifact` `action: "read"` avec `paths` = tous les `project/*.dc.html`, `project/canvas.json`, `project/ds/**` et
   `artifact-type/dc-runtime.js` (maquette) ; tous les `project/**` (Strata) ; `out_dir` dans le scratchpad.
3. ```
   python tools/design/export_design.py maquette <scratch>/maquette docs/design/maquette-v1 --runtime <scratch>/maquette/artifact-type/dc-runtime.js --url <maquette> --title "InfraFlowSculptor v1"
   python tools/design/export_design.py strata <scratch>/strata docs/design/strata --runtime <scratch>/maquette/artifact-type/dc-runtime.js --url <strata>
   ```
   Le script échoue (code 1) si un écran ne se rend pas (gabarit non résolu, erreur de composant) : corriger dans le
   canvas, ou étendre le correctif `STRATA_SHIM` du script si c'est un défaut d'usage connu, puis le consigner.
4. Vérifier deux ou trois écrans en capture headless ; mettre à jour `docs/design/README.md` (versions, défauts, tableau
   écrans → jalons si des écrans ont été ajoutés).
5. Lister les étapes du plan non encore exécutées dont les écrans ont changé ; les ajuster ; si une étape déjà exécutée est
   touchée, ajouter une étape de mise à niveau au segment courant. Signaler dans `NEXT.md`.
