# P-08 — Auto-revue du mécanisme de reprise PRD (2026-10-24)

## Décision et périmètre

À la suite du run #762, Florian a explicitement demandé de choisir une solution aux blocages et de poursuivre P-08. Cette instruction autorise une reprise SQL PRD ciblée et, si C8 passe ensuite, au plus un D3 supplémentaire. Cette note valide le mécanisme local et consigne la reprise PRD #778 réellement exécutée ; elle ne prétend pas que le D3 a déjà eu lieu.

Le parcours retenu est un mode `recover-prd-762` du pipeline `orders/infra`, dont le mode normal reste `standard`. Le stage est borné au projet `shop42` et au composant `orders`. Il passe par la connexion `ifs-shop42-prd`, l’environnement protégé `shop42-prd` et le verrou séquentiel. Le script confirme que la souscription Azure active correspond à celle du fichier de release.

## Garde de reprise

La fonction `Invoke-IfsDataAccessRecovery` vérifie sous le bail du journal :

- le projet `shop42`, le composant `orders`, l’unité `ifs-shop42-orders-prd`, la cible `prd` et `protected=true` ;
- un journal existant de schéma v1 avec la même unité et cible ; le mode `RequireExisting` n’initialise ni conteneur ni blob ;
- exactement une opération `ToDo` ou `Started`, avec l’ID `36ab5a2dfdd9b34f84098f22274bb4a6c35c85fb62493d585df6fab21ba2a71f`, en état `Started`, révision 2 et commit `893f2a3dda4d7c8461be9c93f208a9e84d317962` ;
- la correspondance exacte du serveur, de la base, du principal, du niveau et de l’exposition entre l’opération journalisée et l’artefact PRD ;
- l’exposition `Public`, afin qu’aucune règle de pare-feu temporaire ne soit créée. L’appel SQL saute aussi le nettoyage des anciennes règles de pare-feu.

Le parcours ne lance aucune pile ARM, suppression, révocation ou écriture de secret. Il exécute une seule entrée `data-access.sql`, puis enregistre `Done` seulement après succès SQL. Il copie alors sur l’entrée du journal le commit du manifeste courant, après validation du projet, de la révision, de la filiation `p3-role` et du format du SHA. Cette transition évite que `Add-IfsOperation` réarme l’entrée terminée quand la publication de reprise porte un commit plus récent. En cas d’échec SQL, l’opération reste `Started` avec son commit d’origine ; le bail est libéré dans `finally`.

## Vérifications locales

Le test ciblé a d’abord échoué car la fonction n’existait pas. Les tests couvrent ensuite la réussite ciblée, l’ID/commit incorrects, une seconde opération pending, un détail d’accès divergent, une cible non PRD, une exposition restreinte, l’échec SQL avec état conservé et l’interdiction de créer un journal absent.

Résultats après implémentation initiale :

- `IfsRelease.Tests.ps1` : 63/63 réussis ;
- `Test-ReferencePipelines.ps1` : 25/25 YAML valides contre le schéma Azure Pipelines épinglé ;
- `Test-ReferenceBicep.ps1` : quatre composants valides, sans avertissement ;
- déterminisme : 79 fichiers UTF-8 sans BOM, LF, saut final et en-tête ; manifeste recalculé sur 79 fichiers ;
- `python tools/plan/gate.py lint` : réussi ;
- PSScriptAnalyzer avec sévérité `Error` : aucune erreur. Les règles d’encodage sans BOM du dépôt sont conservées.

## Vérifications après correction de continuité du journal

Le test de réussite vérifie maintenant que le commit du manifeste n’est copié dans l’entrée `Done` qu’après l’exécution SQL et qu’un appel ultérieur à `Add-IfsOperation` sur la même clé conserve `Done`. Le test d’échec SQL vérifie que l’état `Started` et le commit initial restent inchangés. Le jeu ciblé de reprise passe 8/8 ; la suite entière passe **132/132**, dont les tests qui exigent une source committée. `Test-ReferenceBicep.ps1` confirme les modules `core`, `data`, `orders` et `platform`, sans avertissement. PSScriptAnalyzer ne relève aucune erreur sur le module et le script de reprise. Le commit de code local est `b57c1e9`.

Contrôles frais avant toute reprise : Cost Management à 17:33Z donne 130,364070 EUR HT réels, 42,458633 EUR prévus et 172,822704 EUR projetés pour octobre ; le scénario complet de reprise, D3 et 2 Go de marge logs reste à 184,749204 EUR, 15,250796 EUR sous l’alerte de 200 EUR. C5 à 17:34Z conserve les 164 ressources, 26 groupes, 7 piles, 59 rôles, 7 budgets et 21 IDs Shop42 sans changement depuis l’état post-#762. Huit définitions collatérales Azure DevOps sont suspendues en révision 12, les deux définitions `orders/infra` restent activées en révision 9 ; aucune exécution ni PR nouvelle n’a été créée.

La comparaison est achevée, mais la publication n’est pas encore poussée dans Azure Repos et la reprise n’est pas exécutée. Avant la mutation SQL, actualiser coût, inventaire et contrôles C1–C9. La reprise ne part qu’après PR fusionnée, CI N réussie, observation sûre de R0 et vérification de l’environnement/identité protégés.

## Répétition de publication fraîche — 2026-10-24T17:52Z

Une publication neuve est produite dans `%LOCALAPPDATA%/InfraFlowSculptor/P08/recovery-20261024T174003Z/fresh-shop42-final`, puis overlayée en révision 2 `p3-role`. `Assert-ProofManifestWorkingTree` valide les 101 fichiers gérés ; le manifeste porte le commit source complet `b57c1e965c862ab673c466b4508f7018f15a8baf`. Le clone Shop42 d’origine est propre avant copie, sur `9e686db86d3501512d70a4ab4e9136da78589e52`, manifeste révision 2 `p3-role`, 99 fichiers.

La comparaison logique finale est archivée dans `%LOCALAPPDATA%/InfraFlowSculptor/P08/recovery-20261024T174003Z/publication-comparison-final.json`. Les quatre changements de source à copier sont `.ifs/templates/infra-release.yml`, `.ifs/templates/infra-target-stages.yml`, `.ifs/templates/scripts/IfsRelease.psm1` et `orders/infra/pipelines/release.yml`; les deux ajouts sont `.ifs/templates/infra-data-access-recovery.yml` et `.ifs/templates/scripts/Invoke-IfsDataAccessRecovery.ps1`. Les deux écarts d’API du clone (`src/api/Program.cs` et son test) sont spécifiques à P8 et restent intacts. `.ifs/install/tests/IfsInstall.Tests.ps1` ne sera pas ajouté ; `data/infra/server-diagnostics.bicep` sera conservé.

La publication fraîche a aussi des fingerprints `data` et `orders` différents : `data` diffère parce que le clone contient le fichier historique `server-diagnostics.bicep`, que la source fraîche n’a pas ; `orders` diffère à cause du pipeline release attendu. Le recalcul sur l’arbre conservé du clone a confirmé que seul le fingerprint `orders` évolue ; les fingerprints `core`, `data` et `platform` restent identiques à la base Shop42.

## Clone Shop42 recalculé — 2026-10-24T17:53Z

Les six chemins permis ont été copiés dans le clone `C:/Users/flori/RiderProjects/shop42` et le manifeste a été recalculé depuis son arbre réel. La preuve `%LOCALAPPDATA%/InfraFlowSculptor/P08/recovery-20261024T174003Z/target-recalculation.json` confirme 101 fichiers gérés, révision 2, filiation `p3-role`, commit source complet `b57c1e965c862ab673c466b4508f7018f15a8baf`, validation de tous les hashes et exactement sept chemins Git modifiés (les six fichiers plus `.ifs/manifest.json`). Parmi les fichiers déjà présents, seuls les quatre chemins de source attendus ont changé ; les deux nouveaux chemins figurent au manifeste. `data/infra/server-diagnostics.bicep` reste présent, `IfsInstall.Tests.ps1` est absent, et seuls les fingerprints `orders` évoluent. `core`, `data` et `platform` restent identiques. Le clone est encore non committé et non poussé.

## Contrôles externes avant publication — état historique

La reprise n’est pas encore publiée ou exécutée. Il reste à publier les fichiers avec `Publish-PilotReference.ps1`, créer et valider une PR Shop42, fusionner dans `main`, construire l’artefact CI N, traiter le run automatique R0 sans l’approuver, puis lancer le mode recovery seulement après vérification de l’identité et de l’environnement protégés.

Avant l’écriture SQL, rafraîchir coûts, prévision, inventaire C5, C1–C4 et C6–C9. Après succès, relire le journal sous l’identité PRD du pipeline et confirmer `pendingOperations=0` par le préflight. Le D3 supplémentaire reste conditionné à C8 publié avant chacune des approbations ; un contrôle absent, un coût important, une autre opération pending ou toute divergence impose l’arrêt. P-08 reste `BLOQUE` jusqu’à la preuve finale.

## Publication et reprise ciblée PRD #778 — 2026-10-24T18:39Z

Verdict : VALIDÉ pour la résolution de l’opération data-access PRD. PR #77 est fusionnée au SHA main 1abacadd605c0ca6c342ac305ff083be680d56c4 après les validations 771–775 ; N #776 a réussi sur ce SHA. R0 #777 a été annulé avant toute tâche d’écriture après refus de rejet pour approbateur unique bloqué ; timeline et artefact montrent Deploy_dev annulé avant tâche et PRD ignoré.

Run recovery-only #778 : succeeded à 18:39:13Z, source main 1abacadd et ressource CI #776 vérifiées, approbation PRD protégée unique. L’artefact local ifs-data-access-recovery.json confirme le projet shop42, unité ifs-shop42-orders-prd, cible prd, ID 36ab5a2d…a71f, révision 2, commit source historique 893f2a3… et completionCommit 3533c9c… ; error=null, aucune règle temporaire. La mutation se limite au SQL idempotent et à l’état Done du journal ; C5 ne détecte aucune écriture Resource Manager.

Préconditions relues avant l’unique D3 conditionnel : C3 sans exécution active/en file ni PR active (filtre sur status), huit pipelines collatéraux désactivés et 80/82 activés ; C4 frais depuis N #776 avec fingerprint 77dc939ffd65b288a6534f5172191753ea05a06bb3d14bdad7d9626e112a87b1 et pendingOperations=0 ; C5 cinq ensembles identiques et 21 IDs Shop42 présents ; C6 image D2 et HTTP 200 en DEV/PRD ; coût estimé complet 185,323195 EUR HT ; C9 Approval 138 et groupe unique relus. Le D3 reste conditionné au passage de C8 avant toute approbation. Le rapport d’auto-revue ne qualifie pas P8 de terminée : l’unique D3 et le nettoyage après réussite restent à faire.

## Résolution du blocage P-08 après #762 — 2026-10-24T18:59Z

Blocage résolu : CONFIRMÉ

Le blocage était l’opération data-access PRD laissée `Started` par #762, qui faisait échouer le préflight C4/C8. Le code a été corrigé avec test rouge puis vert, publié et fusionné dans Shop42, puis exécuté par la reprise ciblée #778 sous l’environnement PRD protégé. L’artefact recovery confirme `Succeeded` pour l’opération exacte, son commit de complétion, `error=null` et l’absence de règle temporaire. Le préflight frais issu de N #776 confirme `pendingOperations=0` et le fingerprint attendu. C5 après-run ne montre aucune différence Azure. Cette vérification lève le blocage historique et remet P-08 en cours ; elle n’autorise pas à ignorer les contrôles de l’essai D3.

## Vérifications effectuées

- PR #77 fusionnée sur `main` au SHA `1abacadd605c0ca6c342ac305ff083be680d56c4` ; CI N #776 réussit sur ce SHA et R0 #777 est annulé avant toute tâche d’écriture, sans approbation.
- Le run recovery-only #778 consomme le CI #776 attendu, s’exécute dans `shop42-prd` et termine `succeeded`. L’artefact local indique l’opération `36ab5a2dfdd9b34f84098f22274bb4a6c35c85fb62493d585df6fab21ba2a71f`, révision 2, résultat `Succeeded`, `completionCommit=3533c9c…`, `error=null`, sans règle de pare-feu temporaire.
- C4 frais : fingerprint `77dc939ffd65b288a6534f5172191753ea05a06bb3d14bdad7d9626e112a87b1`, `pendingOperations=0`. C5 frais à 19:08:25Z : 164 ressources, 26 groupes, 7 piles, 59 rôles et 7 budgets ; les cinq collections sont exactement égales au snapshot post-#778 et les 21 IDs Shop42 suivis sont présents. Le deployment script VPD hors Shop42 qui explique l’écart historique 163→164 était déjà présent avant #762 et n’a pas changé. Snapshot hors dépôt : `%LOCALAPPDATA%\InfraFlowSculptor\P08\c5-before-D3-20261024T190825Z.json`.
- C3 relu via le champ ADO `status` à 19:01:57Z : aucun run actif/en file parmi les 100 derniers, aucune PR active ; les huit définitions collatérales sont `disabled` en révision 12, les deux `orders/infra` sont `enabled` en révision 9.
- C6 : DEV et PRD sont `Running`, sur le digest D2 consigné, avec `/health` HTTP 200. C9 : Approval 138 et ExclusiveLock 87 sont présents ; l’approbateur et le groupe sont conformes, et `requesterCannotBeApprover=true` avant l’essai.
- Coût avant D3, lectures réussies à 19:04:11Z : ActualCost MTD 131,724309 EUR HT ; Forecast `/forecast` restant 41,098486 EUR ; projection 172,822795 EUR. L’estimation complète avec #770, #778, un D3 et 2 Go de logs reste à 185,323195 EUR HT, 14,676805 EUR sous l’alerte préexistante, sans constituer un plafond garanti. Les réponses brutes sont dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\p08-cost-actual-D3-20261024T190407Z.json` et `p08-cost-forecast-D3-20261024T190407Z.json`.
- Vérifications locales déjà consignées : suite 132/132, quatre composants Bicep sans avertissement, 25 pipelines YAML valides, 79 fichiers déterministes, PSScriptAnalyzer sans erreur ; `python tools/plan/gate.py lint`, tests unitaires du plan et `git diff --check` passent.
- Un `previewRun=true` du pipeline 82 sur la révision 9 valide la compilation YAML sans créer de run (réponse `id=-1`, `state=unknown`, `finalYaml` présent). L’API d’aperçu ne renvoie pas la sélection des ressources ; la ressource CI #776 et le SHA seront vérifiés sur le run D3 réel avant toute approbation.

Le seul travail externe restant avant l’unique D3 est l’activation temporaire du paramètre C9, suivie d’une dernière lecture C3/C4/C5/C6/coût. Le run doit épingler la ressource CI #776 et le SHA `1abacadd…`. C8 sera contrôlé sur l’aperçu DEV avant toute approbation puis sur l’aperçu PRD avant son approbation. Toute divergence impose l’arrêt sans nouvel essai. Après l’état terminal, C9 sera restauré ; le nettoyage ne commencera qu’après satisfaction des conditions de la procédure T.

## Auto-revue D3 #780 et blocage du nettoyage — 2026-10-24

Le run #779 a échoué avant la première tâche sur la sélection de ressource pipeline ; il n’a pas écrit de ressource. L’unique
D3 autorisé #780 a réussi en DEV et PRD sur le CI #776 et le SHA main `1abacadd…`. C8 est satisfait avant les deux
approbations : zéro création, recréation, détachement/suppression, révocation et opération pending. Les deux rapports
`ifs-report/v2` sont `Succeeded`, révision 2, même commit de complétion `3533c9c…`, empreintes approuvées correspondant aux aperçus publiés,
`error` vide ; aucune ressource détachée, aucun accès révoqué et aucune règle de pare-feu temporaire. C3 reste sans run
actif/en file ni PR active ; C9 est restaurée à `requesterCannotBeApprover=true`. C5 confirme la stabilité des IDs suivis.

Le What-If T exact de la pile Orders PRD montre pourtant cinq ressources `Modify` et deux `NoChange` sur l’Container App,
ses diagnostics, son environnement et l’identité managée ; une propriété `principalType` de l’attribution de rôle est
marquée `Unsupported`. Les valeurs live de plusieurs propriétés
scalaires correspondent à `before`, ce qui empêche de qualifier la différence de simple bruit non interprétable. La recette
interdit alors la mise à jour de la pile. Le seuil coût ne justifie pas la procédure d’urgence ; l’unique D3 permis a déjà été
consommé. Aucun changement ni nettoyage Azure n’a suivi le préflight.

Verdict de l’auto-revue : **D3 validé, étape P-08 incomplète**. Le blocage porte maintenant sur le choix de la cible de
réconciliation PRD ou d’une autre règle de nettoyage. Il est formalisé dans [`P-08-blocage-nettoyage-20261024.md`](P-08-blocage-nettoyage-20261024.md) et `NEXT.md`. Les sorties locales sont propres du point de vue des secrets : seules les preuves ADO anonymisées sont ajoutées au dépôt ; les paramètres et rapports bruts restent sous `%LOCALAPPDATA%`.
