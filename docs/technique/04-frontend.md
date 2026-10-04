# 04 — Frontend

## 1. Génération et place dans le dépôt

L'application est **générée**, jamais créée à la main ([DT-18](01-decisions.md#dt-18--frontend--angular-22-généré-par-ng-template)) :

```powershell
# Depuis la racine du dépôt. Le paquet est publié sur GitHub Packages ; voir S-09 pour le repli « npm pack ».
cd src/frontend
npx -p @angular/cli@22 -p @floriandrevet/ng-template@22.0.0 ng new ifs-web `
  --collection=@floriandrevet/ng-template `
  --ssr=false --ui=tailwind --auth=oidc --i18n=true --docker=true --ci=false `
  --skip-git
```

Puis les adaptations d'IFS (étapes S-09 à S-14) : configuration OIDC à l'exécution, tokens Strata, composants
`app-ds-*`, coquille, client API généré, Playwright.

## 2. Structure

```
src/frontend/ifs-web/
├── public/
│   ├── config.json              { apiUrl, oidc: { authority, clientId, scope, provider } } — régénéré au démarrage
│   ├── i18n/fr.json, en.json    Transloco
│   └── azure-icons/*.svg        icônes Azure officielles (DT-19)
├── scripts/
│   ├── write-config.mjs         écrit public/config.json depuis les variables IFS_* (Aspire, Docker)
│   └── build-tokens.mjs         docs/design/strata/tokens.json → src/styles/tokens.css + @theme Tailwind
├── src/
│   ├── styles/                  tokens.css (généré), base.css, fonts.css
│   └── app/
│       ├── core/                config (APP_CONFIG), http (intercepteurs : base URL, organisation, idempotence,
│       │                        erreurs problem+json), auth (OIDC, garde), i18n, api/generated (DT-20)
│       ├── ds/                  composants Strata : app-ds-button, app-ds-icon, …
│       ├── shell/               coquille : barre latérale (portées), barre supérieure, fil d'Ariane, palette
│       ├── shared/              motifs produit réutilisés : grille propriétés × environnements, impact,
│       │                        conflit de version, constats en contexte, implicite visible, visualiseur de code
│       └── features/
│           ├── home/            Accueil
│           ├── organization/    membres, invitations, équipes, rôles, connexions, audit, réglages
│           ├── profile/         profil, jetons d'API
│           ├── projects/        liste, assistant de création
│           ├── project/         vue d'ensemble, environnements, nommage, membres, paramètres, constats,
│           │                    historique, publication, révisions, installation, déploiements
│           ├── component/       composant, réglages, ressources
│           ├── resource/        onglets générés depuis le descripteur
│           ├── proposals/       (jalon 2)
│           └── backoffice/      exploitation IFS (rôles internes)
├── e2e/                         Playwright
└── Dockerfile, nginx.conf       (issus du template, option docker)
```

## 3. Règles qui ne se discutent pas

1. **Rien de calculé localement** ([RG-UI-02](../specs/26-interface.md)) : noms Azure, valeurs effectives,
   implicites, constats, ordre viennent de l'API. Un composant qui reformule une règle métier en TypeScript
   est un défaut.
2. **Rien d'annoncé** ([P9](../specs/01-principes.md), [RG-UI-05](../specs/26-interface.md)) : la maquette
   montre toutes les fonctions de tous les lots. Une étape n'implémente que les zones de son jalon ; les
   autres **n'apparaissent pas** (ni grisées, ni « bientôt »). Chaque étape UI liste les zones exclues.
3. **Libellés mot pour mot** de la maquette, dans `fr.json`, puis traduits dans `en.json`.
4. **Uniquement des variables `--ifs-*`** et les composants `app-ds-*` ; aucune couleur en dur. Les classes
   Tailwind utilisent les noms générés depuis les tokens (`bg-surface-1`, `text-text-2`, `border-line`).
5. **Accessibilité WCAG 2.2 AA** ([EXG-12](../specs/27-exigences-non-fonctionnelles.md)) : vrais `<button>`,
   `<a href>`, `<label>`, focus visible, rien porté par la seule couleur ni le seul survol
   ([RG-UI-04](../specs/26-interface.md)) ; le graphe a son équivalent en liste.
6. **Petit écran** ([RG-UI-16](../specs/26-interface.md)) : sous 760 px la barre latérale disparaît
   (menu), les grilles passent à une colonne, les tableaux défilent dans leur boîte ; aucun défilement
   horizontal de la page.
7. **Saisie préservée** ([RG-UI-15](../specs/26-interface.md)) : brouillon local des formulaires non sensibles,
   état d'enregistrement visible, `Idempotency-Key` sur chaque commande.

## 4. Correspondance Strata → Angular

| Strata (`docs/design/strata/components/`) | Angular (`src/app/ds/`) | Étape |
|---|---|---|
| `Button` (primary, secondary, ghost, danger ; md, sm ; icône ; lien) | `app-ds-button` | S-11 |
| `Icon` | `app-ds-icon` | S-11 |
| `Badge` (tons, mono, dot, icône) | `app-ds-badge` | S-11 |
| `TextField` (label, hint, error, mono, changed) | `app-ds-text-field` (+ `app-ds-cidr-field`, J0) | S-11 |
| `Segmented` | `app-ds-segmented` | S-11 |
| `Toggle` | `app-ds-toggle` | S-11 |
| `Tabs` (compteurs) | `app-ds-tabs` | S-11 |
| `Banner` | `app-ds-banner` | S-11 |
| `Panel` | `app-ds-panel` | S-11 |
| `ResourceIcon` (icône officielle, tuile) | `app-ds-resource-icon` | S-12 |
| `ResourceRow` | `app-ds-resource-row` | S-12 |
| `GeneratedName` | `app-ds-generated-name` | S-12 |
| `GoldenPathRail` | `app-ds-golden-path` | S-12 |
| Dialogue (motif du README) | `app-ds-dialog` (CDK Dialog) | S-12 |
| Tableau dense (motif) | `app-ds-table` | S-12 |
| Sélecteur natif stylé (maquette) | `app-ds-select` | S-12 |

Chaque composant a : une API d'entrées fidèle à `components/index.d.ts`, un test Vitest (rendu, accessibilité,
états), une page dans la galerie de développement `/dev/design-system` (montée seulement hors production).

## 5. Fidélité aux maquettes

Pour chaque écran :
1. Ouvrir `docs/design/maquette-v1/preview/<Écran>.html` (statique, hors ligne) ; c'est la référence visuelle.
2. Implémenter avec les composants `app-ds-*`, les libellés exacts, les espacements des tokens.
3. Test Playwright `e2e/visual/<ecran>.spec.ts` : capture de l'écran implémenté à 1440 px et 390 px, avec
   les données de démonstration de la recette ; comparaison **humaine** au moment de la revue (les captures
   sont jointes à la demande de revue, dossier `docs/plan/revues/captures/<R-nn>/`). Pas de comparaison
   pixel à pixel automatique avec la maquette : les données diffèrent.
4. Aucun défilement horizontal à 390 px (assertion Playwright `scrollWidth <= clientWidth`).

## 6. Configuration à l'exécution et authentification

- `config.json` (template) étendu : `{ "apiUrl": "...", "oidc": { "authority", "clientId", "scope",
  "provider": "keycloak" | "entra" } }`.
- `angular-auth-oidc-client` est configuré par `StsConfigHttpLoader` à partir de `config.json` (pas de
  constantes). Pour `provider = entra` : `authWellknownEndpointUrl` =
  `https://login.microsoftonline.com/common/v2.0`, validation stricte de l'émetteur désactivée à la lecture
  du document de découverte (multi-tenant), `customParamsAuthRequest: { prompt: 'select_account' }`.
- Intercepteurs (ordre) : base URL → jeton → organisation active (`X-Ifs-Organization`) → `Idempotency-Key`
  sur `POST/PUT/PATCH/DELETE` → erreurs (`problem+json` → `ApiError` typé, 409 de version → dialogue
  [VersionConflict](../design/maquette-v1/preview/VersionConflict.html)).

## 7. État et données

- Signaux et `httpResource` d'Angular 22 pour les lectures ; services de fonctionnalité fins autour du client
  généré pour les commandes.
- Pas de magasin global (NgRx) : l'état serveur est la vérité ; après chaque commande, la ressource concernée
  est relue ([RG-UI-02](../specs/26-interface.md)).
- Préférences (langue, favoris, récents) lues et écrites via l'API ([RG-UI-07](../specs/26-interface.md)).

## 8. Navigation

Routes (préfixe `/o/:orgId` pour tout ce qui dépend de l'organisation active) :

| Route | Écran (maquette) | Jalon |
|---|---|---|
| `/login` | [Login](../design/maquette-v1/preview/Login.html) | S-13 |
| `/invitations/:token` | [InviteAccept](../design/maquette-v1/preview/InviteAccept.html) | J0 |
| `/o/:orgId` | [Main](../design/maquette-v1/preview/Main.html) | J0 |
| `/o/:orgId/projects` | [Projects](../design/maquette-v1/preview/Projects.html) | J0 |
| `/o/:orgId/projects/new` | [NewProject](../design/maquette-v1/preview/NewProject.html) → Wizard* | J0 |
| `/o/:orgId/p/:projectId` | [ProjectOverview](../design/maquette-v1/preview/ProjectOverview.html) | J0 |
| `/o/:orgId/p/:projectId/environments` | [ProjectEnvironments](../design/maquette-v1/preview/ProjectEnvironments.html) | J0 |
| `/o/:orgId/p/:projectId/naming` | [ProjectNaming](../design/maquette-v1/preview/ProjectNaming.html) | J0 |
| `/o/:orgId/p/:projectId/findings` | [ProjectFindings](../design/maquette-v1/preview/ProjectFindings.html) | J0 |
| `/o/:orgId/p/:projectId/components/:componentId` | [Component](../design/maquette-v1/preview/Component.html) | J0 |
| `/o/:orgId/p/:projectId/resources/:resourceId/:tab` | Resource* | J0 |
| `/o/:orgId/p/:projectId/revisions[/:n]` | [Revisions](../design/maquette-v1/preview/Revisions.html), [RevisionDetail](../design/maquette-v1/preview/RevisionDetail.html) | J0 |
| `/o/:orgId/p/:projectId/publishing` | [ProjectPublishPlan](../design/maquette-v1/preview/ProjectPublishPlan.html) | J0 |
| `/o/:orgId/p/:projectId/install` | [InstallChecklist](../design/maquette-v1/preview/InstallChecklist.html) | J0 |
| `/o/:orgId/p/:projectId/deployments` | [Deployments](../design/maquette-v1/preview/Deployments.html) | J0 |
| `/o/:orgId/p/:projectId/members` | [ProjectMembers](../design/maquette-v1/preview/ProjectMembers.html) | J0 |
| `/o/:orgId/p/:projectId/settings` | [ProjectSettings](../design/maquette-v1/preview/ProjectSettings.html) | J0 |
| `/o/:orgId/p/:projectId/history` | [ProjectHistory](../design/maquette-v1/preview/ProjectHistory.html) | J1 |
| `/o/:orgId/settings/*` | Org* | J0 (membres, connexions), J1 (audit), J2 (équipes, rôles) |
| `/profile`, `/profile/tokens` | [Profile](../design/maquette-v1/preview/Profile.html), [ApiTokens](../design/maquette-v1/preview/ApiTokens.html) | J1 |
| `/backoffice/*` | Backoffice* | J1 |

La répartition complète écran → jalon est dans [`../design/README.md`](../design/README.md#écrans-et-jalons).
