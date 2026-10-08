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

## Pilote Azure SQL — P-02 (2026-10-06)

- Le module AVM `sql/server` 0.22.0 accepte la création de bases via `databases`; la base pilote reste donc dans le module, avec son SKU explicite.
- Le module SQL ne prend pas de paramètre de diagnostic pour le serveur lui-même. `server-diagnostics.bicep`, ciblé sur le groupe de ressources, configure l’extension `Microsoft.Insights/diagnosticSettings`; les diagnostics de la base sont transmis au module SQL.
- L’accès applicatif orders utilise l’identité système de la Container App. `data-access.sql` crée un principal Entra avec le SID dérivé du ClientId, marque les principaux IFS par la propriété `ifs-managed` et refuse d’adopter un principal existant sans marqueur. `data-access-remove.sql` retire uniquement les rôles pris en charge et supprime seulement un principal marqué qui ne possède aucun objet.
- Les scripts et manifestes sont vérifiés hors ligne; aucun abonnement, groupe Entra ou serveur SQL Azure n’a été utilisé pour cette étape.
