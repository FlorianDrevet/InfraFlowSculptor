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
- Les 124 ressources, 16 groupes et 4 attributions du snapshot du 2026-10-08 restent l'inventaire de référence ; aucune ressource n'a été créée par Luna.
- Inventaire exact avant toute écriture Resource Manager capturé le `2026-10-15T12:29:09Z` : 133 ressources, 16 groupes, 0 pile de souscription, 28 attributions de rôle et 7 budgets. Les IDs sont conservés hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\baseline-20261015T1229Z.json` ; le snapshot du 2026-10-08 reste inchangé.
- Les noms globaux `kv-shop42-main-dev/prd`, `sql-shop42-orders-dev/prd`, `crshop42mainshared` et `stifsshop42dev/prd/shared` sont disponibles. Le contrôle SQL a été fait par l’action officielle `Microsoft.Sql/checkNameAvailability`, car `az sql server check-name` n’existe pas dans l’Azure CLI installé.
- Les vues de coût divergent au contrôle du 2026-10-15. L’API Usage donne 70,840618 EUR de coût réel du 1er au 14 octobre ; l’API Forecast donne 103,835293 EUR du 15 au 31, soit 174,675911 EUR projetés pour octobre. L’API Usage attribue notamment 40,901084 EUR à Azure Container Apps, 12,033698 EUR à SQL Database, 6,577436 EUR à Log Analytics et 4,559451 EUR à Container Registry. L’objet budget `budget-sandbox-monthly` indique toujours `currentSpend=0,430301 EUR`, mais retourne `forecastSpend=235,765571 EUR` pour un budget de 200 EUR/mois. Aucun des deux résultats ne permet de garantir le plafond. Avec le haut du sous-total pilote de 30,17 EUR, le calcul Usage/Forecast atteint environ 204,85 EUR et celui de l’objet budget 265,94 EUR, hors frais annexes encore non chiffrés. Décision opérationnelle : ne pas créer de ressource Azure et ne pas lancer de pipeline d’installation avant réconciliation et confirmation d’un plafond compatible.
- Aucun groupe de ressources, rôle Azure, identité managée, registre, coffre, serveur SQL, espace Log Analytics ou pile n’a été créé par Luna. Les préparatifs Azure DevOps et Entra sont consignés dans `NEXT.md` et n’appartiennent pas à cet inventaire Resource Manager.

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
| — | — | — | — | — | — | Aucune ressource créée par Luna à ce jour |

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
