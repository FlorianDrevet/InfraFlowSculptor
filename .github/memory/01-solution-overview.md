# 01 — Vue d'ensemble

## Le produit
InfraFlowSculptor (IFS) : SaaS multi-organisations, hébergé dans l'UE. L'équipe décrit une fois son infrastructure
Azure et ses conventions ; IFS génère et tient à jour, dans ses dépôts git, le Bicep, les pipelines Azure DevOps et le
kit d'installation, avec le câblage de sécurité déduit. Spécifications : `docs/specs/` (v1, DEC-01 à DEC-112).

## Surfaces (cible)
api (REST `/v1`), worker (génération, publication, suivi), web (Angular), mcp (jalon 3). Voir `docs/technique/00-vue-d-ensemble.md`.

## Flux critiques
Modéliser → valider → générer une révision immuable → publier par pull request → les pipelines du client déploient →
IFS suit les déploiements par les rapports `ifs-report.json`.

## État
2026-10-06 : S-01 à S-16 du socle sont implémentés ; S-17 met à jour la mémoire, le graphe de code et le guide de démarrage.
R-01 est le prochain verrou. Les fonctions métier décrites par la cible restent à construire dans les phases suivantes.
