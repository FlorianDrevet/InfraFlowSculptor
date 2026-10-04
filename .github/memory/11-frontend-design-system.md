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
_(à remplir à S-10, S-11, S-12 : génération des tokens, composants disponibles et leurs entrées)_
