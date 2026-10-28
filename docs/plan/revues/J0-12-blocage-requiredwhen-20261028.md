# J0-12 — sémantique `requiredWhen:` du catalogue

Blocage confirmé : CONFIRMÉ

La route `effective-values` rencontre deux propriétés du catalogue dont la condition commence par `requiredWhen:` :
`capacityReservationLevel` pour `sku=CapacityReservation` et `administratorLogin` pour `authentication=EntraEtSql`.
La sémantique n'était pas explicitée dans le plan, la spécification ou le schéma du catalogue.

Le 2026-10-28, Florian a délégué cet arbitrage à Claude CLI et lui a demandé de retenir sa recommandation. Claude recommande
de traiter `requiredWhen:` comme une condition d'applicabilité, au même titre que `requires:` : masquer la propriété quand
la condition est fausse et la conserver quand elle est vraie. Cette interprétation suit la définition des conditions comme
règles d'applicabilité dans la spec, RG-RES-02 et RG-RES-10. Elle ne crée pas de validation de valeur obligatoire, qui ne
fait pas partie de J0-12.

**Décision appliquée :** `EffectiveValueResolver` évalue `requiredWhen:` avec le même parseur que `requires:`. La propriété
reste présente avec sa valeur effective quand la condition correspond.

## Vérifications effectuées

- Claude CLI a été appelé en lecture seule avec `claude -p --restricted` et le texte de la spec, le schéma et les deux
  descripteurs du catalogue, le résolveur et ses tests. Il a signalé que conserver ces propriétés hors condition serait
  un effet de bord du seul traitement de `requires:`.
- Les tests `HidesCapacityReservationLevelUnlessTheSkuRequiresIt` et
  `HidesAdministratorLoginUnlessSqlAuthenticationRequiresIt` ont échoué avant le correctif pour l'absence du filtrage,
  puis passé après le correctif. Ils couvrent les deux valeurs par défaut réelles du catalogue ainsi que leurs valeurs
  conditionnelles : 11 tests ciblés passés au total.
- Aucun changement n'a été fait au catalogue, à la spec ou au plan ; aucun service Azure ni coût n'est impliqué.

La preuve de déblocage gate est dans [la décision et ses vérifications](J0-12-deblocage-requiredwhen-20261028.md).
