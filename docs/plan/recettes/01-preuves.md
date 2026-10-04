# Recette des preuves (verrou R-03)

> **Rédigée par Luna à l'étape [P-07](../01-preuves.md#p-07--outillage-des-preuves-et-recette-pas-à-pas)**, selon le
> plan de cette étape : ordre P1, P9, P3, P8, P2, P4, P5, P7 ; pour chacune l'état initial, les actions exactes
> (portail Azure, Azure DevOps, commandes), le résultat attendu, la preuve à recueillir, la durée et le nettoyage.
> Critères de référence : [reference-pilote § 4](../reference-pilote.md#4-critères-dacceptation-du-jalon-0).

## Prérequis (à réunir avant P-07)

| Prérequis | Pourquoi |
|---|---|
| 1 à 3 abonnements Azure de test, vides, dont vous êtes **Owner** | Le script du kit attribue des rôles (P1) |
| Une organisation Azure DevOps et un projet où vous êtes administrateur, un dépôt Azure Repos `shop` | Pipelines, environnements, approbations |
| Le droit de créer des groupes Entra dans le tenant (`sg-shop-sql-admins`) et un groupe Azure DevOps « Shop Release Approvers » | Administrateur SQL Entra, approbateurs de prd |
| Azure CLI connecté (`az login`), extension `azure-devops` | Exécution du script Azure |
| Environ 30 € de consommation Azure sur la durée des preuves | SQL serverless, Container Apps, journaux |
