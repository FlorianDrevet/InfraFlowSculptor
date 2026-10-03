# Traitement de la contre-revue du 3 octobre 2026

Réponse à [la contre-revue](2026-10-03-contre-revue-et-idees-fonctionnalites.md), sur le commit `0a25751`.
Les 16 constats sont fondés. Les corrections sont les décisions [DEC-99 à DEC-112](../specs/03-decisions.md).
Comme la contre-revue le demande, chaque garantie corrigée est décrite par un scénario complet (état
initial, acteur, ordre des effets, interruption, état final, preuve) dans
[91 — Scénarios critiques](../specs/91-scenarios-critiques.md), qui sert de critère d'acceptation.

## Vérifications externes

| Point | Résultat |
|---|---|
| Verrous et RBAC (B01) | Confirmé : « A cannot-delete lock on a resource or resource group prevents the deletion of Azure RBAC assignments », et les ressources d'extension héritent des verrous ([verrous Azure](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/lock-resources)). Gérer les verrous exige `Microsoft.Authorization/locks/*` : une levée temporaire donnerait à l'identité de déploiement le pouvoir de retirer toute protection. |
| Alternative retenue (B01) | Les piles de déploiement ont un refus `denyDelete` avec jusqu'à cinq principaux exclus, groupes compris, qui s'applique aussi aux propriétaires ([piles de déploiement](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deployment-stacks)). L'identité de déploiement exclue peut révoquer ; personne d'autre ne supprime. Poser ce refus exige le rôle Azure Deployment Stack Owner. |
| Permissions du what-if (B03) | Confirmé : le what-if a « the same permission requirements » qu'un déploiement ([what-if](https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/deploy-what-if)). Une identité d'aperçu en lecture seule n'existe pas pour Bicep : la barrière doit venir des contrôles de la connexion. |
| Sujet OIDC GitHub (B12) | Confirmé : sans environnement, le sujet porte la branche ou `pull_request` ([OIDC GitHub](https://docs.github.com/en/actions/reference/security/oidc)). |

## Constats B01 à B16

| ID | Verdict | Traitement |
|---|---|---|
| B01 | Fondé | Plus aucun verrou de gestion ; refus `denyDelete` de la pile en cible protégée, identité de déploiement exclue ; protection par l'outil en Terraform et Pulumi, preuve P10 avant le lot 3 (DEC-99, RG-GEN-20, scénario T01). |
| B02 | Fondé | Journal d'opérations chez le client, écrit avant toute mutation, repris en premier par toute release suivante ; états partiellement appliquée et indéterminée élargis (DEC-100, RG-PIP-12, T02, T03, T16). |
| B03 | Fondé | Frontière de confiance définie : branche par défaut relue ; contrôles « contrôle de branche » et « modèle requis » sur chaque connexion ; approbation sur la connexion applicative protégée ; aucun point d'extension dans les pipelines d'infrastructure (DEC-101, RG-PIP-13). |
| B04 | Fondé | Empreinte des effets définie, image et trafic exclus et relus sous verrou ; une livraison intercalée est conservée, un effet changé exige une nouvelle approbation (DEC-102, T04). |
| B05 | Fondé | L'accès aux données appartient au composant source et s'exécute après son déploiement (DEC-103, RG-LIA-21, T07). |
| B06 | Fondé | Double consentement borné à un effet, droits revérifiés, approbation qui tombe si l'effet change (DEC-104, RG-LIA-24, T05, T06). |
| B07 | Fondé | Pas de mot de passe `Généré` dans un coffre existant (`VAL-SEC-GENERE-EXISTANT`) ; secret de pipeline écrit avant le déploiement (DEC-105, T08). |
| B08 | Fondé | Décommissionnement : retrait demandé, unité vide déployée, révocations prouvées, retrait cible par cible ; exclu du pilote (DEC-106, UC-CMP-04, T10). |
| B09 | Fondé | Révisions contaminées non téléchargeables, liste des copies git, expurgations réappliquées après restauration (DEC-107, RG-HIS-12, T11). |
| B10 | Fondé | Versions de correctif immuables ; version retirée consultable mais non publiable (DEC-107, RG-EXP-05, T12). |
| B11 | Fondé | Recalcul des noms effectifs et contrôle des collisions avec tous les projets de l'organisation (DEC-108, RG-PRJ-09, T13). |
| B12 | Fondé | Environnement GitHub d'aperçu limité à la branche par défaut, avec son identifiant fédéré (DEC-101, kit 23 § 2, T14). |
| B13 | Fondé | Secret identifié par son emplacement physique, un seul écrivain déclaré (`VAL-PAR-SECRET-PROPRIETAIRE`), jamais supprimé par IFS (DEC-105, T09). |
| B14 | Fondé | Contrat des pipelines du client : environnement commun (verrou), connexion applicative, rapport de livraison standard, champs de build facultatifs (DEC-109, RG-APP-22, T15). |
| B15 | Fondé | Critères chiffrés de sortie et règle de décision : élargir, resserrer, corriger ou arrêter (DEC-110). |
| B16 | Fondé | Justification des 13 mois corrigée : au-delà de la recommandation générale de la CNIL, justifiée par un cycle annuel de revue des accès (EXG-06, RG-ORG-22). |

## Cohérence documentaire (§ 2.3)

| Point | Traitement |
|---|---|
| Bicep : « la pile détache tout » contre suppression selon le composant | La pile est toujours en `detachAll` ; les suppressions passent par l'étape 10, journalisées (DEC-85, 22 § 3.2). |
| Numéro de révision dans les pipelines | Lu à l'exécution dans le manifeste, jamais écrit dans les fichiers (RG-SUI-02). |
| Propositions au jalon 3 contre brouillons et demandes au jalon 2 | Le mécanisme de propositions et son écran passent au jalon 2 ; le serveur MCP reste au jalon 3. L'export JSON existe dès le jalon 0 ; l'import au jalon 3 (04 § 2). |
| « Aucune question sans réponse » | Remplacé par : arbitrages consignés, preuves ouvertes listées (04 § 7). |

## Registre des preuves enrichi

P2 couvre désormais la protection réelle (T01). P5 entre dans la condition de sortie du pilote. Ajouts :
P7 interruption brutale (T02, T16), P8 livraison pendant une release (T04), P9 accès aux données entre
composants (T07), P10 protection Terraform et Pulumi avant le lot 3 ([04 § 7.2](../specs/04-perimetre-et-lots.md)).

## Idées de fonctionnalités

| Idée | Suite donnée |
|---|---|
| M01 fiche d'opération reprenable | Lot 1 : journal et fiche d'opération dans le suivi (DEC-100, 28 § 3.1). |
| M02 retrait d'un composant | Lot 1, jalon 1 ; exclu du pilote (DEC-106). |
| M03 contrat de préparation par cible | Retenu cette fois : vérification réelle depuis chaque cible, états vérifié, déclaré, à faire (DEC-111). La réponse précédente le jugeait couvert par `-WhatIf`, à tort : un mode qui affiche les actions ne prouve pas les droits ni la connectivité. |
| F01 accès avec motif, responsable et date de revue | Lot 2, vague E ; rappels seulement. |
| F02 mise à jour des modèles instanciés | Lot 2, vague G. |
| F03 rapport de livraison des pipelines existants | Lot 1, jalon 2 (DEC-109). |
| F04 diagnostic guidé | Lot 2, vague E, sans IA d'abord. |
| F05 versions éligibles au retour arrière | Lot 2, vague C. |
| F06 dossier de transmission et de sortie | Lot 1, jalon 3. |
| F07 revue des ressources détachées | Responsable, motif, date de revue au lot 1 ; coût au lot 2 (28 § 5). |
| F08, F09, F10 | Lot 3, après preuve du besoin. |
| F11 mode démonstration | Lot 1, jalon 3, exclu des indicateurs. |
| F12 comparaison de variantes | Non retenue. |
| Correcteur autonome par IA, place de marché | Non retenus, comme la contre-revue le recommande. |
