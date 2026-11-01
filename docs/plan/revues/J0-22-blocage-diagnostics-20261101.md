# J0-22 — diagnostics absents du plan — 2026-11-01

> **Blocage confirmé : CONFIRMÉ**

## Vérifications effectuées

- J0-22 demande une comparaison octet pour octet avec `reference/pilot/bicep-azdo/`.
- Les Bicep de référence portent, par ressource, le nom du paramètre de diagnostic, la destination Log Analytics, les catégories de journaux et de métriques, ainsi que le type de destination.
- `DeploymentPlan` ne contient pas de paramètres de diagnostic par ressource. `ExternalReference` ne porte que la ressource cible et ne relie pas la source au workspace. RG-GEN-07 interdit à l’émetteur de lire `WiringResult`.
- Les catalogues épinglés par le plan (`2026.10-pilot.3`) et celui présent dans le dépôt (`2026.10-pilot.5`) n’exposent que `diagnostics.supported`; ils ne donnent ni destination ni catégories. L’émetteur ne peut pas reconstruire ces données sans les inventer.
- La fixture de plan actuelle provient du seeder API minimal et ne correspond pas intégralement à la référence : elle est épinglée sur `.3`, alors que la référence SQL utilise les propriétés d’administration introduites en `.4` et `.5`; les tags `costCenter` de la référence ne sont pas présents dans le modèle seedé. Une fixture complète devra être produite depuis un modèle de référence et le catalogue correspondant, pas complétée dans l’émetteur.
- `dotnet test ...Emitters.Tests.csproj --no-restore --configuration Release --filter "FullyQualifiedName~ReleaseAndDataAccessFilesMatchTheReference|FullyQualifiedName~EmitIsDeterministic"` : **2/2 réussis**.
- Le test complet `PilotPlanEmitsBicepFilesThatMatchTheReference` échoue encore : les modules `main`, `types`, paramètres, rôles et helpers ne sont pas émis. Les fichiers `release.*.json` et scripts SQL émis correspondent à la référence dans le test ciblé.
- Vérification locale indépendante du sujet Keycloak : `/alive` répond 200 et les métadonnées OIDC du realm `ifs` répondent 200. La recette R-05 consigne que le mot de passe de test était déjà dans le realm et que M13 est passée; aucun changement de mot de passe ni redémarrage n’est nécessaire pour J0-22.

## Recommandation Claude Sonnet

En consultation en lecture seule, Sonnet recommande un correctif ciblé du plan J0-21 : ajouter une représentation neutre et résolue des diagnostics par ressource à `TargetPlan`, la construire dans `DeploymentPlanBuilder` à partir des liaisons résolues, ajouter les tests et l’instantané Verify, puis documenter le champ dans `docs/technique/03 §4`. J0-22 pourrait alors émettre les diagnostics sans lire `WiringResult` ni coder les paramètres en dur. Cette recommandation ne constitue pas une validation de conception à elle seule.

## Décision requise

Les consignes du dépôt interdisent de modifier `docs/technique/` ou de reprendre J0-21 dans J0-22. Florian doit autoriser explicitement le correctif J0-21b et la mise à jour de `docs/technique/03 §4`, ou maintenir J0-22 bloquée.

Le code J0-22 préparatoire reste local et non committé. Aucun push ni `gate.py done` n’a été effectué : le test de comparaison complet n’est pas vert et la revue R-06 n’a pas commencé.
