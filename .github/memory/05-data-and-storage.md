# 05 — Données et stockage

> **État initial vérifié au 2026-10-05**, complété par les étapes ci-dessous d'après le code réel.
> Cible : `docs/technique/02-backend.md` § 4, DT-04, DT-09, DT-25, DT-31, DT-32. Étapes S-06, S-08, J0-04.

## Faits vérifiés

- `AddIfsDbContext("ifs")` utilise `ConnectionStrings:ifs` en local/Aspire. En Azure, `PostgresDataSourceFactory` lit
  `Azure:ifs:Endpoint`, `Database` et `Username`, impose TLS et renouvelle un jeton Entra PostgreSQL toutes les
  50 minutes; aucun mot de passe n'est configuré.
- L'AppHost local épingle PostgreSQL 17 et son volume nommé `ifs-postgres-17-data`; le volume préexistant de
  PostgreSQL 18.3 reste conservé. `EFCore.NamingConventions` applique `snake_case`.
- `IfsDbContext` convertit les identifiants forts en `Guid`, filtre globalement `IOrganizationOwned` sur
  `ICurrentOrganization`, et utilise `Version` comme jeton de concurrence optimiste qu'il incrémente aux écritures.
- Les tables transverses sont `outbox_messages`, `idempotency_keys`, `user_idempotency_keys` et `scheduled_job_leases`; la migration initiale
  est `20261005144850_S_06_Socle`.
- `UnitOfWorkBehavior` enveloppe les commandes d'une transaction, sauvegarde une fois et écrit les événements de
  domaine dans l'outbox dans cette transaction. Les conflits sont renvoyés en `CONFLICT_VERSION`.
- Les écritures idempotentes sont isolées par tenant, utilisateur et clé cliente. Pour `POST /v1/organizations`, exécuté
  avant toute organisation active, `user_idempotency_keys` applique une contrainte sur les trois colonnes de portée ; la
  migration est `20261027170637_R_04_UserIdempotencyKeys`. Dans une organisation, la clé persistée est dérivée de
  `(tenantId, objectId, clientKey)` pour éviter de rejouer une réponse entre utilisateurs. Réservation, écriture métier
  et réponse stockée partagent une transaction ; le hash détecte la réutilisation avec un autre corps (422) et
  `IdempotencyKeyPurge` purge les deux tables après 24 h.
- **Écart à valider à R-01 :** `ConcurrencyConflictException` est déclarée dans `Application.Common.Persistence`,
  pas dans Infrastructure comme écrit au plan, afin que `UnitOfWorkBehavior` puisse la traduire sans dépendance de
  l'Application vers Infrastructure.
- Génération d'une migration : `dotnet ef migrations add <Nom> -p src/backend/InfraFlowSculptor.Infrastructure
  -s src/backend/InfraFlowSculptor.Api -o Persistence/Migrations`; la fabrique de conception emploie une chaîne
  locale factice.
- J0-03 ajoute `organization_invitations` (adresse, rôles, expiration, état et `token_hash` unique) par la migration `J0_03_Invitations`. L'événement `InvitationCreated` passe par `outbox_messages` dans la transaction de la commande puis enfile un job `notifications`; le contenu d'outbox est vidé après envoi ou dix échecs. Le payload conserve le jeton brut pendant la livraison et n'est pas chiffré au niveau applicatif; R-04 consigne ce risque résiduel, car aucun mécanisme de clés partagé entre API et Worker n'est spécifié.
- J0-04 ajoute `audit_events`, `audit_personal_data` et `audit_purge_requests` (migration `20261027122817_J0_04_AuditEvents`). Un déclencheur refuse `UPDATE`/`DELETE` sur `audit_events`; `purge_audit_events` passe par la table de requêtes et borne le cutoff à 13 mois. Les noms/adresses personnels sont séparés, l'acteur reste référencé par identifiant opaque. La recette CI #40522297231 valide l'écriture/lecture ; le déclencheur local a été vérifié par psql après que le profil pgweb sans connexion a empêché l'exécution dans son UI.
- R-04 renforce le déclencheur : une suppression imbriquée exige le marqueur transactionnel posé par la fonction de purge. `PUBLIC` n'a pas `EXECUTE` sur `purge_audit_events`; avant l'appel depuis le Worker hébergé, J0-31 doit donner ce droit à l'identité Worker réellement utilisée. Le worker n'est pas déployé par R-04.

## Pilote Azure SQL — P-02 (2026-10-06)

- Le module AVM `sql/server` 0.22.0 accepte la création de bases via `databases`; la base pilote reste donc dans le module, avec son SKU explicite.
- Le module SQL ne prend pas de paramètre de diagnostic pour le serveur lui-même. `server-diagnostics.bicep`, ciblé sur le groupe de ressources, configure l’extension `Microsoft.Insights/diagnosticSettings`; les diagnostics de la base sont transmis au module SQL.
- L’accès applicatif orders utilise l’identité système de la Container App. `data-access.sql` crée un principal Entra avec le SID dérivé du ClientId, marque les principaux IFS par la propriété `ifs-managed` et refuse d’adopter un principal existant sans marqueur. `data-access-remove.sql` retire uniquement les rôles pris en charge et supprime seulement un principal marqué qui ne possède aucun objet.
- Les scripts et manifestes sont vérifiés hors ligne; aucun abonnement, groupe Entra ou serveur SQL Azure n’a été utilisé pour cette étape.
- J0-06 stocke les versions du catalogue comme JSON embarqués dans `InfraFlowSculptor.Catalog`; aucune table, migration ni ressource Azure n'est nécessaire. Le chargeur calcule une empreinte SHA-256 déterministe des documents de chaque version. La CI restaure les modules Bicep AVM épinglés dans son cache pour vérifier les correspondances sans connexion ni écriture Azure.

## J0-11 — persistance sans PostgreSQL JSONB

- Les valeurs métier de ressources sont relationnelles : nœuds scalaires et nœuds `array`/`object` typés, relations
  dédiées pour propriétés, surcharges, présence, IDs existants, noms forcés, enfants et tags. Les arbres sont limités
  en profondeur et en nombre de nœuds. Les valeurs `array`/`object` déclarent le type racine au catalogue et conservent
  récursivement leurs valeurs imbriquées.
- La migration `20261028141051_J0_11_ResourcesAndRelationalStorage` convertit les colonnes JSONB préexistantes
  `project_wizard_drafts.content_json`, `audit_events.diff`, `idempotency_keys.response`,
  `user_idempotency_keys.response` et `outbox_messages.payload` en `text`, en préservant leurs valeurs JSON. Elle ajoute
  aussi les tables de ressources et l'ID persistant des cibles `Single`. Son `Down` refuse de rétablir JSONB.
- Règle active : aucune colonne PostgreSQL `jsonb`. Le JSON sérialisé en `text` est réservé aux protocoles opaques
  existants ; les données requêtables du modèle sont relationnelles. Test d'intégration `JsonbMigrationTests` vérifie
  conservation des valeurs et zéro colonne `jsonb` après migration.
