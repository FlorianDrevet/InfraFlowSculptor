# Dette de tests

| Étape | Code non couvert | Raison | Étape qui solde |
|---|---|---|---|
| S-08 | Report pour budget épuisé et escalade réessai / dead-letter de `SessionJobProcessorService`; boucle de renouvellement du runner non exercée de bout en bout | Les tests de réussite et d'équité sont couverts par AppHost; les branches de settlement demandent un seam de session Service Bus. L'acquisition/renouvellement du store, l'outbox, son seuil d'échec définitif et le dispatcher de domaine ont des tests Infrastructure; la boucle hébergée du runner reste à couvrir si R-01 l'exige. | R-01 — décider si des seams supplémentaires sont exigés |
