# J0-21b — diagnostics résolus dans le plan — 2026-11-01

Correctif de J0-21 autorisé par Florian après recommandation de Claude Sonnet. La garde d'étape n'accepte que les identifiants à deux chiffres ; le correctif est donc suivi comme prérequis documenté à J0-22 et ne change pas les lignes ni le texte du plan gérés par le gate.

## Modifications

- `TargetPlan` porte `DiagnosticSettings`; `DiagnosticSettingPlan` expose la ressource source et les réglages Azure, tandis que `DiagnosticDestinationPlan` porte la destination logique et son contexte local ou externe.
- `DeploymentPlanBuilder` matérialise les liens Diagnostics résolus pour chaque cible. Il filtre les sources absentes et les types sans support de diagnostic, résout le workspace et trie les résultats de façon stable.
- Les valeurs standards suivent les références pilote : `allLogs`, `AllMetrics` et `Dedicated`; `ContainerApp` ne porte que `AllMetrics`. La destination vient du lien résolu. `Release.AccessObjectTypes` se base sur la même liste de diagnostics.
- `excludedCategories` n'est pas persisté dans `LinkAggregate`/`LinkSnapshot`; le plan porte une liste vide et cette limite est documentée dans `docs/technique/03-moteur-et-generation.md`. Aucun support d'exclusion n'est revendiqué.
- Le seeder de la référence renseigne le workspace par défaut de `core` lorsque ce réglage est absent, sans remplacer une destination existante. Le test API vérifie les diagnostics locaux et externes par cible.

## Vérifications effectuées

- **Blocage résolu : CONFIRMÉ** — l'information de destination et les valeurs de diagnostic du profil standard sont désormais portées par le `DeploymentPlan`; l'émetteur pourra respecter RG-GEN-07 sans lire `WiringResult`.
- TDD : le test moteur ciblé échouait à la compilation faute de `TargetPlan.DiagnosticSettings`, puis passe après ajout du contrat et du builder.
- `dotnet test src/backend/tests/InfraFlowSculptor.Engine.Tests/InfraFlowSculptor.Engine.Tests.csproj --no-restore --configuration Release --filter FullyQualifiedName~DiagnosticSettingsResolveLocalAndExternalWorkspacesWithReferenceDefaults` : **1/1 réussi**.
- `dotnet test src/backend/tests/InfraFlowSculptor.Engine.Tests/InfraFlowSculptor.Engine.Tests.csproj --no-restore --configuration Release` : **221/221 réussis**.
- `dotnet test src/backend/tests/InfraFlowSculptor.Api.Tests/InfraFlowSculptor.Api.Tests.csproj --no-restore --configuration Release --filter FullyQualifiedName~DeploymentPlanExposesThePilotOrdersProductionPlanDeterministically` : **1/1 réussi**, y compris l'identité externe du workspace et les diagnostics de `core`.
- La documentation technique §4 et la mémoire moteur ont été mises à jour.

## Limite conservée

Le paramètre de formulaire `excludedCategories` n'a pas de stockage dans le modèle actuel. Son ajout et la configuration utilisateur des catégories restent à traiter dans une étape dédiée; il n'est pas inclus dans cette correction.
