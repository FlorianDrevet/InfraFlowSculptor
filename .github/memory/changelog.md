# Changelog de la mémoire

| Date | Auteur | Changement |
|---|---|---|
| 2026-10-04 | Claude | Création de la mémoire : vue d'ensemble, structure, agents, graphe, design system ; autres fichiers en attente du code |
| 2026-10-04 | Claude | DT-33 identité managée, DT-34 langues, DT-30 thème prêt, DT-22 alternatives, guide Keycloak, maquette corrigée (v28) |
| 2026-10-04 | Claude | DT-22 confirmée : Mediator remplace MediatR |
| 2026-10-04 | Claude | DT-08 confirmée : toutes les décisions techniques sont tranchées |
| 2026-10-04 | Claude | Revue du plan par Luna traitée (18 constats) : statut EN_ATTENTE_DE_RECETTE, jalons découpés refusés par gate.py |
| 2026-10-05 | Luna | S-01 — prérequis machine, configurations du dépôt, tests du garde-fou et hook pre-commit |
| 2026-10-05 | Luna | S-02 — squelette CQRS généré dans `src/backend/` et solution compilée |
| 2026-10-05 | Luna | S-03 — backend .NET 10, ServiceDefaults, Mediator, API `/v1/version`, ProblemDetails et retrait de l'auth locale |
| 2026-10-05 | Luna | S-04 — cinq projets de tests, tests Domain/API et règles d'architecture automatisées |
| 2026-10-05 | Luna | S-05 — AppHost Aspire, émulateurs, résolution de clients Azure, tests d'acceptation et recette locale |
| 2026-10-05 | Luna | S-06 — EF Core/PostgreSQL 17, isolation par organisation, concurrence, outbox, idempotence et migration initiale |
| 2026-10-05 | Luna | S-07 — authentification OIDC Keycloak/Entra, utilisateur courant, résolution d'adresse vérifiée, `/v1/me` et OAuth2 PKCE dans Scalar; recette locale partielle, volume Keycloak conservé |
| 2026-10-06 | Luna | S-08 — worker, outbox, sessions, fairness, leases et traces; conteneurs locaux persistants entre les lancements; corrections additives Keycloak conservant le volume; revue Sonnet traitée (renouvellement de bail, warmup worker, batch/retries d'outbox); recette Alice/Nina et import 26.6 validés; 57 tests et build 15 projets verts |
| 2026-10-06 | Luna | S-09 — application Angular 22, configuration runtime et OIDC; rôle offline_access corrigé sur les comptes locaux persistants, bearer limité à l'API; textes d'authentification traduits, sortie config ignorée par Git; recette Bob/profil/rechargement; revue Sonnet; 57 tests .NET, 4 Python, 3 Angular et build 15 projets verts |
