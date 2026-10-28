# J0-12 — Contrat des valeurs effectives

Blocage confirmé : CONFIRMÉ

État au 2026-10-28 : les trois décisions sont reçues. Leur levée est consignée dans [la preuve de déblocage](J0-12-deblocage-20261028.md).

J0-12 est exécutable pour les calculs qui s'appuient sur les cibles et les ressources du snapshot, mais deux choix de contrat empêchent de terminer la règle et la route publique sans anticiper une étape ou inventer un contrat.

1. **RG-RES-07** porte sur une ressource liée. `ModelSnapshot` et `ResourceSnapshot` ne contiennent pas encore de liaisons ; `LinkAggregate` et `WiringEngine` arrivent en J0-15, et le constat `VAL-RES-PRESENCE` relève de J0-20. Florian a confirmé le 2026-10-28 que RG-RES-07 sera couvert en J0-15 et le constat en J0-20 ; J0-12 couvre les cibles et présences calculables depuis le snapshot actuel.
2. **Réponse de `GET /v1/projects/{id}/resources/{rid}/effective-values`** : Florian a confirmé le 2026-10-28 le DTO proposé : une entrée par cible avec id/code, présence, région, tags effectifs et propriétés `{name, value, provenance}` ; provenance `override`, `resource`, `catalogDefault` ou `absent`, avec `value: null` pour une propriété absente ; les ressources existantes renvoient 200 avec une liste de propriétés vide.
3. **Nom de propriété Key Vault** : la vérification manuelle J0-12 cite `enablePurgeProtection`, mais le catalogue et les propriétés stockées utilisent `purgeProtection`, traduit en paramètre Bicep `enablePurgeProtection`. Florian a confirmé le 2026-10-28 que l'API expose `purgeProtection`, conformément à la séparation moteur/émetteur. La recette conserve son nom de paramètre Bicep ; cette divergence de vocabulaire reste à noter dans l'auto-revue J0-12.

## Vérifications effectuées

- `git pull` a été exécuté sur `impl/j0-modele`; la branche locale est propre par rapport à `origin/impl/j0-modele` au début de J0-12.
- `python tools/plan/gate.py check` autorisait J0-12 au statut `A_FAIRE`.
- Lecture de J0-12, RG-RES-01/02/05/06/07, RG-PRJ-05/06/08, RG-CMP-02/04, DEC-09, du modèle d'entrée du moteur, des snapshots, du catalogue et de J0-15.
- Consultation en lecture seule de Claude CLI : RG-RES-07 ne peut pas être couvert honnêtement sans liaisons ; recommandation de le reporter à J0-15/J0-20. Claude recommande également `purgeProtection` comme nom public, en signalant l'incohérence de la documentation.
- Arbitrage de Florian reçu le 2026-10-28 : le contrat public utilise `purgeProtection`; `enablePurgeProtection` reste le nom de traduction Bicep.
- Arbitrages de Florian reçus le 2026-10-28 : RG-RES-07 est reportée à J0-15 et VAL-RES-PRESENCE à J0-20 ; le DTO `effective-values` proposé est confirmé.
- Les travaux indépendants déjà préparés couvrent cibles, présences, régions, groupes, tags, valeurs par défaut/surcharges et provenance. `dotnet test src/backend/tests/InfraFlowSculptor.Engine.Tests/InfraFlowSculptor.Engine.Tests.csproj -c Release --no-restore` : 22/22 réussis. `dotnet test src/backend/tests/InfraFlowSculptor.Architecture.Tests/InfraFlowSculptor.Architecture.Tests.csproj -c Release --no-restore` : 9/9 réussis.
- Aucun endpoint public, migration, appel Azure, commit ou push J0-12 n'a été ajouté. Les changements de moteur restent locaux en attente des décisions ci-dessus.
