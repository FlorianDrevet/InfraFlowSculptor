---
name: memory-management
description: "Use when updating MEMORY.md or .github/memory/ after a step (rubrique 🧠 du plan). Routing, format and rules of the project memory."
---

# Tenir la mémoire

- `MEMORY.md` : index (< 80 lignes) et commandes vérifiées. Le détail va dans `.github/memory/`.
- La mémoire décrit **ce qui existe et a été vérifié** dans le dépôt (chemins, commandes qui marchent, conventions
  appliquées, pièges rencontrés). La cible est dans `docs/technique/`, pas ici. Pas de spéculation, pas de secret, pas de
  valeur de configuration sensible.
- Une ligne dans `changelog.md` par étape qui modifie la mémoire : `| date | Luna/Claude | étape — changement |`.
- Dates absolues (`2026-10-04`). Un fait contredit est corrigé, pas doublé. Fichier > 150 lignes : le scinder et
  l'ajouter à l'index de `MEMORY.md`.

## Routage

| Information | Fichier |
|---|---|
| Produit, surfaces, flux | `01-solution-overview.md` |
| Arborescence | `02-project-structure.md` |
| Agrégats, tranches, contrats | `03-domain-model.md` |
| Angular, coquille, e2e | `04-frontend.md` |
| Base, blob, outbox, audit, secrets | `05-data-and-storage.md` |
| Agents, skills, verrous, règles d'architecture | `06-agents-skills.md` |
| graphify | `07-code-graph.md` |
| Aspire, émulateurs, worker, release | `08-runtime-and-orchestration.md` |
| Auth, build, tests, CI, déploiement | `09-auth-and-build.md` |
| Routes de l'API | `10-api-endpoints.md` |
| Strata en Angular | `11-frontend-design-system.md` |
| Moteur, catalogue, émetteurs | `12-engine.md` |
| Entra, Azure DevOps, GitHub, e-mail | `13-integrations.md` |
