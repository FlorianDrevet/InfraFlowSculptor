# 08 — Exécution et orchestration

> S-05 implémenté le 2026-10-05 ; compléter aux étapes citées, d'après le code réel.
> Cible : `docs/technique/05-execution-locale.md`, DT-08, DT-17. Étapes S-05, S-08, P-03 à P-08, J0-24, J0-31.

## Faits vérifiés

- `src/backend/InfraFlowSculptor.AppHost` orchestre PostgreSQL 17 avec pgweb, Azurite, Service Bus Emulator et quatre files à sessions, Redis avec RedisInsight, MailPit, Keycloak avec import du royaume `ifs`, l'émulateur Key Vault, Gitea 1.24 et l'API. PostgreSQL, Azurite, Keycloak et Gitea utilisent des volumes persistants.
- Le Service Bus Emulator exige le namespace `sbemulatorns` dans `UserConfig`; l'AppHost remplace la valeur par défaut basée sur le nom de ressource `servicebus`. Le namespace exposé à l'application reste la chaîne de connexion Aspire `servicebus`.
- Keycloak Aspire écoute en HTTPS sur le port fixe 8080; l'émetteur correct est `https://localhost:8080/realms/ifs`. La découverte OIDC retourne cet émetteur. Les comptes de démonstration et leurs GUID stables figurent dans `docs/technique/05-execution-locale.md`; les secrets locaux restent dans User Secrets.
- Les contrôles locaux du 2026-10-05 ont donné toutes les ressources S-05 Running/Healthy après 139,5 s, `/health` = Healthy, pgweb servi avec une base `ifs` sans table, MailPit vide, RedisInsight HTTP 200. `tools/dev/gitea-init.ps1` vérifie l'existence de l'utilisateur, de l'organisation et des trois dépôts privés; deux exécutions réussissent.
- Le script Gitea lance `gitea admin` avec `docker exec --user git`; il génère le mot de passe via `RandomNumberGenerator.Create()` pour compatibilité PowerShell et neutralise temporairement le traitement terminant des stderr natifs. Mot de passe et jeton ne sont pas commis.
- Le test d'acceptation démarre les émulateurs, attend leur intégrité puis réessaie `/health` jusqu'au HTTP 200. `DistributedApplicationTestingBuilder` peut laisser l'état du projet API à `Waiting` alors que son endpoint est déjà sain; le test vérifie donc l'endpoint réel.
- Écarts de paquets S-05 : `Aspire.Hosting.Keycloak` est en preview `13.5.3-preview.1.26425.3`, `CommunityToolkit.Aspire.Hosting.MailPit` en `13.6.0`, et `AzureKeyVaultEmulator.Aspire.Hosting` en `3.1.3`. Le paquet Keycloak 13.5.3 stable et MailPit 13.5.3 n'étaient pas disponibles lors de l'implémentation.
- L'interface de connexion du compte Keycloak n'a pas été soumise via l'automatisation : la skill `computer-use` interdit d'automatiser les dialogues d'authentification. L'échange de jeton local Alice et la découverte OIDC ont été vérifiés par HTTP; le contrôle visuel reste noté dans `NEXT.md`.
