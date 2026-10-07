# 10 — Routes de l'API

> S-14 implémenté le 2026-10-06 ; compléter aux étapes suivantes, d'après le code réel.
> Cible : DT-05, DT-06. Chaque étape qui ajoute une route l'inscrit ici (méthode, chemin, nom, permission, portée de débit).

## Faits vérifiés

- `GET /alive` : santé liveness fournie par ServiceDefaults en développement.
- `GET /v1/version` — `GetVersion`, anonyme : version informative de l'assembly et environnement (`VersionResponse`).
- Développement : OpenAPI `GET /openapi/v1.json`, Scalar `/scalar`, titre « InfraFlowSculptor API v1 ».
- Routes inconnues : `application/problem+json`, statut 404 et `traceId` via ProblemDetails/status-code pages.
- `GET /v1/me` — `GetMe`, authentification requise, limite `read` : `tenantId`, `objectId`, `displayName`, `verifiedEmail` dérivés de `ICurrentUser`.
- En développement et en test, `POST /v1/dev/ping-job` enfile un job authentifié et renvoie 202 ; `GET /v1/dev/ping-job/{jobId}` retourne son état pour l'organisation courante. Ces routes sont exclues du document OpenAPI.
- Limites disponibles : `read` 600/min, `write` 120/min, `generate` 10/min, `publish` 5/min; partition `oid`, sinon adresse IP; rejet 429 avec `Retry-After` et ProblemDetails.
- `ProblemDetailsMapper.ToProblem(List<Error>)` applique ErrorOr/metadata; les exceptions renvoient `code: INTERNAL` et `traceId` sans détail.
- S-14 versionne `src/backend/InfraFlowSculptor.Api/openapi/v1.json`, produit au build par `Microsoft.Extensions.ApiDescription.Server`; `dotnet build` régénère le document. Le contexte de génération force l'autorité locale Keycloak pour garder le contrat déterministe, tandis que Scalar runtime utilise l'autorité configurée.
- Le client généré par `ng-openapi-gen` est normalisé en LF avec une seule fin de ligne afin que `git diff --check` et la comparaison soient reproductibles sous Windows/Linux.
- `OpenApiDocumentTests.BuildDocumentMatchesRuntimeOpenApiDocument` compare le document runtime au fichier commité. Si un endpoint ajoute des services injectés, garder leurs registrations de contexte build alignées avec la configuration runtime; le test protège le contrat.
- `ApiFactory` fixe `Auth:Authority` par `UseSetting` et une source mémoire ajoutée en dernier; `OpenApiDocumentTests` passe même quand le processus fournit volontairement une autre `Auth__Authority`.
