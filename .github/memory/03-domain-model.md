# 03 — Modèle de domaine

> Fondations ajoutées en S-03 ; J0-01 est en cours.
> Cible : `docs/specs/05-modele-de-donnees.md`, `docs/technique/02-backend.md` § 2-3.

## Faits vérifiés

- `Domain/Common/Models/` contient `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `EnumValueObject<TEnum>` et `IHasVersion`.
- `Application/Common/Extensibility/` contient `IKeyed<TKey>`, `Registry<TKey,TService>` et les attributs de métadonnées du plan.
- Les tests Application couvrent le registre, le dispatcher de jobs, les événements de domaine, la confidentialité des journaux et les compteurs de télémétrie.
- J0-01 ajoute `UserProfileAggregate.UserProfile` (identité `(TenantId, ObjectId)`, nom, adresse vérifiée, langue/thème et dates) et `OrganizationAggregate` (nom de 2 à 80 caractères, membres et version). Le créateur reçoit `Administrator`; les rôles sont `Administrator`, `ConnectionManager`, `Auditor` et `Member`.
- Les profils se synchronisent dans `UserProfileSyncMiddleware` à la création puis lorsque `LastSeenAt` dépasse cinq minutes. `OrganizationContextMiddleware` contrôle l’adhésion avant les routes d’organisation ; le renommage exige le rôle administrateur et est couvert par les tests API.
- Les tranches Application présentes sont `CreateOrganization`, `ListMyOrganizations`, `GetOrganization`, `RenameOrganization` et `UpdateUserPreferences`. La création passe par `IPlanLimitGuard`; l’implémentation actuelle est `UnlimitedPlanLimitGuard`.
- `AuthorizationBehavior` applique `[RequiresOrganizationRole(OrganizationRole.Administrator)]` avant validation et unité de travail. `FeatureCatalog` recense les clés et états des lots 1 à 4 ; `IFeatureAvailability` prend en charge les activations par organisation.
- La migration `J0_01_Organizations` ajoute `user_profiles`, `organizations`, `organization_members` et `organization_feature_access`. `seed demo` prépare Contoso/Alice-Bob-Chloé, Fabrikam/David et les profils sans organisation Emma/Nina.
- Au 2026-10-27, les tests Domain (12), Application (7), API (25), Architecture (8) et Acceptance (3) passent ; le build de la solution passe sans avertissement ni erreur. Les fixtures de jobs créent maintenant leurs organisations et fournissent `X-Ifs-Organization`, comme l’impose le middleware de contexte.
