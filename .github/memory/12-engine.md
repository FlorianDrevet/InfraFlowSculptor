# 12 — Moteur, catalogue et émetteurs

> Au 2026-10-27, le catalogue pilote est implémenté (J0-06) ; le moteur de calcul et les émetteurs métier restent prévus pour les phases suivantes.
> Cible : `docs/technique/03-moteur-et-generation.md`, DT-14 à DT-16. Étapes J0-06, J0-12 à J0-24.

## Faits vérifiés

- `InfraFlowSculptor.Catalog` charge les JSON embarqués de `catalog/2026.10-pilot/` : neuf descripteurs ARM, 24 régions, rôles Azure avec leurs identifiants, pins, schéma Azure Pipelines et deux étapes de pipeline. Les schémas sont sous `catalog/schema/`; le hash SHA-256 canonique est épinglé dans `catalog/published-hashes.json`.
- `CatalogVersion` expose une collection immuable; `ICatalogProvider` retourne les versions disponibles et `LatestPublished`. L'API expose les versions, la recherche par catégorie/texte et le détail d'un type sans calcul métier.
- Les tests valident tous les documents JSON, l'empreinte, les rôles et les propriétés mappées aux paramètres des modules AVM restaurés. `CatalogEndpointTests` valide les libellés français « Coffre de clés » et « Identité managée affectée par l’utilisateur » sur le filtre `category=security`.
