# 04 — Frontend

> S-09 implémenté le 2026-10-06 ; compléter aux étapes citées, d'après le code réel.
> Cible : `docs/technique/04-frontend.md`. Étapes S-09 à S-14, puis chaque étape UI.

## Faits vérifiés

- `src/frontend/ifs-web` est l'application Angular 22 générée depuis le template ng-template, intégrée à Aspire par `AddJavaScriptApp` sur le port 4200.
- `scripts/write-config.mjs` produit `public/config.json` depuis l'environnement d'exécution; le même build peut viser une API et un fournisseur OIDC configurés au démarrage.
- OIDC utilise `StsConfigHttpLoader` et `authInterceptor()`. L'intercepteur de base URL passe avant celui d'authentification; la route protégée est limitée au préfixe API `/v1/` pour éviter l'envoi du bearer à des hôtes ressemblants ou à des ressources non API.
- La page d'accueil utilise `httpResource('/v1/me')`; le test navigateur S-09 s'est connecté en Bob, a affiché « Hello Bob Durand » et a conservé la session au rechargement.
- `npm run lint`, les tests Angular et `npm run build` passent après la configuration OIDC; le test de configuration couvre API absolue, hôte ressemblant et API de même origine.
- La route d'échec OIDC (`/unauthorized`) et la page profil utilisent Transloco en français et anglais; le scope de contrôle i18n vérifie les 11 clés utilisées. `prebuild` et `prestart` régénèrent `public/config.json`, ignoré par Git.
