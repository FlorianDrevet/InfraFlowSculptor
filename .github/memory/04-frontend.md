# 04 — Frontend

> S-14 implémenté le 2026-10-06 ; compléter aux étapes citées, d'après le code réel.
> Cible : `docs/technique/04-frontend.md`. Étapes S-09 à S-14, puis chaque étape UI.

## Faits vérifiés

- `src/frontend/ifs-web` est l'application Angular 22 générée depuis le template ng-template, intégrée à Aspire par `AddJavaScriptApp` sur le port 4200.
- `scripts/write-config.mjs` produit `public/config.json` depuis l'environnement d'exécution; le même build peut viser une API et un fournisseur OIDC configurés au démarrage.
- OIDC utilise `StsConfigHttpLoader` et `authInterceptor()`. L'intercepteur de base URL passe avant celui d'authentification; la route protégée est limitée au préfixe API `/v1/` pour éviter l'envoi du bearer à des hôtes ressemblants ou à des ressources non API.
- La page d'accueil utilise `Api.invoke(getMe, …)` depuis le client `ng-openapi-gen` et `rxResource`; le délai reste 10 s. `ApiConfiguration.rootUrl` reste vide afin que `apiBaseUrlInterceptor` fournisse l'URL d'API runtime.
- `npm run lint`, les tests Angular et `npm run build` passent après la configuration OIDC; le test de configuration couvre API absolue, hôte ressemblant et API de même origine.
- La route d'échec OIDC (`/unauthorized`) et la page profil utilisent Transloco en français et anglais; le scope de contrôle i18n vérifie les 11 clés utilisées. `prebuild` et `prestart` régénèrent `public/config.json`, ignoré par Git.
- S-10 génère les tokens Strata, le thème Tailwind v4, les thèmes et les données de galerie; la route `/dev/design-system` est visible seulement en mode dev. Les polices Instrument Sans et JetBrains Mono sont servies depuis Fontsource localement.
- S-13 ajoute la coquille desktop/mobile, son `NavigationRegistry`, la connexion Keycloak locale, les pages d'erreur/not-found, les préférences de langue et de thème; les contrôles de thème restent absents avec le seul thème sombre disponible.
- Les intercepteurs `X-Ifs-Organization`, `Idempotency-Key` et `Accept-Language` sont limités aux requêtes de l'API `/v1/` sur son origine configurée; les échanges OIDC Keycloak ne reçoivent donc pas ces en-têtes. L'appel `/v1/me` expire après 10 s et affiche une référence d'erreur locale.
- `npm run e2e` couvre les vues 1440×900 et 390×844, l'authentification locale, le tiroir mobile, le changement/persistance de langue, axe et la page d'erreur. Au 2026-10-06, 6 scénarios passent; `npm run lint`, `npm test`, `npm run build`, `npm run tokens:check` et `npx tsc --noEmit -p tsconfig.e2e.json` passent aussi.
- S-14 utilise `ng-openapi-gen` 1.1.0 (Observables) depuis `openapi/v1.json`; `npm run api:generate` normalise les fins de ligne LF et la fin de fichier puis laisse les sorties ignorées par ESLint/Prettier. `npm run api:check` compare les sorties normalisées via un dossier temporaire et renvoie 1 en cas de dérive.
