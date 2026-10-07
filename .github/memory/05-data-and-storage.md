# 05 — Données et stockage

> **État vérifié au 2026-10-05.** À compléter aux étapes citées, d'après le code réel.
> Cible : `docs/technique/02-backend.md` § 4, DT-04, DT-09, DT-25, DT-31, DT-32. Étapes S-06, S-08, J0-04.

## Faits vérifiés

- `AddIfsDbContext("ifs")` utilise `ConnectionStrings:ifs` en local/Aspire. En Azure, `PostgresDataSourceFactory` lit
  `Azure:ifs:Endpoint`, `Database` et `Username`, impose TLS et renouvelle un jeton Entra PostgreSQL toutes les
  50 minutes; aucun mot de passe n'est configuré.
- L'AppHost local épingle PostgreSQL 17 et son volume nommé `ifs-postgres-17-data`; le volume préexistant de
  PostgreSQL 18.3 reste conservé. `EFCore.NamingConventions` applique `snake_case`.
- `IfsDbContext` convertit les identifiants forts en `Guid`, filtre globalement `IOrganizationOwned` sur
  `ICurrentOrganization`, et utilise `Version` comme jeton de concurrence optimiste qu'il incrémente aux écritures.
- Les tables transverses sont `outbox_messages`, `idempotency_keys` et `scheduled_job_leases`; la migration initiale
  est `20261005144850_S_06_Socle`.
- `UnitOfWorkBehavior` enveloppe les commandes d'une transaction, sauvegarde une fois et écrit les événements de
  domaine dans l'outbox dans cette transaction. Les conflits sont renvoyés en `CONFLICT_VERSION`.
- L'idempotence est limitée à l'organisation et à la clé; le hash de requête détecte la réutilisation avec un autre
  corps (422), et la réponse stockée permet le rejeu. La purge des clés expirées est sous `Persistence/Admin` et
  utilise `IgnoreQueryFilters()` uniquement dans cette requête d'administration.
- **Écart à valider à R-01 :** `ConcurrencyConflictException` est déclarée dans `Application.Common.Persistence`,
  pas dans Infrastructure comme écrit au plan, afin que `UnitOfWorkBehavior` puisse la traduire sans dépendance de
  l'Application vers Infrastructure.
- Génération d'une migration : `dotnet ef migrations add <Nom> -p src/backend/InfraFlowSculptor.Infrastructure
  -s src/backend/InfraFlowSculptor.Api -o Persistence/Migrations`; la fabrique de conception emploie une chaîne
  locale factice.
