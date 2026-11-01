# Résolution du blocage R-05 — recette M13 Scalar

Date : 2026-11-01
Branche : `impl/j0-modele`

Blocage résolu : CONFIRMÉ

## Vérifications effectuées

- Authentification OAuth2 dans Scalar via le realm Keycloak local `ifs`, avec le persona de test `chloe@contoso.example`. `GET /v1/me` confirme `Chloe Petit` et cette adresse vérifiée.
- `GET /v1/projects/77777777-7777-4777-8777-777777777777/permissions/me`, avec l'organisation Contoso `33333333-3333-4333-8333-333333333333`, répond 200. La version est `1661`; la seule permission est `project.read`, sans `members.manage`.
- `GET /v1/organizations/33333333-3333-4333-8333-333333333333/members` confirme le membre Bob Bernard (`01a17cd5-bb5e-7b53-abd2-8f141ce90140`) dans Contoso.
- Dans Scalar, un seul appel a été envoyé à `POST /v1/projects/77777777-7777-4777-8777-777777777777/members/01a17cd5-bb5e-7b53-abd2-8f141ce90140/roles`, avec le rôle `Auditor`, la portée `Project` et `expectedVersion: 1661`. La réponse affichée est HTTP 403 `PROJECT_PERMISSION_REQUIRED`, permission `members.manage`, conforme à M13. Aucun nouvel essai n'a été envoyé.
- Le mot de passe utilisé était déjà présent dans le realm local de test `src/backend/InfraFlowSculptor.AppHost/Realms/ifs-realm.json`; aucune modification ni réinitialisation du mot de passe n'a été nécessaire. Sa valeur n'est pas reproduite ici.
- Le jeton OAuth a ensuite été effacé de l'interface Scalar. L'AppHost IFS utilise le volume PostgreSQL existant `ifs-postgres-17-data`; aucun volume n'a été supprimé ou réinitialisé. Les AppHosts Vole-Papillon restent arrêtés.
- Aucune écriture Azure, Azure DevOps, Entra ou service externe; aucun coût cloud.

La recette Scalar M13 est terminée. Son résultat et le contexte local sont consignés dans [`../recettes/suivi.md`](../recettes/suivi.md) et [`R-05-autovalidation.md`](R-05-autovalidation.md). Le blocage documentaire antérieur, fondé sur l'absence de mot de passe dans les variables d'environnement et User Secrets, était incomplet : le compte de test était déjà défini dans le realm local.
