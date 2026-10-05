# 09 — Authentification et build

> À remplir par Luna aux étapes citées, d'après le code réel.
> Cible : DT-03, DT-07, `docs/technique/06-tests-et-qualite.md`. Étapes S-01, S-03, S-07, S-15, J0-05, J0-31.

## Faits vérifiés

- S-01 ajoute `tools/versions.json` et `pwsh tools/dev/check-prereqs.ps1`. Le script compare les versions minimales configurées et vérifie que le daemon Docker répond.
- Le contrôle automatisé est couvert par `python -m unittest discover tools/dev/tests`; les scénarios utilisent des commandes `.cmd` simulées.
- S-02 compile le squelette avec `dotnet build src/backend/InfraFlowSculptor.slnx`; la génération initiale a réussi avec 31 avertissements tolérés par cette étape.
- Les fichiers générés du template avaient des espaces finaux; ils ont été retirés avant le commit pour que `git diff --cached --check` soit propre. Le build reste à 0 erreur.
- Machine relevée le 2026-10-05 : .NET `10.0.301`, Node `24.13.0`, Aspire `13.6.0`, Python `3.14.2`, PowerShell `7.6.6`, Bicep `0.47.16`, Docker `29.5.3`, Azure CLI `2.88.0`, Git `2.52.0`, GitHub CLI `2.88.1`.
- Node (`24.15.0` minimum) et Azure CLI (`2.90.0` minimum) restent sous le minimum. La mise à jour Node par winget a demandé une élévation administrateur; la mise à jour Azure CLI a été interrompue et la version est restée inchangée. Ces écarts sont aussi suivis dans `NEXT.md`.
