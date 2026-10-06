# 11 — Design system Strata dans le frontend

## Source
- `docs/design/strata/` (export du 2026-10-03, version `1791059335-ccc5`) : `README.md` (règles), `tokens.json`
  (valeurs), `components/bundle.css` (géométrie), `components/index.d.ts` (API), `rendered/` (aperçus).
- Maquette : `docs/design/maquette-v1/preview/` (72 écrans, version `1791061331-d805`).

## Règles (DT-19)
- Sombre uniquement (DT-30). Couleur = signal : `signal` pour agir (un seul bouton primaire par écran), `ember` pour ce
  qui a changé, `identity-*` pour identités et accès.
- Instrument Sans (400/500/600) et JetBrains Mono ; grille 4 px ; rayons 6/10/12 ; une seule ombre (`shadow-overlay`).
- Composants Angular `app-ds-*` (correspondance : `docs/technique/04-frontend.md` § 4).

## Implémentation

### S-10 — Fondations (2026-10-06)
- `src/frontend/ifs-web/scripts/build-tokens.mjs` lit `docs/design/strata/tokens.json` sans dépendance Node tierce et génère `src/styles/tokens.css`, `src/styles/theme.css`, `src/app/core/theme/themes.generated.ts` et `foundations.generated.ts`.
- `npm run tokens` régénère; `npm run tokens:check` compare les quatre sorties octet par octet sans les réécrire; `npm run tokens:test` teste CSS exact, thèmes multiples avec valeur de repli, et le mode `--check`.
- Tailwind v4 reçoit les espaces, couleurs, rayons, ombres, tailles, polices et échelle typographique via `@theme`. `styles.css` importe Tailwind, le thème, les tokens, les polices et la base dans cet ordre.
- Instrument Sans 400/500/600 et JetBrains Mono 400/500 viennent des paquets Fontsource et sont servis comme fichiers locaux; aucune URL Google Fonts n'apparaît dans le build.
- La route `/dev/design-system` n'est enregistrée qu'en développement (`isDevMode()`); sa page « Fondations » lit les exports générés et expose toutes les couleurs, les groupes typographiques, les espacements et les rayons.
- Validations : `npm test` (3 tests Angular + 3 générateur), lint/i18n (11 clés), `tokens:check`, build Angular. La galerie locale a été inspectée; l'ouverture de la référence `file://` a été bloquée par la politique du navigateur, donc la comparaison visuelle côte à côte est partielle. Les valeurs affichées ont été comparées aux exports produits depuis le JSON source.

S-11 et S-12 : composants Strata et motifs à documenter d'après le code réel.
