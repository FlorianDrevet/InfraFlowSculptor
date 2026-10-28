# Couverture des maquettes

Ce registre complète la [correspondance des écrans et jalons](../README.md#écrans-et-jalons). Toute interface livrée doit
être reliée à une maquette précise. Si aucune maquette dédiée n'existe, inscrire l'écran ici avec la mention explicite
**« Aucune maquette dédiée »**, puis consigner la référence de repli, les captures et les écarts. Une interface sans
maquette est réalisée au plus près de l'écran approuvé le plus proche et du design system Strata ; elle ne peut pas être
déclarée identique à une maquette absente.

## Écrans existants sans maquette dédiée

| Écran / route | Étape | Maquette | Référence de repli et état de vérification |
|---|---|---|---|
| Création d’organisation — `/organizations/new` | J0-01 | **Aucune maquette dédiée** | Écran déjà implémenté. Repli visuel : [OrgSettings](preview/OrgSettings.html) pour les motifs d’organisation et les composants/tokens Strata. La comparaison directe n’est pas consignée ; fidélité visuelle à vérifier avant une prochaine évolution de cet écran. |
| Erreur serveur — `/error` | S-13 | **Aucune maquette dédiée** | Écran technique existant. Repli : motifs de message et composants Strata ; comparaison visuelle à une référence non consignée. |
| Échec de connexion — `/unauthorized` | S-13 | **Aucune maquette dédiée** | Écran technique existant. Repli : [Login](preview/Login.html) et composants/tokens Strata ; comparaison visuelle à une référence non consignée. |
| Page introuvable — `**` | S-13 | **Aucune maquette dédiée** | Écran technique existant. Repli : motifs de message et composants Strata ; comparaison visuelle à une référence non consignée. |

Ces entrées décrivent l'état du dépôt au 2026-10-28. Elles ne prétendent pas que les écrans existants ont déjà été
comparés à leur référence de repli.

## Écrans avec maquette dont la comparaison directe reste à consigner

Les écrans J0-09 ont des maquettes correspondantes et des captures du front dans
`docs/plan/revues/captures/R-05/`. La recette consigne les parcours desktop/mobile, les captures et les contrôles Axe,
mais pas une comparaison côte à côte avec chaque aperçu. La fidélité trait pour trait n'est donc pas attestée par cette
preuve seule.

| Écran | Maquette | État de la preuve au 2026-10-28 |
|---|---|---|
| Liste et favoris | [Projects](preview/Projects.html) | Captures front desktop/mobile présentes ; comparaison directe non consignée. |
| Vue d’ensemble du projet | [ProjectOverview](preview/ProjectOverview.html) | Captures front desktop/mobile présentes ; comparaison directe non consignée. |
| Environnements | [ProjectEnvironments](preview/ProjectEnvironments.html) | Captures front desktop/mobile présentes ; comparaison directe non consignée. |
| Édition d’environnement | [EnvironmentEdit](preview/EnvironmentEdit.html) | Captures front desktop/mobile présentes ; comparaison directe non consignée. |
| Membres du projet | [ProjectMembers](preview/ProjectMembers.html) | Captures front desktop/mobile présentes ; comparaison directe non consignée. |
| Paramètres du projet | [ProjectSettings](preview/ProjectSettings.html) | Captures front desktop/mobile présentes ; comparaison directe non consignée. |

## Écrans J0-10

| Écran / route | Étape | Maquette | Référence et preuve |
|---|---|---|---|
| Liste des composants — `/o/:orgId/projects/:projectId/components` | J0-10 | **Aucune maquette dédiée** | Repli : [ProjectOverview](preview/ProjectOverview.html) pour la liste liée au projet, puis [Component](preview/Component.html) pour la navigation et les panneaux Strata. Captures attendues dans `docs/plan/revues/captures/R-05/` après la recette authentifiée. La liste ajoute un en-tête, l’état vide et les liens vers les composants ; ces états n’ont pas de référence dédiée. |
| Résumé des composants — `/o/:orgId/projects/:projectId` | J0-10 | [ProjectOverview](preview/ProjectOverview.html) | La section réutilise la maquette et ajoute le compteur/listing prévu par l’étape ; captures dans `docs/plan/revues/captures/R-05/`. Comparaison directe à consigner après recette. |
| Détail du composant — `/o/:orgId/projects/:projectId/components/:componentId` | J0-10 | [Component](preview/Component.html) | Captures `Component-1440.png` et `Component-390.png` prévues dans `docs/plan/revues/captures/R-05/`. Les ressources et dépendances sont des états vides ; J0-10 ne livre pas leur édition. |
| Réglages du composant — `/o/:orgId/projects/:projectId/components/:componentId/settings` | J0-10 | [ComponentSettings](preview/ComponentSettings.html) | Captures `ComponentSettings-1440.png` et `ComponentSettings-390.png` prévues dans `docs/plan/revues/captures/R-05/`. Sont exclus selon l’étape : abonnement par environnement, code additionnel, duplication et suppression d’un composant publié. |

## Modèle pour une nouvelle interface sans maquette

Ajouter une ligne avant ou au début du travail, puis la compléter avant la revue :

| Écran / route | Étape | Maquette | Référence de repli et état de vérification |
|---|---|---|---|
| `<nom et route>` | `<ID>` | **Aucune maquette dédiée** | `Implémenté au plus près de <aperçu de référence> et de Strata.` Captures : `<chemins>`. Écarts/limites : `<détail>` ; comparaison terminée : `<oui/non>`. |

Si une maquette est ajoutée ensuite dans Claude Design, l'exporter dans `preview/`, mettre à jour le tableau de
`docs/design/README.md` et remplacer cette entrée par le chemin exact de la nouvelle référence.
