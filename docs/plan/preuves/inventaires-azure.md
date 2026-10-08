# Inventaire Azure des preuves

Ce registre sépare les ressources préexistantes de celles créées par Luna. Un nom, un préfixe ou un tag ne suffit jamais
à autoriser une suppression. Les suppressions ciblent uniquement des IDs exacts consignés comme créés par Luna et absents
de l'inventaire de référence.

## État au 2026-10-08

- Souscription de preuve : ID fourni par l'utilisateur lors du préflight, non recopié dans le dépôt.
- Région cible : `northeurope`.
- Ressources créées par Luna : **aucune**.
- Inventaire de référence capturé après confirmation du plafond : 124 ressources, 16 groupes, aucune pile de souscription et 4 attributions de rôle.
- Les IDs exacts sont conservés hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\baseline-20261008.json` pour éviter de publier l'identifiant de souscription. Reprendre une capture avant la première écriture Azure si l'état a changé.
- Budget préexistant à l'échelle de la souscription : 200 EUR/mois ; dépense réelle affichée ≈ 0,4303 EUR le 2026-10-08. Alertes existantes : réel à 80 %, prévision à 100 %. Aucun budget créé/modifié par Luna.
- Préflight bloqué : le provisionnement SQL `GP_S_Gen5_1` est restreint en North Europe. Aucune ressource n'a été déployée.

## Inventaire de référence avant création

Avant chaque jeu (`shop42`, `shop43`, `shop44`), enregistrer l'heure UTC, la souscription et tous les IDs déjà présents dans
les portées prévues : groupes et ressources qu'ils contiennent, piles de déploiement, identités et attributions de rôle.
Un ID présent ici est préexistant et ne doit jamais être supprimé. Vérifier aussi la disponibilité des noms globaux.

| Capture UTC | Jeu / portée | Type d'objet | ID exact | Groupe / portée parente | Notes |
|---|---|---|---|---|---|
| 2026-10-08T06:31:45Z | Préflight P-08 initial | Ressources, groupes, piles, attributions de rôle | IDs exacts dans le snapshot local mentionné ci-dessus | Souscription de preuve | 124 ressources, 16 groupes, 0 pile, 4 attributions ; ne pas supprimer ces éléments |

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
