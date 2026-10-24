# P-08 — Auto-revue D2b.3.1–3.6

Date : 2026-10-23 20:50 UTC

Verdict : **VALIDÉ pour la livraison D2b**

Cette auto-revue vérifie les six préconditions de la garde DEV et des déclencheurs avant de commiter ou publier le correctif
dans Azure Repos. Elle ne valide pas C1–C9 pour D3 et n'autorise aucune écriture de ressource Azure.

## Checklist

- **D2b.3.1 — Garde présente : VALIDÉ.** La lecture Azure DevOps confirme que `Pre-check approvals` précède `Exclusive Lock`
  dans `shop42-dev` ; le groupe `[shop42]\Shop42 Release Approvers`, le délai de 30 jours et l'auto-approbation désactivée
  correspondent aux valeurs attendues. Le groupe contient Florian DREVET uniquement. Aucun contrôle n'a été changé.
- **D2b.3.2 — Seul `Deploy_dev` écrit : VALIDÉ.** Dans le clone de preuve, `Preview_dev` est un job ordinaire qui appelle
  `Invoke-IfsInfraPreview.ps1`. `Deploy_dev` est le seul déploiement lié à `shop42-dev` qui appelle
  `Invoke-IfsInfraDeploy.ps1`. `Deploy_prd` dépend de `Deploy_dev` ; aucun autre appel d'écriture pour la cible DEV n'a été trouvé.
- **D2b.3.3 — Inventaire des pipelines : VALIDÉ.** Les dix définitions CI/release attendues et les déclencheurs sont identifiés.
  Les huit définitions collatérales sont suspendues, les deux `orders/infra` restent actives ; aucune exécution n'est en cours
  ou en file et aucune PR n'était active avant cette livraison.
- **D2b.3.4 — C9 et clones : VALIDÉ.** L'auto-approbation est désactivée après le traitement terminal de R0, conformément à la séquence.
  Le clone de preuve est sur `origin/main` `3f5917588b83d11fc07d7f9838f5e895e037dd6d` et sa branche porte exactement deux
  fichiers modifiés : `.ifs/manifest.json` et `.ifs/templates/scripts/IfsRelease.psm1`. Le clone original reste propre sur
  `2875cf3cb3fbb0eef2c777dc6d4565d4b16612aa`.
- **D2b.3.5 — Suspension : VALIDÉ.** Les IDs 74, 76, 77, 79, 83, 85, 86 et 88 sont `disabled`, révision 10 ; 80 et 82
  restent `enabled`, révision 9. Les cinq validations PR 75/78/81/84/87 et les définitions hors périmètre 73/89 sont inchangées.
  Les files sont vides.
- **D2b.3.6 — Déclencheurs compris : VALIDÉ.** Les cinq CI ciblent `main` avec `.ifs/**`; les cinq releases ont
  `trigger: none` et une ressource CI sur `main`. Les cinq validations PR n'ont pas de déclencheur YAML ; les politiques
  Build Validation 9–13 ciblent les IDs 75/78/81/84/87 sur `main`, y compris `/.ifs/templates/**`. Ces YAML PR n'ont aucune
  tâche ni connexion Azure. 73/89 ont `trigger: none`, `pr: none`. L'override de l'ID 80 désactive son déclenchement CI sur
  `main` ; N doit donc être lancé manuellement une fois, puis la release 82 doit produire R0. Aucun YAML n'a été changé.

$1## Exécution N/R0 après la fusion — 2026-10-23

- `main` a été relu à `492dfa0df7f9c22f182f1fec443f90c041ed823d`. Le CI manuel N #759 a réussi, `reason=manual`,
  `sourceBranch=refs/heads/main`, `sourceVersion=492dfa0df7f9c22f182f1fec443f90c041ed823d` (50 secondes).
- La page Azure DevOps indique que R0 #760 a été déclenché automatiquement depuis le run #759. Le champ CLI/API `reason`
  indique néanmoins `manual` et `triggeredByBuild` est vide ; l’association au run amont est confirmée par l’interface
  et la carte de ressource pipeline.
- `Preview_dev` a réussi (1 job, 1m19). `Deploy_dev` est resté en attente du seul Pre-check approvals ; l’approbation
  `25e14872-ef9e-475e-8438-35810e388c90` était liée au run 760 et `blockedApprovers` contenait Florian, auteur de N.
- Le refus via l’API ADO a échoué avec `ApprovalBlockedException`. L’option `Allow approvers to approve their own runs`
  (C9) a été activée puis enregistrée, mais l’approbation existante est restée bloquée. C9 a été rétablie à `false` et
  vérifiée dans l’interface.
- R0 #760 et son approbation ont été annulés. Timeline : `Preview_dev` succeeded ; `Deploy_dev` canceled avant qu'une tâche
  de déploiement démarre ; `Preview_prd` et `Deploy_prd` skipped. Aucun déploiement ou changement de ressource Azure n'a été
  lancé par R0. Le statut reste `canceled`, pas `rejected`. L'autorisation du 2026-10-23 accepte cette fin comme sûre car l'API
  a refusé le rejet avec `ApprovalBlockedException` pour l'unique approbateur bloqué et la timeline confirme l'absence d'écriture.
  Les données C5 qui manquaient ont été relevées et consignées dans `P-08-autonomie-et-c5.md`.

- Le manifeste `shop42` reste en révision 2, filiation `p3-role`, projet `shop42`, avec 99 chemins gérés. L'empreinte logique
  du module est `140f6b9f5a7040d82d8bdcb4bb8375f0172e50bf26a38af29b0eb17e0ccefce8` et correspond au manifeste ; les quatre
  empreintes `units.*.fingerprint` sont inchangées. `Assert-ProofManifestWorkingTree` passe avec exactement deux changements.
- Le diff garde le comportement fail-closed : en cas de divergence, il copie l'aperçu recalculé dans l'artefact dédié, puis
  lève l'exception existante ; l'aperçu approuvé reste inchangé et le répertoire temporaire est nettoyé. L'échec de copie retire
  un artefact recalculé potentiellement périmé.
- Vérifications : Pester **122/122** ; `Test-ReferenceBicep.ps1`, `Test-ReferenceDeterminism.ps1` et
  `Test-ReferencePipelines.ps1` réussis ; lint du plan (105 étapes, 18 verrous), tests `tools/plan` **9/9**,
  `git diff --check` et validation du manifeste réussis.
- **Livraison D2b terminée.** Commit de preuve `a5f8197bbe15494b60c52d777f98c2903d162966` ; merge local
  `9e686db86d3501512d70a4ab4e9136da78589e52`, premier parent `2875cf3cb3fbb0eef2c777dc6d4565d4b16612aa`, arbre égal
  au commit de preuve ; push fast-forward vérifié. PR #75 fusionnée vers `main` le 2026-10-23T21:00Z au commit
  `492dfa0df7f9c22f182f1fec443f90c041ed823d`. Les cinq contrôles #754–#758 ont réussi sur le SHA PR
  `60b5b0f81be1cbc36a9f3071780116b8528b03b4`.



## Coût et limites

Au relevé ActualCost du 2026-10-23T21:42:22Z, le réel MTD est **120,79 EUR** ; la dernière prévision réussie, au 20:50Z,
est **161,68 EUR** fin octobre, dont **40,89 EUR** prévus du 24 au 31 octobre ; les groupes Shop42 totalisaient **6,32 EUR**
au relevé précédent. Azure Pipelines affiche 440/1800 minutes hébergées consommées dans le quota gratuit. L'estimation D3 est
**6,50 EUR HT** (7,80 EUR avec le scénario illustratif de 20 % de taxe), avec une projection mensuelle de **168,18 EUR HT**.
Elle réutilise les ressources actuelles ; les hypothèses et limites sont détaillées dans `P-08-autonomie-et-c5.md`. La prévision
Cost Management à 21:42Z a renvoyé HTTP 429 ; le relevé réussi de 20:50Z reste la dernière prévision. Le budget de 200 EUR reste
une alerte et l'estimation ne constitue pas un plafond garanti.

## Préflight D3 — C4

Le 2026-10-23, une unique passe What-If en lecture seule sur l'artefact `infra` N #759 et l'identité Azure CLI personnelle a
reproduit l'empreinte attendue `77dc939ffd65b288a6534f5172191753ea05a06bb3d14bdad7d9626e112a87b1`. Le fichier validé est
`%LOCALAPPDATA%\InfraFlowSculptor\P08\D3-pre\N759-20261023T213712Z\D3-pre\ifs-preview.json` ; son SHA-256 est
`98c9dc42f18c650c79e836706cb121aaa2f7169be1803817791eaeb6e6d770ba`. Le compte indique 0 création, 5 modifications,
0 recréation, 0 suppression, 0 révocation, **1 opération en attente `Started`** et 1 écriture de données prévue. Cette lecture
ne constitue pas C8 : C8 sera lue sur l'aperçu publié par le run D3, puis respectée strictement. Si l'opération est encore en
attente, aucun stage de déploiement ne sera approuvé.

La tentative de nettoyage des deux répertoires temporaires Pester sous `%TEMP%` a été rejetée par l'auto-review de commande ;
ils restent locaux, sans changement du dépôt ou d'Azure. Les répertoires de répétition D2b sont absents, comme consigné dans
`P-08-resolution.md`.
