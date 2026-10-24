# P-08 — Auto-revue du mécanisme de reprise PRD (2026-10-24)

## Décision et périmètre

À la suite du run #762, Florian a explicitement demandé de choisir une solution aux blocages et de poursuivre P-08. Cette instruction est la nouvelle décision qui autorise une reprise SQL PRD ciblée et, si C8 passe ensuite, au plus un D3 supplémentaire. Cette note valide la conception locale ; elle ne prétend pas que la reprise ou le D3 ont eu lieu.

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

## Contrôles externes encore requis

La reprise n’est pas encore publiée ou exécutée. Il reste à publier les fichiers avec `Publish-PilotReference.ps1`, créer et valider une PR Shop42, fusionner dans `main`, construire l’artefact CI N, traiter le run automatique R0 sans l’approuver, puis lancer le mode recovery seulement après vérification de l’identité et de l’environnement protégés.

Avant l’écriture SQL, rafraîchir coûts, prévision, inventaire C5, C1–C4 et C6–C9. Après succès, relire le journal sous l’identité PRD du pipeline et confirmer `pendingOperations=0` par le préflight. Le D3 supplémentaire reste conditionné à C8 publié avant chacune des approbations ; un contrôle absent, un coût important, une autre opération pending ou toute divergence impose l’arrêt. P-08 reste `BLOQUE` jusqu’à la preuve finale.
