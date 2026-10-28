# Auto-validation R-04 — Revue de sécurité des accès

Verdict : VALIDÉ

- **Segment** : J0-01 à J0-05 — isolation, identité, organisations, invitations, audit, rôles et permissions projet.
- **Branche / pull request** : `impl/j0-acces` → `impl/socle` — [PR #4](https://github.com/FlorianDrevet/InfraFlowSculptor/pull/4), ouverte.
- **Revue Claude CLI** : diff depuis `origin/impl/j0-acces` (`cb86d7c7591df75db2af6df909aa5448b1f3e0e5`).

## Vérifications exécutées

| Commande | Résultat observé |
|---|---|
| `python tools/plan/gate.py check` | R-04 autorisée ; blocage d'idempotence résolu et documenté. |
| `python tools/plan/gate.py lint` | 105 étapes et 18 verrous cohérents. |
| `dotnet test src/backend/InfraFlowSculptor.slnx --configuration Debug --no-restore` | 205/205 : Domain 42, Architecture 8, Application 13, Infrastructure 66, API 73, Acceptance 3. |
| `dotnet build src/backend/InfraFlowSculptor.slnx --configuration Release --no-restore --warnaserror` | Réussi, 0 avertissement, 0 erreur. |
| `npm run test` | 63/63 : Angular 57, tokens 3, icônes 3. |
| `npm run lint` | ESLint et i18n réussis ; 168 clés traduites présentes et utilisées en français et anglais. |
| `npm run build` | Build Angular réussi. |
| `npm run api:check` | Client généré cohérent avec OpenAPI v3.1.1 : 30 modèles, 3 services. |
| `git diff --check` | Aucun problème d'espacement ou de conflit. `testResults.xml` reste hors commit. |

## Revue Claude CLI

Commande de la relecture finale :

```powershell
git diff --no-ext-diff --unified=30 origin/impl/j0-acces -- src/backend src/frontend |
  claude -p --restricted 'Relis le diff de code fourni contre origin/impl/j0-acces en lecture seule. Vérifie le code environnant et les tests. Signale uniquement les défauts actionnables de correction, sécurité ou fiabilité avec gravité, fichier et ligne exacte. Ne modifie aucun fichier. Pour chaque constat déjà corrigé, indique-le. Si aucun défaut ne subsiste, dis-le et mentionne les lacunes de test pertinentes.'
```

Claude a signalé un risque moyen : une violation d'unicité pendant une transaction détenue par le filtre pourrait laisser PostgreSQL dans un état avorté avant la persistance de la réponse 409. Le test `MembershipConflictRollsBackToSavepointBeforeIdempotencyResponseCommits` force maintenant cette violation avec deux connexions PostgreSQL : le `SaveChanges` échoue, le comportement renvoie `CONFLICT_VERSION`, puis la réponse 409 d'idempotence est persistée et commitée dans la transaction externe. Il passe. Le comportement correspond au savepoint EF Core documenté ([transactions et savepoints EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions#savepoints)). Le test API concurrent vérifie aussi l'invitation finale `Accepted` et l'unique appartenance après deux acceptations concurrentes.

Autres constats et traitement :

| Constat | Traitement et preuve |
|---|---|
| Réponse idempotente perdue si son enregistrement échoue après l'écriture métier | Corrigé : réservation, commande et réponse partagent une transaction ; les échecs relâchent la réservation. Tests API et transaction EF. |
| Rejeu d'une réponse idempotente entre utilisateurs | Corrigé : dérivation de clé avec tenant, utilisateur et clé cliente pour les routes organisationnelles ; tests API et magasin. La création d'organisation utilise la table utilisateur dédiée. |
| Réponses d'erreur persistées avec la clé | Faux positif : DT-31 spécifie que la même clé avec la même requête renvoie la réponse enregistrée, sans exception selon le statut. Les erreurs `IResult` sont donc rejouées comme la réponse initiale ; une nouvelle tentative métier est une nouvelle requête avec une nouvelle clé. Les exceptions qui sortent du filtre ne sont pas mises en cache. |
| Suppression de rôle de projet insuffisamment autorisée | Faux positif : `[RequiresPermission(MembersManage)]` est contrôlé par `AuthorizationBehavior` avant le gestionnaire. Le test lecteur reçoit `PROJECT_PERMISSION_REQUIRED` et le rôle reste attribué. |
| `UserProfileSyncMiddleware` après une première connexion concurrente | Aucun défaut confirmé : 32 requêtes parallèles vers la liste d'organisations réussissent et renvoient la même liste ; le handler relit le profil depuis la base. Le test ne prouve pas isolément que chaque exécution passe par la branche de conflit. |
| DELETE imbriqué d'un audit | Corrigé : la migration n'autorise la purge imbriquée qu'avec le marqueur transactionnel posé par la fonction de purge ; le test négatif passe. |
| La garde de `InvitationAggregate.ComputeTokenHash` a été retirée | Défaut confirmé et corrigé : `ArgumentException.ThrowIfNullOrWhiteSpace` est rétabli. Le test de domaine a échoué avant correction puis passe. La prévisualisation vide renvoie 404 ; le validateur d'acceptation rejette le jeton blanc par 400. |
| `GRANT EXECUTE ... TO CURRENT_USER` ne couvrirait pas l'identité du Worker | Le grant implicite au migrateur a été retiré. Avant d'exécuter la purge depuis le Worker hébergé, J0-31 devra accorder `EXECUTE` à l'identité Worker réelle. Aucun déploiement Azure Worker n'est réalisé dans R-04. |
| État `Pending` après interruption et rétention de 24 h | La réservation est désormais dans la transaction métier et ne survit pas à son rollback ; la purge garde la rétention de 24 h spécifiée par DT-31. |
| Transaction d'idempotence et savepoint non vérifiés | Couvert : `IdempotencyStoreTests` vérifie l'invisibilité avant commit, la persistance après commit et la disparition après rollback ; `MembershipConflictRollsBackToSavepointBeforeIdempotencyResponseCommits` vérifie le conflit d'unicité et le commit de la réponse 409 (test ciblé réussi). |
| Acceptation concurrente sans preuve d'une seule appartenance | Corrigé côté preuve : le test API vérifie l'invitation `Accepted` et une seule appartenance du profil après les deux requêtes (1/1 ciblé). |
| Préférences utilisateur : retry sans changement local non couvert | Lacune hors du périmètre d'accès R-04 ; aucun défaut de sécurité ou de correction observé. |

## Recette exécutée

| Scénario | Résultat observé | Preuve |
|---|---|---|
| A14 — permissions sur le projet `shop` | Alice : HTTP 200, 15 permissions ; Chloé : HTTP 200, `project.read` uniquement. | Scalar local ; résultat consigné dans [`suivi.md`](../recettes/suivi.md). |
| Dernier propriétaire projet | L'interface affiche l'erreur `RG-ORG-11` ; le scénario Playwright ciblé réussit en desktop et mobile (2/2). | Tests API et captures de la recette J0-02. |
| Parcours « Emma sans organisation » | Échec constaté : la base PostgreSQL persistante contient déjà Emma comme membre de Contoso depuis une recette antérieure. Le scénario ne correspond donc pas à cet état ; la base n'a pas été réinitialisée ni modifiée pour masquer l'écart. | État et commande de relance consignés dans `NEXT.md` ; exécution précédente dans `suivi.md`. |
| Ressources Azure | Aucune écriture Azure pour R-04. Au contrôle du 2026-10-27, aucune ressource active ni aucun groupe shop42/43/44 ne subsiste ; six Key Vault standards sont en suppression différée, sans frais d'usage. | [Nettoyage résiduel shop42](../preuves/nettoyage-residuel-shop42-20261027.md), [clôture shop44](P-08-nettoyage-shop44-20261027.md). |

## Écarts

- La recette d'Emma est affectée par l'état persistant local ; ne pas supprimer ni réinitialiser son appartenance uniquement pour faire passer ce scénario.
- La relecture Claude a relevé une lacune de test pour le retry de préférences sans changement local ; elle est hors périmètre R-04.
- La réponse 409 `IDEMPOTENCY_IN_PROGRESS` après épuisement du délai de 30 s n'a pas de test dédié.

## Contrôles de coût et sécurité

- Aucun groupe ou ressource Azure shop42/43/44 n'est actif au contrôle du 2026-10-27. Les six Key Vault standards restants sont en soft-delete avec purge différée et sans frais d'usage. Aucune écriture Azure n'a été réalisée pour R-04.
- Le payload de l'outbox contient le jeton d'invitation brut jusqu'à l'envoi ou à l'abandon après dix tentatives. Aucun chiffrement applicatif partagé entre API et Worker n'est défini ; cet écart est conservé dans `.github/memory/05-data-and-storage.md`.
- Avant l'appel de la purge d'audit depuis le Worker hébergé en J0-31, l'identité Worker devra recevoir explicitement le droit SQL `EXECUTE`. Aucun Worker Azure n'est déployé par R-04.
- Aucun secret, jeton ou mot de passe n'a été ajouté à Git, à la documentation ou à la mémoire.
- Les revues Claude CLI restent requises aux verrous du plan ; chaque constat est vérifié et consigné, et le verdict du gate reste celui de Codex.

## Conclusion

Les contrôles de R-04, A14 et les vérifications automatiques passent. Le scénario local dépendant de l'état persistant et les deux prérequis de sécurité différés ci-dessus sont consignés ; ils ne bloquent pas le périmètre R-04 ni n'autorisent une écriture Azure.
