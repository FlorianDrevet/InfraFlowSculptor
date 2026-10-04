# Recette du jalon 0 — pilote

Une section par verrou. Chaque section part de `aspire run` puis `dotnet run --project src/backend/InfraFlowSculptor.Api -- seed demo`
(sauf mention), avec une fenêtre de navigation privée par utilisateur.

## § Accès (verrou R-04) — 40 min

| # | Qui | Action | Attendu |
|---|---|---|---|
| A1 | alice | Se connecter | Organisation « Contoso » active |
| A2 | alice | Créer l'organisation « Contoso Labs », puis revenir à « Contoso » | Le sélecteur bascule ; chaque organisation ne montre que ses données |
| A3 | david | Coller une URL de page de Contoso copiée chez alice | « Introuvable » (jamais « interdit ») |
| A4 | alice | Membres : bob → « Gestionnaire des connexions » | Rôle affiché ; une ligne d'audit « rôle modifié » avec avant/après |
| A5 | alice | Se retirer de l'organisation | Refus : dernier administrateur (règle citée) |
| A6 | alice | Inviter `nina@contoso.example` | E-mail dans MailPit, en français, lien valable 7 jours |
| A7 | nina | Ouvrir le lien, se connecter | Refus : adresse non vérifiée ; rien n'a changé dans les membres |
| A8 | alice | Restreindre au tenant Contoso, inviter `emma@outlook.example` | Refus à l'envoi : tenant non autorisé |
| A9 | alice | Lever la restriction, réinviter emma ; emma ouvre le lien | Emma est membre |
| A10 | alice | Révoquer une invitation, ouvrir son lien | « Invitation révoquée » |
| A11 | alice | Journal d'audit, filtre « aujourd'hui » | Toutes les actions ci-dessus, auteur, date UTC, avant/après |
| A12 | chloe | Ouvrir le journal d'audit par son URL | « Interdit », permission nommée |
| A13 | — | pgweb : `UPDATE audit_events SET action = 'x'` | Erreur du déclencheur |
| A14 | alice, chloe | Scalar : `GET /v1/projects/{projet de démonstration}/permissions/me` | alice : toutes ; chloe : `project.read` |

## § Modélisation (verrou R-05) — 2 h

**État de départ** : `seed demo` **sans** projet (`seed demo --without-projects`). Objectif : modéliser **seul, à l'écran,
sans aide**, le projet pilote de [`reference-pilote.md` § 1](../reference-pilote.md#1-modèle), en notant le temps.

| # | Action | Attendu |
|---|---|---|
| M1 | Assistant : `shop`, Bicep + Azure DevOps, `dev` puis `prd` protégé (approbateur « Shop Release Approvers »), préréglage Compact, publication passée | Projet créé ; alice propriétaire |
| M2 | Reprendre l'assistant interrompu sur un autre navigateur | Reprise à l'étape où vous étiez |
| M3 | Composants `core`, `data`, `orders` (par environnement), `platform` (unique, cible `shared`) et leurs groupes `main` | Visibles dans la barre latérale ; code de cible `dev` refusé pour `platform` |
| M4 | Ressources de chaque composant (tableaux de § 1.3) avec leurs surcharges | Noms calculés à l'ajout identiques à § 2.1 ; `crshopmainshared` avec l'explication de l'assainissement |
| M5 | Liaisons et paramètres de `ca api` (§ 1.3, § 1.4) | Implicites de § 2.3 visibles, chacun avec son origine (survol, focus clavier, toucher) |
| M6 | `Test__Token = ghp_…` (40 caractères) | Refus avant enregistrement, conversion proposée |
| M7 | Application de `ca api` (§ 1.5) | Registre de build `platform / cr main`, sans promotion |
| M8 | Constats | Exactement ceux de § 2.4 ; compteur dans l'en-tête |
| M9 | Retirer l'hébergement de `ca api`, corriger en un clic | Erreur puis disparition |
| M10 | Deux onglets, même surcharge modifiée | Dialogue de conflit, deux versions côte à côte |
| M11 | Composant `orders` | « Dépend de : core, data, platform » ; vue d'ensemble : ordre `core, data, platform, orders` |
| M12 | 390 px de large sur la vue d'ensemble, une ressource, les constats | Consultation possible, aucun défilement horizontal |
| M13 | chloe sur le projet | Aucun bouton de modification ; commande forcée par Scalar → « interdit » |

## § Génération (verrou R-06) — 30 min

| # | Action | Attendu |
|---|---|---|
| G1 | « Générer la révision 1 » | Position dans la file, puis « Générée » |
| G2 | Fichiers → `orders/infra/main.prd.bicepparam` | Noms en clair, toutes les valeurs, coloration |
| G3 | Télécharger l'archive, comparer à `reference/pilot/bicep-azdo/` (outil de diff) | Identique |
| G4 | Générer à nouveau sans changement | « Aucun changement depuis la révision 1 » |
| G5 | Plan de déploiement de `orders` en `prd` | Attributions avec origines, secret attendu, accès aux données, dépendances |
| G6 | Introduire une erreur, tenter de générer | Écran « Génération bloquée » listant l'erreur |
| G7 | Exporter le projet | JSON sans membre ni secret ; ligne d'audit |

## § Publication (verrou R-07) — 45 min

| # | Action | Attendu |
|---|---|---|
| P1 | Organisation → Connexions → Gitea ; ouvrir `shop` au projet `shop` | Trois dépôts listés ; `shop` ouvert au seul projet `shop` |
| P2 | Plan de publication : mono-dépôt `contoso/shop` | Enregistré |
| P3 | Publier la révision 1 | Préparation « 1 dépôt » ; pull request dans Gitea, description lisible |
| P4 | Fusionner dans Gitea ; modifier à la main `orders/infra/main.bicep` sur `main` | — |
| P5 | Rétention prd de `log main` 90 → 120, générer la révision 2, publier | Diff de la modification manuelle, confirmation exigée ; la pull request ne touche que les fichiers concernés |
| P6 | Couper l'API (tableau de bord) pendant l'écriture, la relancer, republier | Ni second commit ni seconde pull request |
| P7 | Paramètres du projet | Code du projet verrouillé, avec l'explication |
| P8 | Répéter P1–P3 avec votre Azure DevOps de test (connexion par jeton de repli expirant dans 10 jours) | Pull request dans Azure Repos ; avertissement d'expiration ; e-mail d'alerte |

## § Livraison et pilote (verrou R-08) — une demi-journée

**État de départ** : IFS `dev` hébergé (J0-31), votre organisation Azure DevOps de test, abonnements de test vides.

| # | Action | Attendu |
|---|---|---|
| L1 | Créer votre organisation dans l'IFS hébergé, connexion Azure DevOps (principal de service), modéliser le projet pilote (ou l'importer quand J3 existera ; ici : le recréer), générer, publier | Pull request dans votre dépôt `shop` |
| L2 | Fusionner ; suivre la liste de contrôle : script Azure, pipeline d'installation, `MAIN_PAYMENTS_API_KEY` | Liste de contrôle cochée au fur et à mesure (automatique, fait, vérifié) |
| L3 | Laisser les pipelines s'exécuter ; approuver prd après lecture de l'aperçu | Matrice : « révision 1 déployée » partout, sans action de votre part |
| L4 | Pousser l'application témoin (`src/api`) | CI, image par empreinte, déploiement dev puis prd ; `/health` 2xx ; `/health/dependencies` tout vert |
| L5 | Rejouer les preuves P2, P3, P4, P5, P7, P8, P9 de [`01-preuves.md`](01-preuves.md), **avec la sortie générée par IFS** | Mêmes résultats qu'en phase P ; le suivi d'IFS affiche les états (partiellement appliquée, en attente d'approbation…) et la fiche d'opération |
| L6 | Inventaire des ressources détachées après P4 | `id-shop-extra-prd`, avec la commande de suppression |
| L7 | Chronométrer L1 à L4 (temps actif, hors attente des droits) | < 1 jour ([00 § 8](../../specs/00-vision.md)) — à consigner |
| L8 | Interface en anglais (Profil → langue) | Tous les textes traduits |
