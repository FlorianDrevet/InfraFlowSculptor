# Audit SQL de P9 — 2026-10-16

Requête en lecture seule sur `sys.database_role_members`, authentifiée avec le jeton Microsoft Entra de l’opérateur.
Les noms des bases et des principaux sont ceux du jeu `shop42`.

| Cible | Principal de l’API | Type SQL | Rôles constatés |
|---|---|---|---|
| DEV — `sqldb-shop42-orders-dev` | `ca-shop42-api-dev` | `EXTERNAL_USER` | `db_datareader`, `db_datawriter` |
| PRD — `sqldb-shop42-orders-prd` | `ca-shop42-api-prd` | `EXTERNAL_USER` | `db_datareader`, `db_datawriter` |

Aucun autre rôle n’est affecté à ces principaux. Le contrôle applicatif `/health/dependencies` a également retourné
`sql-least-privilege=true` après démarrage à froid : l’essai `CREATE TABLE` est refusé en DEV et en PRD.

Les rapports de release associés sont [data 636 DEV](p1-data-release-636-succeeded/artifacts/ifs-report-dev/ifs-report.json),
[data 636 PRD](p1-data-release-636-succeeded/artifacts/ifs-report-prd/ifs-report.json),
[orders 692 DEV](p1-orders-infra-release-692-main-clientid-succeeded/artifacts/ifs-report-dev/ifs-report.json) et
[orders 692 PRD](p1-orders-infra-release-692-main-clientid-succeeded/artifacts/ifs-report-prd/ifs-report.json).

Deux règles SQL temporaires nommées `ifs-p08-temp-20261016`, limitées à l’adresse publique de l’opérateur, ont permis
cet audit. Elles ont été supprimées sur les serveurs DEV et PRD. La liste finale ne contient que la règle
préexistante `AllowAzureServices`.
