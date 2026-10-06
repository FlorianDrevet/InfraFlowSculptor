# 02 — Structure du dépôt

## Existant (2026-10-06)
| Chemin | Rôle |
|---|---|
| `docs/specs/` | Spécifications fonctionnelles v1 (ne pas modifier sans Claude) |
| `docs/reviews/` | Revues fonctionnelles des specs |
| `docs/technique/` | Conception technique, décisions `DT-nn` |
| `docs/plan/` | Plan d'exécution (`NN-*.md`), revues de verrou, recettes, journal |
| `docs/design/` | Maquette v1 et Strata exportés en HTML statique + zip |
| `tools/plan/gate.py` | Garde-fou du plan (étape courante, verrous) |
| `tools/plan/tests/test_gate.py` | Tests `unittest` du lint, des transitions, des verrous et du crochet |
| `tools/versions.json`, `tools/dev/check-prereqs.ps1` | Minimums d'outils et contrôle de prérequis local |
| `tools/dev/tests/test_check_prereqs.py` | Teste le contrôle avec des commandes Windows simulées |
| `tools/design/export_design.py` | Export statique de la maquette et du design system |
| `.githooks/pre-commit` | Appelle `tools/plan/gate.py precommit` avant chaque commit |
| `.editorconfig`, `.gitattributes`, `.gitignore`, `.nvmrc` | Fins de ligne, indentations, fichiers ignorés et version Node |
| `src/backend/` | Solution .NET 10 : AppHost, Api, Application, Contracts, Domain, Infrastructure, ServiceDefaults et Worker ; six projets de test sous `tests/` |
| `src/frontend/ifs-web/` | Application Angular 22, génération du client depuis OpenAPI et tests Vitest/Playwright |
| `samples/witness-app/` | Application témoin ASP.NET Core .NET 10, contrôles de santé Azure SQL/Log Analytics/App Configuration/Service Bus et tests xUnit hors solution backend |
| `reference/pilot/bicep-azdo/` | Sortie Bicep de référence écrite à la main pour `core`, `data`, `platform` et `orders`, avec paramètres dev/prd/shared et fichiers de release |
| `reference/pilot/pins.json`, `reference/release-module/schemas/` | Versions AVM/Bicep et image de démarrage épinglées; schéma des données de release |
| `reference/tools/Test-ReferenceBicep.ps1` | Build, lint, formatage strict et build des fichiers de paramètres de la sortie Bicep |
| `src/backend/InfraFlowSculptor.AppHost/Realms/` | Configuration d'import du royaume Keycloak local `ifs` |
| `src/backend/InfraFlowSculptor.Api/openapi/` | Document OpenAPI versionné et vérifié au build/test |
| `NEXT.md`, `MEMORY.md` | Étape courante du plan et index de mémoire vérifié |
| `src/backend/Directory.Build.props` | Framework commun, nullable, analyse recommandée, erreurs sur avertissements, globalisation invariante |
| `.agents/skills/`, `.claude/skills/`, `.github/skills/` | Skills de Luna, de Claude, partagées |
| `.github/memory/` | Cette mémoire |

## P-02 — pilote Bicep (2026-10-06)

- Le projet pilote contient quatre composants subscription-scope. Chaque composant exporte ses types Bicep et porte les valeurs par cible dans `main.<cible>.bicepparam`.
- Les références inter-composants sont déclarées `existing` avec abonnement, groupe et nom. Les attributions RBAC sur Key Vault, Log Analytics et ACR sont des modules resource-group-scope avec `guid(scope, principal, role)` et `principalType: 'ServicePrincipal'`.
- Les versions AVM et le digest de l’image de démarrage sont consignés dans `reference/pilot/pins.json`; les valeurs d’abonnement et l’objet du groupe administrateur SQL restent des marqueurs jusqu’à la préparation Azure.

## Cible
Arborescence de `docs/technique/00-vue-d-ensemble.md` § 4 : `src/backend`, `src/frontend/ifs-web`, `catalog/`,
`reference/`, `samples/witness-app`, `infra/`.
