# Blocage P-08 — What-If des piles shop43 — 2026-10-26

Blocage confirmé : CONFIRMÉ

## Condition bloquante

La procédure T de docs/plan/recettes/01-preuves.md, § 10, demande de rejouer le modèle et les paramètres du dernier déploiement, puis d'arrêter le retrait de denyDelete si le What-If prévoit une modification d'une ressource. Le préflight a exporté les modèles enregistrés par Azure et les paramètres de chaque pile, sans valeur secrète, depuis les piles correspondant aux derniers runs réussis.

Résultats en format FullResourcePayloads :

- ifs-shop43-orders-prd, release #843, commit 9440556f6918d5657eba607cf9913b2fb95c4a8b : 5 Modify, 2 NoChange, 2 Unsupported, 1 Ignore. Les cinq ressources concernées sont le Container App, ses diagnostics, l'environnement Container Apps, ses diagnostics et l'identité de déploiement. Les deux Unsupported sont des affectations de rôles dynamiques ACR Pull et Key Vault Secrets User.
- ifs-shop43-data-prd, release #793, commit c350eca3ea69e6bda307868b1c036650d72190e4 : 4 Modify, 3 NoChange, 1 Ignore. Les ressources concernées sont le serveur SQL, ses paramètres d'audit, la base et ses diagnostics.
- ifs-shop43-platform-shared, release #795, commit c350eca3ea69e6bda307868b1c036650d72190e4 : 1 Modify, 2 NoChange. La ressource concernée est l'ACR partagé.

Ces changements dépassent le seul retrait de la protection de pile. Les prévisions ResourceIdOnly « Deploy » ont été remplacées par les résultats FullResourcePayloads, qui identifient les types de changements. Aucun modèle, paramètre ou ressource Azure n'a été modifié pour ce préflight.

## Décision de règle retenue

Le 2026-10-26, Florian a délégué à Codex le choix de la règle. Codex retient la réconciliation du modèle et de l'état live, sans tolérance générale aux `Modify`. Une différence signalée par What-If n'est classée comme bruit que si l'absence d'effet est démontrée pour la propriété et la ressource exactes. Les limites documentées de What-If (par exemple une fonction `reference()` non résolue ou une valeur par défaut omise) ne constituent pas à elles seules une autorisation de mise à jour.

Le retrait de `denyDelete` et `deleteAll` restent suspendus tant qu'une modification effective est prévue ou qu'un delta n'est pas classé. Les corrections de modèle ou de ressources devront être consignées comme une étape P-08 explicite avant de reprendre T.

## État des preuves et portée

Le gate autorisait P-08 EN_COURS avant ce blocage. C3 ne montrait aucune build ou PR active/en attente. Le C5 post-#843 reste à 174 ressources, 29 groupes, 7 piles, 63 attributions de rôles et 7 budgets. Les cinq groupes orders/data/platform ont un contenu live qui correspond exactement aux IDs des captures C5; les IDs shop43 sont absents de la baseline du 2026-10-15. Les captures de propriété et les What-If expurgés sont conservés hors dépôt sous %LOCALAPPDATA%/InfraFlowSculptor/P08/shop43-deny-preflight-20261025T234253Z/.

Aucune mise à jour de denyDelete, suppression d'identité ou deleteAll n'a été exécutée après ces What-If. Contrôle J+1 exécuté à 2026-10-26T00:09:36Z. La requête ActualCost par ressource/groupe couvre le 25–27/10 et retourne zéro ligne par ressource ; la requête MTD quotidienne par groupe retourne 62 lignes shop42, 4,656561 EUR cumulés, avec dernière date d'usage disponible 20261024. Les données ne couvrent donc pas encore le 25/10 (jour de suppression) ni le 26/10 ; le contrôle est inconclusif, aucune absence de coût après suppression n'est revendiquée. Requêtes, réponses et erreurs 429 sous %LOCALAPPDATA%/InfraFlowSculptor/P08/shop42-j1-cost-20261026/. Refaire le contrôle quand UsageDate atteint le 25/10 ou après.

## Vérifications effectuées

- python tools/plan/gate.py check autorisait P-08 avant le nouveau blocage.
- C3 Azure DevOps : zéro run en cours, zéro run en attente et zéro PR active.
- Les paramètres stockés correspondent aux paramètres déclarés des modèles exportés; aucun paramètre de type secureString/secureObject n'existe dans ces piles.
- Les groupes Azure ciblés, leurs contenus et les piles gérées correspondent aux captures C5 et aux rapports de run; aucune ressource shop43 correspondante n'existait dans la baseline.
- What-If en format ResourceIdOnly a été remplacé par FullResourcePayloads, conformément à la distinction des types de changement de la documentation Microsoft.
- Documentation consultée : https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/deploy-what-if.

## Diagnostic ciblé après la délégation (2026-10-26 07:25Z)

Les trois What-If ont été relancés sans écriture avec les modèles et paramètres exacts exportés des piles, `--result-format FullResourcePayloads` et `--no-pretty-print`. Les résultats gardent 5/4/1 `Modify` et les 2 `Unsupported` de `orders-prd`.

- **orders-prd** : le Container App prédit le retrait du trafic `latestRevision=true, weight=100`; le live est en mode `Single`. Le modèle contient des références dynamiques vers le workspace, l'ACR et Key Vault; What-If conserve ces expressions non résolues dans certains champs d'application. Microsoft documente cette limite. Les diagnostics de l'API et du managed environment prédisent `logAnalyticsDestinationType: Dedicated`, tandis que les deux ressources live renvoient `null`. Pour le managed environment, `null` sélectionne AzureDiagnostics et `Dedicated` sélectionne les tables propres à la ressource; c'est un changement effectif à réconcilier, pas un bruit accepté. Les valeurs d'environnement n'ont pas été affichées.
- **data-prd** : le modèle demande `sku.name=GP_S_Gen5_1` et `minCapacity=0.5`; le live renvoie `sku.name=GP_S_Gen5`, `currentServiceObjectiveName=GP_S_Gen5_1` et `minCapacity=0.5`. Cette différence de représentation du SKU reste classée comme non résolue; elle n'est pas déclarée sans effet. Le diagnostic SQL live utilise déjà `Dedicated`.
- **platform-shared** : le live renvoie un chiffrement `disabled`, `LegacyRegistryPermissions` et `networkRuleBypassAllowedForTasks=false`; le What-If omet les deux premières propriétés et demande `false` pour la troisième. Les deux omissions restent à justifier contre les valeurs par défaut du fournisseur avant de les tolérer.

Lectures effectuées via Azure CLI/ARM en lecture seule. Sources Microsoft : [limites de What-If et fonctions `reference()`](https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/deploy-what-if), [valeur par défaut AzureDiagnostics de `logAnalyticsDestinationType`](https://learn.microsoft.com/en-us/rest/api/monitor/diagnostic-settings/get?view=rest-monitor-2021-05-01-preview), [mode Single et poids de trafic des Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/revisions).

**Conclusion** : le retrait de protection ne reprend pas. La destination des journaux du managed environment et le SKU SQL ne sont pas réconciliés; aucun écart annoncé n'est accepté sans preuve de non-effet. Aucun update de pile, approbation de déploiement ou `deleteAll` shop43 n'a été exécuté pendant ce diagnostic.

## Résolution du verrou avant nettoyage P4 — 2026-10-26 08:35Z

Blocage résolu : CONFIRMÉ

Florian a délégué le choix de règle puis a demandé de terminer la recette sans attendre un jour supplémentaire, en gardant les coûts bas. La règle appliquée reste ciblée : chaque `Modify` doit être classé à partir de l'état live et du comportement exact du fournisseur; aucune tolérance générale n'est accordée aux différences What-If.

Les What-If FullResourcePayloads exacts ont été relancés le 2026-10-26 vers 08:26Z depuis les exports des déploiements réussis, sans valeur `secureString`/`secureObject`. Ils ont tous `status=Succeeded` et gardent les comptes précédents : `orders-prd` 5 `Modify`, 2 `NoChange`, 2 `Unsupported`, 1 `Ignore`; `data-prd` 4 `Modify`, 3 `NoChange`, 1 `Ignore`; `platform-shared` 1 `Modify`, 2 `NoChange`. Les réponses brutes non expurgées sont sous `%LOCALAPPDATA%/InfraFlowSculptor/P08/shop43-deny-preflight-20261025T234253Z/`; elles ne sont pas copiées dans le dépôt.

Classification, avec comparaison des chemins du What-If et des lectures ARM/CLI enregistrées dans `resume-20261026T0814Z` :

- `ca-shop43-api-prd` : la chaîne `identity.type` diffère uniquement par l'espacement, avec les deux mêmes identités. Le trafic `latestRevision=true, weight=100` est configuré sur une app `Single`, où les poids de trafic ne sont pas appliqués. `cooldownPeriod=300` et `pollingInterval=30` sont les valeurs par défaut live. Les différences de références dynamiques restent des références vers les ressources déjà utilisées par l'app; l'app et la release #843 sont saines. Les deux affectations de rôle `Unsupported` restent les affectations ACR Pull et Key Vault Secrets User présentes dans le C5 actuel; le What-If ne prédit ni création ni retrait d'ID.
- `diag-ca-shop43-api-prd` : la liste live `logs` est vide; le changement de destination n'a donc pas de journal à rerouter. Les métriques existantes restent inchangées.
- `cae-shop43-main-prd` : `peerAuthentication.mtls.enabled=false` est le défaut live; `vnetConfiguration.internal=false` correspond au réseau public courant. `customerId` est une référence dynamique du workspace déjà utilisé par les diagnostics de l'environnement, et non une nouvelle valeur littérale.
- `diag-cae-shop43-main-prd` : un essai ciblé de mise à jour vers `Dedicated` a été suivi immédiatement d'une lecture ARM et d'une lecture CLI; les deux renvoient toujours `logAnalyticsDestinationType=null`, avec les mêmes catégories de logs/métriques. Le déploiement exact #843 a également réussi sans rendre cette propriété persistante. La destination reste `AzureDiagnostics`; aucun changement live n'est revendiqué. Preuves : `reconcile-env-update.json`, `orders-diag-env-after.json`, `orders-diag-env-cli-after.json`.
- `id-shop43-api-prd` : `isolationScope=None` est l'état live et le défaut lorsque le modèle omet la propriété.
- SQL `data-prd` : l'IPv6 est absent/désactivé live; la version est le défaut `12.0`. Les champs d'audit concernant une souscription de stockage/clé ne sont pas actifs, car aucun stockage d'audit n'est configuré. La base est en pause automatique (`autoPauseDelay=15`), avec `requestedServiceObjectiveName=currentServiceObjectiveName=GP_S_Gen5_1`, capacité 1 et minimum 0.5; le SKU live est la représentation normalisée `GP_S_Gen5`. Les valeurs explicites `Default`, `AutoPause`, `0` réplicas HA, `useFreeLimit=false` et `minCapacity=0.5` correspondent aux valeurs live/défauts. Les diagnostics SQL conservent les mêmes journaux et les trois catégories de métriques disponibles.
- ACR `platform-shared` : `encryption.status=disabled` et `roleAssignmentMode=LegacyRegistryPermissions` sont les valeurs live par défaut du registre sans CMK; `networkRuleBypassAllowedForTasks=false` est déjà l'état live.

Les deltas sont donc limités à des défauts ou alias normalisés, des références non résolues par What-If, des propriétés sans catégorie active et le champ `Dedicated` que le fournisseur ne persiste pas sur cette ressource. Aucun `Modify` effectif n'est identifié. Cette conclusion autorise uniquement le nettoyage prévu par la procédure T avec les modèles/paramètres exportés exacts, puis contrôle C5/C3/coût avant chaque `deleteAll`.

Préflight frais avant toute suppression, répertoire `%LOCALAPPDATA%/InfraFlowSculptor/P08/pre-delete-refresh-20261026T0831Z/` : C5 comparé au post-#843 strictement identique sur les cinq collections d'IDs (174 ressources, 29 groupes, 7 piles, 63 rôles, 7 budgets, zéro ajout/retrait); C3 shop43 a zéro run `inProgress`, zéro run `notStarted` et zéro PR active. Cost Management Forecast relu à 08:35Z : réel quotidien publié 24/10 = 10,799363 EUR et 25/10 = 6,822276 EUR; le MTD ajusté remplace les deux lignes antérieures par ces valeurs, soit 115,699984 EUR. Prévision 26–31/10 = 36,786961 EUR; projection = 152,486945 EUR. Avec la réserve complète P4/P5/P7 de 22,15 EUR, scénario prudent = 174,636945 EUR, soit 25,363055 EUR sous l'alerte préexistante de 200 EUR (qui n'est pas un plafond).

Le contrôle J+1 shop42 reste inconclusif parce que les données de consommation n'atteignent pas le jour de suppression. Florian a explicitement demandé de ne pas retarder la recette pour ce contrôle; il reste marqué inconclusif et ne sera pas présenté comme réussi. Cette instruction lève l'attente J+1, pas les contrôles de coût et d'inventaire avant action.

**Décision :** le verrou What-If P4 est levé pour les seules piles `orders-prd`, `data-prd` et `platform-shared` après les classifications ci-dessus. Reprendre la procédure T et consigner l'état de chaque opération avant de poursuivre. Les conclusions historiques ci-dessus décrivent l'état au moment du diagnostic initial.

## Vérifications effectuées

- Les trois What-If exacts ont été relancés; leurs statuts, types de changements et chemins des propriétés ont été relus sans imprimer les payloads de valeurs.
- Les propriétés concernées ont été confrontées aux lectures ARM/CLI actuelles; les écritures ciblées `Dedicated` ont été suivies de deux lectures qui confirment la normalisation à `null`.
- C5 a été comparé par ID au post-#843 sur les cinq collections; aucun ajout ni retrait. C3 inProgress/notStarted/PR active est vide.
- Forecast relu; le coût prudent avec réserve P4/P5/P7 reste sous l'alerte existante de 200 EUR.
- Le contrôle J+1 est gardé inconclusif, et son attente n'est pas utilisée comme preuve de réussite.

## Correction après le premier update P4 — 2026-10-26 08:56Z

La conclusion ci-dessus selon laquelle aucun `Modify` effectif n'était identifié est infirmée pour `data-prd`. Après l'update exact de la pile `ifs-shop43-data-prd` (terminé `succeeded`, 7 IDs gérés, `failedResources=[]`, mode `none`, actions `detach`/`fail` préservées), l'état SQL est passé de `Paused` à `Resuming`, puis `Online`. Le service objective demeure `GP_S_Gen5_1`, capacité 1, minimum 0.5, auto-pause 15 minutes. L'update de pile `orders-prd` avait terminé `succeeded`, 9 IDs, aucun échec, mode `none`, sans delta C5.

La cause précise du réveil SQL n'est pas isolée; il est observé pendant le redéploiement du modèle exact et contredit l'hypothèse de non-effet retenue plus haut. L'API Azure SQL `Databases - Pause` a répondu `FeatureDisabledOnSelectedEdition` pour cette base; elle ne peut donc pas être mise en pause manuellement. Son auto-pause reste configurée à 15 minutes. Référence API vérifiée : https://learn.microsoft.com/en-us/rest/api/sql/databases/pause?view=rest-sql-2025-01-01.

À partir de cette correction, aucun autre `az stack sub create` ne sera appliqué à `data-prd`. Le prochain objectif immédiat est de supprimer les piles déjà mises en mode `none` dans l'ordre de T et d'arrêter les frais du SQL au plus vite. Le coût prudent préflighté demeure 174,636945 EUR, 25,363055 EUR sous l'alerte non plafonnante de 200 EUR; ce calcul ne déduit pas le réveil récent, dont la durée effective reste à consigner via le nouvel ActualCost quand publié.


## P5 core PRD, What-If et P4 nettoyée — 2026-10-26 12:15Z

La procédure T est reprise après la fin de P5 et la décision de Florian d'appliquer une réconciliation ciblée des différences What-If. Cette règle vaut propriété par propriété et ressource par ressource ; elle n'autorise aucune tolérance globale.

P5 shop43 : release #860 s'est terminée `PartiallyApplied`, avec `deploy-unit=Done` et `secret-write=Started`. La reprise #861 a réutilisé le même identifiant d'opération secret, s'est terminée `Succeeded` et n'a pas créé de doublon. La source exacte est CI #858, SHA `3b3d0f409c204e4aa08961617428250d58f68b51`. Le secret de la variable originale du groupe #9 n'a jamais été lu ou modifié ; le groupe temporaire #11 et son autorisation de pipeline ont été supprimés. Captures anonymisées dans `p08-p5-missing-secret-failure` et `p08-p5-missing-secret-recovery`.

Le What-If core PRD relancé à 12:15:50Z avec les trois fichiers de l'artefact CI #858 rapporte 4 `Modify`, 2 `NoChange` et 1 `Ignore`. Manifeste source : `core-prd-whatif-source.json`, incluant le deployment stack exact et les empreintes suivantes : `main.bicep` `4A146EB7FE6D8934F4674BB28F8BE13F5297EF7F9676F5A8CE1C8B1E882742FF`, `main.prd.bicepparam` `4658D9A7068F20E831DB207C12BE149C7DFAA72174D393EC041E142E2C2FB248`, `release.prd.json` `66ED21776513D0336F3D8438F0E174352163B5A27AC5D5DA219896F42346C3C5`.

| Ressource | Différences annoncées | État live après le `deploy-unit` réussi de #860 | Décision limitée à cette propriété |
|---|---|---|---|
| `appi-shop43-main-prd` | `kind` Delete ; `properties.Flow_Type` et `properties.Request_Source` Create | `kind=web`, Flow_Type absent, Request_Source absent | Les trois champs précis restent inchangés après l'update exact. |
| Diagnostics App Insights | `properties.logs` et `properties.metrics` Array ; `properties.logAnalyticsDestinationType` Create | `allLogs` et `AllMetrics` activés ; destination `null` | Catégories inchangées ; la destination ne persiste pas sur cette ressource. |
| `kv-shop43-main-prd` | `properties.createMode` Create | `createMode=null`, RBAC, soft delete et protection contre la purge activés | `createMode` reste absent après l'update exact. |
| Diagnostics Key Vault | `properties.logs` et `properties.metrics` Array | Destination `Dedicated`, `allLogs` activé, catégorie `audit` désactivée, `AllMetrics` activé | Les catégories live correspondent au modèle ; aucune différence effective identifiée dans ces propriétés. |
| `log-shop43-main-prd` | `NoChange` (features `NoEffect`) | Le workspace est conservé | Aucun changement. |
| Smart Detector App Insights PRD | `Ignore` | Le même ID que dans la baseline P1 | Aucun changement. |

L'état après run et les What-If sont sous `%LOCALAPPDATA%\InfraFlowSculptor\P08\pre-core-dev-delete-20261026T121122Z\` (`core-prd-whatif-fresh.json`, `core-prd-live-summary.json`, lectures ARM/CLI). Cette preuve permet l'update du stack protégé avec le modèle et les paramètres exacts de CI #858, en conservant les paramètres de protection requis jusqu'à vérification de `denySettings.mode=none`. Elle ne transforme pas l'effet observé sur SQL `data-prd` pendant l'update P4 en règle sans risque ; ce stack a déjà été supprimé.

Préflight juste avant la suppression core-dev, à 12:27:19Z, confirmé par des lectures de propriété à 12:27:45Z : C5 = 159/24/2/51/7 ; C3 = zéro build en cours/en file, zéro release active, zéro PR active. Les 7 IDs gérés par `ifs-shop43-core-dev`, l'ID de pile, l'ID du groupe et les quatre ressources directes sont identiques à la baseline post-P1 ; l'affectation KV gérée figure aussi dans les rôles baseline. Les données sont sous `%LOCALAPPDATA%\InfraFlowSculptor\P08\pre-core-dev-delete-20261026T122702Z\`.

Le dernier relevé de coût réussi demeure celui de 11:50Z : ActualCost 148,565572 EUR HT jusqu'au 25/10, Forecast 27,519768 EUR du 26 au 31/10, projection 176,085340 EUR. Les rafraîchissements ActualCost de 12:12Z et 12:28Z ont reçu HTTP 429. L'estimation P7 au profil minimal est au plus 11,0748 EUR ; la réserve complète de 22,15 EUR donne 198,235340 EUR HT sous l'alerte préexistante non plafonnante de 200 EUR. Réponses et erreurs de rafraîchissement conservées hors dépôt.

## Procédure T — core-dev supprimée — 2026-10-26 12:38Z

Avant `deleteAll`, le C5 de 12:27:19Z était à 159/24/2/51/7 et C3 ne montrait aucun build actif/en file, release active ou PR active. La propriété de la pile core-dev, de ses 7 IDs gérés, de son groupe et de ses 4 ressources directes a été vérifiée contre la baseline post-P1 ; tous les IDs correspondaient.

La suppression `az stack sub delete --name ifs-shop43-core-dev --action-on-unmanage deleteAll --resources-without-delete-support fail` a terminé ; l'ID de pile et le groupe ne sont plus présents. C5 post-suppression à 12:38:03Z : 155/23/1/49/7. Comparaison insensible à la casse des cinq collections : 4 ressources, 1 groupe, 1 pile et 2 attributions de rôle supprimés, aucun ID ajouté, budgets inchangés.

Les deux rôles supprimés sont eux aussi attribués à Luna : `20281781-048b-5145-ac68-11f1abb88482` est le `Role Based Access Control Administrator` au scope du groupe core-dev, inscrit dans le diff d'installation shop43 et absent de la baseline pré-shop43 ; `15f8e5c3-ed7e-599f-87fd-31ed9bec60e5` est le `Key Vault Secrets Officer` inscrit dans les 7 IDs gérés du stack P1, lui aussi absent de la baseline. Les deux sont absents après le `deleteAll`.

Capture complète : `%LOCALAPPDATA%\InfraFlowSculptor\P08\after-core-dev-delete-20261026T123803Z\`. Aucun rôle préexistant n'a disparu. Le dernier coût réussi reste 148,565572 EUR MTD, Forecast 27,519768 EUR ; le refresh à 12:47Z a été refusé par HTTP 429. Estimation restante P7 + réserve : 198,235340 EUR HT.

## What-If frais core PRD — détails des chemins — 2026-10-26 12:44Z

Le préflight avant update du stack `ifs-shop43-core-prd` est frais à 12:44:35Z : C5 `155/23/1/49/7`, C3 sans build en cours/en file, release active ou PR active. La pile est `succeeded`, `denyDelete`, zéro `failedResources`, 7 IDs gérés ; l'ID de pile, le groupe `rg-shop43-core-main-prd`, ses quatre ressources directes et les sept IDs gérés correspondent à P1. `groupIdMatchP1` est vérifié à partir du C5 (champ `groups`), pas de la propriété `resourceGroups` qui n'existe pas dans ce format.

Le What-If subscription-level exécuté à 12:44Z avec `main.bicep`, `main.prd.bicepparam` et `release.prd.json` de CI #858 reproduit 4 `Modify`, 2 `NoChange`, 1 `Ignore`. Il annonce notamment :

- App Insights : `kind` Delete ; `properties.Flow_Type` et `properties.Request_Source` Create. Après #860, `kind=web`, Flow_Type et Request_Source sont `null`.
- Diagnostics App Insights : destination `Dedicated` Create ; les tableaux logs/metrics omettent `retentionPolicy.days=0`. Après #860, destination `null`, `allLogs` et `AllMetrics` activés, chacun avec `retentionPolicy.days=0` et `enabled=false`.
- Key Vault : `properties.createMode=default` Create. Après #860, createMode reste absent ; purge protection, RBAC et soft delete sont vrais.
- Diagnostics Key Vault : logs/metrics sont proposés en modification parce que `retentionPolicy.days=0` serait omis ; le modèle omet également l'entrée `logs[1]` `categoryGroup=audit, enabled=false`. Après #860, les journaux `allLogs` et `audit` désactivé sont tous deux présents, les métriques `AllMetrics` sont activées, les rétentions restent à `days=0, enabled=false` et la destination reste `Dedicated`.
- Workspace : `NoChange` avec `features` `NoEffect`; Smart Detector : `Ignore`.

Ces valeurs exactes sont les seules classées comme différences What-If sans effet persistant, démontrées par le déploiement P5 exact #860 et les lectures ARM/CLI après ce run. L'artefact et les empreintes sont dans `core-prd-whatif-summary.json`; la sortie What-If brute est `core-prd-whatif.json` sous `%LOCALAPPDATA%\InfraFlowSculptor\P08\pre-core-prd-update-20261026T124417Z\`. Le refresh Cost Management ActualCost correctement formé à 12:47Z a renvoyé HTTP 429 ; le dernier succès 11:50Z et l'estimation complète restent ceux consignés dans NEXT.md.

## Correction du paramètre runtime du stack core PRD — 2026-10-26 12:53Z

La première mise à jour exacte du stack a bien retiré `denyDelete` et conservé `detach`/`fail`, mais le retour Azure a montré qu'elle avait détaché l'attribution Key Vault `1ff37b2e-e96a-5611-a20b-975f2f0e5329` : 6 IDs gérés et 1 ID détaché. La lecture du modèle a identifié le paramètre manquant : `main.bicep` déclare `deploymentPrincipalId` et conditionne le module `deploymentSecretsOfficer` à sa présence ; `IfsRelease.psm1` l'ajoute explicitement aux paramètres de déploiement depuis l'identité de déploiement. La valeur inscrite dans le stack avant update est `1dbe8a76-de6d-4463-bdda-a611e5db5125`.

Le C5 post-update de 12:52:33Z est strictement identique au pré-update sur les cinq collections : 155 ressources, 23 groupes, 1 pile, 49 rôles, 7 budgets, zéro ajout/retrait ; C3 reste vide. L'attribution détachée existe toujours dans Azure, n'était pas dans la baseline pré-shop43 et figure dans P1. Aucun changement de ressource ou de rôle live n'a persisté.

Le What-If est refait avec l'artefact CI #858 et `deploymentPrincipalId` complet : 4 `Modify`, 3 `NoChange`, 1 `Ignore`. L'attribution `Key Vault Secrets Officer` exacte est `NoChange`, et redevient incluse dans le modèle. Sortie sous `%LOCALAPPDATA%\InfraFlowSculptor\P08\pre-core-prd-update-20261026T124417Z\core-prd-whatif-full-params.txt`. La prochaine mise à jour réapplique ces mêmes fichiers avec le paramètre runtime; avant `deleteAll`, les 7 IDs gérés, zéro `failedResources`, l'absence de détachement et l'état C5/C3 seront revérifiés.
