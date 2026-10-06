# InfraFlowSculptor

[![CI](https://github.com/FlorianDrevet/InfraFlowSculptor/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/FlorianDrevet/InfraFlowSculptor/actions/workflows/ci.yml)

Décrivez une fois votre infrastructure Azure et vos conventions. InfraFlowSculptor produit et tient à
jour, dans vos dépôts git, le Bicep, les pipelines Azure DevOps et le kit d'installation qui la
déploient, avec le câblage de sécurité (identités, rôles, secrets) déduit automatiquement.

## État du dépôt

Conception terminée, implémentation pas encore commencée. **Où on en est : [`NEXT.md`](NEXT.md).**

| Dossier | Contenu |
|---|---|
| [`docs/specs/`](docs/specs/README.md) | Spécifications fonctionnelles v1 — quoi |
| [`docs/technique/`](docs/technique/README.md) | Conception technique et décisions `DT-nn` — comment |
| [`docs/plan/`](docs/plan/README.md) | Plan d'implémentation : phases, jalons, verrous de revue, recettes — dans quel ordre |
| [`docs/design/`](docs/design/README.md) | Maquette v1 (72 écrans) et design system Strata, en HTML statique et en zip |

Pour la lecture, commencer par :
1. [Vision](docs/specs/00-vision.md)
2. [Principes](docs/specs/01-principes.md)
3. [Périmètre et lots](docs/specs/04-perimetre-et-lots.md)
4. [Projet de référence](docs/specs/90-projet-de-reference.md)
5. [Plan d'implémentation](docs/plan/README.md)

## Qui fait quoi

Claude (Opus) conçoit et relit à chaque verrou ; Codex (modèle Luna) implémente étape par étape. Consignes :
[`CLAUDE.md`](CLAUDE.md), [`AGENTS.md`](AGENTS.md). Mémoire du projet : [`MEMORY.md`](MEMORY.md).

## Reprendre le travail

```powershell
git pull
python tools/plan/gate.py status      # étape courante, statut, prochain verrou
git config core.hooksPath .githooks   # une fois par clone (à partir de l'étape S-01)
```

*(La section « Démarrer » — prérequis, `aspire run`, utilisateurs de démonstration — est écrite à l'étape S-17.)*
