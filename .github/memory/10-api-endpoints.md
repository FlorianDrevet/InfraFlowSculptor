# 10 — Routes de l'API

> S-14 implémenté le 2026-10-06 ; J0-01/J0-02 ajoutent les routes d’organisation, les fonctions et les contrôles d’accès aux membres.
> Cible : DT-05, DT-06. Chaque étape qui ajoute une route l'inscrit ici (méthode, chemin, nom, permission, portée de débit).

## Faits vérifiés

- `GET /alive` : santé liveness fournie par ServiceDefaults en développement.
- `GET /v1/version` — `GetVersion`, anonyme : version informative de l'assembly et environnement (`VersionResponse`).
- Développement : OpenAPI `GET /openapi/v1.json`, Scalar `/scalar`, titre « InfraFlowSculptor API v1 ».
- Routes inconnues : `application/problem+json`, statut 404 et `traceId` via ProblemDetails/status-code pages.
- `GET /v1/me` — `GetMe`, authentification requise, limite `read` : identité synchronisée et préférences de langue/thème du profil.
- `PUT /v1/me/preferences` — `UpdateMePreferences`, authentification requise, limite `write`, route sans organisation ; valide `fr|en` et `system|dark|light`.
- `GET /v1/organizations` — `ListMyOrganizations`, authentification requise, limite `read`, route sans organisation ; renvoie uniquement les appartenances de l’utilisateur.
- `POST /v1/organizations` — `CreateOrganization`, authentification requise, limite `write`, route sans organisation ; le créateur devient administrateur.
- `GET /v1/organizations/{id}` — `GetOrganization`, authentification requise, limite `read`, en-tête `X-Ifs-Organization` obligatoire et adhésion vérifiée.
- `PUT /v1/organizations/{id}` — `RenameOrganization`, authentification requise, limite `write`, en-tête d’organisation et rôle `Administrator` obligatoires.
- `GET /v1/features` — `GetFeatures`, authentification requise, limite `read`, route sans organisation ; liste les clés du catalogue, leur état et leur disponibilité pour l’organisation active.
- Les routes `/v1/organizations` et `/v1/me` sont marquées `OrganizationAgnostic`; les routes détail/renommage masquent les organisations absentes ou étrangères derrière 404. Le middleware des fonctions renvoie également 404 quand une route requiert une fonction désactivée.
- En développement et en test, `POST /v1/dev/ping-job` enfile un job authentifié et renvoie 202 ; `GET /v1/dev/ping-job/{jobId}` retourne son état pour l'organisation courante. Ces routes sont exclues du document OpenAPI.
- Limites disponibles : `read` 600/min, `write` 120/min, `generate` 10/min, `publish` 5/min; partition `oid`, sinon adresse IP; rejet 429 avec `Retry-After` et ProblemDetails.
- `ProblemDetailsMapper.ToProblem(List<Error>)` applique ErrorOr/metadata; les exceptions renvoient `code: INTERNAL` et `traceId` sans détail.
- S-14 versionne `src/backend/InfraFlowSculptor.Api/openapi/v1.json`, produit au build par `Microsoft.Extensions.ApiDescription.Server`; `dotnet build` régénère le document. Le contexte de génération force l'autorité locale Keycloak pour garder le contrat déterministe, tandis que Scalar runtime utilise l'autorité configurée.
- Le client généré par `ng-openapi-gen` est normalisé en LF avec une seule fin de ligne afin que `git diff --check` et la comparaison soient reproductibles sous Windows/Linux.
- `OpenApiDocumentTests.BuildDocumentMatchesRuntimeOpenApiDocument` compare le document runtime au fichier commité. Si un endpoint ajoute des services injectés, garder leurs registrations de contexte build alignées avec la configuration runtime; le test protège le contrat.
- `ApiFactory` fixe `Auth:Authority` par `UseSetting` et une source mémoire ajoutée en dernier; `OpenApiDocumentTests` passe même quand le processus fournit volontairement une autre `Auth__Authority`.
- `GET /v1/organizations/{id}/members` — `ListOrganizationMembers`, membre, limite `read`; la recherche et les résultats sont limités aux membres de l’organisation.
- `PUT /v1/organizations/{id}/members/{memberId}/roles` — `ChangeOrganizationMemberRoles`, membre avec rôle `Administrator`, limite `write`, idempotence ; conflit `RG-ORG-05` si la dernière administratrice est rétrogradée.
- `DELETE /v1/organizations/{id}/members/{memberId}` — `RemoveOrganizationMember`, administrateur, limite `write`, idempotence ; protège la dernière administratrice.
- `POST /v1/organizations/{id}/leave` — `LeaveOrganization`, membre, limite `write`, idempotence ; protège la dernière administratrice.
- `PUT /v1/organizations/{id}/access-restrictions` — `UpdateOrganizationAccessRestrictions`, administrateur, limite `write`, idempotence ; enregistre domaines et tenants autorisés.
- J0-03 — `GET /v1/invitations/{token}` (`GetInvitationPreview`) et `POST /v1/invitations/{token}/accept` (`AcceptInvitation`) sont authentifiés, limités à `read`/`write` et marqués `OrganizationAgnostic`. Sous `/v1/organizations/{id}/invitations` : `GET` liste, `POST` invite, `POST /{invitationId}/resend` renvoie et `DELETE /{invitationId}` révoque ; les commandes de gestion exigent `Administrator`, sont limitées à `read`/`write` et idempotentes pour les écritures.
- J0-04 — `GET /v1/organizations/{id}/audit` (`ListOrganizationAuditEvents`) accepte `from`, `to`, `page` et `pageSize`; l'endpoint exige un membre, puis `[RequiresOrganizationRole]` réserve la requête aux administrateurs/auditeurs avec `audit.lire`. Le débit utilise la limite `read`, l'isolation reste celle de l'organisation courante et un refus est 403.
- J0-05 — `/v1/projects/{id}/permissions/me` et `/members/{memberId}/permissions` sont des lectures `read` protégées par `project.read`; un projet sans accès reste masqué en 404. `POST /members/{memberId}/roles` et `DELETE /members/{memberId}/roles/{role}` sont `write`, idempotents et exigent `members.manage` ; la matrice d'attribution RG-ORG-23 et le dernier propriétaire RG-ORG-11 sont vérifiés dans les handlers/domaine. `POST /ownership` est `write`, idempotent, exige l'administrateur d'organisation et le droit de lire le projet, puis écrit l'audit. L'autorisation projet est vérifiée avant le rôle d'organisation pour préserver le 404 des projets invisibles.
- R-04 : `GET /v1/organizations/{id}/audit` rejette avec `AUDIT_PAGE_INVALID` un index de page dont l'offset dépasse `Int32.MaxValue`; l'API évite ainsi un overflow et un 500 sur une requête extrême.
- R-04 : `POST /v1/organizations` utilise `Idempotency-Key` scoppé par tenant et utilisateur dans `user_idempotency_keys`, car aucune organisation active n'existe lors de sa création. Les écritures avec organisation active dérivent la clé stockée de `(tenantId, objectId, clientKey)` pour isoler les réponses entre utilisateurs. Réservation, commande et réponse sont transactionnelles ; une réponse identique est rejouée et une clé réutilisée avec un autre corps reçoit `IDEMPOTENCY_KEY_REUSED`.
