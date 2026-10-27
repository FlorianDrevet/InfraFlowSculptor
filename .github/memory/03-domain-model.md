# 03 — Modèle de domaine

> Fondations ajoutées en S-03 ; J0-01 et J0-02 sont implémentées.
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
- J0-02 ajoute la gestion des rôles, le retrait et le départ des membres ; la dernière administratrice ne peut pas être retirée, partir ou perdre ce rôle (`RG-ORG-05`). `IProjectOwnershipCheck` reste un adaptateur sans projet jusqu'à J0-05.
- Les restrictions d'organisation stockent des domaines e-mail normalisés et des identifiants de tenants autorisés. Les membres hors de ces règles sont signalés dans la liste, sans être supprimés automatiquement. La migration `J0_02_OrganizationAccessRestrictions` ajoute ces propriétés.
- Les tests finaux J0-02 passent : Domain 17, Architecture 8, Application 7, Infrastructure 35, API 34 et Acceptance 3 ; le build .NET est sans avertissement ni erreur.
- J0-03 ajoute `InvitationAggregate` (adresse normalisée, rôles, état, expiration de 7 jours et empreinte SHA-256 du jeton) ; les jetons utilisent 32 octets aléatoires encodés en Base64URL. L'acceptation exige l'adresse vérifiée correspondante et un tenant autorisé ; une nouvelle tentative après acceptation est idempotente si l'appartenance existe.
- La migration `J0_03_Invitations` ajoute `organization_invitations`. `Invite`, `ResendInvitation`, `RevokeInvitation` et `AcceptInvitation` appliquent l'accès d'organisation ; `InvitationCreated` déclenche l'envoi via l'outbox.
- J0-04 ajoute les événements d'audit à l'unité de travail via `IAuditWriter` et les attributs `[Audited]`; `RenameOrganization` et `ChangeOrganizationMemberRoles` fournissent les différences avant/après dans la transaction de commande.
