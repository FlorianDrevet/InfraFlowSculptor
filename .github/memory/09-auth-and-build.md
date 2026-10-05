# 09 — Authentification et build

> À remplir par Luna aux étapes citées, d'après le code réel.
> Cible : DT-03, DT-07, `docs/technique/06-tests-et-qualite.md`. Étapes S-01, S-03, S-07, S-15, J0-05, J0-31.

## Faits vérifiés

- S-01 ajoute `tools/versions.json` et `pwsh tools/dev/check-prereqs.ps1`. Le script compare les versions minimales configurées et vérifie que le daemon Docker répond.
- Le contrôle automatisé est couvert par `python -m unittest discover tools/dev/tests`; les scénarios utilisent des commandes `.cmd` simulées.
- S-02 compile le squelette avec `dotnet build src/backend/InfraFlowSculptor.slnx`; la génération initiale a réussi avec 31 avertissements tolérés par cette étape.
- S-03 fixe le SDK avec `global.json` (`10.0.203`, `latestFeature`) et applique `Directory.Build.props` (nullable, analyse `latest-recommended`, `TreatWarningsAsErrors`). Le build actuel passe avec 0 avertissement et 0 erreur.
- S-04 ajoute cinq projets xUnit (`Domain`, `Application`, `Infrastructure`, `Api`, `Architecture`) sous `src/backend/tests/`. `dotnet test src/backend/InfraFlowSculptor.slnx` passe avec 13 tests et 0 avertissement; `dotnet build` passe avec 0 avertissement et 0 erreur. Le projet Infrastructure.Tests est prêt pour les premières vérifications de persistance en S-06.
- Les projets de test partagent les versions centrales de xUnit 2.9.3, AwesomeAssertions, NSubstitute, coverlet, MVC Testing et NetArchTest. Le projet d'architecture utilise aussi Roslyn pour vérifier les fichiers source et les clés d'extension.
- `InfraFlowSculptor.ServiceDefaults` vient des modèles Aspire `13.5.3`; il fournit OpenTelemetry, la découverte/résilience HTTP et `/alive` en développement.
- Le parcours d'authentification local et ses configurations/secrets du template ont été retirés en S-03. OIDC local arrivera en S-07.
- Écarts de versions à revoir en R-01 : les versions `10.0.7` de `Microsoft.Extensions.Http.Resilience` et `Microsoft.Extensions.ServiceDiscovery` n'existent pas; les deux sont épinglées à `10.10.0`. `Microsoft.OpenApi` est épinglé directement à `2.7.5` pour corriger GHSA-v5pm-xwqc-g5wc.
- Versions S-03 : `Mediator.Abstractions`/`SourceGenerator` `3.0.2`, `Scalar.AspNetCore` `2.17.13`, `FluentValidation.DependencyInjectionExtensions` `12.1.1`.
- Les fichiers générés du template avaient des espaces finaux; ils ont été retirés avant le commit pour que `git diff --cached --check` soit propre. Le build reste à 0 erreur.
- Machine relevée le 2026-10-05 : .NET `10.0.301`, Node `24.13.0`, Aspire `13.6.0`, Python `3.14.2`, PowerShell `7.6.6`, Bicep `0.47.16`, Docker `29.5.3`, Azure CLI `2.88.0`, Git `2.52.0`, GitHub CLI `2.88.1`.
- Node (`24.15.0` minimum) et Azure CLI (`2.90.0` minimum) restent sous le minimum. La mise à jour Node par winget a demandé une élévation administrateur; la mise à jour Azure CLI a été interrompue et la version est restée inchangée. Ces écarts sont aussi suivis dans `NEXT.md`.
- S-05 ajoute l'AppHost Aspire, Infrastructure/Azure, quatre tests du résolveur, le contrôle d'architecture sur la construction directe des clients Azure, et un test d'acceptation `Aspire.Hosting.Testing` qui attend les émulateurs puis `/health` HTTP 200.
- S-05 : `dotnet build src/backend/InfraFlowSculptor.slnx --warnaserror` passe avec 14 projets, 0 avertissement/erreur; `dotnet test src/backend/InfraFlowSculptor.slnx` passe avec 21 tests, 0 avertissement (20 hors acceptation et 1 test AppHost/émulateurs).
- AppHost et émulateurs locaux : `aspire start`, `aspire describe`, `aspire stop --non-interactive`; les secrets `postgres-password`, `keycloak-admin-password`, le mot de passe Gitea et son jeton résident dans les User Secrets de l'AppHost. Ne pas copier leurs valeurs dans le dépôt ou la mémoire.
- Exceptions de versions pour l'hébergement S-05 : Keycloak Aspire `13.5.3-preview.1.26425.3`, MailPit Aspire `13.6.0`, émulateur Key Vault `3.1.3`; détails d'exécution et namespace Service Bus dans `08-runtime-and-orchestration.md`.
