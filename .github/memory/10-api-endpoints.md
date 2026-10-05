# 10 — Routes de l'API

> **Rien d'implémenté au 2026-10-04.** À remplir par Luna aux étapes citées, d'après le code réel.
> Cible : DT-05, DT-06. Chaque étape qui ajoute une route l'inscrit ici (méthode, chemin, nom, permission, portée de débit).

## Faits vérifiés

- `GET /alive` : santé liveness fournie par ServiceDefaults en développement.
- `GET /v1/version` — `GetVersion`, anonyme : version informative de l'assembly et environnement (`VersionResponse`).
- Développement : OpenAPI `GET /openapi/v1.json`, Scalar `/scalar`, titre « InfraFlowSculptor API v1 ».
- Routes inconnues : `application/problem+json`, statut 404 et `traceId` via ProblemDetails/status-code pages.
- Limites disponibles : `read` 600/min, `write` 120/min, `generate` 10/min, `publish` 5/min; partition `oid`, sinon adresse IP; rejet 429 avec `Retry-After` et ProblemDetails.
- `ProblemDetailsMapper.ToProblem(List<Error>)` applique ErrorOr/metadata; les exceptions renvoient `code: INTERNAL` et `traceId` sans détail.
