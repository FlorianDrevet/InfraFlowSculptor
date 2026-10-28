# J0-12 — Déblocage de la sémantique `requiredWhen:`

Blocage résolu : CONFIRMÉ

Florian a demandé à Codex de consulter Claude CLI puis de retenir sa recommandation. Claude recommande d'appliquer à
`requiredWhen:` le même comportement d'applicabilité que `requires:` : exclure la propriété lorsque la condition est
fausse et l'inclure avec sa valeur effective lorsqu'elle est vraie. La spécification décrit les conditions comme des
règles d'applicabilité (RG-RES-02 et RG-RES-10) ; les descripteurs concernés sont `capacityReservationLevel` avec
`sku=CapacityReservation` et `administratorLogin` avec `authentication=EntraEtSql`.

Codex a appliqué cette recommandation à `EffectiveValueResolver`. Aucun validateur conditionnel n'est ajouté en J0-12 ;
aucune modification n'est apportée à la spec, au plan ou au catalogue.

## Vérifications effectuées

- La revue sémantique de Claude CLI a été lancée avec `claude -p --restricted` en lecture seule, avec le texte de la
  spécification, le schéma et les descripteurs du catalogue, le résolveur et ses tests. Claude conclut que l'intention
  « propriété applicable seulement si » correspond au filtrage d'applicabilité et que les deux descripteurs doivent être
  cachés hors condition.
- TDD : les deux tests sur les descripteurs réels échouaient avant le correctif, car les propriétés apparaissaient pour
  `PerGB2018` et `EntraSeule`. Après le correctif, ils vérifient aussi les valeurs par défaut 100 et `sqladmin` lorsque
  les conditions sont vraies.
- `dotnet test src/backend/tests/InfraFlowSculptor.Engine.Tests/InfraFlowSculptor.Engine.Tests.csproj --configuration Release --filter FullyQualifiedName~EffectiveValueResolverTests` : 11/11 réussis.
- Aucune écriture Azure, dépense, migration ou modification de données persistantes n'a été effectuée.
