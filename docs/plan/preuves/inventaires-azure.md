# Inventaire Azure des preuves

Ce registre sépare les ressources préexistantes de celles créées par Luna. Un nom, un préfixe ou un tag ne suffit jamais
à autoriser une suppression. Les suppressions ciblent uniquement des IDs exacts consignés comme créés par Luna et absents
de l'inventaire de référence.

## État au 2026-10-08

- Souscription de preuve : ID fourni par l'utilisateur lors du préflight, non recopié dans le dépôt.
- Région cible de ce préflight initial : `northeurope`.
- Ressources créées par Luna : **aucune**.
- Inventaire de référence capturé après confirmation du plafond : 124 ressources, 16 groupes, aucune pile de souscription et 4 attributions de rôle.
- Les IDs exacts sont conservés hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\baseline-20261008.json` pour éviter de publier l'identifiant de souscription. Reprendre une capture avant la première écriture Azure si l'état a changé.
- Budget préexistant à l'échelle de la souscription : 200 EUR/mois ; dépense réelle affichée ≈ 0,4303 EUR le 2026-10-08. Alertes existantes : réel à 80 %, prévision à 100 %. Aucun budget créé/modifié par Luna.
- Préflight bloqué : le provisionnement SQL `GP_S_Gen5_1` est restreint en North Europe. Aucune ressource n'a été déployée.

## Décision de région au 2026-10-15

- Région cible des preuves : `francecentral`, selon la décision utilisateur consignée dans [DT-42](../../technique/01-decisions.md#dt-42--coût-des-preuves-éphémères-azure).
- Préflight en lecture seule du 2026-10-15 : `francecentral` est listée ; les fournisseurs `Microsoft.App`, `Microsoft.Sql`, `Microsoft.ContainerRegistry`, `Microsoft.OperationalInsights`, `Microsoft.Insights`, `Microsoft.KeyVault`, `Microsoft.Storage` et `Microsoft.ManagedIdentity` sont enregistrés et annoncent France Central pour le type de ressource vérifié ; `GP_S_Gen5_1` retourne `Available=True`. Contrôles faits dans la souscription CLI par défaut, dont l'ID correspond au snapshot local de référence.
- `ifs-witness:local` mesure 446 Mo (`docker image ls`), sous les 10 Go inclus dans ACR Basic.
- Le budget préexistant `budget-sandbox-monthly` reste à 200 EUR/mois avec les alertes réelles à 80 % et prévisionnelles à 100 % actives. Au contrôle du 2026-10-15, le coût réel retourné était 0,430301 EUR et aucune prévision n'était retournée. La liste montre aussi six budgets de groupes de ressources à 200 EUR/mois chacun ; aucun budget n'a été créé ou modifié.
- Tarifs Retail API France Central relevés le 2026-10-15 : SQL serverless 0,5739 EUR/vCore-h ; ACR Basic 0,1466 EUR/jour ; ingestion Log Analytics 2,4287 EUR/Go au-delà des 5 Go gratuits ; rétention Log Analytics 0,1056 EUR/Go-mois ; stockage de données SQL General Purpose 0,1265 EUR/Go-mois. Le sous-total chiffré est 6,75–30,17 EUR ; voir la [recette § 0.2 bis](../recettes/01-preuves.md#02-bis-estimation-france-central-retail-api-relevée-le-2026-10-15). Les coûts annexes, l'usage des crédits et le coût actuel de la souscription restent inconnus ; l'estimation et le plafond de 200 EUR attendent confirmation.
- Aucun déploiement, groupe, rôle ou autre changement Azure n'a eu lieu pendant ces contrôles.
- À cette date, les 124 ressources, 16 groupes et 4 attributions du snapshot du 2026-10-08 restaient l'inventaire de référence ; aucune ressource n'avait été créée par Luna.
- Inventaire exact avant toute écriture Resource Manager capturé le `2026-10-15T12:29:09Z` : 133 ressources, 16 groupes, 0 pile de souscription, 28 attributions de rôle et 7 budgets. Les IDs sont conservés hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\baseline-20261015T1229Z.json` ; le snapshot du 2026-10-08 reste inchangé.
- Les noms globaux `kv-shop42-main-dev/prd`, `sql-shop42-orders-dev/prd`, `crshop42mainshared` et `stifsshop42dev/prd/shared` sont disponibles. Le contrôle SQL a été fait par l’action officielle `Microsoft.Sql/checkNameAvailability`, car `az sql server check-name` n’existe pas dans l’Azure CLI installé.
- Les vues de coût divergent au contrôle du 2026-10-15. L’API Usage donne 70,840618 EUR de coût réel du 1er au 14 octobre ; l’API Forecast donne 103,835293 EUR du 15 au 31, soit 174,675911 EUR projetés pour octobre. L’API Usage attribue notamment 40,901084 EUR à Azure Container Apps, 12,033698 EUR à SQL Database, 6,577436 EUR à Log Analytics et 4,559451 EUR à Container Registry. L’objet budget `budget-sandbox-monthly` indique toujours `currentSpend=0,430301 EUR`, mais retourne `forecastSpend=235,765571 EUR` pour un budget de 200 EUR/mois. Aucun des deux résultats ne permet de garantir le plafond. Avec le haut du sous-total pilote de 30,17 EUR, le calcul Usage/Forecast atteint environ 204,85 EUR et celui de l’objet budget 265,94 EUR, hors frais annexes encore non chiffrés. À cette date, Luna a suspendu les créations. Le 2026-10-16, l utilisateur a confirmé quil ne fixe aucun plafond personnel et a autorisé la poursuite ; le contrôle Cost Management suivant est consigné ci-dessous.
- Au 2026-10-15, aucun groupe de ressources, rôle Azure, identité managée, registre, coffre, serveur SQL, espace Log Analytics ou pile n’avait été créé par Luna. Les déploiements Shop42 autorisés ensuite sont consignés ci-dessous.

## Inventaire de référence avant création

Avant chaque jeu (`shop42`, `shop43`, `shop44`), enregistrer l'heure UTC, la souscription et tous les IDs déjà présents dans
les portées prévues : groupes et ressources qu'ils contiennent, piles de déploiement, identités et attributions de rôle.
Un ID présent ici est préexistant et ne doit jamais être supprimé. Vérifier aussi la disponibilité des noms globaux.

| Capture UTC | Jeu / portée | Type d'objet | ID exact | Groupe / portée parente | Notes |
|---|---|---|---|---|---|
| 2026-10-08T06:31:45Z | Préflight P-08 initial | Ressources, groupes, piles, attributions de rôle | IDs exacts dans le snapshot local mentionné ci-dessus | Souscription de preuve | 124 ressources, 16 groupes, 0 pile, 4 attributions ; ne pas supprimer ces éléments |
| 2026-10-15T12:29:09Z | P-08 avant toute écriture Azure | Ressources, groupes, piles, attributions de rôle, budgets | IDs exacts dans `baseline-20261015T1229Z.json` hors dépôt | Souscription de preuve | 133 ressources, 16 groupes, 0 pile, 28 attributions, 7 budgets ; inventaire exact préexistant |

## Ressources créées par Luna

Après **chaque déploiement**, y compris s'il échoue ou s'arrête partiellement, capturer à nouveau les ressources, groupes,
piles et attributions de rôle. Comparer le relevé avant/après par ID, sans filtre de nom sensible à la casse. Ajouter au
registre chaque nouvel ID avant de lancer un autre déploiement. Un déploiement Bicep pouvant créer plusieurs ressources,
la capture se fait à la fin de l'opération, puis les IDs obtenus de `az stack sub show` et `az resource list` sont consignés.

| Date UTC | Jeu / étape | Type d'objet | ID exact | Groupe / portée parente | Pile | Résultat de création |
|---|---|---|---|---|---|---|
| 2026-10-16T12:28:09Z | Shop42 P1 et P3b #713/#714 | Ressources, groupes, piles, attributions de rôle | IDs exacts hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\after-run-714-20261016T122809Z.json` | 10 groupes Shop42/IFS, tous `francecentral` | 7 piles, toutes `francecentral` | 30 ressources du pilote, 10 groupes, 7 piles et 29 rôles ajoutés depuis la baseline. Le run #713 DEV a réussi et son PRD a été refusé avant mutation après la dérive; le run #714 a convergé DEV/PRD. Le workspace a été restauré à 90 jours. Un objet apparu dans le groupe préexistant `rg-ds-magellan` est hors périmètre et n’est pas attribué à Luna. |
| 2026-10-16T13:35:10Z | Contrôle après API release #715, avant la prochaine release infra | Ressources, groupes, piles, attributions de rôle, budgets | IDs exacts hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\after-run-715-20261016T1331Z.json` | 10 groupes Shop42/IFS, tous `francecentral` | 7 piles, toutes `francecentral` | 163 ressources, 26 groupes, 7 piles, 57 rôles et 7 budgets ; aucun nouvel ID depuis #714, aucune ressource Shop42 hors France Central. Le run #715 a restauré l’image D1 et `/health` répond 200. |
| 2026-10-16T14:22:37Z | Contrôle après run infra #722, What-If échoué avant mutation | Ressources, groupes, piles, attributions de rôle, budgets | IDs exacts hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\after-run-722-20261016T1420Z.json` | 10 groupes Shop42/IFS, tous `francecentral` | 7 piles, toutes `francecentral` | 163 ressources, 26 groupes, 7 piles, 57 rôles et 7 budgets ; aucun nouvel ID depuis #715, aucune ressource Shop42 hors France Central. Le run #722 a échoué au What-If en lecture seule car Azure CLI refuse le paramètre objet `caApi.image` ; aucun déploiement n’a eu lieu. |
| 2026-10-16T16:01:46Z | Contrôle après release infra #730 réussie | Ressources, groupes, piles, attributions de rôle, budgets | IDs exacts hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\after-run-730-20261016T160146Z.json` | 10 groupes Shop42/IFS ; ressources régionales en `francecentral` | 7 piles, toutes `francecentral` | 163 ressources, 26 groupes, 7 piles, 57 rôles et 7 budgets ; aucun ID ajouté ou retiré depuis #722. Les deux seules ressources Shop42 déclarées `global` sont les règles Smart Detector `Failure Anomalies - appi-shop42-main-dev/prd`, déjà présentes au snapshot #715 ; aucune ressource régionale Shop42 n’est hors France Central. |
| 2026-10-16T19:39:06Z | Contrôle après API release #734 DEV, avant approbation PRD | Ressources, groupes, piles, attributions de rôle, budgets | Inventaire exact hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\after-api-734-dev-20261016T193906Z.json` | 10 groupes Shop42/IFS ; ressources régionales en `francecentral` | 7 piles, toutes `francecentral` | 163 ressources, 26 groupes, 7 piles, 57 rôles et 7 budgets ; aucun ID ajouté ou retiré depuis #730 et aucune ressource régionale Shop42 hors France Central. Le rapport API DEV du run #734 confirme `Succeeded`, HTTP 200 et le digest `sha256:4af4e78a6a7d90facd49f513593b9f5018513c7dfb1bd724984ca36d064fee7f`. |
| 2026-10-16T19:45:38Z | Contrôle après API release #734 DEV/PRD | Ressources, groupes, piles, attributions de rôle, budgets | Inventaire exact hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\after-api-734-prd-20261016T194538Z.json` | 10 groupes Shop42/IFS ; ressources régionales en `francecentral` | 7 piles, toutes `francecentral` | 163 ressources, 26 groupes, 7 piles, 57 rôles et 7 budgets ; aucun ID ajouté ou retiré depuis le contrôle DEV et aucune ressource régionale Shop42 hors France Central. Le rapport API PRD confirme `Succeeded`, HTTP 200 et le digest `sha256:4af4e78a6a7d90facd49f513593b9f5018513c7dfb1bd724984ca36d064fee7f`. L’Container App PRD est en France Central, provisioning `Succeeded`, trafic 100 % vers la dernière révision saine. |
| 2026-10-16T22:35:49Z | P-08 après API #739 et run infra #738 échoué avant application | Ressources, groupes, piles, attributions de rôle, budgets | Inventaire exact hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\after-infra-738-20261016T223549Z\inventory.json` | 10 groupes Shop42/IFS ; ressources régionales en `francecentral` | 7 piles, toutes `francecentral` | 163 ressources, 26 groupes, 7 piles, 57 rôles et 7 budgets ; aucun ID ajouté ou retiré depuis le snapshot après API #737, aucune ressource régionale Shop42 hors France Central. L’API reste sur D2 et `/health` répond 200 en DEV/PRD. Le rapport infra PRD #738 est `FailedBeforeMutation` car les effets ont changé depuis l’aperçu approuvé ; le nettoyage est suspendu. |

Inclure les groupes de ressources, piles, identités, ressources détachées et attributions de rôle. Pour les piles, conserver
le commit source, les paramètres sans secret, la liste des principaux exclus de `denySettings`, ainsi que les IDs de toutes
les ressources et de tous les groupes gérés. Pour les attributions de rôle, conserver leur ID d'attribution exact, pas
seulement le principal ou le rôle.

## Journal des contrôles de coût

Contrôler *Cost Management → Cost analysis* au démarrage et à la fin de chaque session, avant et après chaque déploiement ou
run Azure significatif, puis après le nettoyage. Les coûts réels peuvent être retardés : le contrôle est périodique, pas
continu, et un budget Azure déclenche des alertes sans arrêter les ressources. Comparer le réel et la prévision affichés au
plafond confirmé dans `NEXT.md`. Si le seuil est atteint ou que la prévision l'atteint, suspendre les créations et lancer
la procédure d'urgence ci-dessous.

| Date UTC | Étape / opération | Coût réel affiché | Prévision affichée | Plafond confirmé | IDs contrôlés | Décision / action |
|---|---|---:|---:|---:|---|---|
| 2026-10-08 | Préflight P-08 initial | 0,4303 EUR | Non retournée par la commande budget | 200 EUR/mois | Snapshot local du 2026-10-08 | Aucun déploiement ; arrêt sur la restriction SQL en North Europe |
| 2026-10-15T09:10Z | Préflight P-08 France Central, lecture seule | 0,430301 EUR | Non retournée | 200 EUR/mois préexistant, confirmation de l'estimation France Central en attente | Souscription CLI par défaut, identique au snapshot privé | Région, fournisseurs, SKU et budget consultés ; aucun changement Azure |
| 2026-10-15 | P-08, coût et noms avant déploiement | 70,840618 EUR (API Usage) ; objet budget : 0,430301 EUR | 174,675911 EUR (API Forecast) ; objet budget : 235,765571 EUR | 200 EUR/mois | Snapshot exact du 2026-10-15T12:29:09Z ; aucun ID nouveau | Vues de coût incohérentes ; ajout du sous-total pilote haut de 30,17 EUR porte Usage/Forecast à 204,85 EUR et le calcul du budget à 265,94 EUR, hors frais annexes. Arrêt avant toute écriture Azure ; aucune ressource créée par Luna |
| 2026-10-16 | P1 Shop42 et P3b runs #713/#714 | Non retournés de façon exploitable par `az consumption usage list` ; les budgets listés ne contiennent pas de coût actuel/prévision | Non retournée | Aucun plafond demandé par l’utilisateur le 2026-10-16 | Snapshot après run #714 ci-dessus ; 7 budgets existants de 200 EUR, inchangés | Contrôle de coûts partiel ; aucun budget créé ou modifié. Les coûts Azure n’ont pas été fournis par ces commandes. |
| 2026-10-16T13:31Z | P-08 après #715, avant la release infra Orders suivante | 76,962911 EUR (Cost Management ActualCost, MTD) | 193,555296 EUR (Cost Management Forecast, octobre) | Aucun plafond personnel fixé ; budget préexistant de 200 EUR/mois inchangé | Snapshot après-run-715 de 13:35Z : 163 ressources, 26 groupes, 7 piles, 57 rôles, 7 budgets ; aucun ID nouveau depuis #714 | L’utilisateur a confirmé l’absence de plafond et autorisé la poursuite. Aucun budget n’a été créé ou modifié. |
| 2026-10-16T14:20Z | P-08 après l’échec What-If #722, avant la release corrigée | 77,768644 EUR (Cost Management ActualCost, MTD) | 192,749563 EUR (Cost Management Forecast, octobre) | Aucun plafond personnel fixé ; budget préexistant de 200 EUR/mois inchangé | Snapshot après-run-722 de 14:22Z : 163 ressources, 26 groupes, 7 piles, 57 rôles, 7 budgets ; aucun ID nouveau depuis #715 | #722 a échoué avant mutation. Aucun budget n’a été créé ou modifié. |
| 2026-10-16T19:10:20Z | P-08 après release infra #730, avant la release API témoin | 78,723647 EUR (Cost Management ActualCost, MTD) | 191,794559 EUR (Cost Management Forecast, octobre) | Aucun plafond personnel fixé ; budget préexistant de 200 EUR/mois inchangé | Snapshot après-run-730 de 16:01:46Z : 163 ressources, 26 groupes, 7 piles, 57 rôles, 7 budgets ; aucun ID ajouté ou retiré depuis #722 | Les deux lectures Cost Management affichent ces valeurs ; aucun budget n’a été créé ou modifié. Le coût et l’inventaire seront actualisés avant les prochains déploiements. |
| 2026-10-16T19:36Z | P-08 après API release #734 DEV, avant l’approbation PRD | 78,723647 EUR (Cost Management ActualCost, MTD) | 110,391181 EUR restant à prévoir du 16 au 31 octobre ; projection calculée 189,114828 EUR | Aucun plafond personnel fixé ; budget préexistant de 200 EUR/mois inchangé | Snapshot après-run-734-DEV de 19:39:06Z : 163 ressources, 26 groupes, 7 piles, 57 rôles, 7 budgets ; aucun ID ajouté ou retiré depuis #730 | Contrôle effectué après le déploiement DEV et avant l’approbation PRD. Aucun budget n’a été créé ou modifié. |
| 2026-10-16T19:44Z | P-08 après API release #734 DEV/PRD | 78,723647 EUR (Cost Management ActualCost, MTD) | 110,391181 EUR restant à prévoir du 16 au 31 octobre ; projection calculée 189,114828 EUR | Aucun plafond personnel fixé ; budget préexistant de 200 EUR/mois inchangé | Snapshot après-run-734-PRD de 19:45:38Z : 163 ressources, 26 groupes, 7 piles, 57 rôles, 7 budgets ; aucun ID ajouté ou retiré depuis le contrôle DEV | Contrôle effectué après le déploiement PRD. Les valeurs de coût n’ont pas changé depuis 19:36Z ; aucun budget n’a été créé ou modifié. |
| 2026-10-16T22:37Z | P-08 après API #739 et échec du run infra #738 | 80,945803 EUR (Cost Management ActualCost, MTD) | 106,655714 EUR prévus du 17 au 31 octobre ; projection calculée 187,601517 EUR | Aucun plafond personnel fixé ; budget préexistant de 200 EUR/mois inchangé | Snapshot après-run-738 de 22:35:49Z : 163 ressources, 26 groupes, 7 piles, 57 rôles, 7 budgets ; aucun ID ajouté ou retiré depuis le snapshot après API #737 | Cost Management actualisé après les déploiements ; aucun budget n’a été créé ou modifié. La prévision est conservée hors dépôt avec le snapshot d’inventaire. |

## Nettoyage d'urgence si le seuil de coût est atteint

1. Arrêter les runs et toute nouvelle création ; noter le coût affiché, la prévision et l'heure du contrôle.
2. Comparer l'état Azure aux deux tableaux ci-dessus. Chaque ID candidat doit être marqué « Créé par Luna » et absent de
   l'inventaire de référence. Relever à nouveau les ressources gérées par chaque pile et le contenu complet des groupes.
3. N'utiliser `deleteAll` que si **chaque** ressource et groupe qu'il peut supprimer porte un ID du registre marqué créé par
   Luna. Si un ID manque, est préexistant ou n'est pas clairement attribué, ne pas supprimer la pile ni le groupe : supprimer
   seulement les ressources individuelles dont l'ID est enregistré et laisser le conteneur intact.
4. Si une pile a `denyDelete`, ne pas la mettre à jour depuis le working tree courant en urgence. Vérifier l'ID du principal
   opérateur et les principaux exclus enregistrés sur la pile. Si cet opérateur n'est pas explicitement exclu et que la
   suppression est refusée, s'arrêter et signaler la pile verrouillée ; ne pas rejouer `az stack sub create` ni modifier les
   deny settings pendant une intervention d'urgence. La mise à jour d'une pile est un redéploiement qui peut réconcilier son
   modèle ; Microsoft documente `az stack sub create` comme mécanisme de mise à jour de la pile ([documentation](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deployment-stacks?tabs=azure-cli)).
5. Supprimer uniquement les IDs inscrits comme créés par Luna, y compris les ressources détachées et les attributions de rôle.
   Un préfixe de nom ne suffit jamais et aucun ID absent du registre ne doit être supprimé.
6. Ajouter la date, le coût et les IDs supprimés, puis vérifier les IDs et groupes exacts après l'opération.

| Contrôle | Date UTC | Coût affiché | Seuil confirmé | Ressources Luna supprimées | Résultat |
|---|---|---:|---:|---|---|
| Aucun contrôle exécuté | — | — | — | — | Aucune ressource créée |
