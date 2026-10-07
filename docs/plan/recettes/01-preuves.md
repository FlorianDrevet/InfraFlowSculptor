# Recette des preuves (verrou R-03)

> Rédigée à P-07 à partir de la sortie de référence et des scripts locaux. Ordre d'exécution : P1, P9, P3, P8,
> P2, P4, P5, P7. Chaque preuve se termine par une capture du run Azure DevOps et une ligne dans
> [resultats.md](../preuves/resultats.md).
>
> **État au 2026-10-07 :** NEXT.md ne recense aucun abonnement de test, projet Azure DevOps ni groupe Entra.
> Cette vérification de portée a donc échoué pour l'instant : les preuves ci-dessous ne sont pas exécutables tant
> que ces prérequis ne sont pas créés. Aucune action Azure ou Azure DevOps n'a été lancée pendant P-07.
> Le montant de dépense mentionné dans une ancienne ébauche n'est pas une estimation vérifiée ; définissez votre
> budget de test avant P-08.

## 1. Préparer l'environnement

### Prérequis à réunir

| Élément | Valeur et droits requis |
|---|---|
| Abonnements Azure | Un abonnement ou trois abonnements de test ; renseigner les IDs dev, prd, shared. Droits Owner ou User Access Administrator pour l'installation des rôles. Pour P2, confirmer que le refus denyDelete est installé avant l'essai de suppression. |
| Tenant et groupes SQL | Tenant contenant les abonnements ; groupe sg-shopNN-sql-admins créé et ses IDs d'objet pour dev et prd. Un même ID peut être utilisé si le groupe est commun aux deux cibles. |
| Azure DevOps | Organisation, projet, dépôt Azure Repos et droits d'administrateur du projet. Créer le groupe de sécurité ShopNN Release Approvers avec les membres autorisés. |
| Poste | Azure CLI connecté au bon tenant, extension azure-devops, PowerShell 7, Git, accès aux abonnements et au projet. `Save-AdoRecordings.ps1` requiert PowerShell 7.5+ pour conserver les timestamps JSON à l'identique. Installer sqlcmd seulement si vous préférez l'utiliser plutôt que l'éditeur de requête du portail. |
| Code projet | Choisir un identifiant libre, par exemple shop42, puis le garder constant pour ce jeu de ressources. Les noms de ressources et le projet Azure DevOps doivent suivre le code publié. |
| Budget | Valider une limite de dépense et un plan de conservation/suppression des ressources de test. Aucun montant n'a été vérifié dans NEXT.md. |

L'état doit être confirmé par vous dans [NEXT.md](../../../NEXT.md) avant la première exécution. L'organisation Azure DevOps
se passe au script de capture comme son slug (par exemple contoso), pas comme une URL complète.

### Publier la référence dans Azure Repos

Dans une session PowerShell, définissez le chemin du clone IFS et celui du dépôt Azure Repos :

    $ifsRoot = 'C:/src/InfraFlowSculptor'
    $shopRepo = 'C:/src/shop42'
    Set-Location $ifsRoot

Le dépôt Azure Repos doit être cloné avant l'appel. Avec ses valeurs réelles, vérifier d'abord sans toucher au clone :

    pwsh -File tools/proofs/Publish-PilotReference.ps1 -RepositoryPath $shopRepo -ProjectCode shop42 -SubscriptionDev '<GUID-abonnement-dev>' -SubscriptionPrd '<GUID-abonnement-prd>' -SubscriptionShared '<GUID-abonnement-shared>' -SqlAdminGroupObjectIdDev '<GUID-groupe-SQL-dev>' -SqlAdminGroupObjectIdPrd '<GUID-groupe-SQL-prd>' -WhatIf

Le résultat doit indiquer les fichiers de référence et témoins attendus et confirmer que le clone n'a pas été modifié.
Puis publier réellement :

    pwsh -File tools/proofs/Publish-PilotReference.ps1 -RepositoryPath $shopRepo -ProjectCode shop42 -SubscriptionDev '<GUID-abonnement-dev>' -SubscriptionPrd '<GUID-abonnement-prd>' -SubscriptionShared '<GUID-abonnement-shared>' -SqlAdminGroupObjectIdDev '<GUID-groupe-SQL-dev>' -SqlAdminGroupObjectIdPrd '<GUID-groupe-SQL-prd>'

Le script affiche l'état Git du clone et ne crée aucun commit. Vérifier .ifs/manifest.json, les trois IDs d'abonnement,
les IDs du groupe SQL, les noms shop42 et les fichiers sous src/api/. Pour une publication réelle, les sources
reference/pilot/bicep-azdo/** et samples/witness-app/** doivent être propres et commitées, et le clone cible doit être
propre. Le script copie uniquement les fichiers suivis par Git, hors .git/, bin/, obj/, TestResults/ et .vs/ ; les
fichiers non suivis sont ignorés. `-WhatIf` fonctionne sur un arbre source modifié et laisse le clone cible inchangé.
Vérifier que bin/, obj/, .vs/, .git/ et TestResults/ ne sont pas présents sous src/api/.

Pour le dépôt neuf, créer le commit initial et pousser main afin que le pipeline d'installation puisse le lire.
Après l'installation du kit, faire passer les changements de preuve par des branches et pull requests vers main.
Le script de publication ne commit ni ne pousse les fichiers.

### Installer le kit et créer les pipelines

Depuis la racine du clone Azure Repos, suivre intégralement .ifs/install/SETUP.md dans cet ordre :

1. Configurer Azure CLI et la valeur par défaut Azure DevOps, puis exécuter d'abord azure-setup.ps1 -WhatIf.
2. Après inspection de toutes les actions prévues, exécuter azure-setup.ps1 sans -WhatIf et confirmer le texte demandé par le script.
3. Créer et lancer la définition d'installation depuis .ifs/install/install.pipeline.yml sur main. Attendre le rapport
   d'installation sans erreurs ; résoudre d'abord toute étape « à confirmer ».
4. Dans Azure DevOps, vérifier les environnements shopNN-dev, shopNN-prd et shopNN-shared, les contrôles
   d'approbation et le verrou séquentiel. Vérifier les connexions de service et leur périmètre.
5. Dans **Pipelines → Library**, saisir MAIN_PAYMENTS_API_KEY comme secret dans ifs-shopNN-dev et
   ifs-shopNN-prd. La valeur reste dans la bibliothèque ; ne pas la mettre dans un fichier, une commande, une capture
   ou un journal.

Les définitions sont créées à partir des chemins YAML. Les pipelines d'infrastructure sont sous
core/infra/pipelines/, data/infra/pipelines/, platform/infra/pipelines/ et orders/infra/pipelines/.
Les pipelines de l'application témoin sont orders/apps/api/pipelines/ci.yml et
orders/apps/api/pipelines/release.yml. Le pipeline d'installation est .ifs/install/install.pipeline.yml.

### Capture anonymisée après chaque run

Depuis la racine du clone InfraFlowSculptor (pas depuis le dépôt Azure Repos), lancer une capture par identifiant de run :

    pwsh -File tools/proofs/Save-AdoRecordings.ps1 -Organization '<slug-organisation>' -Project 'shop42' -RunId 123456 -Scenario 'p1-core-dev'

Remplacer 123456 par le BuildId du run. Choisir un Scenario différent pour chaque run (par exemple ajouter
-run-123456) ; le script refuse tout dossier de capture déjà existant. Il construit la capture dans un dossier temporaire
et ne publie le dossier final qu'après la réussite de toutes les lectures et anonymisations. Le script demande un PAT avec le droit de
lecture de build si AZURE_DEVOPS_EXT_PAT et ADO_RECORDINGS_PAT ne sont pas définis ; le PAT reste en mémoire et
n'est pas écrit dans le dépôt. Il capture build, timeline, liste et contenu textuel des artefacts ifs-report-*,
ifs-preview-* et ifs-app-report-*, ainsi qu'un instantané des approbations du projet au moment de la capture.

L'API [Approvals - Query](https://learn.microsoft.com/en-us/rest/api/azure/devops/approvalsandchecks/approvals/query?view=azure-devops-rest-7.1)
ne fournit pas de BuildId ou RunId dans le schéma de ses objets. `approvals.json` contient donc un instantané de projet,
avec le RunId demandé comme contexte, et ne prétend pas rattacher chaque approbation à ce run. Pour les étapes qui
se chevauchent (P3a, P8), capturer aussi dans Azure DevOps la page de chaque run et son étape d'approbation ; utiliser
la timeline, le nom de ressource et les heures comme éléments de corrélation, jamais comme preuve d'un lien API.
L'instantané est limité à 1000 éléments (`possiblyTruncated` vaut vrai à cette limite). Les noms de scénario doivent
suivre ^[a-z0-9]+(?:-[a-z0-9]+)*$. Si un artefact est binaire ou ne peut pas être anonymisé, le script le refuse au
lieu de l'enregistrer brut.

Anonymisations appliquées : organisation → `org-redacted`, projet → `project-redacted`, abonnements →
`SUBSCRIPTION_ID_001`, IDs d'objet → `OBJECT_ID_001`, GUID → `GUID_001`, adresses → `ADDRESS_001`,
identités → `PERSON_001`, hôtes/pools/dépôts → `HOST_001`, adresses IP → `IP_ADDRESS_001`, descripteurs →
`IDENTITY_DESCRIPTOR_001`, champs et motifs de secrets → `SECRET_REDACTED`. Les pseudonymes déterministes par capture
gardent les relations répétées, dont les `parentId` de timeline ; les dates ISO restent des chaînes à leur précision
d'origine. Vérifier le contenu avant de le committer dans le dépôt IFS. La sortie est déposée dans
src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/<scénario>/.

## 2. P1 — Première installation et application témoin

**Durée indicative :** 2 à 4 heures, hors attente des agents CI et des autorisations.

**État initial :** abonnement(s) de test sans ressources du pilote, dépôt main contenant la référence publiée, aucun
pipeline d'infrastructure lancé.

1. Exécuter les étapes d'installation ci-dessus. Dans Azure DevOps, ouvrir les exécutions des pipelines
   d'infrastructure et vérifier le nom de run, le commit et le manifeste.
2. Déployer core en dev puis prd, en relisant ifs-preview-<cible> avant d'approuver prd.
3. Déployer data en dev puis prd. Après création des bases sqldb-shopNN-orders-dev et
   sqldb-shopNN-orders-prd, ouvrir chaque base dans le portail Azure avec l'identité membre du groupe administrateur
   Entra SQL. Dans **SQL database → Query editor (preview)**, copier le contenu du fichier
   src/api/sql/witness-schema.sql et l'exécuter une fois par base. Cela crée dbo.witness_checks avant la release
   d'orders.
4. Déployer platform sur sa cible shared protégée, puis orders en dev et prd, en relisant l'aperçu avant chaque
   approbation.
5. Dans Azure DevOps, vérifier les builds et releases du témoin via orders/apps/api/pipelines/ci.yml puis
   orders/apps/api/pipelines/release.yml. La release déploie l'image par digest et attend les approbations nécessaires
   pour prd.
6. Dans **Azure Portal → Container Apps → ca-shopNN-api-dev/prd → Overview**, copier le FQDN. Depuis un terminal,
   interroger chaque cible :

        Invoke-RestMethod 'https://<fqdn>/health'
        Invoke-RestMethod 'https://<fqdn>/health/dependencies'

   /health doit renvoyer status: ok et une version d'image. /health/dependencies doit réussir pour secret, logs,
   sql et sql-least-privilege. Les contrôles appconfig et servicebus peuvent être skipped selon leur configuration
   de jalon.

**Résultat attendu :** les releases d'infrastructure réussissent dans leurs cibles ; le témoin répond en dev et prd ;
l'image est référencée par digest ; le contrôle SQL prouve l'écriture/lecture et l'interdiction de CREATE TABLE.

**Preuves :** IDs et liens de run, ifs-preview-*, ifs-report-*, ifs-app-report-*, réponses de /health et
/health/dependencies sans secret. Capturer séparément chaque run (ex. p1-core, p1-data, p1-platform, p1-orders,
p1-api-dev, p1-api-prd).

**Nettoyage :** ne supprimer aucune ressource entre les preuves. Garder les sorties et les ressources jusqu'à la fin
de R-03.

## 3. P9 — Journal d'accès aux données entre composants

**Durée indicative :** 15 à 30 minutes.

**État initial :** P1 réussi ; mêmes fichiers de release et mêmes bases ; les pipelines data et orders ont terminé.

1. Ouvrir l'artefact ifs-report-dev et ifs-report-prd du run orders. Dans chaque ifs-report.json, chercher
   journal.operations et relever l'opération kind: "data-access" avec l'identité système de ca api et la base
   sqldb-shopNN-orders-<cible>.
2. Ouvrir les rapports ifs-report-* du run data. Le tableau journal.operations doit être vide : la release data crée
   la base, mais n'accorde pas l'identité de l'application.
3. Confirmer dans SQL que ca api a uniquement db_datareader et db_datawriter, et que l'essai de création de table
   par /health/dependencies a été refusé.

**Résultat attendu :** le journal d'orders porte l'accès aux données ; celui de data ne contient aucune opération.

**Preuves :** liens des runs data et orders, extraits anonymisés de journal.operations, résultat du contrôle
sql-least-privilege. Capturer les runs si les artefacts ne sont pas déjà enregistrés (p9-data, p9-orders).

**Nettoyage :** aucun ; conserver les ressources.

## 4. P3 — Approbation obsolète et dérive Azure

**Durée indicative :** 1 à 2 heures, hors propagation Azure.

### P3a — Une révision plus récente modifie un rôle

**État initial :** sur main, la référence est à la révision 1 ; le run orders en prd est absent de la file ; accès
Azure du témoin fonctionnels.

1. Dans Azure DevOps, lancer la release orders/infra/pipelines/release.yml. Attendre que ifs-preview-prd soit publié
   et que le stage protégé prd attende une approbation. Ne l'approuvez pas encore.
2. Capturer immédiatement ce run (p3a-orders-old-run-<id>). Noter le manifestFingerprint de l'aperçu et le commit du
   run.
3. Dans le clone Azure Repos, depuis le dépôt IFS, appliquer l'overlay écrit à la main :

        Set-Location $ifsRoot
        pwsh -File tools/proofs/New-ProofRevision.ps1 -RepositoryPath $shopRepo -Revision p3-role

   Le changement remplace, dans orders, Log Analytics Reader par Log Analytics Data Reader. Créer une branche,
   committer les seuls fichiers générés, ouvrir puis fusionner la PR vers main. Laisser le nouveau CI/release démarrer.
4. Revenir au run initial en attente et l'approuver. Il doit s'arrêter au contrôle de fraîcheur, avant toute mutation.
   Vérifier que l'aperçu plus récent déclenche son propre run et sa propre approbation prd ; capturer les deux runs
   (p3a-orders-old-result-run-<id> et p3a-orders-new-run-<id>).
5. Relire les rapports et les journaux : aucun changement Azure ne doit provenir du run obsolète ; la nouvelle
   release ne s'applique qu'après sa propre approbation.

**Résultat attendu :** le run initial est refusé à l'étape de contrôle de fraîcheur ; le nouveau run attend une
approbation indépendante.

**Preuves :** run/commit/empreintes des deux versions, timeline montrant l'arrêt avant mutation, artefacts
ifs-preview-prd et ifs-report-prd.

**Nettoyage :** approuver ensuite la nouvelle release afin que main et Azure convergent avant P3b. Ne pas supprimer
les ressources.

### P3b — L'état Azure dérive pendant l'attente

**État initial :** core et son espace log-shopNN-main-prd sont synchronisés ; sa rétention est de 90 jours.

1. Lancer core/infra/pipelines/release.yml et attendre l'approbation prd après la publication de l'aperçu.
2. Capturer le run en attente (p3b-core-pending-run-<id>).
3. Dans le portail Azure, ouvrir **Log Analytics workspaces → log-shopNN-main-prd → Usage and estimated costs →
   Data retention**. Modifier la rétention de 90 à 120 jours et enregistrer.
4. Approuver le run initial. Il doit s'arrêter au contrôle d'empreinte avant mutation. Capturer le résultat
   (p3b-core-drift-run-<id>).
5. Dans le portail, rétablir 90 jours. Relancer la release core, vérifier un nouvel aperçu et l'approuver ; capturer
   le run de convergence.

**Résultat attendu :** l'approbation de l'aperçu figé ne peut pas écraser la dérive non relue ; après restauration,
une nouvelle release remet l'état désiré.

**Preuves :** aperçus/empreintes, captures du réglage 120 puis 90 jours, timeline du refus, rapport de convergence.

**Nettoyage :** rétention restaurée à 90 jours et release core convergée.

## 5. P8 — Livraison applicative pendant l'attente d'infrastructure

**Durée indicative :** 45 à 90 minutes.

**État initial :** l'API orders répond en prd avec un digest D1 ; aucun run de release d'infrastructure n'est en attente.

1. Déclencher la release orders/infra/pipelines/release.yml. Quand l'aperçu prd est publié et attend une approbation,
   noter D1 et capturer le run (p8-infra-pending-run-<id>).
2. Faire une modification bénigne, relue et limitée au code témoin sous src/api/ afin que le build produise une
   nouvelle image. Fusionner la PR dans main, puis vérifier l'exécution de orders/apps/api/pipelines/ci.yml et le
   nouvel image.json avec un digest D2, différent de D1.
3. Laisser la release applicative orders/apps/api/pipelines/release.yml déployer D2 en dev puis en prd. Approuver
   le stage prd de l'application et vérifier /health sur la nouvelle image. Capturer le run applicatif
   (p8-app-prd-run-<id>).
4. Revenir à l'ancienne release d'infrastructure en attente et l'approuver. Vérifier dans ifs-report-prd que l'image
   D2 est reconduite et que l'empreinte appliquée égale celle approuvée. Capturer le run final
   (p8-infra-result-run-<id>).

**Résultat attendu :** l'infrastructure conserve D2, malgré le déploiement applicatif intervenu pendant
l'approbation ; les empreintes approuvée et appliquée sont égales.

**Preuves :** image.json avec D1/D2, rapport applicatif prd, aperçu et rapport d'infrastructure, /health.

**Nettoyage :** aucun rollback d'image ; l'état en prd reste D2. Garder la modification témoin jusqu'à R-03.

## 6. P2 — Révoquer l'accès à Log Analytics en prd

**Durée indicative :** 1 à 2 heures, hors propagation du rôle.

**État initial :** projet pilote isolé, sortie Azure à la révision 1, contrôle logs fonctionnel, pile core protégée
par denyDelete.

L'overlay p2 a été écrit contre la sortie initiale. Il ne s'applique pas au-dessus de p3-role. Utiliser pour P2 un
projet Azure DevOps, un dépôt et un clone local isolés (code shop43), puis créer son jeu de ressources propre. Depuis
`$ifsRoot`, définir `$shopRepo = 'C:/src/shop43'`, cloner le dépôt Azure Repos à cet emplacement et reprendre les
étapes « Publier la référence » et « Installer le kit » avec le code shop43. Exécuter ensuite le baseline complet de
P1 pour ce projet : core dev/prd, data dev/prd et schéma témoin SQL, platform shared, orders dev/prd, puis build,
release et contrôles de santé de l'API. P2 exige que l'accès logs fonctionne avant la révocation et que denyDelete
soit déjà vérifié. Réutiliser les abonnements ou groupes SQL seulement si les périmètres et les noms sont isolés ;
prévoir le budget pour ce second jeu de ressources. Garder ce clone pour P4 et P5.

1. Dans ce dépôt isolé, vérifier que `$shopRepo` vaut `C:/src/shop43`, puis appliquer l'overlay :

        pwsh -File tools/proofs/New-ProofRevision.ps1 -RepositoryPath $shopRepo -Revision p2

   Créer une branche, committer les changements générés et fusionner vers main.
2. Lancer la release orders/infra/pipelines/release.yml. Relire l'aperçu prd, approuver, puis attendre le rapport
   ifs-report-prd.
3. Dans le rapport d'aperçu, vérifier que `revokedAccess` contient l'attribution Log Analytics Reader de l'identité
   id-shop43-api-prd. Après le déploiement, vérifier dans le journal que l'opération `kind: revoke` est `Done` (la note
   « déjà absent » est possible si le déploiement de pile a déjà retiré le rôle). Vérifier dans le portail que le rôle
   a disparu. Attendre la propagation RBAC, puis interroger /health/dependencies : logs doit échouer ; noter que les
   autres contrôles restent fonctionnels.
4. Vérifier que log-shop43-main-prd et ses données existent toujours.
5. Avant tout essai de suppression, ouvrir **Deployment stacks** et confirmer que la pile core en prd applique
   effectivement denyDelete à l'espace. Si le refus n'est pas visible ou si sa portée est incertaine, ne pas lancer
   la suppression et consigner la preuve comme KO/incomplète. Si denyDelete est confirmé, tenter la suppression
   depuis le portail, vérifier qu'Azure la refuse et que l'espace existe toujours.

**Résultat attendu :** l'attribution disparaît et le contrôle logs échoue après propagation ; l'espace et ses données
restent présents ; denyDelete bloque l'essai de suppression.

**Preuves :** aperçu/rapport orders, journal de révocation, réponses /health/dependencies, vue de la pile et message
Azure du refus. Capturer (p2-orders-prd-run-<id>).

**Nettoyage :** ne pas supprimer la pile ou l'espace. Garder le code P2 et son clone pour P4/P5.

## 7. P4 — Une cible prd saute une révision

**Durée indicative :** 2 à 4 heures.

**État initial :** dépôt isolé P2 à la révision 2 ; mêmes branches, ressources et manifeste après la preuve P2.

1. Appliquer rev3, committer et fusionner :

        pwsh -File tools/proofs/New-ProofRevision.ps1 -RepositoryPath $shopRepo -Revision 3

   Déployer orders en dev puis prd ; vérifier id-shop43-extra-prd gérée par la pile orders prd. Capturer les deux
   runs (p4-rev3-dev-run-<id>, p4-rev3-prd-run-<id>).
2. Appliquer rev4, committer et fusionner. Déployer uniquement en dev. Pour prd, refuser l'approbation ou laisser le
   stage en attente ; ne pas appliquer cette révision en prd. Vérifier que id-shop43-extra-prd reste dans Azure et
   reste gérée par la pile prd. Capturer les runs dev et prd.
3. Appliquer rev5, committer et fusionner. Déployer en dev puis prd. Relire ifs-report-prd : prd doit passer
   directement de la révision 3 à 5, détacher id-shop43-extra-prd de la pile sans supprimer l'identité Azure et
   l'inscrire dans detachedResources. Capturer les deux runs.

**Résultat attendu :** dev applique chaque révision ; prd ne reçoit pas rev4 ; rev5 détache l'identité d'Azure sans
la supprimer.

**Preuves :** commits/PR, trois aperçus, approbation/refus prd de rev4, rapports rev3/rev5 et présence de l'identité
après détachement.

**Nettoyage :** conserver l'identité détachée jusqu'à la revue R-03. Ne pas tenter de la supprimer.

## 8. P5 — Reprise après secret absent

**Durée indicative :** 45 à 90 minutes.

**État initial :** projet isolé P2/P4 ; MAIN_PAYMENTS_API_KEY défini et vérifié par une release core réussie ; aucune
approbation core en attente.

1. Lancer core/infra/pipelines/release.yml. Après publication de l'aperçu prd et avant l'approbation, ouvrir
   **Pipelines → Library → ifs-shop43-prd**.
2. Retirer temporairement la valeur de MAIN_PAYMENTS_API_KEY. Si l'interface ne permet pas d'enregistrer une valeur
   secrète vide, supprimer temporairement la variable. Ne pas copier ni afficher sa valeur. Capturer l'écran de la
   bibliothèque sans valeur.
3. Approuver la release prd. Elle doit publier un rapport PartiallyApplied qui nomme l'étape en échec ; capturer
   le run (p5-core-missing-secret-run-<id>).
4. Restaurer immédiatement la variable secrète dans Library, puis relancer la release core. Elle doit reprendre/
   terminer sans créer de ressources en double. Capturer le run de reprise.

**Résultat attendu :** l'échec partiel est explicite et journalisé ; après restauration, la relance réussit sans
doublon.

**Preuves :** timeline, ifs-report-prd de l'échec et de la reprise, état de la variable sans exposer la valeur.

**Nettoyage :** la valeur secrète est restaurée ; confirmer la release core réussie.

## 9. P7 — Interruption brutale et reprise du journal

**Durée indicative :** 1 à 2 heures.

**État initial :** jeu de ressources de test propre où orders gère le rôle Log Analytics Reader en prd, aucune
opération de révocation en attente. Pour garder l'overlay P2 indépendant du clone déjà utilisé pour P4, utiliser un
projet/repo/clone isolé (code shop44) et exécuter son installation puis le baseline complet de P1. Depuis `$ifsRoot`,
définir `$shopRepo = 'C:/src/shop44'` avant les commandes de cette section.

1. Dans ce clone neuf à la révision 1, appliquer l'overlay p2, créer une branche, committer les fichiers générés et
   fusionner vers main.
2. Lancer la release d'infrastructure orders en prd. Dans **Run pipeline → Variables**, définir
    IFS_TEST_ABORT_AFTER à deploy-unit pour ce seul run. Si Azure DevOps refuse la variable à la file, autoriser
    explicitement sa modification à la file dans les paramètres de la définition ; ne pas enregistrer de valeur
    persistante. Cette valeur de test n'est pas un secret. Approuver le stage protégé prd pour que la tâche atteigne
    l'étape deploy-unit ; l'arrêt brutal survient après le déploiement de la pile, avant la fermeture du journal.
3. Après l'échec brutal de la tâche, ne pas modifier le journal Azure Storage ifs-operations et ne pas relancer
   d'autre pipeline. Dans le compte de stockage stifsshop44prd, ouvrir Containers → ifs-operations → blob
   ifs-shop44-orders-prd.json et relever l'opération encore à faire. Capturer le run (p7-aborted-run-<id>).
   Le rapport final peut être absent parce que le processus s'est terminé avant son écriture ; conserver la timeline
    et l'état du journal. Le journal doit montrer deploy-unit en Started, puis revoke et data-access en ToDo. Le processus
    de renouvellement s'arrête quand son propriétaire disparaît, mais le bail Azure acquis pour 60 secondes subsiste jusqu'à expiration :
    attendre 65 secondes après la fin du run avant la reprise.
4. Dans le clone shop44, appliquer rev3 (elle attend le manifeste rev2), committer puis fusionner vers main :

        pwsh -File tools/proofs/New-ProofRevision.ps1 -RepositoryPath $shopRepo -Revision 3

    La release orders suivante doit rejouer les opérations rev2 encore pendantes dans l'ordre du journal, sous la
    release approuvée rev3, puis produire son rapport avec la révision 3. Lancer cette release sans définir
    IFS_TEST_ABORT_AFTER et capturer
   (p7-resumed-newer-run-<id>).
5. Relire le journal avant/après : deploy-unit, revoke et data-access de rev2 passent à Done, sans nouvel ID d'opération
   pour la même action ; le manifeste et le rapport portent la révision 3. L'attribution Log Analytics Reader doit être absente.

**Résultat attendu :** le premier run laisse des opérations à reprendre ; après expiration du bail, le run approuvé
rev3 les termine dans l'ordre du journal, sans créer de doublon, et le rapport final porte la révision 3. La variable
d'arrêt n'est pas conservée après le run.

**Preuves :** deux timelines, captures des variables de file sans donnée secrète, état du journal ifs-operations
avant/après, rapport de reprise, attribution absente dans Azure.

**Nettoyage :** retirer la variable de test si elle a été enregistrée au niveau de la définition du pipeline.
Garder les ressources jusqu'à R-03.

## 10. Consigner les résultats et nettoyer après R-03

Après chaque preuve, ajouter une ligne dans resultats.md : P1, P9, P3a, P3b, P8, P2, P4, P5 ou P7, date, OK/KO,
liens/captures anonymisés, remarque. Pour chaque KO, conserver le message et les artefacts ; ne pas corriger une
question de conception sans la revue Claude.

Ne pas arrêter les conteneurs Aspire locaux et ne pas supprimer de ressources Azure entre les preuves. Après R-03,
décider si les ressources restent disponibles pour le jalon 0 ou si les seuls groupes et ressources de test peuvent
être supprimés par une personne autorisée. Vérifier avant cette décision les ressources détachées, les piles
denyDelete, le registre partagé et les journaux ; ne pas supprimer un élément encore référencé par un run ou une preuve.

Temps indicatifs hors files d'attente et délais de propagation. Le plan ne donne pas de durée maximale ni de budget
Azure confirmé.
