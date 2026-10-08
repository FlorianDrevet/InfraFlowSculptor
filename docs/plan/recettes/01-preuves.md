# Recette des preuves (verrou R-03)

> Rédigée à P-07 à partir de la sortie de référence et des scripts locaux. Ordre d'exécution : P1, P9, P3, P8,
> P2, P4, P5, P7. Chaque preuve se termine par une capture du run Azure DevOps et une ligne dans
> [resultats.md](../preuves/resultats.md).
>
> **État au 2026-10-08 :** la souscription fournie par l'utilisateur est active et North Europe est la seule région retenue.
> Aucun projet Azure DevOps ni groupe Entra n'a encore été créé. Le budget mensuel préexistant de 200 EUR à l'échelle
> de la souscription et sa dépense réelle affichée ont été vérifiés. Aucun budget n'a été créé ou modifié par Luna.
> Le préflight SQL retourne une restriction de provisionnement pour `GP_S_Gen5_1` en North Europe ; P-08 est bloqué
> à ce contrôle. Aucune ressource n'a été déployée.
>
> **Stratégie de coût ([DT-42](../../technique/01-decisions.md#dt-42--coût-des-preuves-éphémères-azure)) :** une seule
> souscription, `northeurope` uniquement, profil minimal ([reference-pilote § 1.6](../reference-pilote.md#16-profil-de-coût-des-preuves)),
> un jeu de ressources par projet (`shop42`, `shop43`, `shop44`) déployé **après la suppression du précédent**, et suppression
> de chaque jeu dès sa dernière preuve dépendante ([§ 10](#10-supprimer-les-ressources-azure-et-consigner-les-résultats)).
> Les assertions de P1, P9, P3, P8, P2, P4, P5 et P7 sont inchangées.

## 0. Verrou de coût et préflight bloquants

### 0.1 Verrou de dépense (aucune commande Azure avant)

Ne lancez **aucune** commande Azure (ni création, ni déploiement, ni kit, ni préflight qui crée quoi que ce soit) tant que vous
n'avez pas : (1) fixé un **plafond de dépense** en euros pour toute la recette ; (2) relu l'estimation du § 0.2
**actualisée** (relire les tarifs publics du jour, `northeurope`, devise de votre contrat) ; (3) répondu « confirmé » à
Claude et consigné dans [NEXT.md](../../../NEXT.md) (ligne « Plafond de dépense et estimation ») le plafond, la date et
l'estimation. Sans cela : statut `BLOQUE`. Un **budget Azure est une alerte différée** (données de coût en retard de
plusieurs heures), **pas un coupe-circuit**. Le contrôle du 2026-10-08 a trouvé un budget mensuel préexistant de 200 EUR
à l'échelle de la souscription, avec alerte à 80 % du coût réel et à 100 % prévisionnel. Ne pas créer de doublon ni
modifier ce budget sans nouvelle demande. **Contrôle de coût et d'inventaire** : au démarrage et à la fin de
chaque session, avant et après chaque déploiement ou run Azure significatif, puis après chaque nettoyage. Consigner l'heure
UTC, le coût affiché et les IDs vérifiés dans [l'inventaire](../preuves/inventaires-azure.md). Vérifier dans
*Cost Management → Cost analysis* le coût réel et la prévision disponible ; les données pouvant être retardées, ce contrôle
n'est pas une surveillance continue ni une garantie de plafond. Pour l'inventaire, lister les ressources, piles, groupes et
attributions de rôle de la souscription sans filtrer par nom ; comparer les IDs exacts au registre, en comparaison
insensible à la casse. Une pause de plus de 24 h ne déclenche pas, à elle seule, la suppression du jeu : contrôler le réel
et la prévision avant de reprendre, intégrer SQL, Log Analytics, stockage et ACR à l'estimation, et continuer uniquement si
le plafond le permet. Si le seuil est atteint, suivre la procédure d'urgence de [l'inventaire Azure](../preuves/inventaires-azure.md).

Si le coût réel ou l'estimation disponible atteint le seuil confirmé dans `NEXT.md`, arrêter les nouveaux runs et lancer
le nettoyage d'urgence décrit dans [l'inventaire Azure](../preuves/inventaires-azure.md) : supprimer uniquement les IDs
créés par Luna, même si toutes les captures ne sont pas encore terminées. Un nom ou un préfixe seul ne prouve pas la
propriété ; ne jamais exécuter `deleteAll` si une pile ou un groupe contient un élément absent de l'inventaire.

### 0.2 Estimation (ordre de grandeur, tarifs Retail API `northeurope` vérifiés le 2026-10-08)

Les tarifs unitaires proviennent de l'[API officielle Azure Retail Prices](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices). Réponse EUR de référence : SQL General Purpose serverless Gen5 à 0,4683 €/vCore-heure, ACR Basic à 0,1466 €/jour, et ingestion Log Analytics à 2,4287 €/Go au-delà des 5 premiers Go gratuits par mois et par compte de facturation. L'estimation ne suppose pas que ce crédit Log Analytics reste disponible : l'usage préexistant du compte est inconnu. Les tarifs non USD de cette API restent des prix de référence ; vérifier la devise de facturation réelle avant de créer le budget.
Le calcul ci-dessous ne couvre pas le stockage, les sauvegardes, le trafic sortant, les taxes ni les services annexes
(Key Vault, stockage technique, registre de journaux d'activité) ; il ne constitue donc pas le total de la recette.

| Poste | Hypothèse | Profil minimal | Plan précédent (hypothèse de comparaison : les trois jeux sont conservés 10 jours chacun jusqu'à R-03 ; SQL prd `GP_Gen5_2` zone redondante, ACR `Standard`) |
|---|---|---|---|
| SQL, 2 bases × 3 projets | ≤ 4 h de calcul actif par base et par projet (installation, schéma, data-access de chaque release d'`orders`, contrôles, + 15 min de pause différée) | si une base `prd` gratuite par souscription est confirmée : `prd` ≈ 0 €, `dev` (12 base-heures) ≈ **2,8–5,6 €** ; sans offre : 24 base-heures ≈ **5,6–11,2 €** selon 0,5–1 vCore | prd : (0,2733 + 0,164) €/h × 24 h × 10 jours × 3 projets ≈ **315 €**, + dev serverless |
| ACR | 1 par projet, ≤ 2 jours facturés | Basic 0,1466 €/jour ≈ **0,9 €** | Standard 0,5866 €/jour × 10 j × 3 ≈ **18 €** |
| Container Apps | min 0 / max 1, quelques minutes actives | **≈ 0 € si le crédit gratuit mensuel est encore disponible** ; zéro réplica = zéro consommation de ressources | min 1 en prd : facturé en continu |
| Log Analytics | Hypothèse : 1 Go/jour/espace, 2 espaces par projet, ≤ 1 jour par projet = 6 Go estimés ; le daily cap peut dépasser sa valeur | ≈ 2,4287 €/Go = **14,6 €** si le crédit mensuel du compte est déjà consommé ; les 5 premiers Go peuvent être gratuits sinon | idem, sans plafond |
| **Postes chiffrés** | Hypothèses ci-dessus ; hors services annexes | **≈ 18,3 à 26,8 €** selon l'offre SQL gratuite | **≈ 340 € et plus** |

Le montant de 18,3–26,8 € est une estimation des seuls postes chiffrés, avec les hypothèses de durée indiquées ; les
ressources annexes, le stockage, les sauvegardes, le trafic sortant, les taxes et le dépassement éventuel du plafond Log Analytics s'ajoutent. Le plafond de 1 Go/jour/espace est un garde-fou et peut dépasser sa valeur, il ne garantit pas une facture maximale. Le budget mensuel préexistant de **200 EUR au niveau de la souscription** a été vérifié le 2026-10-08 : alertes configurées à 80 % du coût réel et 100 % prévisionnel ; dépense réelle affichée ≈ 0,4303 EUR. Aucun budget supplémentaire n'a été créé, et aucune alerte n'arrête automatiquement les ressources. Le préflight Azure a bloqué sur SQL serverless `GP_S_Gen5_1`, dont le provisionnement est restreint en North Europe ; ne pas changer de région ou de SKU avant décision. Si l'offre SQL gratuite n'est pas acceptée par l'API, conserver le SQL payant et refaire l'estimation ; si ACR Basic est insuffisant, arrêter et demander une décision avant toute hausse de SKU. Si les durées dépassent les hypothèses, actualiser l'estimation avant de continuer.

Sources techniques : [SQL serverless](https://learn.microsoft.com/en-us/azure/azure-sql/database/serverless-tier-overview?tabs=general-purpose&view=azuresql),
[Container Apps facturation](https://learn.microsoft.com/en-us/azure/container-apps/billing),
[ACR SKU](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-skus),
[SQL zone redundancy](https://learn.microsoft.com/en-us/azure/reliability/reliability-sql-database),
[plafond Log Analytics](https://learn.microsoft.com/en-us/azure/azure-monitor/logs/daily-cap), [tarification Azure Monitor](https://azure.microsoft.com/en-us/pricing/details/monitor/) et
[budgets Azure](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets).

### 0.3 Préflight bloquant avant toute création

Chaque ligne doit être cochée et consignée (capture ou sortie sans secret). Le **premier échec arrête la recette** :
aucun repli West Europe, aucun changement de SKU, aucune création « pour voir ».

| # | Contrôle | Commande ou lieu | Arrêt si |
|---|---|---|---|
| 1 | Région `northeurope` disponible pour la souscription | `az account list-locations --query "[?name=='northeurope']" -o table` | vide |
| 2 | Fournisseurs enregistrés avec `North Europe` | `az provider show -n Microsoft.App --query "resourceTypes[?resourceType=='managedEnvironments'].locations"` ; idem `Microsoft.Sql`/`servers`, `Microsoft.ContainerRegistry`/`registries`, `Microsoft.OperationalInsights`/`workspaces`, `Microsoft.Insights`/`components`, `Microsoft.KeyVault`/`vaults`, `Microsoft.Storage`/`storageAccounts`, `Microsoft.ManagedIdentity`/`userAssignedIdentities` ; enregistrer les fournisseurs manquants | une région absente |
| 3 | SQL serverless `GP_S_Gen5_1` disponible | `az sql db list-editions --location northeurope --edition GeneralPurpose --service-objective GP_S_Gen5_1 --available -o table` | non disponible |
| 4 | Zone redondante désactivée dans le clone | `Select-String -Path <clone>/data/infra/main.*.bicepparam -Pattern 'zoneRedundant'` et `Select-String -Path <clone>/orders/infra/main.*.bicepparam -Pattern 'zoneRedundant'` : chaque valeur doit être explicitement `false` (SQL dans `data`, environnement Container Apps dans `orders`) | une valeur absente ou `true` |
| 5 | Offre SQL gratuite | (a) `bicep build` du clone profilé accepte `useFreeLimit`/`freeLimitExhaustionBehavior` avec `sql/server:0.22.0` ; (b) après franchissement du verrou de dépense, exécuter un What-If ARM avec le profil par défaut **sans** `-SqlFreeOffer` pour vérifier que l'API accepte aussi `freeLimitExhaustionBehavior` quand `useFreeLimit` est faux ; si l'offre gratuite est envisagée, exécuter un second What-If avec `-SqlFreeOffer` ; (c) le portail (*Create SQL database*, souscription et région cibles) propose l'offre gratuite ; (d) `az resource list --subscription <id> --resource-type Microsoft.Sql/servers/databases --query "[].{id:id,useFreeLimit:properties.useFreeLimit,behavior:properties.freeLimitExhaustionBehavior}" -o table` ne montre aucune base existante avec `useFreeLimit = true` ; (e) le **décalage** « une base » (schéma AVM) / « dix bases » (documentation) est noté : ne pas supposer plus d'**une** base gratuite, la base `prd` la reçoit | (a), (b) ou (c) échoue : omettre `-SqlFreeOffer`, refaire l'estimation et reconfirmer le plafond au § 0.1 avant tout déploiement ; (d) échoue : même arrêt et estimation sans offre |
| 6 | ACR Basic suffisant | taille de l'image témoin (`docker image ls`) ≪ 10 GiB ; un seul build/push par CI de P1, P8 ; limites Basic publiées (débit de lecture/écriture) relues dans la documentation du jour | limite dépassée : arrêt ; toute hausse de SKU exige une nouvelle décision et une estimation confirmée |
| 7 | Plafond d'ingestion sans effet sur les assertions | le contrôle `logs` n'exécute que `print 1` ; vérifier au premier run P1 que `/health/dependencies` réussit | `logs` en erreur à cause du plafond |
| 8 | Commande de passage `denyDelete` → `none` | `az stack sub create --help` (CLI 2.90.0) : le CLI met à jour une pile avec `create` ; il n'expose pas de commande `update` dédiée. Pour le nettoyage normal, répéter le dernier modèle, commit et paramètres déployés, puis comparer le What-If avant mise à jour. En urgence, ne pas mettre à jour la pile : utiliser seulement un opérateur déjà exclu du deny, sinon s'arrêter et signaler le verrou | le modèle exact ou les paramètres manquent, l'opérateur n'est pas exclu, ou le What-If annonce une modification de ressource |
| 9 | Suppression `deleteAll` | `az stack sub delete --help` : option acceptée ; Microsoft précise que `deleteAll` supprime aussi les groupes de ressources gérés et tous leurs contenus. Relever les IDs des piles, groupes et ressources, puis vérifier que chacun est créé par Luna ; les éléments non gérés ou non consignés imposent une suppression individuelle par ID et le maintien du groupe | un seul ID du groupe ou de la pile manque à l'inventaire ou n'est pas créé par Luna |
| 10 | Une seule souscription | les trois IDs `-SubscriptionDev/Prd/Shared` sont identiques ; vous êtes Owner (ou User Access Administrator) | autre cas |
| 11 | Noms globaux et inventaire de référence | `shop42` libre pour `kv-`, `sql-`, `cr…` (`VAL-NOM-DISPONIBILITE`) ; capturer avant création les IDs préexistants dans toutes les portées, sans filtre sensible à la casse ; un coffre `prd` supprimé reste **réservé** (protection contre la purge, 90 j) | nom pris ou portées ambiguës : autre code |
| 12 | Crédit mensuel Container Apps Consumption disponible | Cost Management → Cost analysis, vérifier l'usage de Container Apps de la souscription ; si le crédit gratuit est déjà consommé, recalculer l'estimation avec les secondes actives attendues | usage inconnu ou crédit épuisé |

Si l'offre gratuite SQL atteint sa limite mensuelle, `AutoPause` rend la base inaccessible jusqu'au mois suivant : arrêter
la recette et ne jamais basculer vers `BillOverUsage` sans nouvelle estimation et confirmation. Le schéma AVM du dépôt ne
permettant qu'une base `useFreeLimit` par souscription, appliquer l'offre uniquement à `prd`; après suppression de cette
base, attendre jusqu'à une heure avant d'essayer de libérer un nouvel emplacement pour le jeu suivant ([FAQ Microsoft SQL gratuite](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer-faq?view=azuresql)).

**Résultat du préflight le 2026-10-08 :** la souscription et North Europe sont accessibles, mais `az sql db list-editions`
retourne pour `GP_S_Gen5_1` une restriction de provisionnement dans cette région. Conformément au contrôle 3, la recette
s'arrête ici : aucun changement de région/SKU et aucun déploiement tant qu'une décision n'a pas levé ce blocage.

### 0.4 Réveil SQL et démarrage à froid

Contrôles manuels, à appliquer chaque fois qu'une étape touche SQL ou l'API ; ils **ne remplacent aucune assertion**.

- **Réveil SQL après pause** : attendre au moins 15 min sans requête, puis vérifier dans le portail Azure que la base a
  bien l'état `Paused` avant de lancer l'essai ; une simple période d'inactivité ne prouve pas qu'Azure l'a mise en pause.
  Si elle est encore en ligne, vérifier qu'aucune session ne la maintient active et ne compter l'essai qu'une fois l'état
  `Paused` observé. Un premier échec de connexion transitoire est
  attendu. Procédure bornée : relancer la **même** requête ou le même `/health/dependencies` jusqu'à **5 essais**,
  espacés de **60 s**, soit **5 min au plus** ; la release d'`orders` réessaie déjà 10 min pour `data-access`
  ([P-03](../01-preuves.md#p-03--module-de-release-powershell)). Capturer le nombre d'essais. Au-delà : échec de la
  preuve, **pas** de relance en boucle ; consigner KO et ne rien corriger sans Luna. Le contrôle `sql` doit réussir au
  final **avec les mêmes sous-contrôles** (écriture/lecture, `CREATE TABLE` refusé) ; une réussite partielle n'est pas OK.
- **Démarrage à froid de `ca api`** : après inactivité, vérifier dans la vue des révisions/réplicas que le nombre de
  réplicas actifs est zéro. S'il en reste un, attendre et vérifier de nouveau ; ne pas compter un redémarrage à chaud.
  Premier `Invoke-RestMethod 'https://<fqdn>/health' -TimeoutSec 20` : jusqu'à
  **6 essais**, espacés de **20 s** (≈ 2 min), puis `/health/dependencies` jusqu'à **3 essais** espacés de **30 s**. Le
  contrôle de santé de la release applicative reste à 2xx sous 5 min (P-03). Au-delà : KO consigné.
- Après chaque série de captures, **aucun** démarrage forcé à froid ou à chaud supplémentaire : on n'ajoute pas d'activité
  facturée pour « confirmer ».

## 1. Préparer l'environnement

### Prérequis à réunir

| Élément | Valeur et droits requis |
|---|---|
| Abonnements Azure | **Une seule souscription** de test (le même ID pour dev, prd, shared) ; région `northeurope` uniquement. Droits Owner ou User Access Administrator pour l'installation des rôles. Pour P2, confirmer que le refus denyDelete est installé avant l'essai de suppression. |
| Tenant et groupes SQL | Tenant contenant les abonnements ; groupe sg-shopNN-sql-admins créé et ses IDs d'objet pour dev et prd. Un même ID peut être utilisé si le groupe est commun aux deux cibles. |
| Azure DevOps | Organisation, projet, dépôt Azure Repos et droits d'administrateur du projet. Créer le groupe de sécurité ShopNN Release Approvers avec les membres autorisés. |
| Poste | Azure CLI connecté au bon tenant, extension azure-devops, PowerShell 7, Git, accès aux abonnements et au projet. `Save-AdoRecordings.ps1` requiert PowerShell 7.5+ pour conserver les timestamps JSON à l'identique. Installer sqlcmd seulement si vous préférez l'utiliser plutôt que l'éditeur de requête du portail. |
| Code projet | Choisir un identifiant libre, par exemple shop42, puis le garder constant pour ce jeu de ressources. Les noms de ressources et le projet Azure DevOps doivent suivre le code publié. |
| Budget | Verrou du [§ 0.1](#01-verrou-de-dépense-aucune-commande-azure-avant) franchi (plafond et estimation confirmés dans NEXT.md) ; préflight [§ 0.3](#03-préflight-bloquant-avant-toute-création) entièrement coché. |

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

N'ajouter `-SqlFreeOffer` à la commande réelle qu'après réussite de toutes les vérifications du préflight 5 et recalcul
de l'estimation. Sinon, laisser l'option absente : les bases restent en offre payante. Si l'offre échoue au What-If ou au
portail, republier le profil sans `-SqlFreeOffer`, refaire l'estimation et reconfirmer le plafond avant tout déploiement.

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

Pour les étapes 3 et 6, appliquer les contrôles de [§ 0.4](#04-réveil-sql-et-démarrage-à-froid) (réveil SQL, démarrage à
froid) : mêmes assertions, essais bornés.

**Nettoyage :** aucune suppression avant la fin de P8 : `shop42` sert à P9, P3 et P8. Garder les captures Azure DevOps.

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

**Nettoyage :** aucun rollback d'image ; l'état en prd reste D2. Garder la modification témoin (git) jusqu'à R-03.
**Dernière preuve de `shop42`** : une fois P8 capturée en entier (`p8-infra-pending`, `p8-app-prd`, `p8-infra-result`) et
P1, P9, P3a, P3b, P8 consignées dans resultats.md, supprimer le jeu Azure `shop42` ([§ 10](#10-supprimer-les-ressources-azure-et-consigner-les-résultats),
procédure T). Ne démarrer `shop43` qu'ensuite.

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
soit déjà vérifié. La souscription est la même ; les noms `shop43` sont isolés. Ce second jeu n'est créé **qu'après la
suppression de `shop42`** et après les préflights du [§ 0.3](#03-préflight-bloquant-avant-toute-création) (inchangés, à
revérifier). Appliquer les contrôles de [§ 0.4](#04-réveil-sql-et-démarrage-à-froid) aux étapes 3 (`/health/dependencies`). Garder ce clone pour P4 et P5.

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

**Nettoyage :** ne pas supprimer la pile ni l'espace avant la fin de P5 ; ne pas tenter la suppression de `log-shop43-main-prd`
après l'essai refusé. Garder le code P2 et son clone pour P4/P5.

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
3. Appliquer rev5, committer et fusionner. L'overlay augmente `maxReplicas` de 1 à 2, après le profil de coût publié.
   Déployer en dev puis prd. Relire ifs-report-prd : prd doit passer
   directement de la révision 3 à 5, détacher id-shop43-extra-prd de la pile sans supprimer l'identité Azure et
   l'inscrire dans detachedResources. Capturer les deux runs.

**Résultat attendu :** dev applique chaque révision ; prd ne reçoit pas rev4 ; rev5 détache l'identité d'Azure sans
la supprimer.

**Preuves :** commits/PR, trois aperçus, approbation/refus prd de rev4, rapports rev3/rev5 et présence de l'identité
après détachement.

**Nettoyage (fin de P4) :** après avoir capturé la présence de `id-shop43-extra-prd` hors pile et son entrée dans
`detachedResources`, et si P4 est consignée OK, exécuter la procédure T du [§ 10](#10-supprimer-les-ressources-azure-et-consigner-les-résultats)
pour `orders`, `data`, `platform` **et** l'identité détachée de `shop43`. **Garder `core` (dev et prd) et le kit** : P5 relance la
release core, dont le stage dev.

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

**Nettoyage :** la valeur secrète est restaurée ; confirmer la release core réussie. **Dernière preuve de `shop43`** : captures
`p5-*` enregistrées et P2, P4, P5 consignées → supprimer `core` (dev, prd) puis le kit de `shop43` ([§ 10](#10-supprimer-les-ressources-azure-et-consigner-les-résultats)).
Ne démarrer `shop44` qu'ensuite.

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
Avant toute suppression, copier **hors Azure** le blob `ifs-shop44-orders-prd.json` (avant/après) et les captures du journal,
car le compte `stifsshop44prd` est supprimé avec le kit. **Dernière preuve de `shop44`** : P7 consignée → supprimer le jeu
entier ([§ 10](#10-supprimer-les-ressources-azure-et-consigner-les-résultats)). Aucune ressource Azure ne subsiste ensuite.

## 10. Supprimer les ressources Azure et consigner les résultats

Après chaque preuve, ajouter une ligne dans resultats.md (voir ci-dessous). Les ressources **Azure** sont ensuite supprimées
par jeu, dès la dernière preuve qui en dépend ([DT-42](../../technique/01-decisions.md#dt-42--coût-des-preuves-éphémères-azure)) :
`shop42` après P8 ; `shop43` : `orders`, `data`, `platform` et l'identité détachée après P4, puis `core` et le kit après P5 ;
`shop44` après P7. Les preuves Azure DevOps (runs, captures `Recordings/`, `resultats.md`) restent **jusqu'à R-03**.

### Procédure T — suppression d'un jeu (ou d'une partie)

**Porte (tout doit être vrai, sinon ne rien supprimer)** : (1) une capture `Save-AdoRecordings.ps1` et une ligne dans
resultats.md existent pour chaque scénario qui dépend des piles ou ressources précises à supprimer ; les scénarios encore
à exécuter mais indépendants de ces ressources ne bloquent pas leur nettoyage ; (2) un **KO ou une preuve incomplète**
suspend le nettoyage normal et vous décidez, avec le coût de maintien du § 0.2, de rejouer ou non ; en cas de seuil de coût
atteint, appliquer la procédure d'urgence de l'inventaire ;
(3) l'inventaire de référence avant création et les IDs exacts créés par Luna sont conservés hors Azure dans
[inventaires-azure.md](../preuves/inventaires-azure.md) ; vous avez aussi noté les ressources détachées lues dans `ifs-report-prd`,
et les Object IDs des identités `id-ifs-deploy-shopNN-*` et `id-ifs-app-shopNN-*` (rapport du kit) ;
(4) les préflights 8 et 9 du [§ 0.3](#03-préflight-bloquant-avant-toute-création) sont cochés.

Noms des piles (une par composant et cible, `unit` des `release.<cible>.json`) : `ifs-shopNN-core-dev|prd`,
`ifs-shopNN-data-dev|prd`, `ifs-shopNN-orders-dev|prd`, `ifs-shopNN-platform-shared`. Vérifier avant :

    az stack sub list --subscription <id> -o table

1. **Identité détachée (P4 seulement)** : supprimer explicitement `id-shop43-extra-prd` seulement si son ID relevé dans
   `detachedResources` est aussi inscrit comme créé par Luna dans l'inventaire : `az identity delete --ids <id>`.
2. **Retirer `denyDelete`** des piles protégées (`prd`, `shared`) du périmètre, une par une, avant leur suppression.
   Microsoft documente `az stack sub create` comme mécanisme de mise à jour ; il réévalue donc le modèle. Pour le nettoyage
   normal, utiliser le commit, le modèle et les paramètres exacts de la dernière exécution, jamais le working tree courant.
   Avant l'update, exécuter le What-If avec ce contenu et arrêter si une ressource autre que la protection de pile semble
   changer. Contrôler ensuite `denySettings.mode = none`. En urgence de coût, ne pas effectuer cet update : suivre le point 4
   de [la procédure d'urgence](../preuves/inventaires-azure.md) et utiliser seulement un principal déjà exclu du deny.
3. **Supprimer les piles dans l'ordre inverse des dépendances**, une à la fois, en attendant la fin de chacune :
   `orders` (dev, prd) → `data` (dev, prd) → `platform` (shared) → `core` (dev, prd) :

   Pour une pile au scope abonnement, Microsoft précise que `deleteAll` supprime les ressources, les groupes de ressources
   gérés et tout leur contenu. Avant chaque suppression, lister tous les IDs présents dans chaque groupe concerné, puis les
   comparer à l'inventaire : chaque ressource et groupe doit être marqué créé par Luna et absent du relevé de référence. Si
   un seul ID manque, est préexistant ou n'est pas clairement attribué, ne pas lancer `deleteAll` ; supprimer uniquement
   les ressources individuelles explicitement inventoriées et laisser le groupe en place
   ([documentation Microsoft](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deployment-stacks?tabs=azure-cli)).

        az stack sub delete --name ifs-shopNN-<composant>-<cible> --action-on-unmanage deleteAll --subscription <id> --yes

   `deleteAll` n'est utilisé qu'après la porte. Pour la fin de P4, s'arrêter après `platform` (`core` reste pour P5).
4. **Vérifier** : relister tous les IDs de ressources et de groupes de la souscription, puis comparer au registre. Si un
   groupe persiste, ne le supprimer que si son ID exact est enregistré comme créé par Luna et que chaque ressource qu'il
   contient est également enregistrée comme créée par Luna ; sinon le conserver.
5. **Kit (fin de jeu seulement)** : avant `azure-setup.ps1`, comparer les attributions de rôle au relevé de référence ; après
   l'installation, enregistrer dans l'inventaire les IDs exacts des seules attributions créées par Luna. Supprimer uniquement
   ces IDs, après relecture de `az role assignment list --all --subscription <id>` ; ne jamais retirer une attribution
   simplement parce qu'elle vise une identité du kit. Pour chaque groupe `rg-ifs-shopNN-dev`, `rg-ifs-shopNN-prd`,
   `rg-ifs-shopNN-shared`, vérifier d'abord son ID exact et tous les IDs de ressources qu'il contient. Supprimer le groupe
   seulement si le groupe et chacune de ses ressources sont enregistrés comme créés par Luna ; sinon supprimer seulement
   les IDs individuels autorisés et laisser le groupe. Les attributions préexistantes restent intactes, même si elles ciblent
   un principal du kit.
6. **Reste hors Azure** : connexions de service, groupes de variables et définitions Azure DevOps restent jusqu'à R-03 (coût nul) ;
   l'inscription Entra n'est pas touchée. Noms **réservés** : coffre `prd` (protection contre la purge) et espaces Log Analytics
   supprimés ne sont plus réutilisables sous leur nom : un nouveau jeu prend un nouveau code.
7. **Contrôle final** : lister toutes les ressources, groupes, piles et attributions de rôle à la souscription, sans filtre
   de nom ; comparer chaque ID exact à l'inventaire en comparaison insensible à la casse. Consigner la date dans NEXT.md
   (« ressources supprimées ») et, au prochain jour, vérifier dans *Cost analysis* qu'aucune ligne ne court encore.

### Consigner les résultats

Après chaque preuve, ajouter une ligne dans resultats.md : P1, P9, P3a, P3b, P8, P2, P4, P5 ou P7, date, OK/KO,
liens/captures anonymisés, remarque. Pour chaque KO, conserver le message et les artefacts ; ne pas corriger une
question de conception sans la revue Claude.

Ne pas arrêter les conteneurs Aspire locaux. Ne supprimer aucune ressource Azure **avant** la dernière preuve qui en dépend
et la porte de la procédure T ; après, la suppression n'attend pas R-03. Le jalon 0 redéploie depuis des abonnements vides.
À R-03 ne subsistent que les preuves Azure DevOps et resultats.md.

Temps indicatifs hors files d'attente et délais de propagation. La recette n'a pas de durée maximale : le coût dépend de
la durée de vie des ressources, bornée par le plafond du [§ 0.1](#01-verrou-de-dépense-aucune-commande-azure-avant).
