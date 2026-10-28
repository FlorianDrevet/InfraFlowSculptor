# P-08 — Résolution du blocage D2b.3.5

Date de contrôle : 2026-10-23 20:50 UTC.

Blocage résolu : CONFIRMÉ

## Vérifications effectuées

- Organisation/projet : `floriandrevet0332/shop42`.
- Avant la suspension, les dix définitions CI/release ciblées étaient `enabled`, révision 9. Codex a changé uniquement le réglage de traitement des nouvelles exécutions pour les huit définitions collatérales :
  - `core/infra` : CI 74 et release 76 ;
  - `data/infra` : CI 77 et release 79 ;
  - `orders/apps/api` : CI 83 et release 85 ;
  - `platform/infra` : CI 86 et release 88.
- Lecture Azure DevOps après modification : ces huit définitions sont `disabled`, révision 10. `orders/infra` CI 80 et release 82 restent `enabled`, révision 9.
- Les pipelines PR 75, 78, 81, 84 et 87 restent `enabled`, révision 9 ; les définitions hors périmètre 73 et 89 restent `enabled`, révision 1.
- Trois lectures des exécutions (`inProgress`, `notStarted`, `postponed`) ne retournent aucun run. La liste des PR actives est vide.
- Inventaire des déclencheurs : les cinq YAML CI ciblent `main` avec des filtres de chemin incluant `.ifs/**` ; les cinq YAML release ont `trigger: none` et une ressource pipeline CI sur `main`. Les cinq YAML PR ont `trigger: none` et `pr: none` ; les seules validations PR sont les politiques Build Validation 9 à 13, actives et bloquantes sur `main`, qui ciblent les définitions 75, 78, 81, 84 et 87 et incluent toutes `/.ifs/templates/**` dans leurs filtres. Les CI 80 et release 82 restent donc les seuls pipelines CI/release activables pour N/R0 ; le CI 80 a l’override UI « Disable continuous integration » sur `main`, et les cinq releases conservent leur déclencheur de ressource sur `main`.
- Les pipelines hors périmètre 73 (`install`) et 89 (`finalize`) ont tous deux `trigger: none`, `pr: none` dans leur YAML ; ils ne démarrent pas automatiquement pendant cette fenêtre.
- Les cinq YAML de validation PR ne contiennent pas de tâche `AzureCLI`, `AzurePowerShell` ni de connexion Azure.
- L’environnement `shop42-dev` conserve `Pre-check approvals` en ordre 1 avant `Exclusive Lock` en ordre 2, délai 30 jours, groupe `[shop42]\Shop42 Release Approvers` inchangé. Le groupe a un seul membre, Florian DREVET ; `Allow approvers to approve their own runs` reste décoché. Aucun contrôle d’approbation n’a été retiré.
- Le fetch Git authentifié a réussi ; `origin/main` est `3f5917588b83d11fc07d7f9838f5e895e037dd6d`. La branche distante `fix/p08-keep-recalculated-preview` reste à `2875cf3cb3fbb0eef2c777dc6d4565d4b16612aa`. Le clone original est propre.
- Cost Management API au 2026-10-23T20:50Z : souscription, réel MTD 120,79 EUR ; prévision de fin de mois recalculée à 161,68 EUR (40,89 EUR restant). L’API de prévision n’avait des valeurs réelles que jusqu’au 21/10 ; le calcul utilise le réel quotidien jusqu’au 23/10 et la prévision du 24 au 31/10, pour éviter le double comptage. Groupes Shop42, réel MTD 6,32 EUR. Azure Pipelines affiche toujours 440/1800 minutes hébergées consommées dans le quota gratuit.
- Aucune ressource Azure n’a été créée, modifiée ou supprimée. Le relevé de coût ne constitue pas encore l’estimation complète exigée avant toute écriture Azure ; le contrôle de coût C5/C8 demeure actif.

Les contrôles ont été relus dans l’interface Azure DevOps et vérifiés par Azure DevOps CLI. La liste des IDs ci-dessus est la portée exacte de la suspension temporaire ; aucune autre définition n’a été modifiée.
