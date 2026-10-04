# Jalon 0 — Pilote

**But** ([04 § 2.0](../specs/04-perimetre-et-lots.md), [DEC-93](../specs/03-decisions.md)) : qu'une équipe pilote crée
**puis fasse évoluer** un service conforme à ses conventions, avec Bicep, Azure DevOps et Azure Repos, deux
environnements, une application conteneur, un catalogue réduit — et qu'IFS **génère** la sortie prouvée en
phase P.

**Périmètre** : le tableau de [04 § 2.0](../specs/04-perimetre-et-lots.md). **Exclu** : suppression d'un composant
déployé, rôles par composant et demandes d'accès entre équipes, coffres existants, réseau privé, Static Web
App, jetons d'API, historique du modèle (jalon 1), brouillons (jalon 2).

**Critère de sortie** : [reference-pilote § 4](reference-pilote.md#4-critères-dacceptation-du-jalon-0), cette fois
avec la sortie **générée par IFS** ; preuves P1–P5, P7–P9 « vérifiées » ; recette
[`recettes/02-jalon-0.md`](recettes/02-jalon-0.md) ; verrou [`R-08`](#-r-08--sortie-du-jalon-0).

**Règle d'interface pour tout le jalon.** Chaque étape UI cite les écrans de `docs/design/maquette-v1/preview/`
et liste ses **zones exclues** : elles n'apparaissent pas ([P9](../specs/01-principes.md)). Libellés mot pour mot.
Captures 1440 et 390 jointes à la demande de revue du segment.

**Règle API pour tout le jalon.** Chaque nouvelle route a : son test d'isolation (scénario déclaré dans
`IsolationScenarios`), sa portée de limitation de débit, `.WithName()`, et le client Angular régénéré
(`npm run api:generate`).

---

## Segment « accès » — branche `impl/j0-acces`

### J0-01 — Profils d'utilisateur et organisations

| | |
|---|---|
| **Spécifications** | [10 § 2-3](../specs/10-organisations-et-acces.md) : RG-ORG-02, UC-ORG-01, UC-ORG-02, RG-ORG-04 ; [RG-DON-01](../specs/05-modele-de-donnees.md) ; [EXG-01](../specs/27-exigences-non-fonctionnelles.md) |
| **Maquette** | [OrgSettings](../design/maquette-v1/preview/OrgSettings.html) — zones : nom de l'organisation ; **exclues** : plan et facturation, tenants/domaines autorisés (J0-02), politiques, télémétrie (J1), accès support (J1) ; sélecteur d'organisation de [Sidebar](../design/maquette-v1/preview/Sidebar.html) |
| **Dépend de** | R-03 |
| **Commit** | `feat(organisation): profils d'utilisateur, création et choix de l'organisation` |

🎯 **Objectif.** Un utilisateur connecté a un profil tenu à jour ; il crée une organisation dont il devient
administrateur et choisit l'organisation active. Le cadre des tests d'isolation de **toutes** les routes existe.

🔧 **À faire.**
1. Domaine `UserProfileAggregate` (`UserProfile` : `UserKey (TenantId, ObjectId)`, `DisplayName`, `Email?`,
   `PreferredLanguage`, `CreatedAt`, `LastSeenAt`), `OrganizationAggregate` (`Organization` : nom 2–80,
   `Members` : `OrganizationMember (UserProfileId, Roles : OrganizationRole[])`, version) ; `OrganizationRole` :
   `Administrator`, `ConnectionManager`, `Auditor`, `Member` ([10 § 3.1](../specs/10-organisations-et-acces.md)).
2. Middleware `UserProfileSyncMiddleware` : à chaque requête authentifiée, crée ou met à jour le profil depuis les
   revendications si `LastSeenAt` a plus de 5 min ([RG-ORG-02](../specs/10-organisations-et-acces.md)). `PreferredLanguage`
   et `PreferredTheme` (`system` par défaut) sont exposés par `GET /v1/me` et modifiables par `PUT /v1/me/preferences` ;
   `LanguageService` et `ThemeService` du frontend lisent la préférence serveur en premier ([RG-UI-07](../specs/26-interface.md),
   [DT-34](../technique/01-decisions.md#dt-34--langues--français-et-anglais-commutables)).
3. Tranches : `CreateOrganization` (UC-ORG-01, créateur administrateur), `ListMyOrganizations`,
   `GetOrganization`, `RenameOrganization` (administrateur). Routes `/v1/organizations` (non soumises à
   l'en-tête d'organisation) et `/v1/organizations/{id}`.
4. `OrganizationContextMiddleware` : lit `X-Ifs-Organization`, vérifie l'adhésion, positionne
   `ICurrentOrganization` ; absent ou étranger → 404 `NOT_FOUND` ; les routes « hors organisation » sont
   marquées `.WithMetadata(new OrganizationAgnostic())`.
5. Cadre EXG-01 : `Api.Tests/Isolation/IsolationScenarios.cs` (dictionnaire nom de route → fabrique de requête),
   `EndpointIsolationTests` de [technique 06 § 4](../technique/06-tests-et-qualite.md#4-isolation-exg-01) ; données
   `IsolationFixture` (Contoso avec alice, Fabrikam avec david). Routes existantes déclarées
   (`GetMe`, `GetVersion` marquée publique, routes de dev exclues hors `Development`).
6. Migration `J0_01_Organizations`. Données de démonstration (`seed demo`, options `--without-projects` pour partir sans projet, [technique 05 § 4](../technique/05-execution-locale.md#4-données-de-démonstration)) : organisations Contoso et Fabrikam.
7. Interface : `ActiveOrganizationStore` (signal, persistance locale de la dernière organisation choisie) ;
   sélecteur d'organisation dans la barre latérale (forme de `Sidebar.html`) ; page « Créer une organisation »
   quand l'utilisateur n'en a aucune ; page réglages de l'organisation (nom). Entrées de navigation enregistrées.

✅ **Vérification automatique.** Tests de domaine (nom 2–80, administrateur à la création), d'application
(`CreateOrganization`), d'API (isolation : david ne voit pas Contoso → 404) ; e2e `organization.spec.ts`.

🧪 **Test manuel.**
1. `seed demo` puis connexion en alice → organisation « Contoso » active, nom en haut de la barre latérale.
2. Créer « Contoso Labs » → elle devient active ; revenir à « Contoso » par le sélecteur.
3. Connexion en david → seule « Fabrikam » est proposée ; ouvrir à la main l'URL d'une page de Contoso
   (copiée depuis la session d'alice) → page « introuvable ».
4. Connexion en emma (aucune organisation) → page « Créer une organisation ».

🧠 **Mémoire.** `03-domain-model.md`, `10-api-endpoints.md`, `04-frontend.md` (organisation active).

### J0-02 — Membres et réglages d'accès de l'organisation

| | |
|---|---|
| **Spécifications** | [10 § 3, § 5](../specs/10-organisations-et-acces.md) : RG-ORG-05, RG-ORG-07, UC-ORG-11, UC-ORG-12, UC-ORG-13 ; tenants et domaines autorisés |
| **Maquette** | [OrgMembers](../design/maquette-v1/preview/OrgMembers.html) — zones : liste des membres, rôles d'organisation, retrait ; **exclues** : équipes (J2), invitations (J0-03) ; [OrgSettings](../design/maquette-v1/preview/OrgSettings.html) — zones : domaines et tenants autorisés |
| **Dépend de** | J0-01 |
| **Commit** | `feat(organisation): membres, rôles d'organisation, tenants et domaines autorisés` |

🎯 **Objectif.** L'administrateur gère les membres et restreint qui peut entrer.

🔧 **À faire.**
1. Commandes `ChangeMemberRoles`, `RemoveMember`, `LeaveOrganization`, `UpdateAccessRestrictions`
   (`AllowedEmailDomains[]`, `AllowedTenantIds[]`) ; requête `ListMembers` (recherche limitée aux membres,
   [RG-ORG-07](../specs/10-organisations-et-acces.md)).
2. Règles : dernier administrateur ([RG-ORG-05](../specs/10-organisations-et-acces.md)) — erreur `RG-ORG-05` ;
   dernier propriétaire d'un projet (le contrôle existera en J0-05 ; ici un port `IProjectOwnershipCheck` qui
   renvoie « aucun projet » jusqu'à J0-05) ; tenants autorisés ⇒ comptes personnels exclus.
3. Écrans de la maquette (zones retenues) ; dialogue d'impact au retrait ([RG-UI-03](../specs/26-interface.md)).

✅ **Vérification automatique.** Tests de chaque règle ; isolation des nouvelles routes ; e2e.

🧪 **Test manuel.**
1. alice : donner à bob le rôle « Gestionnaire des connexions » → visible dans la liste.
2. alice : tenter de se retirer elle-même alors qu'elle est seule administratrice → message citant la règle.
3. alice : restreindre aux tenants Contoso → la liste des membres signale emma (si elle était membre) comme hors
   restriction ; ajouter le domaine `contoso.example`.
4. chloe (membre simple) : la page des membres est en lecture seule.

🧠 **Mémoire.** `03-domain-model.md`, `10-api-endpoints.md`.

### J0-03 — Invitations et e-mails

| | |
|---|---|
| **Spécifications** | [10 § 3.2](../specs/10-organisations-et-acces.md) : UC-ORG-03, UC-ORG-04, UC-ORG-05, RG-ORG-06 ; [26 § 4](../specs/26-interface.md) (invitation, e-mail toujours) ; [DT-07](../technique/01-decisions.md#dt-07--authentification--oidc-entra-en-azure-keycloak-en-local) (adresse vérifiée), [DT-11](../technique/01-decisions.md#dt-11--e-mail--acs-en-azure-mailpit-en-local) |
| **Maquette** | [OrgMembers](../design/maquette-v1/preview/OrgMembers.html) — zones : invitations en attente, inviter, renvoyer, révoquer ; **exclues** : équipes, rôles de projet dans l'invitation (J0-05 les active) ; [InviteAccept](../design/maquette-v1/preview/InviteAccept.html) |
| **Dépend de** | J0-02 |
| **Commit** | `feat(organisation): invitations par e-mail et acceptation vérifiée` |

🎯 **Objectif.** Inviter quelqu'un par e-mail ; l'invité n'entre que si son adresse **vérifiée** correspond.

🔧 **À faire.**
1. `InvitationAggregate` (adresse, rôles d'organisation, expiration 7 jours, jeton aléatoire 256 bits dont seule
   l'empreinte est stockée, état `Pending|Accepted|Revoked|Expired`).
2. Commandes `Invite`, `ResendInvitation`, `RevokeInvitation`, `AcceptInvitation` ; refus de domaine ou tenant non
   autorisé ([RG-ORG-06](../specs/10-organisations-et-acces.md)) ; acceptation : `ICurrentUser.VerifiedEmail` doit
   égaler l'adresse (insensible à la casse), tenant autorisé, sinon erreur expliquant qu'une nouvelle invitation
   doit être envoyée à la bonne adresse.
3. Port `IEmailSender` + adaptateurs SMTP (MailKit → MailPit) et ACS (`EmailClient(Uri, IfsAzureCredential)`),
   choisis par la règle de [DT-33](../technique/01-decisions.md#dt-33--identité-managée-partout--chaîne-de-connexion-seulement-en-local) (nom `email` : chaîne `ConnectionStrings:email` = MailPit, sinon
   `Azure:email:Endpoint` + identité managée) ; gabarit
   `Invitation.fr.html/.txt` et `.en` (nom de l'organisation, lien `/invitations/{jeton}`, expiration ; rien
   d'autre). Envoi par la file `notifications` (outbox).
4. Interface : section invitations ; page d'acceptation (`InviteAccept.html`) avec les trois issues (acceptée,
   adresse non vérifiée ou différente, expirée).

✅ **Vérification automatique.** Tests : jeton stocké sous forme d'empreinte ; expiration ; adresse différente ;
`email_verified=false` ; e-mail mis en file dans la transaction ; isolation.

🧪 **Test manuel.**
1. alice invite `nina@contoso.example` puis `bob2@contoso.example`.
2. MailPit → deux e-mails, en français, avec le lien.
3. Ouvrir le lien de nina, se connecter en nina → refus « adresse non vérifiée ».
4. Inviter `emma@outlook.example` alors que l'organisation est limitée au tenant Contoso → refus à l'envoi.
5. Lever la restriction, réinviter emma, ouvrir le lien, se connecter en emma → elle rejoint Contoso.
6. Révoquer l'invitation de bob2 puis ouvrir son lien → « invitation révoquée ».

🧠 **Mémoire.** `03-domain-model.md`, `05-data-and-storage.md` (e-mail).

### J0-04 — Journal d'audit (écriture et consultation simple)

| | |
|---|---|
| **Spécifications** | [10 § 8](../specs/10-organisations-et-acces.md) : RG-ORG-20, RG-ORG-21, RG-ORG-22, UC-ORG-19 ; [EXG-06](../specs/27-exigences-non-fonctionnelles.md) ; [DT-25](../technique/01-decisions.md#dt-25--journal-daudit-en-ajout-seul) |
| **Maquette** | [OrgAudit](../design/maquette-v1/preview/OrgAudit.html) — zones : liste, filtre par période ; **exclues** : filtres par auteur et objet, export CSV (J3) |
| **Dépend de** | J0-03 |
| **Commit** | `feat(audit): journal d'audit en ajout seul` |

🎯 **Objectif.** Toute action de J0-01 à J0-03, et toutes les suivantes, laisse une trace inaltérable.

🔧 **À faire.**
1. Table `audit_events (id, organization_id, project_id?, occurred_at, actor_kind, actor_id, actor_display,
   via (Screen|Api|Mcp|Support), action, object_type, object_id, diff jsonb)` ; déclencheur PostgreSQL qui refuse
   `UPDATE`/`DELETE` ; fonction `purge_audit_events(cutoff)` `SECURITY DEFINER`, seule voie de suppression.
2. `IAuditWriter` appelé par `UnitOfWorkBehavior` : chaque commande déclare son action (`[Audited("organization.member.removed")]`)
   et fournit avant/après ; écrit dans la même transaction. Rattrapage des commandes de J0-01 à J0-03.
3. Tâche planifiée `PurgeAuditEventsJob` (quotidienne, > 13 mois).
4. Requête `ListAuditEvents` (administrateur ou auditeur d'organisation ; période ; pagination serveur) et écran.

✅ **Vérification automatique.** Test d'intégration : `UPDATE audit_events` lève une erreur ; chaque commande
existante produit exactement un événement ; purge ; isolation.

🧪 **Test manuel.**
1. alice renomme l'organisation, change un rôle de bob → OrgAudit montre deux lignes, auteur alice, avant/après.
2. pgweb : `UPDATE audit_events SET action='x'` → erreur du déclencheur.
3. chloe ne voit pas le journal (lien absent ; URL directe → « interdit », avec la permission nommée).

🧠 **Mémoire.** `05-data-and-storage.md` (audit).

### J0-05 — Permissions, rôles de projet et autorisation

| | |
|---|---|
| **Spécifications** | [10 § 4](../specs/10-organisations-et-acces.md) : permissions, rôles prédéfinis, RG-ORG-08 à 14, RG-ORG-23, UC-ORG-08 à 10 ; [DEC-89](../specs/03-decisions.md) |
| **Maquette** | [ProjectMembers](../design/maquette-v1/preview/ProjectMembers.html) — zones : membres directs, rôles Propriétaire/Contributeur/Lecteur, droits effectifs ; **exclues** : équipes, portées par composant (J2), autres rôles prédéfinis (J1) |
| **Dépend de** | J0-04 |
| **Commit** | `feat(autorisation): permissions, rôles de projet et contrôle systématique` |

🎯 **Objectif.** Chaque commande et chaque requête vérifie la permission sur le projet (et, plus tard, le
composant), avec « introuvable » ou « interdit » selon [RG-ORG-13](../specs/10-organisations-et-acces.md).

🔧 **À faire.**
1. `Domain/Common/Security/Permission.cs` (les 15 permissions de [10 § 4.1](../specs/10-organisations-et-acces.md), noms
   techniques `project.read`, `model.edit`…), `ProjectRoles.cs` (les 8 rôles et leurs permissions, table exacte
   de 10 § 4.2), `RoleAssignment (ProjectId, Principal (UserProfileId), Role, Scope = Project)`.
2. `IProjectAuthorizer` + `AuthorizationBehavior` + attribut `[RequiresPermission(Permission.X)]` ; un test
   d'architecture échoue si une commande ou requête portant un `ProjectId` n'a pas l'attribut.
3. Commandes `AssignProjectRole`, `RemoveProjectRole` (plafond [RG-ORG-23](../specs/10-organisations-et-acces.md), dernier
   propriétaire nominatif [RG-ORG-11](../specs/10-organisations-et-acces.md)), requête `GetEffectivePermissions`
   (UC-ORG-10). Le port `IProjectOwnershipCheck` de J0-02 devient réel.
4. L'administrateur d'organisation lit tous les projets ; la **prise de propriété** est une commande journalisée.
5. Écran membres du projet, limité aux trois rôles du pilote (les autres existent côté serveur).
   *(Les projets n'existent qu'en J0-07 : les tests de cette étape utilisent un projet créé directement en base
   par la fixture ; l'écran est relié en J0-09.)*

✅ **Vérification automatique.** Tests par règle ; matrice rôle × permission (une ligne par rôle de 10 § 4.2) ;
isolation : membre sans rôle → 404 ; lectrice sur commande → 403 nommant la permission.

🧪 **Test manuel.** Vérifié avec J0-09 (l'écran a besoin d'un projet) ; ici : Scalar, alice
`GET /v1/projects/{id}/permissions/me` sur le projet de démonstration → toutes ; chloe → `project.read`.

🧠 **Mémoire.** `09-auth-and-build.md` (autorisation), `03-domain-model.md`.

### 🔒 R-04 — Revue de sécurité des accès

**Périmètre** : `J0-01` à `J0-05`. **Branche** : `impl/j0-acces` → `[R-04] Accès, organisation, audit, autorisation`.

**Claude vérifie** : isolation de **chaque** route (EXG-01), absence de fuite par les messages d'erreur, jetons
d'invitation (aléa, empreinte, expiration), adresse vérifiée, audit inaltérable et complet, autorisation
systématique (aucune requête sans `[RequiresPermission]`), pas de données personnelles dans les journaux,
OWASP ASVS niveau 2 sur ces surfaces.

**Recette utilisateur** : [`recettes/02-jalon-0.md`](recettes/02-jalon-0.md) § Accès.

**Après approbation** : branche suivante `impl/j0-modele`.

---

## Segment « modèle et moteur » — branche `impl/j0-modele`

### J0-06 — Catalogue du pilote

| | |
|---|---|
| **Spécifications** | [15 § 1-3, 5](../specs/15-catalogue.md) (types du pilote), [DEC-13](../specs/03-decisions.md), [DEC-92](../specs/03-decisions.md), [P7](../specs/01-principes.md) |
| **Technique** | [DT-14](../technique/01-decisions.md#dt-14--moteur-pur-et-catalogue-json), [03 § 2](../technique/03-moteur-et-generation.md#2-catalogue) |
| **Dépend de** | R-04 |
| **Commit** | `feat(catalogue): descripteurs versionnés du pilote` |

🎯 **Objectif.** Les neuf types du pilote décrits en données, validés par schéma, chargés par une bibliothèque
sans dépendance.

🔧 **À faire.**
1. Projet `InfraFlowSculptor.Catalog` + `tests/InfraFlowSculptor.Catalog.Tests` ; `catalog/schema/*.schema.json`
   (toutes les sections de [15 § 1](../specs/15-catalogue.md)).
2. `catalog/2026.10-pilot/` : `catalog.json`, `regions.json` (table de [15 § 5](../specs/15-catalogue.md)),
   `roles.json` (rôles des 9 types **avec leur identifiant de définition Azure**, vérifié sur
   `https://learn.microsoft.com/azure/role-based-access-control/built-in-roles`), `pins.json` (repris de
   `reference/pilot/pins.json`), `schemas/azure-pipelines.json`, `steps/DependencyCache.json`,
   `steps/ImageScan.json`, `types/*.json` pour les 9 types — valeurs de [15 § 3](../specs/15-catalogue.md)
   **revérifiées** sur la documentation Microsoft de chaque type, date dans `verifiedAgainstDocsOn`.
3. `CatalogLoader` (ressources embarquées), `CatalogVersion` (objet immuable), `ICatalogProvider`
   (versions disponibles, dernière publiée) ; test d'empreinte des versions publiées.
4. API : `GET /v1/catalog/versions`, `GET /v1/catalog/{version}/types` (filtre `category`, recherche),
   `GET /v1/catalog/{version}/types/{type}` (lecture, tout membre).

✅ **Vérification automatique.** Chaque descripteur valide son schéma ; chaque propriété `generation.Bicep.map`
correspond à une entrée du module AVM épinglé (test qui lit le `main.json` du module restauré par
`bicep restore` dans le cache de la CI) ; isolation.

🧪 **Test manuel.** Scalar : `GET /v1/catalog/2026.10-pilot/types?category=security` → Key Vault et identité
managée, libellés français.

🧠 **Mémoire.** `03-domain-model.md` (catalogue), `05-data-and-storage.md`.

### J0-07 — Projets et environnements (API)

| | |
|---|---|
| **Spécifications** | [11 § 2, 4, 5](../specs/11-projets-et-environnements.md) : RG-PRJ-01, 05 à 08, RG-ENV-01 à 03, UC-PRJ-02 à 05, UC-ENV-01 à 03 ; [RG-DON-02 à 05](../specs/05-modele-de-donnees.md) |
| **Dépend de** | J0-06 |
| **Commit** | `feat(projet): projets, environnements et tags` |

🎯 **Objectif.** Le projet et sa chaîne d'environnements existent, avec version du modèle, verrouillage des codes
après publication et suppression réversible.

🔧 **À faire.**
1. `ProjectAggregate` : champs de [11 § 2](../specs/11-projets-et-environnements.md) (langage et plateforme limités à
   `Bicep` et `AzureDevOps` en J0 ; version du catalogue épinglée), `Environments` (champs de [11 § 4](../specs/11-projets-et-environnements.md)),
   tags projet et environnement, `ModelVersion`, `FirstPublishedAt?`, `DeletedAt?`, éditeur coordinateur
   (`CoordinatorUserProfileId?`, [DEC-93](../specs/03-decisions.md)).
2. Commandes et requêtes de la colonne Spécifications, chacune avec sa permission. Ordre continu des
   environnements ([RG-ENV-02](../specs/11-projets-et-environnements.md)) ; impact d'un changement d'abonnement ou de
   région après publication (liste des ressources, confirmation explicite — [RG-ENV-03](../specs/11-projets-et-environnements.md)).
3. `ModelChangeTracker` (Application) : toute commande marquée `[ModelChange]` incrémente `ModelVersion` dans la
   même transaction.
4. Suppression (30 jours, restauration) et tâche de purge quotidienne.

✅ **Vérification automatique.** Tests : formats (`^[a-z][a-z0-9]{1,9}$`…), unicité insensible à la casse, ordre,
verrou après publication (simulé), tags système réservés, 50 tags ; isolation.

🧪 **Test manuel.** Scalar en alice : créer un projet `shop`, ajouter `dev` puis `prd` protégé sans approbateur
→ 400 citant la règle d'approbateurs ; ajouter l'approbateur → OK ; insérer `test` en position 2 → `prd` passe
en 3.

🧠 **Mémoire.** `03-domain-model.md`, `10-api-endpoints.md`.

### J0-08 — Assistant de création de projet

| | |
|---|---|
| **Spécifications** | [11 § 3](../specs/11-projets-et-environnements.md) : UC-PRJ-01, RG-PRJ-02, RG-PRJ-03, RG-PRJ-04 ; [13 § 3](../specs/13-nommage.md) (préréglages) ; [DEC-36](../specs/03-decisions.md) |
| **Maquette** | [NewProject](../design/maquette-v1/preview/NewProject.html) — **exclues** : modèles de projet (L2), import (J3) ; [WizardIdentity](../design/maquette-v1/preview/WizardIdentity.html) ; [WizardTools](../design/maquette-v1/preview/WizardTools.html) — seuls Bicep et Azure DevOps (P9) ; [WizardEnvironments](../design/maquette-v1/preview/WizardEnvironments.html) ; [WizardNaming](../design/maquette-v1/preview/WizardNaming.html) ; [WizardPublishing](../design/maquette-v1/preview/WizardPublishing.html) — étape facultative, préréglage mono-dépôt seulement, dépôts disponibles après J0-26 ; [WizardReview](../design/maquette-v1/preview/WizardReview.html) |
| **Dépend de** | J0-07 |
| **Commit** | `feat(projet): assistant de création avec brouillon serveur` |

🎯 **Objectif.** Créer un projet en six étapes, reprises sur n'importe quel poste, création atomique.

🔧 **À faire.**
1. `ProjectWizardDraft` (utilisateur, organisation, contenu `jsonb`, expiration 30 jours) ; commandes
   `SaveWizardDraft`, `DiscardWizardDraft`, `CreateProjectFromWizard` (une transaction : projet,
   environnements, nommage, plan de publication facultatif, rôle propriétaire du créateur — [RG-PRJ-04](../specs/11-projets-et-environnements.md)).
2. Aperçu des noms de l'étape 4 calculé par le **moteur** (endpoint `POST /v1/naming/preview` qui accepte un
   projet non enregistré ; le moteur de nommage complet arrive en J0-13 — à cette étape, gabarits et jetons
   simples, sans assainissement par type ; J0-13 remplace l'implémentation sans changer le contrat).
3. Écrans de l'assistant, avec enregistrement du brouillon à chaque étape et reprise.

✅ **Vérification automatique.** Tests : atomicité (échec simulé → rien créé), reprise du brouillon, expiration ;
e2e `wizard.spec.ts` (parcours complet à 1440 et 390).

🧪 **Test manuel.**
1. alice : Projets → Nouveau projet → « Boutique en ligne », code proposé `boutiquee…` ; le remplacer par `shop`.
2. Outils : seuls Bicep et Azure DevOps sont proposés, avec l'explication de ce qu'ils produisent.
3. Environnements `dev` puis `prd` (protégé, approbateur « Shop Release Approvers »).
4. Nommage : préréglage Compact, l'aperçu montre `kv-shop-main-dev`.
5. Fermer l'onglet à l'étape 5, se reconnecter sur un autre navigateur → l'assistant reprend à l'étape 5.
6. Passer la publication, créer → page du projet ; alice en est propriétaire.

🧠 **Mémoire.** `04-frontend.md` (assistant), `03-domain-model.md`.

### J0-09 — Écrans du projet : liste, vue d'ensemble, environnements, paramètres, membres

| | |
|---|---|
| **Spécifications** | [11](../specs/11-projets-et-environnements.md) UC-PRJ-02, 03, 04, UC-ENV-01 à 03 ; [26 § 1](../specs/26-interface.md) |
| **Maquette** | [Projects](../design/maquette-v1/preview/Projects.html) ; [ProjectOverview](../design/maquette-v1/preview/ProjectOverview.html) — zones : en-tête, composants (vide jusqu'à J0-10), environnements ; **exclues jusqu'à leur étape** : rail Modéliser→Déployer (J0-24/30), constats (J0-20), déploiements (J0-30), installation (J0-29), coûts (L2), « depuis la révision » (J0-24) ; [ProjectEnvironments](../design/maquette-v1/preview/ProjectEnvironments.html), [EnvironmentEdit](../design/maquette-v1/preview/EnvironmentEdit.html) — **exclues** : fenêtres de déploiement, délais, stratégie DNS (L2) ; [ProjectSettings](../design/maquette-v1/preview/ProjectSettings.html) — zones : identité, tags, tags système, éditeur coordinateur, suppression ; **exclues** : changement de langage/plateforme (J1), exécuteurs (J1) ; [ProjectMembers](../design/maquette-v1/preview/ProjectMembers.html) (J0-05) |
| **Dépend de** | J0-08 |
| **Commit** | `feat(projet): écrans du projet` |

🎯 **Objectif.** Naviguer dans un projet et régler ce que J0-07 et J0-05 permettent.

🔧 **À faire.** Écrans de la maquette (zones retenues), navigation de portée projet enregistrée dans la barre
latérale, dialogue d'impact pour la suppression et les changements d'environnement ([RG-UI-03](../specs/26-interface.md)),
gestion du conflit de version ([VersionConflict](../design/maquette-v1/preview/VersionConflict.html)), favoris
(préférence serveur).

✅ **Vérification automatique.** e2e `project.spec.ts` ; captures ; `axe`.

🧪 **Test manuel.**
1. alice : liste des projets → `shop` avec ses compteurs.
2. Ouvrir deux onglets sur l'environnement `prd`, modifier la description dans l'un puis dans l'autre → le second
   affiche les deux versions côte à côte.
3. Membres : donner à bob « Contributeur », à chloe « Lecteur » ; chloe ne voit aucun bouton de modification.
4. Supprimer le projet (saisie du nom) → disparaît ; le restaurer depuis la liste des projets supprimés.

🧠 **Mémoire.** `04-frontend.md`.

### J0-10 — Composants et groupes de ressources

| | |
|---|---|
| **Spécifications** | [12 § 2-5](../specs/12-composants-et-groupes-de-ressources.md) : UC-CMP-01, 02, 05, 06, RG-CMP-01 à 04 ; cible propre `Single` |
| **Maquette** | [Component](../design/maquette-v1/preview/Component.html) — zones : groupes, ressources (liste), dépendances ; [ComponentSettings](../design/maquette-v1/preview/ComponentSettings.html) — zones : identité, mode, cibles, cible propre, ressources retirées, protection contre la suppression ; **exclues** : abonnement par environnement (J2), code additionnel (J1), duplication (J1), suppression d'un composant déployé (J1) |
| **Dépend de** | J0-09 |
| **Commit** | `feat(composant): composants, cibles propres et groupes de ressources` |

🎯 **Objectif.** Structurer le projet en composants `PerEnvironment` et `Single`, et en groupes de ressources.

🔧 **À faire.** `ComponentAggregate` (champs de [12 § 2-3](../specs/12-composants-et-groupes-de-ressources.md), défauts :
`PerEnvironment`, tous les environnements, `Détacher`, protection activée ; cible propre `shared` protégée avec
les approbateurs du dernier environnement), `ResourceGroup` ; commandes correspondantes ; suppression d'un
composant **jamais publié** seulement ; écrans.

✅ **Vérification automatique.** Tests des formats, unicité, cible propre (code ≠ codes d'environnement), suppression
refusée d'un groupe non vide ; isolation ; e2e.

🧪 **Test manuel.** alice : créer `core` et `orders` (par environnement), `platform` (unique, cible `shared` dans
l'abonnement C) ; dans chacun un groupe `main` ; essayer le code de cible `dev` pour `platform` → refus.

🧠 **Mémoire.** `03-domain-model.md`.

### J0-11 — Ressources : propriétés, surcharges, présence, existantes, enfants

| | |
|---|---|
| **Spécifications** | [14](../specs/14-modele-des-ressources.md) : RG-RES-01 à 14, UC-RES-01, 02, 04, 05 ; [DEC-11](../specs/03-decisions.md), [DEC-12](../specs/03-decisions.md), [DEC-18](../specs/03-decisions.md) |
| **Dépend de** | J0-10 |
| **Commit** | `feat(ressource): ressources pilotées par le descripteur` |

🎯 **Objectif.** Une seule API générique, pilotée par le descripteur, pour tous les types : ajouter un type au
catalogue ne demande aucun code ([P7](../specs/01-principes.md)).

🔧 **À faire.**
1. `Resource` (dans `ComponentAggregate`) : type, nom logique, groupe, description, existante, propriétés
   (`jsonb` : nom → valeur), surcharges (`EnvironmentOverride (EnvironmentId, Property, Value | Empty)`),
   présences, identifiants existants par environnement, nom forcé par environnement, enfants, tags, version.
2. `PropertyValueValidator` (Application) : contrôle **à la saisie** de chaque valeur contre le descripteur
   (type, bornes, motif, énumération, dépréciation, irréversibilité, verrouillage après publication — [RG-RES-03](../specs/14-modele-des-ressources.md)) ;
   les trois cas hérité / vide / absent de [RG-RES-14](../specs/14-modele-des-ressources.md) : commande explicite
   `ClearOverride`, jamais `null` implicite.
3. Commandes : `AddResource`, `SetResourceProperties`, `SetOverride`, `ClearOverride`, `SetPresence`,
   `SetExistingResourceId` (validation du format et du type ARM — [RG-RES-09](../specs/14-modele-des-ressources.md)),
   `SetForcedName`, `AddChild`/`UpdateChild`/`RemoveChild`, `MoveResource`, `DeleteResource` (l'impact vient du
   moteur en J0-15 ; ici, suppression simple et test marqué « à compléter en J0-15 » dans `test-debt.md`).
4. `ModelSnapshotBuilder` (Application) : charge un projet complet et produit le `ModelSnapshot` du moteur
   ([technique 03 § 3](../technique/03-moteur-et-generation.md#3-le-modèle-dentrée)) ; requête interne réutilisée
   par toutes les étapes du moteur.

✅ **Vérification automatique.** Tests par règle ; un test « nouveau type sans code » : ajouter un descripteur de
test dans le catalogue de test, créer une ressource de ce type par l'API → fonctionne sans modification ; isolation.

🧪 **Test manuel.** Scalar : ajouter `kv main` à `core`, mettre `enablePurgeProtection=false` en surcharge `dev`,
puis tenter de repasser `true → false` sur la valeur de la ressource après simulation de publication → refus
expliquant l'irréversibilité.

🧠 **Mémoire.** `03-domain-model.md` (ressources, surcharges), `05-data-and-storage.md` (`jsonb`).

### J0-12 — Moteur : cibles, présences, valeurs effectives et tags

| | |
|---|---|
| **Spécifications** | [RG-RES-01, 02, 05, 06, 07](../specs/14-modele-des-ressources.md), [RG-PRJ-05, 06, 08](../specs/11-projets-et-environnements.md), [RG-CMP-02, 04](../specs/12-composants-et-groupes-de-ressources.md), [DEC-09](../specs/03-decisions.md) |
| **Technique** | [03 § 1](../technique/03-moteur-et-generation.md#1-rôle) |
| **Dépend de** | J0-11 |
| **Commit** | `feat(moteur): valeurs effectives, présences et tags` |

🎯 **Objectif.** Le premier bloc du moteur pur : pour chaque ressource et chaque cible, la valeur effective de
chaque propriété avec sa provenance, la présence, la région, les tags effectifs.

🔧 **À faire.** Projet `InfraFlowSculptor.Engine` + `tests/InfraFlowSculptor.Engine.Tests` ; `EffectiveValueResolver`,
`TargetResolver`, `TagResolver` ; `ModelSnapshotBuilder` de test fluide (`Engine.Tests/Builders/`) ; règles
d'architecture du moteur ([technique 06 § 3](../technique/06-tests-et-qualite.md#3-tests-darchitecture-extraits-obligatoires)) ;
API `GET /v1/projects/{id}/resources/{rid}/effective-values` (tableau propriétés × cibles, provenance).

✅ **Vérification automatique.** Un test par règle ; valeurs effectives du projet pilote ([reference-pilote § 1.3](reference-pilote.md#13-composants))
pour `log main` (30/90), `kv main` (purge non/oui), `ca api` (CPU, réplicas).

🧪 **Test manuel.** Scalar : `effective-values` de `kv main` → `enablePurgeProtection` `dev: false (surcharge)`,
`prd: true (ressource)` et `softDeleteRetentionInDays: 90 (défaut du catalogue)`.

🧠 **Mémoire.** `12-engine.md` (nouveau fichier thématique : blocs du moteur, entrées, sorties) ; index dans `MEMORY.md`.

### J0-13 — Moteur : nommage

| | |
|---|---|
| **Spécifications** | [13](../specs/13-nommage.md) entier (lot 1) : RG-NOM-01 à 11, UC-NOM-01 à 05 ; [DEC-05 à 07](../specs/03-decisions.md), [DEC-60](../specs/03-decisions.md) |
| **Maquette** | [ProjectNaming](../design/maquette-v1/preview/ProjectNaming.html) — **exclues** : disponibilité vérifiée par API Azure (L2) ; panneau « Pourquoi ce nom ? » de [ResourceGeneral](../design/maquette-v1/preview/ResourceGeneral.html) |
| **Dépend de** | J0-12 |
| **Commit** | `feat(moteur): nommage par gabarits, assainissement et unicité` |

🎯 **Objectif.** Le nom affiché est le nom déployé, par construction ([DEC-05](../specs/03-decisions.md)).

🔧 **À faire.** `NamingEngine` (choix du gabarit sur 5 niveaux, abréviation retenue, jetons, assainissement en
4 étapes, longueur sans troncature, explication [RG-NOM-03](../specs/13-nommage.md)) ; unicité (nom logique,
nom Azure par portée, comparaison à l'organisation pour la portée globale) ; impact d'un changement de nom
après publication ([RG-NOM-10](../specs/13-nommage.md)) ; disponibilité DNS ([RG-NOM-09](../specs/13-nommage.md) lot 1)
par le worker, `INameAvailabilityProbe`, cache 24 h ; commandes de gabarits et d'abréviations (projet,
composant) ; l'endpoint `POST /v1/naming/preview` de J0-08 bascule sur ce moteur ; écran nommage avec aperçu en
direct de toutes les ressources.

✅ **Vérification automatique.** Un test par règle et par exemple de [13](../specs/13-nommage.md) ; **tous** les noms de
[reference-pilote § 2.1](reference-pilote.md#21-noms-azure) (dont `crshopmainshared`) ; un gabarit qui dépasse 24
caractères pour un Key Vault → constat `VAL-NOM-LONGUEUR` avec la longueur.

🧪 **Test manuel.**
1. Nommage du projet → aperçu : `crshopmainshared` (tirets retirés, explication affichée).
2. Gabarit par type pour Key Vault `{abbr}-{project}-{component}-{name}-{env}-{region}` → `kv main` dépasse
   24 caractères : constat avec le nom, sa longueur, la limite et l'action proposée.
3. Revenir au gabarit Compact → constat disparu.

🧠 **Mémoire.** `12-engine.md`.

### J0-14 — Écrans des ressources : ajout, général, propriétés, existante, suppression

| | |
|---|---|
| **Spécifications** | [14 § 7](../specs/14-modele-des-ressources.md), [26 § 2](../specs/26-interface.md), [RG-UI-01 à 04, 10](../specs/26-interface.md) |
| **Maquette** | [AddResourceType](../design/maquette-v1/preview/AddResourceType.html) — catalogue filtré au pilote ; [AddResourceConfigure](../design/maquette-v1/preview/AddResourceConfigure.html) ; [ResourceTabs](../design/maquette-v1/preview/ResourceTabs.html) — onglets du descripteur, sans Enfants/Historique en J0 ; [ResourceGeneral](../design/maquette-v1/preview/ResourceGeneral.html) ; [ResourceProperties](../design/maquette-v1/preview/ResourceProperties.html) — grille propriétés × environnements, trois aspects (surcharge, valeur de la ressource, défaut) ; [ResourceExisting](../design/maquette-v1/preview/ResourceExisting.html) — **exclue** : sélection depuis Azure (L2) ; [DeleteImpact](../design/maquette-v1/preview/DeleteImpact.html) ; [VersionConflict](../design/maquette-v1/preview/VersionConflict.html) |
| **Dépend de** | J0-13 |
| **Commit** | `feat(web): ajouter et régler des ressources` |

🎯 **Objectif.** Modéliser à l'écran les ressources du projet pilote, propriété par propriété, environnement par
environnement.

🔧 **À faire.** Écrans de la colonne Maquette ; composants partagés `shared/property-grid/` (rendu générique depuis
le descripteur : booléen, entier, énumération, texte, liste), `shared/generated-names/`,
`shared/provenance/` (survol **et** focus et toucher) ; dialogue d'impact (données du serveur).

✅ **Vérification automatique.** Tests Vitest de la grille (les trois aspects, effacement d'une surcharge par
commande explicite) ; e2e `resources.spec.ts` ; captures.

🧪 **Test manuel.**
1. Dans `core`, ajouter `log main`, `appi main`, `kv main` depuis le catalogue (recherche « vault »).
2. `log main` → Propriétés : rétention 30, surcharge prd 90 → la cellule prd prend l'aspect « surcharge » ;
   effacer la surcharge → retour au défaut.
3. Ajouter une ressource existante (Key Vault) avec un identifiant au mauvais type → refus explicite.
4. Supprimer `appi main` → dialogue d'impact (vide à ce stade) → suppression.

🧠 **Mémoire.** `04-frontend.md` (grille générique).

### J0-15 — Liaisons, identités, attributions de rôles et impact

| | |
|---|---|
| **Spécifications** | [16 § 2-5, 7](../specs/16-liaisons-identites-et-acces.md) pour les types de liaison du pilote (Hébergement, Journalisation, Diagnostics, Télémétrie, Tirage d'image, Accès, Accès aux données, Dépendance de valeur, Lecture de secret) ; RG-LIA-01 à 23 (sauf 24, demandes d'accès : J2) ; [DEC-16, 17, 38, 42, 85, 98](../specs/03-decisions.md) |
| **Dépend de** | J0-14 |
| **Commit** | `feat(moteur): liaisons, câblage implicite et attributions de rôles` |

🎯 **Objectif.** Une seule notion, la liaison ; tout ce qu'elle implique est déduit, visible, et disparaît avec
elle.

🔧 **À faire.**
1. `LinkAggregate` (source, cible, type, paramètres, origine) ; commandes `AddLink`, `UpdateLink`, `RemoveLink` ;
   contrôles à la saisie (même projet, pas sur soi-même, type accepté par les descripteurs, cardinalité, identité
   capable — [RG-LIA-07](../specs/16-liaisons-identites-et-acces.md)).
2. `WiringEngine` (moteur) : liaisons implicites (diagnostics depuis l'espace par défaut, lecture de secret,
   dépendance de valeur), identités implicites (système activée, affectée attachée), attributions de rôles avec
   origines multiples, déduplication et équivalences ([RG-LIA-14, 15](../specs/16-liaisons-identites-et-acces.md)),
   portée externe, composant propriétaire de chaque attribution ([DEC-98](../specs/03-decisions.md)), noms
   déterministes ([RG-LIA-16](../specs/16-liaisons-identites-et-acces.md)), droits des identités de déploiement
   ([RG-LIA-18](../specs/16-liaisons-identites-et-acces.md)) ; accès aux données (niveaux, composant source).
3. `ImpactAnalyzer` (UC-LIA-02) : ce qui disparaît ou casse, gravités ; branché sur `DeleteResource` (solde la
   dette de J0-11), `RemoveLink`, détachement d'identité ([RG-LIA-11](../specs/16-liaisons-identites-et-acces.md)).
4. API : liaisons sortantes et entrantes (explicites et implicites) d'une ressource ; impact d'un retrait.

✅ **Vérification automatique.** Tests par règle ; **tous** les éléments implicites de [reference-pilote § 2.3](reference-pilote.md#23-éléments-implicites-par-environnement)
calculés à l'identique ; déduplication (deux origines, retrait de l'une → l'attribution reste).

🧪 **Test manuel.** Scalar : sur le projet de démonstration complet, liaisons de `ca api` → AcrPull implicite
(origine « tirage d'image », portée abonnement C) ; impact du retrait de la liaison Accès vers `log main` →
« accès révoqué dans Azure au prochain déploiement, y compris en production », gravité Critique.

🧠 **Mémoire.** `12-engine.md`.

### J0-16 — Paramètres applicatifs et secrets de pipeline

| | |
|---|---|
| **Spécifications** | [17](../specs/17-parametres-applicatifs-et-secrets.md) § 2 à 6 (variables d'environnement ; clés App Configuration au J1), RG-PAR-01, 03 à 15, 22 ; [DEC-27](../specs/03-decisions.md), [DEC-95](../specs/03-decisions.md), [DEC-105](../specs/03-decisions.md) |
| **Dépend de** | J0-15 |
| **Commit** | `feat(modele): paramètres applicatifs et secrets de pipeline` |

🎯 **Objectif.** Configurer l'application sans jamais faire transiter un secret en clair.

🔧 **À faire.** `AppSettingAggregate` ; sources littérale (surcharges), sortie (non sensible), secret Key Vault
(alimentations « secret de pipeline » et « géré hors IFS » ; « sortie sensible » au J1) ; nom de variable de
pipeline par défaut ([RG-PAR-12](../specs/17-parametres-applicatifs-et-secrets.md)) ; `SecretValueDetector`
([RG-PAR-05](../specs/17-parametres-applicatifs-et-secrets.md) : motifs de jetons connus, chaînes de connexion, entropie > 4,0
bits/caractère sur ≥ 20 caractères) appliqué **avant** tout enregistrement, avec forçage journalisé ; paramètres
implicites ([RG-PAR-10](../specs/17-parametres-applicatifs-et-secrets.md)) dans le moteur ; droit de lecture implicite
([RG-PAR-09](../specs/17-parametres-applicatifs-et-secrets.md)) ; un seul écrivain par secret physique ([DEC-105](../specs/03-decisions.md)).

✅ **Vérification automatique.** Tests : nom avec `:` refusé ; doublon ; `ghp_…` refusé avant écriture (la valeur
n'apparaît dans aucune table ni aucun journal — test qui inspecte la base) ; secrets attendus du pilote
([reference-pilote § 2.5](reference-pilote.md#25-secrets-de-pipeline)).

🧪 **Test manuel.** Scalar : ajouter à `ca api` `Test__Token = ghp_` + 36 caractères → 400 avec la proposition de
conversion ; forcer en déclarant la valeur non secrète → accepté, événement d'audit « valeur forcée non secrète ».

🧠 **Mémoire.** `12-engine.md`, `03-domain-model.md`.

### J0-17 — Exposition publique et restreinte

| | |
|---|---|
| **Spécifications** | [18 § 2](../specs/18-reseau-et-exposition.md) : RG-NET-01, 02 ; `VAL-NET-IP-VIDE` |
| **Dépend de** | J0-16 |
| **Commit** | `feat(modele): exposition publique et restreinte` |

🎯 **Objectif.** Chaque ressource qui le supporte est publique ou restreinte à des plages IP, par environnement.

🔧 **À faire.** Propriété `exposure` (surchargeable) et `allowedIpRanges` (CIDR IPv4, 1 à 100) portées par les
descripteurs du pilote qui la supportent ; contrôle des CIDR à la saisie ; prise en compte dans le plan
(règles de pare-feu, restrictions d'accès) — la génération suit en J0-22 ; composant `app-ds-cidr-field`.

✅ **Vérification automatique.** Tests CIDR ; liste vide en mode restreint → constat (validation, J0-20).

🧪 **Test manuel.** Vérifié avec l'écran Réseau en J0-18.

🧠 **Mémoire.** `12-engine.md`.

### J0-18 — Écrans liaisons, identité, paramètres, réseau

| | |
|---|---|
| **Maquette** | [ResourceLinks](../design/maquette-v1/preview/ResourceLinks.html) ; [AddLink](../design/maquette-v1/preview/AddLink.html) — types du pilote seulement ; **exclues** : « Lecture de configuration » (J1), « Usage IA » (L2), mention de demande d'accès (J2) ; [ResourceIdentity](../design/maquette-v1/preview/ResourceIdentity.html) ; [ResourceAppSettings](../design/maquette-v1/preview/ResourceAppSettings.html) — **exclues** : clés App Configuration (J1), import depuis un fichier (J2) ; [ResourceNetwork](../design/maquette-v1/preview/ResourceNetwork.html) — zones : exposition publique/restreinte ; **exclues** : privée, points de terminaison privés, domaines (L2) |
| **Dépend de** | J0-17 |
| **Commit** | `feat(web): liaisons, identité, paramètres et exposition` |

🎯 **Objectif.** Relier les ressources du pilote à l'écran et voir l'implicite, son origine, et l'impact d'un retrait.

🔧 **À faire.** Écrans ; implicite en italique `text-3` ou trait pointillé avec origine au survol, focus et toucher
([RG-UI-04](../specs/26-interface.md)) ; panneau « Ce que la liaison produira » alimenté par le serveur (aperçu de
commande : `POST …/links/preview`).

✅ **Vérification automatique.** e2e `links.spec.ts` (créer les liaisons du pilote, vérifier les implicites) ; captures.

🧪 **Test manuel.**
1. `ca api` → Ajouter une liaison → Accès vers `core / log main`, rôle Log Analytics Reader, identité `id api` →
   le panneau annonce l'attribution par environnement.
2. Onglet Identité de `ca api` : identité système, `id api`, rôles détenus avec leurs origines.
3. Paramètres : `Payments__ApiKey` → secret de pipeline ; l'écran montre la variable `MAIN_PAYMENTS_API_KEY`
   et jamais de valeur.
4. Réseau de `sql orders` : restreinte sans plage → constat visible à côté du champ (après J0-20).

🧠 **Mémoire.** `04-frontend.md`.

### J0-19 — Applications : build conteneur, étapes, santé, stratégie directe

| | |
|---|---|
| **Spécifications** | [19](../specs/19-applications-build-et-deploiement.md) § 2, 3, 5, 6.1 (cache, scan), 7, 8, 9 (directe) ; RG-APP-01, 02, 05 à 18 ; [DEC-25, 50, 53](../specs/03-decisions.md) |
| **Maquette** | [ResourceApplication](../design/maquette-v1/preview/ResourceApplication.html) — zones : livraison Pipelines IFS, code source, Dockerfile, dépôt d'image, registre de build, étapes cache et scan, contrôle de santé, stratégie directe ; **exclues** : mode Code (J1), autres étapes (L2), stratégies avancées (L2), pipelines du client (J2), détection (L2) |
| **Dépend de** | J0-18 |
| **Commit** | `feat(application): application conteneur, étapes et contrôle de santé` |

🎯 **Objectif.** Décrire l'application témoin du pilote pour que ses pipelines soient générés.

🔧 **À faire.** `Application` (sur ContainerApp) ; catalogue d'étapes du pilote (`DependencyCache`, `ImageScan`) ;
registre de build déduit des liaisons « tirage d'image » ([RG-APP-07](../specs/19-applications-build-et-deploiement.md)) ;
points d'extension (chemins de modèles d'étapes, contrôlés à la publication en J0-28) ; écran.

✅ **Vérification automatique.** Tests : registre de build `crshopmainshared` sans promotion pour le pilote ; étape
sans valeur pour la pile → constat `VAL-APP-ETAPE`.

🧪 **Test manuel.** `ca api` → Application : chemin `src/api`, Dockerfile par défaut `src/api/Dockerfile`, dépôt
d'image `shop/orders/api`, registre `platform / cr main` affiché, scan Trivy bloquant à `CRITICAL`.

🧠 **Mémoire.** `03-domain-model.md`.

### J0-20 — Ordre des composants, validation et constats

| | |
|---|---|
| **Spécifications** | [20](../specs/20-validation.md) (règles du pilote : projet, composants, nommage, ressources, sécurité, liaisons, paramètres, applications, publication lot 1, `VAL-NET-IP-VIDE`) ; [RG-CMP-05 à 08](../specs/12-composants-et-groupes-de-ressources.md) ; [RG-VAL-01 à 04](../specs/20-validation.md) ; [EXG-07](../specs/27-exigences-non-fonctionnelles.md) |
| **Maquette** | [ProjectFindings](../design/maquette-v1/preview/ProjectFindings.html) ; compteur par gravité dans l'en-tête ; constats en contexte ; corrections en un clic de [20 § 5](../specs/20-validation.md) applicables au pilote ; dépendances sur [Component](../design/maquette-v1/preview/Component.html) et ordre sur [ProjectOverview](../design/maquette-v1/preview/ProjectOverview.html) |
| **Dépend de** | J0-19 |
| **Commit** | `feat(moteur): ordre des composants et validation du modèle` |

🎯 **Objectif.** Un seul moteur de règles produit les constats, identiques pour l'écran, l'API et (plus tard) le MCP ;
la génération est refusée tant qu'il reste une erreur.

🔧 **À faire.** `ComponentOrdering` (dépendances de création, sorties calculables depuis le nom, tri topologique avec
ex æquo par code, cycle → `VAL-CMP-CYCLE` avec les liaisons) ; `ValidationEngine` : une classe par règle
(`IValidationRule`, code, gravité, objet, environnement, message FR/EN, correction) ; cache `HybridCache` par
(projet, version du modèle, catalogue) ; acquittement des avertissements (commentaire obligatoire, tombe si
l'objet change, journalisé) ; niveaux de préparation ([RG-VAL-04](../specs/20-validation.md)) ; test de performance
300 ressources < 2 s ; écrans.

✅ **Vérification automatique.** Un test par règle ; constats **exacts** de [reference-pilote § 2.4](reference-pilote.md#24-constats-attendus) ;
ordre `core, data, platform, orders` ; performance.

🧪 **Test manuel.**
1. Projet de démonstration complet : en-tête « 0 erreur · 1 avertissement · 1 info ».
2. Retirer la liaison d'hébergement de `ca api` → erreur `VAL-LIA-OBLIGATOIRE` à côté du champ, correction en un clic
   (une seule cible possible) → constat disparu.
3. Acquitter `VAL-LIA-SQL-ADMIN` avec un commentaire → l'avertissement passe « acquitté » ; modifier `sql orders` →
   l'acquittement tombe.
4. Composant `orders` → « Dépend de : core, data, platform ».

🧠 **Mémoire.** `12-engine.md` (règles, cache).

### 🔒 R-05 — Revue du modèle et du moteur

**Périmètre** : `J0-06` à `J0-20`. **Branche** : `impl/j0-modele` → `[R-05] Modèle, moteur et écrans de modélisation`.

**Claude vérifie** : un seul lieu de calcul (aucune règle métier dans l'API ni le frontend), pureté du moteur,
descripteurs exacts et vérifiés, résultats du projet pilote identiques à [reference-pilote § 2](reference-pilote.md#2-résultats-attendus-du-calcul),
règles RG/VAL toutes testées, permissions sur chaque commande, secrets jamais stockés, fidélité des écrans,
P9 respecté.

**Recette utilisateur** : [`recettes/02-jalon-0.md`](recettes/02-jalon-0.md) § Modélisation (modéliser le projet pilote
complet à l'écran, seul, en partant de zéro).

**Après approbation** : branche suivante `impl/j0-generation`.

---

## Segment « génération » — branche `impl/j0-generation`

### J0-21 — Plan de déploiement

| | |
|---|---|
| **Spécifications** | [21 § 3.1](../specs/21-generation-et-revisions.md), [RG-GEN-06, 09 à 12, 18, 19, 25](../specs/21-generation-et-revisions.md), [DEC-44](../specs/03-decisions.md), [DEC-86](../specs/03-decisions.md) |
| **Technique** | [03 § 4](../technique/03-moteur-et-generation.md#4-plan-de-déploiement) |
| **Dépend de** | R-05 |
| **Commit** | `feat(moteur): plan de déploiement neutre` |

🎯 **Objectif.** Toutes les décisions réunies dans une représentation neutre, ordonnée, sérialisable — la seule
entrée des émetteurs.

🔧 **À faire.** `DeploymentPlanBuilder` et les records de [technique 03 § 4](../technique/03-moteur-et-generation.md#4-plan-de-déploiement),
y compris `ReleaseDataPlan` (contenu de `release.<cible>.json`) ; sérialisation JSON canonique ; `EngineFacade` ;
API `GET /v1/projects/{id}/deployment-plan?component=&target=`.

✅ **Vérification automatique.** Le plan du projet pilote sérialisé est comparé (Verify) à un instantané relu par
Claude en revue ; ordre stable (deux calculs → même JSON) ; `release.<cible>.json` dérivé du plan identique à
`reference/pilot/bicep-azdo/*/infra/release.*.json`.

🧪 **Test manuel.** Scalar : plan de `orders` en `prd` → attributions avec leurs origines, secret attendu, accès aux
données par identité système, dépendances `core`, `data`, `platform`.

🧠 **Mémoire.** `12-engine.md`.

### J0-22 — Émetteur Bicep

| | |
|---|---|
| **Spécifications** | [21 § 4-6.2, 8](../specs/21-generation-et-revisions.md), [P10](../specs/01-principes.md), [RG-GEN-03, 07, 08, 13, 14](../specs/21-generation-et-revisions.md) |
| **Technique** | [DT-15](../technique/01-decisions.md#dt-15--émetteurs-écrits-à-la-main-sans-moteur-de-gabarits), [03 § 5](../technique/03-moteur-et-generation.md#5-émetteurs) |
| **Dépend de** | J0-21 |
| **Commit** | `feat(generation): émetteur Bicep` |

🎯 **Objectif.** Le plan du projet pilote donne, **octet pour octet**, les fichiers Bicep de `reference/pilot/bicep-azdo/`.

🔧 **À faire.** Projet `InfraFlowSculptor.Emitters.Bicep` + `tests/InfraFlowSculptor.Emitters.Tests` ; `BicepWriter`
(indentation 2, ordre, commentaires, `LF`) ; une classe par forme (`TypesFileEmitter`, `MainFileEmitter`,
`ParamFileEmitter`, `ReleaseDataEmitter`, `DataAccessScriptEmitter`) ; table type → module AVM et correspondance
des propriétés **lues dans le descripteur** (aucun nom de propriété en dur dans l'émetteur) ;
`ReferenceOutputTests` (comparaison de dossiers, différences affichées par fichier et par ligne) ; test de
déterminisme.

✅ **Vérification automatique.** `ReferenceOutputTests` vert pour les fichiers `*/infra/*.bicep`, `*.bicepparam`,
`release.*.json`, `scripts/*.sql` ; tests d'architecture (l'émetteur ne voit que le plan).

🧪 **Test manuel.** Aucun test manuel distinct : la comparaison automatique à la référence **est** la preuve ;
vous pouvez ouvrir le rapport de différences d'un test volontairement cassé (changer une rétention dans le modèle
de test) pour voir sa lisibilité.

🧠 **Mémoire.** `12-engine.md` (émetteurs).

### J0-23 — Émetteurs Azure DevOps, kit d'installation et README

| | |
|---|---|
| **Spécifications** | [22](../specs/22-pipelines.md) (Azure DevOps), [23](../specs/23-kit-installation.md), [DEC-75](../specs/03-decisions.md), [RG-GEN-08](../specs/21-generation-et-revisions.md) |
| **Technique** | [DT-17](../technique/01-decisions.md#dt-17--logique-de-release-en-module-powershell-livré-au-client) |
| **Dépend de** | J0-22 |
| **Commit** | `feat(generation): pipelines Azure DevOps, kit d'installation et README` |

🎯 **Objectif.** L'arborescence complète de [reference-pilote § 3](reference-pilote.md#3-sortie-attendue-dépôt-shop),
identique à la référence.

🔧 **À faire.** `InfraFlowSculptor.Emitters.AzureDevOps` (`YamlWriter`, pipelines par composant et application,
modèles partagés et module PowerShell copiés comme **ressources embarquées** depuis `reference/pilot/bicep-azdo/.ifs/templates/`
par une cible MSBuild — une seule source), `InfraFlowSculptor.Emitters.InstallKit` (`azure-setup.ps1` avec ses
données par cible, `install.pipeline.yml`, `SETUP.md`), `ReadmeEmitter` (`README.ifs.md`, diagramme Mermaid
déterministe) ; `ReferenceOutputTests` étendu à **toute** l'arborescence.

✅ **Vérification automatique.** `ReferenceOutputTests` vert sur l'arborescence entière ; déterminisme.

🧪 **Test manuel.** Aucun distinct (même raison que J0-22).

🧠 **Mémoire.** `12-engine.md`.

### J0-24 — Révisions : génération asynchrone, contrôles de sortie, consultation

| | |
|---|---|
| **Spécifications** | [21 § 2](../specs/21-generation-et-revisions.md) : UC-GEN-01, 02, RG-GEN-01 à 05 ; [EXG-07](../specs/27-exigences-non-fonctionnelles.md), [EXG-11](../specs/27-exigences-non-fonctionnelles.md), [EXG-23](../specs/27-exigences-non-fonctionnelles.md) ; [RG-EXP-09](../specs/40-exploitation-ifs.md) |
| **Technique** | [DT-09](../technique/01-decisions.md#dt-09--fichiers-des-révisions-dans-le-stockage-blob), [DT-16](../technique/01-decisions.md#dt-16--contrôles-de-sortie-dans-le-worker), [DT-32](../technique/01-decisions.md#dt-32--files-et-équité), [03 § 6](../technique/03-moteur-et-generation.md#6-révision) |
| **Maquette** | [Revisions](../design/maquette-v1/preview/Revisions.html) ; [RevisionDetail](../design/maquette-v1/preview/RevisionDetail.html) — onglets Changements, Plan de déploiement, Fichiers, Constats ; **exclus** : Publications (J0-28), comparaison libre (J2) ; [GenerateBlocked](../design/maquette-v1/preview/GenerateBlocked.html) ; bouton « Générer la révision n » et rail de [ProjectOverview](../design/maquette-v1/preview/ProjectOverview.html) (étapes Modéliser, Générer) |
| **Dépend de** | J0-23 |
| **Commit** | `feat(revision): générer, contrôler et consulter les révisions` |

🎯 **Objectif.** Générer une révision immuable du projet entier, contrôlée, consultable, téléchargeable.

🔧 **À faire.**
1. `RevisionAggregate` (numéro croissant, auteur, version du modèle, catalogue, langage, plateforme, état
   `Queued|Generating|Generated|Failed`, constats non bloquants, arborescence chemin → empreinte, résumé des
   changements, position dans la file) ; `GenerateRevision` (refus si erreur ; renvoie la dernière si rien n'a
   changé ; met en file `generation`) ; budget par organisation.
2. Worker : `GenerateRevisionJobHandler` ; `OutputChecker` (CLI Bicep épinglée, cache AVM, schéma YAML,
   PSScriptAnalyzer) ; échec → révision `Failed`, compteur `ifs.output_check_failures`, incident interne (journal
   d'erreur structuré `OutputCheckFailed` sans contenu client) et message « incident IFS ».
3. Stockage blob adressé par empreinte (client `AddIfsBlobServiceClient`, [DT-33](../technique/01-decisions.md#dt-33--identité-managée-partout--chaîne-de-connexion-seulement-en-local)) ; archive zip à la demande ; périmée ([RG-GEN-02](../specs/21-generation-et-revisions.md)) ;
   purge des fichiers non publiés après 90 jours.
4. Image du worker : `Dockerfile` avec Bicep CLI, PowerShell, PSScriptAnalyzer (versions de `pins.json`).
5. Écrans ; visualiseur de code `shared/code-viewer/` (Shiki : bicep, yaml, json, powershell, markdown ;
   arborescence, recherche dans les fichiers, téléchargement).

✅ **Vérification automatique.** Tests : immuabilité, aucune nouvelle révision sans changement, refus avec erreur,
contrôle de sortie en échec simulé ; acceptation : générer le projet pilote dans l'AppHost → fichiers identiques
à la référence ; e2e.

🧪 **Test manuel.**
1. Projet de démonstration → « Générer la révision 1 » → la position dans la file puis « Générée ».
2. Onglet Fichiers : ouvrir `orders/infra/main.dev.bicepparam` → coloré, noms en clair.
3. Télécharger l'archive ; la décompresser ; comparer à `reference/pilot/bicep-azdo/` avec un outil de diff →
   identique (hors manifeste, absent de l'archive).
4. Regénérer sans rien changer → « Aucun changement depuis la révision 1 ».
5. Introduire une erreur (retirer l'hébergement de `ca api`) → bouton remplacé par l'écran « Génération bloquée »
   listant l'erreur.

🧠 **Mémoire.** `08-runtime-and-orchestration.md` (génération), `05-data-and-storage.md` (blob).

### J0-25 — Export du modèle et sortie autonome

| | |
|---|---|
| **Spécifications** | [11 § 6](../specs/11-projets-et-environnements.md) UC-PRJ-07 ; [04 § 2.0](../specs/04-perimetre-et-lots.md) (Sortie : archive du code et export du modèle) ; [DEC-56](../specs/03-decisions.md) |
| **Maquette** | [ProjectSettings](../design/maquette-v1/preview/ProjectSettings.html) — zone : exporter le projet |
| **Dépend de** | J0-24 |
| **Commit** | `feat(projet): export JSON du modèle` |

🎯 **Objectif.** Le client garde toujours son modèle (JSON `ifs-project/v1`) et ses fichiers, sans IFS.

🔧 **À faire.** Sérialisation du `ModelSnapshot` au format `ifs-project/v1` avec schéma publié
(`catalog/schema/ifs-project-v1.schema.json`) ; commande `ExportProject` (`projet.administrer`, journalisée) ; fichier
dans `exports/`, lien temporaire ; test aller-retour : export → désérialisation → instantané identique (l'import
viendra en J3).

✅ **Vérification automatique.** Aller-retour ; schéma ; audit.

🧪 **Test manuel.** Paramètres du projet → Exporter → fichier JSON lisible, sans membre ni secret ; journal
d'audit : « Export du projet shop ».

🧠 **Mémoire.** `03-domain-model.md` (format d'export).

### 🔒 R-06 — Revue de la génération

**Périmètre** : `J0-21` à `J0-25`. **Branche** : `impl/j0-generation` → `[R-06] Plan de déploiement, émetteurs et révisions`.

**Claude vérifie** : émetteurs sans décision métier (tout vient du plan), parité octet pour octet avec la
référence prouvée, déterminisme, contrôles de sortie complets et en échec testé, équité des files, immuabilité,
aucune donnée client dans les incidents.

**Recette utilisateur** : [`recettes/02-jalon-0.md`](recettes/02-jalon-0.md) § Génération.

**Après approbation** : branche suivante `impl/j0-publication`.

---

## Segment « publication » — branche `impl/j0-publication`

### J0-26 — Connexions git

| | |
|---|---|
| **Spécifications** | [24 § 2](../specs/24-depots-et-publication.md) : RG-PUB-01 à 03, UC-PUB-01 ; [DEC-23](../specs/03-decisions.md) ; [EXG-02](../specs/27-exigences-non-fonctionnelles.md) |
| **Technique** | [DT-12](../technique/01-decisions.md#dt-12--secrets-propres-à-ifs--key-vault), [DT-13](../technique/01-decisions.md#dt-13--fournisseurs-git-derrière-un-port-unique--gitea-comme-émulateur), [07 § 2](../technique/07-integrations.md#2-azure-devops) |
| **Maquette** | [OrgConnections](../design/maquette-v1/preview/OrgConnections.html) — zones : Azure DevOps (principal de service, jeton de repli), dépôts ouverts aux projets ; **exclues** : GitHub (J1), GitLab (L3), connexion Azure (L2) |
| **Dépend de** | R-06 |
| **Commit** | `feat(publication): connexions git Azure DevOps et Gitea` |

🎯 **Objectif.** L'organisation relie IFS à son Azure DevOps sans secret (principal de service), ou par un jeton
de repli à expiration obligatoire ; en local, à Gitea.

🔧 **À faire.** `GitConnectionAggregate` (type, nom, organisation Azure DevOps, mode, expiration du jeton de repli,
dépôts ouverts : tous les projets ou liste) ; port `IGitProvider` ([DT-13](../technique/01-decisions.md#dt-13--fournisseurs-git-derrière-un-port-unique--gitea-comme-émulateur))
et adaptateurs `AzureReposProvider` (jeton Entra du principal « IFS Git » obtenu par **fédération depuis l'identité
managée** du worker — `ClientAssertionCredential` alimenté par un jeton de `IfsAzureCredential` pour
`api://AzureADTokenExchange` —, sans secret client ; repli PAT dans `ISecretStore`, lui-même sur Key Vault par
identité managée, [DT-33](../technique/01-decisions.md#dt-33--identité-managée-partout--chaîne-de-connexion-seulement-en-local)) et
`GiteaProvider` (développement) ; test de connexion à l'ajout et à chaque modification ; alertes d'expiration
15 et 3 jours (tâche planifiée, e-mail) ; retrait refusé si un plan de publication l'utilise.

✅ **Vérification automatique.** Tests WireMock (réponses Azure DevOps enregistrées) ; le jeton de repli n'est jamais
en base ni dans un journal (test) ; isolation.

🧪 **Test manuel.**
1. alice (après `gitea-init`) : Organisation → Connexions → « Gitea (développement) » → test → trois dépôts listés.
2. Ouvrir le dépôt `shop` au projet `shop` seulement.
3. Ajouter une connexion Azure DevOps par jeton de repli expirant dans 10 jours (votre organisation de test) →
   avertissement d'expiration visible ; MailPit reçoit l'alerte à J-15 (déclencher la tâche depuis la galerie de
   développement).

🧠 **Mémoire.** `13-integrations.md`, `05-data-and-storage.md` (secrets git).

📌 **Hors dépôt.** Inscription Entra « IFS Git » (environnement `dev` d'IFS) et ajout de son principal de service à
votre organisation Azure DevOps de test : à faire par vous selon [technique 07 § 1](../technique/07-integrations.md#1-entra-id-connexion-des-utilisateurs).

### J0-27 — Plan de publication

| | |
|---|---|
| **Spécifications** | [24 § 3-4](../specs/24-depots-et-publication.md) : RG-PUB-04 à 07, 19 ; [DEC-21](../specs/03-decisions.md) |
| **Maquette** | [ProjectPublishPlan](../design/maquette-v1/preview/ProjectPublishPlan.html) — préréglage mono-dépôt ; **exclus** : autres préréglages (J1), relecteurs par défaut (J1) ; [WizardPublishing](../design/maquette-v1/preview/WizardPublishing.html) rendu fonctionnel |
| **Dépend de** | J0-26 |
| **Commit** | `feat(publication): plan de publication mono-dépôt` |

🎯 **Objectif.** Dire où chaque partie du projet est écrite, sans que cela change ce qui est généré.

🔧 **À faire.** `PublishPlan` (destinations : connexion, dépôt, chemin de base ; parties par composant) — le modèle
couvre les trois préréglages, l'interface n'expose que « Mono-dépôt » ; règles de chevauchement et de compatibilité
(`VAL-PIP-COMPATIBILITE`, `VAL-PUB-DESTINATION`) ; répartition de l'arborescence logique par destination
(`PublishLayout`, utilisé par J0-28).

✅ **Vérification automatique.** Tests de répartition (projet pilote → une destination, arborescence complète) et des règles.

🧪 **Test manuel.** Projet → Publication → mono-dépôt `contoso/shop` (Gitea), racine → enregistrer ; l'assistant d'un
nouveau projet propose maintenant le dépôt à l'étape 5.

🧠 **Mémoire.** `03-domain-model.md`.

### J0-28 — Publication par pull request

| | |
|---|---|
| **Spécifications** | [24 § 5-6](../specs/24-depots-et-publication.md) : RG-PUB-08 à 16, 20, UC-PUB-02, 03, 04 ; [DEC-01](../specs/03-decisions.md), [DEC-22](../specs/03-decisions.md), [DEC-94](../specs/03-decisions.md) ; [91 S12](../specs/90-projet-de-reference.md) (idempotence) |
| **Maquette** | [PublishDialog](../design/maquette-v1/preview/PublishDialog.html) — **exclu** : mode direct (masqué au pilote : « publication par pull request uniquement ») ; [PublishResult](../design/maquette-v1/preview/PublishResult.html) ; onglet Publications de [RevisionDetail](../design/maquette-v1/preview/RevisionDetail.html) ; étape Publier du rail |
| **Dépend de** | J0-27 |
| **Commit** | `feat(publication): préparer et publier par pull request` |

🎯 **Objectif.** Écrire une révision dans le dépôt du client sous forme de pull request relisible, sans jamais
écraser en silence une retouche manuelle, sans doublon en cas de nouvelle tentative.

🔧 **À faire.**
1. `PublicationAggregate` (identifiant d'opération, révision, destinations, état par dépôt, commit de base, branche,
   commit, pull request et son état).
2. Préparation (worker, file `publication`) : lecture de la branche par défaut, comparaison au manifeste, tableau de
   [RG-PUB-11](../specs/24-depots-et-publication.md) ; manifeste absent, modifié ou d'un autre projet → conflit et
   prise de propriété explicite ; présentation à l'écran.
3. Écriture : branche `ifs/<projet>/revision-<n>`, **un commit** par dépôt (fichiers + `.ifs/manifest.json`), pull
   request (titre, description : résumé limité au dépôt, constats, nouveautés de la liste de contrôle) ou mise à
   jour de la pull request IFS ouverte ; vérification que la branche n'a pas avancé ; permissions revérifiées avant
   chaque écriture ; une seule publication à la fois par projet (verrou applicatif) ; message et pied
   `Published-by:`.
4. Points d'extension référencés présents dans la branche cible, sinon refus de la destination ([RG-PUB-15](../specs/24-depots-et-publication.md)).
5. Suivi de l'état des pull requests (ouverte, fusionnée avec son commit, fermée) par la tâche planifiée de suivi
   (préparée ici, appelée par J0-30).
6. Le code projet et des environnements devient verrouillé à la première publication ([RG-PRJ-01](../specs/11-projets-et-environnements.md)).

✅ **Vérification automatique.** Tests WireMock (Azure Repos) et acceptation (Gitea) : publication du projet pilote →
une pull request, contenu identique à la référence + manifeste ; modification manuelle détectée ; nouvelle tentative
après coupure simulée → ni second commit ni seconde pull request ; branche avancée entre préparation et écriture →
nouvelle préparation.

🧪 **Test manuel.**
1. Révision 1 → Publier → préparation : « 1 dépôt, 41 fichiers ajoutés » → Publier dans 1 dépôt.
2. Gitea → pull request « IFS révision 1 — shop », description lisible ; fusionner dans Gitea.
3. Dans Gitea, modifier à la main `orders/infra/main.bicep` sur `main`.
4. Changer la rétention prd de `log main`, générer la révision 2, publier → la préparation montre le diff de la
   modification manuelle et exige la confirmation ; la pull request ne contient que les deux fichiers concernés.
5. Le code du projet est désormais grisé dans les paramètres, avec l'explication.

🧠 **Mémoire.** `13-integrations.md`, `03-domain-model.md`.

### 🔒 R-07 — Revue de la publication

**Périmètre** : `J0-26` à `J0-28`. **Branche** : `impl/j0-publication` → `[R-07] Connexions git et publication`.

**Claude vérifie** : aucune écriture hors des fichiers gérés, détection de modification manuelle, idempotence,
secrets git (jamais en base ni en journal), permissions revérifiées, dépôts ouverts par projet, conduite en cas
d'échec partiel par dépôt.

**Recette utilisateur** : [`recettes/02-jalon-0.md`](recettes/02-jalon-0.md) § Publication (Gitea, puis votre Azure
DevOps de test).

**Après approbation** : branche suivante `impl/j0-livraison`.

---

## Segment « livraison » — branche `impl/j0-livraison`

### J0-29 — Liste de contrôle d'installation

| | |
|---|---|
| **Spécifications** | [23 § 4](../specs/23-kit-installation.md) : RG-INS-06 ; [RG-SUI-04](../specs/28-suivi-des-deploiements.md) ; [DEC-111](../specs/03-decisions.md) |
| **Maquette** | [InstallChecklist](../design/maquette-v1/preview/InstallChecklist.html) — **exclues** : parties GitHub (L2), réseau privé (L2) ; étape Installation de [ProjectOverview](../design/maquette-v1/preview/ProjectOverview.html) |
| **Dépend de** | R-07 |
| **Commit** | `feat(installation): liste de contrôle interactive` |

🎯 **Objectif.** La liste de contrôle de la révision, interactive : automatique, à faire, fait (journalisé),
vérifié (rapport de préparation, daté).

🔧 **À faire.** Modèle de liste de contrôle produit par le moteur (même source que `SETUP.md`) ; états, coche
journalisée (`installation.gerer`), différentiel avec la révision précédente ; lecture du rapport de vérification
de préparation du pipeline d'installation (via J0-30) ; écran.

✅ **Vérification automatique.** Tests : contenu identique à `SETUP.md` ; différentiel (nouveau secret) ; « à revérifier »
après 30 jours.

🧪 **Test manuel.** Projet → Installation : neuf étapes dans l'ordre de [23 § 4](../specs/23-kit-installation.md), commande
du script Azure prête à copier ; cocher « secrets saisis en dev » → l'audit montre la coche.

🧠 **Mémoire.** `03-domain-model.md`.

### J0-30 — Suivi des déploiements et ressources détachées

| | |
|---|---|
| **Spécifications** | [28](../specs/28-suivi-des-deploiements.md) (Azure DevOps) : RG-SUI-01 à 08, 10, UC-SUI-01 à 04 ; [DEC-55](../specs/03-decisions.md), [DEC-61](../specs/03-decisions.md), [DEC-91](../specs/03-decisions.md), [DEC-100](../specs/03-decisions.md) |
| **Maquette** | [Deployments](../design/maquette-v1/preview/Deployments.html) — zones : matrice, fiche d'opération, inventaire des ressources détachées ; **exclues** : dérive (L2), coût des détachées (L2) ; étape Déployer du rail ; [DeploymentDetail](../design/maquette-v1/preview/DeploymentDetail.html) — zones : version en service et précédente ; **exclus** : bleu/vert (L2) |
| **Dépend de** | J0-29 |
| **Commit** | `feat(suivi): suivi des déploiements Azure DevOps` |

🎯 **Objectif.** Savoir, sans accès à Azure, quelle révision tourne où, ce qui attend une approbation, ce qui a
échoué et comment reprendre.

🔧 **À faire.** Adaptateur `AzureDevOpsPipelinesReader` (exécutions des définitions gérées, timeline, artefacts
`ifs-report`, `ifs-app-report`, `ifs-preview`, approbations en attente) ; tâche planifiée 2 min / 15 min ;
corrélation par rapport et empreinte du manifeste ([RG-SUI-02](../specs/28-suivi-des-deploiements.md)) ; états
calculés depuis les événements immuables ([RG-DON-04](../specs/05-modele-de-donnees.md)) : déployée, partiellement
appliquée, en échec avant modification, indéterminée, inconnue (> 30 min), avec fiche d'opération ; écart
(à jour, publiée non fusionnée, fusionnée non déployée, en retard) ; inventaire des ressources détachées
(responsable, motif, date de revue, commande de suppression) ; coche automatique de la liste de contrôle ;
notifications e-mail de [RG-SUI-06](../specs/28-suivi-des-deploiements.md) ; indicateurs [RG-SUI-07](../specs/28-suivi-des-deploiements.md).

✅ **Vérification automatique.** Tests WireMock rejouant des exécutions **réelles** enregistrées pendant la phase P
(fichiers sous `tests/…/AzureDevOps/Recordings/` : release réussie, partiellement appliquée, interrompue, commit de
fusion différent) → états attendus ; état « inconnu » après 30 min (horloge simulée).

🧪 **Test manuel.** Avec votre organisation Azure DevOps de test et le projet publié depuis IFS (recette du segment) :
après la release de `core` en dev, la matrice passe « révision 1 déployée » sans action de votre part en moins de
3 minutes ; une release en attente d'approbation apparaît « en attente d'approbation » avec les approbateurs.

🧠 **Mémoire.** `13-integrations.md`, `08-runtime-and-orchestration.md`.

📌 **Hors dépôt.** Organisation Azure DevOps de test reliée à l'IFS `dev` déployé (J0-31).

### J0-31 — Hébergement d'IFS dans Azure (environnements `dev` et `pilot`)

| | |
|---|---|
| **Spécifications** | [DEC-04](../specs/03-decisions.md), [EXG-04, 06, 09, 10, 11](../specs/27-exigences-non-fonctionnelles.md) |
| **Technique** | [08](../technique/08-hebergement.md), [07 § 1](../technique/07-integrations.md#1-entra-id-connexion-des-utilisateurs) |
| **Dépend de** | J0-30 |
| **Commit** | `feat(infra): héberger IFS dans Azure` |

🎯 **Objectif.** Les équipes pilotes utilisent IFS en ligne, connectées avec leur compte Microsoft.

🔧 **À faire.** `infra/main.bicep` + `infra/parameters/{dev,pilot}.bicepparam` (ressources de [technique 08 § 2](../technique/08-hebergement.md#2-ressources),
AVM épinglés) ; `infra/entra/Configure-IfsEntraApps.ps1` (trois inscriptions, idempotent, `-WhatIf`) ; Dockerfiles
`api`, `worker`, `web` ; `.github/workflows/deploy.yml` (images, what-if, déploiement, job de migration, Container
Apps ; fédération OIDC ; approbation manuelle pour `pilot`) ; `config.json` de `web` en mode Entra ; contrôles de santé
et alertes ; sauvegarde PITR. **Aucune clé** ([DT-33](../technique/01-decisions.md#dt-33--identité-managée-partout--chaîne-de-connexion-seulement-en-local)) : chaque Container App a son identité affectée et reçoit
seulement des variables `Azure__<nom>__Endpoint` (et `AZURE_CLIENT_ID`) ; PostgreSQL en authentification Entra seule
(identités déclarées comme rôles par un script post-déploiement du workflow), stockage `allowSharedKeyAccess: false`,
Service Bus et Application Insights `disableLocalAuth: true`, Redis en Entra seul, registre sans utilisateur admin,
ACS par identité. Test Pester / `bicep` : aucun `listKeys(`, aucune sortie sensible dans `infra/`. Luna ne déploie rien : `az bicep build`, `bicep lint`, Pester des scripts.

✅ **Vérification automatique.** `bicep build`/`lint` sans avertissement ; Pester (`-WhatIf` n'écrit rien) ; job
`images` de la CI vert.

🧪 **Test manuel.** Suivre `infra/README.md` : exécuter le script Entra, lancer le workflow `deploy` vers `dev`,
ouvrir l'URL → connexion avec votre compte Microsoft professionnel → créer une organisation ; portail Azure : aucune
Container App n'a de secret ni de chaîne de connexion dans ses variables, le compte de stockage refuse la clé partagée,
PostgreSQL est en « Microsoft Entra authentication only » ; un compte
personnel (outlook.com) est refusé avec un message clair (les comptes personnels arrivent au jalon 2).

🧠 **Mémoire.** `09-auth-and-build.md` (déploiement), `08-runtime-and-orchestration.md`.

📌 **Hors dépôt.** Abonnements IFS, inscriptions Entra (identifiants), domaine, secrets GitHub du workflow : dans `NEXT.md`.

### J0-32 — Accueil, parcours complet et préparation du pilote

| | |
|---|---|
| **Spécifications** | [06 P-01](../specs/06-parcours-utilisateur.md), [26 § 1](../specs/26-interface.md) (Accueil, Profil), [RG-UI-07](../specs/26-interface.md), [DEC-110](../specs/03-decisions.md) |
| **Maquette** | [Main](../design/maquette-v1/preview/Main.html) — zones : projets favoris et récents, constats bloquants, dernières publications ; **exclues** : propositions (J2) ; [Profile](../design/maquette-v1/preview/Profile.html) — zones : langue ; **exclus** : thème (DT-30), notifications (J3), jetons (J1) |
| **Dépend de** | J0-31 |
| **Commit** | `feat(web): accueil, profil et parcours de bout en bout du pilote` |

🎯 **Objectif.** Le parcours P-01 complet tient d'un bout à l'autre, mesuré, et prêt pour les équipes pilotes.

🔧 **À faire.** Accueil ; profil (langue FR/EN et, quand plusieurs thèmes existeront, thème — persistés côté serveur par
`PUT /v1/me/preferences`, appliqués sans rechargement) ; e2e `pilot-journey.spec.ts` (dans l'AppHost,
Gitea) : créer l'organisation, la connexion, le projet par l'assistant, modéliser le projet pilote, corriger les
constats, générer, publier, vérifier la pull request ; instrumentation des indicateurs du pilote (temps actif par
étape, [04 § 2.0](../specs/04-perimetre-et-lots.md)) en télémétrie produit pseudonymisée ([DEC-83](../specs/03-decisions.md)) ;
traduction anglaise complète (contrôle : aucune clé manquante) ; `docs/plan/recettes/02-jalon-0.md` complété avec la
séquence de démonstration du pilote.

✅ **Vérification automatique.** e2e complet vert ; aucune clé de traduction manquante ; `axe` sur tous les écrans du jalon.

🧪 **Test manuel.** La recette du jalon : séquence de démonstration de [04 § 2.0](../specs/04-perimetre-et-lots.md)
sur l'IFS `dev` hébergé et votre Azure DevOps de test, chronométrée, et critères de
[reference-pilote § 4](reference-pilote.md#4-critères-dacceptation-du-jalon-0) avec la sortie **générée**.

🧠 **Mémoire.** Tous les fichiers ; `changelog.md`.

### 🔒 R-08 — Sortie du jalon 0

**Périmètre** : `J0-29` à `J0-32` et l'ensemble du jalon. **Branche** : `impl/j0-livraison` → `[R-08] Sortie du jalon 0 — prêt pour le pilote`.

**Claude vérifie** : critères de [reference-pilote § 4](reference-pilote.md#4-critères-dacceptation-du-jalon-0) avec la
sortie générée, preuves P1–P5, P7–P9 refaites sur la sortie **générée** (statut « vérifiée » dans [04 § 7.2](../specs/04-perimetre-et-lots.md)),
sécurité de l'hébergement, P9 sur tous les écrans, dette de test soldée.

**Puis Claude détaille le jalon 1** au niveau d'exécution dans [`03-jalon-1-tranche-verticale.md`](03-jalon-1-tranche-verticale.md)
(skill [`detailler-jalon`](../../.claude/skills/detailler-jalon/SKILL.md)), sur le code réel.

**Recette utilisateur** : [`recettes/02-jalon-0.md`](recettes/02-jalon-0.md) complète. **Décision d'ouvrir le pilote** :
la vôtre ; les critères de sortie du pilote lui-même ([DEC-110](../specs/03-decisions.md)) se mesurent ensuite avec
les équipes, en parallèle du jalon 1.

**Après approbation** : branche suivante `impl/j1-<premier segment>` (nom donné par le détail du jalon 1).
