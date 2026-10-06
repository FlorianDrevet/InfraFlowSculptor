# Mémoire du projet — InfraFlowSculptor

> Index léger (< 80 lignes). Le détail vit dans `.github/memory/`. Règles : [`memory-management`](.github/skills/memory-management/SKILL.md).
> La mémoire décrit **ce qui existe** dans le dépôt ; la cible est dans `docs/technique/`, l'ordre dans `docs/plan/`.

## État

- **2026-10-06** : S-01 à S-16 du socle sont implémentés. Backend .NET 10, Aspire, émulateurs, PostgreSQL, Worker/outbox,
  OIDC Keycloak local, Angular 22, Strata, OpenAPI/client généré, CI et observabilité sont en place. Le run CI
  `37460726122` est vert ; détails et écarts de recette restent dans [`NEXT.md`](NEXT.md) et
  [`docs/plan/recettes/suivi.md`](docs/plan/recettes/suivi.md).
- **S-17 en cours** : mémoire vérifiée, graphe de code local et guide « Démarrer » à terminer ; le prochain verrou est
  R-01. Les moteurs/catalogues et intégrations métier restent des étapes futures du plan.

## Fichiers thématiques

- **2026-10-06** : S-08 ajoute le worker/outbox avec 57 tests verts et des conteneurs locaux persistants. S-09 ajoute Angular 22; connexion Keycloak Bob, `/v1/me`, déconnexion, session après rechargement et configuration runtime vérifiés. 57 tests .NET, 4 Python et 3 Angular passent; OIDC limite le bearer token à `/v1/`.
- **2026-10-06** : S-10 génère le design system depuis `docs/design/strata/tokens.json`, embarque les polices Fontsource localement et ajoute la galerie de développement. 3 tests du générateur couvrent sortie exacte, thèmes multiples et `--check`; build frontend et lint verts. La référence statique `file://` n'a pas pu être ouverte via l'automatisation navigateur.

| Fichier | Contenu |
|---|---|
| `.github/memory/01-solution-overview.md` | Le produit, les surfaces, les flux critiques |
| `.github/memory/02-project-structure.md` | Arborescence réelle et rôle de chaque dossier |
| `.github/memory/03-domain-model.md` | Agrégats, tranches CQRS, contrats |
| `.github/memory/04-frontend.md` | Angular : génération, coquille, conventions, e2e |
| `.github/memory/05-data-and-storage.md` | PostgreSQL, blob, outbox, audit, secrets |
| `.github/memory/06-agents-skills.md` | Agents, skills, verrous, règles d'architecture |
| `.github/memory/07-code-graph.md` | graphify : installation locale, génération sans LLM et requêtes |
| `.github/memory/08-runtime-and-orchestration.md` | Aspire, émulateurs, worker, release |
| `.github/memory/09-auth-and-build.md` | Authentification, build, tests, CI, déploiement |
| `.github/memory/10-api-endpoints.md` | Routes `/v1`, OpenAPI |
| `.github/memory/11-frontend-design-system.md` | Strata en Angular : tokens, composants |
| `.github/memory/12-engine.md` | Moteur, catalogue, émetteurs |
| `.github/memory/13-integrations.md` | Entra, Azure DevOps, GitHub, e-mail |
| `.github/memory/changelog.md` | Une ligne par mise à jour non triviale |
| `.github/memory/dream-state.md` | État de consolidation de la mémoire |

## Repères

1. Lire `NEXT.md` avant tout ; `python tools/plan/gate.py check` avant une étape.
2. Un seul moteur de calcul (`InfraFlowSculptor.Engine`), pur ; le frontend ne calcule rien.
3. La sortie de référence (`reference/`) fait foi pour les émetteurs.
4. Chaque route a son test d'isolation (EXG-01).
5. Maquette = libellés et disposition ; Strata = valeurs (tokens) ; rien d'un jalon ultérieur n'apparaît.

## Commandes

| But | Commande |
|---|---|
| État du plan | `python tools/plan/gate.py status` |
| Contrôle avant de coder | `python tools/plan/gate.py check` |
| Vérifier les prérequis | `pwsh tools/dev/check-prereqs.ps1` |
| Tester les garde-fous | `python -m unittest discover tools/plan/tests` |
| Tester le vérificateur de prérequis | `python -m unittest discover tools/dev/tests` |
| Compiler le backend | `dotnet build src/backend/InfraFlowSculptor.slnx` |
| Démarrer l'application locale | `cd src/backend; aspire run` |
| Mettre à jour le graphe de code, sans LLM | `python -m graphify update .` (à la racine) |
| Étape terminée | `python tools/plan/gate.py done <ID>` |
| Réexporter maquette / Strata | `python tools/design/export_design.py maquette|strata …` (voir `docs/design/README.md`) |
