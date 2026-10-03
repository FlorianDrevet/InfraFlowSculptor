# Traitement de la revue fonctionnelle v1 du 3 octobre 2026

Réponse à [la revue critique](2026-10-03-revue-fonctionnelle-v1.md). Chaque constat a été confronté au
corpus et, pour les points techniques, à la documentation Microsoft. Les corrections sont dans les specs,
et les arbitrages dans les décisions [DEC-85 à DEC-98](../specs/03-decisions.md).

## Vérifications externes

| Point | Résultat |
|---|---|
| What-if des piles de déploiement (A21) | Confirmé : « What-if isn't yet available » pour les piles ([limitations Microsoft](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deployment-stacks-known-issues)). L'aperçu Bicep devient un what-if ARM du modèle plus une comparaison avec les ressources gérées par la pile. |
| Approbations Azure DevOps (A22) | Confirmé : les contrôles sont évalués avant le démarrage du stage ; le verrou exclusif vaut `runLatest` par défaut ([approbations et contrôles](https://learn.microsoft.com/en-us/azure/devops/pipelines/process/approvals?view=azure-devops)). |
| Création d'utilisateurs Entra en Azure SQL (A23) | Confirmé, avec une meilleure solution que celle de la revue : la forme documentée `CREATE USER … WITH SID = …, TYPE = E` crée l'utilisateur **sans** consulter l'annuaire ([CREATE USER](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-user-transact-sql?view=azuresqldb-current)). Le prérequis Graph de l'identité du serveur disparaît. |
| Validation des PR Azure Repos (A23) | Confirmé : politique de branche nécessaire. Ajoutée au kit. |

## Constats A01 à A40

Verdicts : **fondé** (corrigé), **fondé en partie** (corrigé, avec un choix différent de la revue),
**déjà couvert** (précisé seulement).

| ID | Verdict | Traitement |
|---|---|---|
| A01 | Fondé | Jalon 0 pilote sur un segment qualifié, avec condition de sortie ([04 § 2.0](../specs/04-perimetre-et-lots.md), DEC-93). |
| A02 | Fondé | Temps actif et temps d'obtention des droits mesurés séparément ([00 § 8](../specs/00-vision.md)). |
| A03 | Fondé | Effets indirects contrôlés ; accès vers un autre composant = demande d'accès ; cascade hors portée refusée (DEC-89, RG-ORG-09, RG-LIA-24). |
| A04 | Fondé | Plafond d'attribution, seul un propriétaire nomme un propriétaire ; restauration et propositions contrôlées effet par effet (RG-ORG-23, UC-HIS-05). |
| A05 | Fondé | `generer` vaut pour le projet entier, même dans une attribution limitée (RG-ORG-10). |
| A06 | Fondé | Deux identités par cible, infrastructure et applicative ; PR sans connexion Azure (DEC-88). |
| A07 | Fondé | Dépôts ouverts aux projets par l'administrateur, droit revérifié à chaque accès (RG-PUB-03). |
| A08 | Fondé | Séquencement fixe de la release ; coffres avant consommateurs (`VAL-SEC-ORDRE`) ; droits de plan de données attribués par le déploiement, pas par le kit (DEC-86, 22 § 3.1). |
| A09 | Fondé | Gravité « Erreur à la publication » ; niveaux modèle valide / générable / publiable / déployable ; bouchons d'extension au contrôle de sortie (RG-VAL-01, RG-VAL-04, RG-GEN-04). |
| A10 | Fondé | Éditeur coordinateur aux jalons 0 et 1 ; brouillons avancés au jalon 2 ; le modèle principal refuse une nouvelle erreur (DEC-93, RG-HIS-13). |
| A11 | Fondé | Objets d'autorisation et de configuration toujours supprimés, même en production ; données conservées (DEC-85). |
| A12 | Fondé | Propriétaire unique de chaque objet dérivé ; identité affectée limitée à son composant (DEC-98). |
| A13 | Fondé | Dépendances de création et sorties calculables depuis le nom distinguées (RG-CMP-05/06). |
| A14 | Fondé | « Toujours sûr » retiré ; évolution en deux temps recommandée (RG-APP-03). |
| A15 | Fondé | Plan de retour (applicable, partiel, impossible, avec intervention) ; les données ne se restaurent pas par IFS (RG-HIS-09). |
| A16 | Fondé | Retraits calculés à l'application contre l'unité de déploiement ; blocs `removed` gardés par cible ; suivi des PR au lot 1 (DEC-91). |
| A17 | Déjà couvert en grande partie | Le code projet était déjà unique dans l'organisation. Ajouté : l'import crée toujours une copie, ressources existantes partagées signalées (RG-PRJ-09). |
| A18 | Fondé | Catalogue figé par projet, montée explicite, support 12 mois (DEC-92). |
| A19 | Fondé en partie | Le lot 1 reste la cible commerciale ; il commence par un pilote réduit qui conditionne la suite (DEC-93). Prototypes de neutralité bornés à un sous-ensemble. |
| A20 | Fondé | Static Web App passe au lot 2 avec son parcours ; politique d'accès Redis intégrée aux liaisons et au cycle de révocation (DEC-66, 15, 16, 19). |
| A21 | Fondé | Voir vérifications externes (DEC-87). |
| A22 | Fondé | Stages Aperçu puis Déploiement ; empreinte vérifiée avant d'appliquer ; verrou séquentiel par cible (DEC-87, RG-PIP-05). |
| A23 | Fondé | Politique de validation de build dans le kit ; création SQL par SID et PostgreSQL par OID, sans Graph (DEC-90, 23 § 3.1). |
| A24 | Fondé | Commit de base vérifié, identifiant d'opération, propriété du manifeste, PR approuvée remplacée sur confirmation (RG-PUB-20, RG-PUB-12). |
| A25 | Fondé | Un tenant par projet, vérifié par le kit ; propriétés déclarées des ressources existantes, marquées « non vérifié » (11, RG-RES-10). |
| A26 | Fondé | Détection avant enregistrement, expurgation (DEC-95, RG-PAR-05, RG-HIS-12). |
| A27 | Fondé | Commandes d'administration non exposées ; demande de publication confirmée côté serveur ; mode proposition par défaut (DEC-96). |
| A28 | Fondé | Durées justifiées par catégorie, sans les présenter comme légales (EXG-06, RG-ORG-22). |
| A29 | Fondé | Files et budgets par organisation (EXG-23). |
| A30 | Fondé | Plus de numéro de révision dans les en-têtes (DEC-94, RG-GEN-08). |
| A31 | Fondé | États déployée, partiellement appliquée, en échec avant modification, inconnu ; fraîcheur (DEC-91, 28 § 3.1). |
| A32 | Fondé | Le kit révoque les accès obsolètes et refuse une version plus ancienne (RG-INS-05, RG-INS-07). |
| A33 | Fondé | Récupération d'une organisation sans administrateur ; reprise après restauration d'IFS ; diagnostic partageable (UC-EXP-07, RG-EXP-11, RG-EXP-12). |
| A34 | Fondé | Origine et auteur accessibles au focus et au toucher ; rien par la seule couleur (RG-UI-04, RG-HIS-05). |
| A35 | Fondé | Saisie préservée, identifiant d'opération, usage mobile défini (RG-UI-15, RG-UI-16). |
| A36 | Fondé | Indicateur d'activation (déploiement, dépendances au vert, deuxième modification), cohortes (00 § 8). |
| A37 | Fondé | Règles de facturation (DEC-78). |
| A38 | Fondé | Critères 10 à 17 du projet de référence (90 § 4). |
| A39 | Fondé en partie | Aucune question ne reste sans réponse, mais chaque garantie critique porte un statut et le registre des preuves liste les prototypes P1 à P6 ; ordre de préséance défini (DEC-97, 04 § 7). |
| A40 | Fondé | Parité garantie sur le sous-ensemble déclaré par la matrice de prise en charge (P11). |

## Contradictions relevées

| Point | Traitement |
|---|---|
| Export et historique | Export avec historique en option (UC-PRJ-07). |
| Exécutions immuables | Événements immuables, état courant calculé (RG-DON-04). |
| Lectures Azure | Catégories admises, cache 24 heures (RG-DON-06). |
| Composant livrable sans intervention | Exception du préréglage « un dépôt par composant » décrite (UC-CMP-01). |
| Gravité des extensions manquantes | Publication refusée dans les deux cas (RG-APP-14, RG-PUB-15). |
| Message de renommage | Calculé par cible selon le traitement effectif (RG-NOM-10). |
| Vagues et renvois | DEC-66 réaligné sur 04 ; renvois `RG-ORG-11` corrigés ; Redis au lot 1 dans A1. |
| Identité `shared` sans droit de poussée | Identité applicative avec `AcrPush` (90 § 2.5). |
| Portée de P7 | Nouvelles valeurs contre nouvelles capacités (P7). |

## Scénarios S01 à S28

| ID | Traitement |
|---|---|
| S01 | DEC-86, critère 10 |
| S02 | DEC-85, critère 11 |
| S03 | DEC-98 |
| S04, S05 | DEC-91, UC-PUB-03, critère 12 |
| S06 | DEC-91, 28 § 3.1, critère 13 |
| S07 | RG-NET-03, RG-LIA-22 |
| S08 | RG-PIP-05, RG-APP-02, critère 14 |
| S09 | Validation du modèle à chaque modification ; RG-HIS-13 dès les brouillons |
| S10 | DEC-89, critère 15 |
| S11 | RG-PUB-20 |
| S12, S13 | RG-PUB-20, critère 16 |
| S14 | RG-PRJ-09 |
| S15 | RG-HIS-09 |
| S16 | Codes verrouillés après publication (RG-PRJ-01, RG-ENV-03, UC-CMP-02) |
| S17 | DEC-95 |
| S18 | RG-VAL-04 |
| S19 | RG-RES-14 |
| S20 | RG-PUB-10 |
| S21 | RG-MCP-08 |
| S22 | RG-SUI-02, critère 17 |
| S23 | Activation (00 § 8), contrôle `/health/dependencies` du projet de référence |
| S24 | RG-UI-15, RG-UI-16 |
| S25 | EXG-23 |
| S26 | DEC-78 (règles de facturation) |
| S27 | UC-ORG-04, UC-EXP-07 |
| S28 | RG-IMP-10 |

## Suggestions de la section 4

| Suggestion | Suite donnée |
|---|---|
| Installation en phases avec reprise, révocation, chaîne de preuve, préparation atomique | Retenues (DEC-85, DEC-86, DEC-91, DEC-93). |
| Diagnostic de préparation par cible | Couvert par le mode `-WhatIf` du script Azure et la vérification du tenant ; un diagnostic autonome n'est pas ajouté. |
| Explication d'un accès, plan orienté conséquences | Déjà couverts par les origines des éléments implicites et l'analyse d'impact ; le résumé distingue désormais accès révoqués et ressources détachées. |
| Intégration aux pipelines existants du client | Retenue : option « Pipelines du client » par application, avec contrat de livraison (RG-APP-22). |
| Catalogue complet de CI applicative « à repousser » | Non retenu : les pipelines applicatifs riches sont un choix produit. Le pilote n'en utilise que le conteneur et le scan d'image. |
| Six langages Pulumi, graphe éditable, import complet | Déjà placés aux lots 2 à 4. |
