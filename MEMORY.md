# Mémoire du projet — InfraFlowSculptor

> Index léger (< 80 lignes). Le détail vit dans `.github/memory/`. Règles : [`memory-management`](.github/skills/memory-management/SKILL.md).
> La mémoire décrit **ce qui existe** dans le dépôt ; la cible est dans `docs/technique/`, l'ordre dans `docs/plan/`.

## État

- **2026-10-06** : S-01 à S-16 du socle sont implémentés. Backend .NET 10, Aspire, émulateurs, PostgreSQL, Worker/outbox,
  OIDC Keycloak local, Angular 22, Strata, OpenAPI/client généré, CI et observabilité sont en place. Le run CI
  `37460726122` est vert ; détails et écarts de recette restent dans [`NEXT.md`](NEXT.md) et
  [`docs/plan/recettes/suivi.md`](docs/plan/recettes/suivi.md).
- **S-17 terminé le 2026-10-06** : mémoire vérifiée, graphe de code local et guide « Démarrer » ajoutés. À cette date, le
  contrôle de prérequis passait; le parcours sur clone neuf restait en attente dans [`NEXT.md`](NEXT.md), car l'AppHost
  actif occupait les ports fixes. Le verrou courant était R-01. Les moteurs/catalogues et intégrations métier étaient
  des étapes futures.
- **2026-10-27** : les preuves P1–P5 et P7–P9 sur la sortie écrite à la main sont prototypées. Le crochet P7
  `IFS_TEST_ABORT_AFTER` a été retiré de la sortie client ; voir `.github/memory/08-runtime-and-orchestration.md` et
  la revue R-03 pour l'écart de journalisation `revoke`.
- **2026-10-27** : J0-01 est implémentée et validée localement : backend, isolation API, préférences, organisations,
  fonctionnalités et interface. Les tests .NET, frontend et Playwright authentifiés (6/6 desktop/mobile), lint, builds
  et OpenAPI passent ; le tiroir mobile a été corrigé. Voir `.github/memory/03-domain-model.md`, `04-frontend.md`,
  `08-runtime-and-orchestration.md`, `09-auth-and-build.md` et `10-api-endpoints.md`.
- **2026-10-27** : J0-02 est validée localement : rôles et cycle de vie des membres, `RG-ORG-05`, restrictions tenant/domaine,
  API et interface. 104 tests .NET, 43 tests frontend, lint/build/OpenAPI et E2E authentifiés (16/16 desktop/mobile) passent.
  Voir `.github/memory/03-domain-model.md`, `04-frontend.md` et `10-api-endpoints.md`.
- **2026-10-27** : J0-03 est validée localement : invitations, vérification d’adresse, outbox e-mail MailPit/ACS et acceptation.
  Voir `.github/memory/03-domain-model.md`, `04-frontend.md`, `05-data-and-storage.md`, `09-auth-and-build.md`,
  `10-api-endpoints.md` et `13-integrations.md`.
- **2026-10-27** : J0-04 est validée par la CI : audit append-only, interface et accès vérifiés ; 18/18 E2E. Les captures et l’écart pgweb/psql sont consignés dans `docs/plan/recettes/suivi.md`.
- **2026-10-27** : J0-05 ajoute permissions et rôles de projet, l'agrégat minimal `shop`, les contrôles de sécurité, la migration et les endpoints. 155 tests backend et 59 frontend, build Release, lint et OpenAPI passent ; détails dans `.github/memory/03-domain-model.md`, `09-auth-and-build.md` et `10-api-endpoints.md`.
- **2026-10-27** : R-04 termine la revue et la recette d'accès. Idempotence par utilisateur, transaction atomique, corrections d'audit et A14 Scalar (Alice : 15 permissions; Chloé : `project.read`). Tests .NET 205/205 et frontend 63/63 ; Release sans avertissement, lint et OpenAPI passent. Le conflit PostgreSQL d'appartenance est couvert avec un savepoint et une réponse 409 commitée. E2E Emma reste affecté par la base persistante. Détails et réserves dans `docs/plan/revues/R-04-autovalidation.md` et les mémoires thématiques.

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

1. Lire `NEXT.md` avant tout ; `python tools/plan/gate.py check` avant une étape. Codex exécute les recettes et auto-valide les verrous.
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
| Auto-valider un verrou après vérification | `python tools/plan/gate.py auto-review <R-nn>` |
| Lever un blocage résolu et consigné | `python tools/plan/gate.py unblock <ID> <preuve>` |
| Reprendre après une recette exécutée par Codex | `python tools/plan/gate.py resume <ID>` |
| Réexporter maquette / Strata | `python tools/design/export_design.py maquette|strata …` (voir `docs/design/README.md`) |
