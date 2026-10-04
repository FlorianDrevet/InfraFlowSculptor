---
name: angular-strata
description: "Use for any change in src/frontend/ifs-web: Angular 22 conventions from ng-template and the Strata design system rules, mockup fidelity and P9 (docs/technique/04-frontend.md, docs/design/README.md)."
---

# Angular et Strata

Références : `docs/technique/04-frontend.md`, `docs/design/README.md`, `docs/design/strata/README.md`.

- Composants autonomes, `OnPush`, sans zone ; `input()`/`output()`/`model()` ; signaux et `httpResource` ; client API
  généré (`core/api/generated`, jamais modifié à la main).
- **Aucun calcul métier** côté client : noms, valeurs effectives, implicites, constats viennent de l'API.
- **Maquette** (`docs/design/maquette-v1/preview/<Écran>.html`) : libellés mot pour mot (dans `public/i18n/fr.json`,
  puis `en.json`), disposition, hiérarchie. Les données affichées dans la maquette sont des exemples.
- **Strata** : uniquement `app-ds-*` et variables `--ifs-*` (ou classes Tailwind générées depuis les tokens) ; aucune
  couleur en dur ; un seul bouton `primary` par écran ; implicite en italique `text-3` ou pointillé, origine au survol,
  focus et toucher ; états dits par le texte, jamais par la seule couleur.
- **P9** : n'implémenter que les zones de l'étape ; les autres n'apparaissent pas (ni grisées, ni « bientôt »).
- Accessibilité WCAG 2.2 AA (vrais `<button>`, `<a href>`, `<label>`, focus visible) ; sous 760 px barre latérale en
  tiroir, aucune page plus large que l'écran.
- Chaque commande : `Idempotency-Key` (intercepteur) ; 409 de version → dialogue de conflit.
- Tests : Vitest pour composants et services ; Playwright pour les parcours (1440 et 390, `axe`).
