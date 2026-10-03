# 28 — Suivi des déploiements

## 1. Objectif

Savoir, sans accès à Azure, ce qui est réellement déployé : quelle révision tourne dans chaque cible,
quel déploiement attend une approbation, lequel a échoué. Fermer la boucle entre le modèle et la
réalité, et rendre mesurable la promesse « premier déploiement réussi » ([DEC-55](03-decisions.md)).

## 2. Source des informations

**RG-SUI-01 — Lecture des plateformes CI.** IFS lit les exécutions des pipelines qu'il gère par les
connexions de l'organisation :

| Plateforme | Accès | Rafraîchissement |
|---|---|---|
| GitHub Actions | Application GitHub d'IFS, permission de lecture sur Actions | Événements `workflow_run` et `deployment_status` reçus en temps réel ; relecture toutes les 15 minutes en filet de sécurité |
| Azure DevOps | Principal de service d'IFS, lecture des builds et des environnements | Lecture toutes les 2 minutes tant qu'une exécution est en cours, toutes les 15 minutes sinon |
| GitLab CI *(lot 3)* | Connexion GitLab | Webhooks de pipeline |

Aucun accès aux abonnements Azure n'est nécessaire.

**RG-SUI-02 — Corrélation.** La preuve qui relie une exécution à une révision est le **rapport de release**
(`ifs-report.json`, [DEC-91](03-decisions.md)) : révision, commit, empreinte du manifeste, cible, étapes
terminées. IFS ne déduit jamais la révision du seul nom de l'exécution ; un commit de fusion différent de
celui de la pull request est rattaché par l'empreinte du manifeste. Chaque pipeline généré porte en plus,
dans son nom d'exécution et ses variables :
- le projet ;
- le composant (et l'application) ;
- le numéro de révision ;
- pour chaque stage, la cible.

IFS relie ainsi chaque exécution à une révision et à une cible sans ambiguïté.

**RG-SUI-03 — Exécutions hors révision.** Une exécution d'un pipeline géré sur un commit qui ne
correspond à aucune révision publiée (fichiers modifiés à la main, branche non IFS) est affichée avec la
mention « hors révision ».

## 3. Ce qu'IFS affiche

### 3.1 Par composant et par cible (infrastructure)

| Information | Contenu |
|---|---|
| État | **Déployée** (toutes les étapes terminées), **partiellement appliquée** (le déploiement a modifié Azure, une étape suivante a échoué : révocations, secrets, clés, accès aux données ou domaines), **en échec avant modification**, **inconnu** (information indisponible). |
| Révision déployée | Dernière révision dont toutes les étapes ont réussi, avec date et lien vers l'exécution. |
| Dernier essai | Révision, étapes terminées, étape en échec, date, lien. Pour un état partiel : ce qui est appliqué, ce qui reste, et la reprise (relancer la release, idempotente). |
| En cours | Exécution en cours ou en attente d'approbation : depuis quand, approbateurs attendus. |
| Écart | « À jour », « révision <n> publiée, non fusionnée », « fusionnée, non déployée », « en retard de <k> révisions ». |
| Fraîcheur | Date de la dernière lecture réussie de la plateforme CI. Au-delà de 30 minutes sans lecture, l'état devient « inconnu » au lieu de rester affiché comme sûr. |

### 3.2 Par application et par environnement

Version livrée (numéro de build et commit, ou tag d'image), date, statut du contrôle de santé, lien.

### 3.3 Vue d'ensemble du projet

Une matrice composants × cibles colorée par état : à jour, en retard, en attente d'approbation, en
échec, jamais déployé. C'est l'écran de pilotage du responsable des déploiements.

## 4. Effets

**RG-SUI-04 — Liste de contrôle.** Le premier déploiement réussi d'un composant dans une cible coche
automatiquement l'étape correspondante de la liste de contrôle ([23 § 4](23-kit-installation.md)).

**RG-SUI-05 — Ressources détachées.** Les ressources que le rapport d'une release déclare détachées entrent
dans l'inventaire des ressources détachées de la cible ([DEC-61](03-decisions.md), section 5), même si la
release a échoué ensuite. Les accès révoqués sont listés dans l'historique de la cible, pas dans
l'inventaire.

**RG-SUI-06 — Notifications.** Échec d'un déploiement, approbation en attente depuis plus de 24 heures,
cible protégée en retard de plus de 3 révisions ([26 § 4](26-interface.md)).

**RG-SUI-07 — Indicateurs.** Pour chaque projet :
- délai entre la création du projet et le premier déploiement réussi ;
- délai entre la publication d'une révision et son déploiement en production ;
- taux de réussite des déploiements.

Pour IFS, les mêmes indicateurs agrégés et anonymisés mesurent la promesse de [00 § 8](00-vision.md).

**RG-SUI-09 — Dérive.** Les résultats du pipeline de dérive ([33 § 5](33-gouvernance-couts-et-supervision.md))
apparaissent dans la matrice (état « dérive ») et dans le détail de chaque composant et cible.

**RG-SUI-10 — Version précédente.** Pour chaque application et environnement, IFS garde la version en
service et la précédente, et affiche l'action de retour arrière propre à la stratégie de déploiement
([UC-APP-03](19-applications-build-et-deploiement.md)).

**RG-SUI-08 — Pas d'action.** IFS n'exécute, n'annule ni n'approuve aucun pipeline. Les liens ouvrent la
plateforme CI, où se font ces actions.

## 5. Inventaire des ressources détachées

| Champ | Contenu |
|---|---|
| Ressource | Nom Azure, type, groupe de ressources, abonnement, cible. |
| Origine | Révision qui l'a détachée, date du déploiement, auteur du retrait. |
| Commande | Commande Azure CLI de suppression, prête à copier. |
| État | `Détachée`, `Supprimée` (déclaré par l'utilisateur), `Conservée` (avec un commentaire). |

**UC-SUI-01 — Traiter une ressource détachée** (`installation.gerer`) : la marquer supprimée ou conservée.
Une ressource réintroduite dans le modèle sous le même nom sort de l'inventaire et la révision suivante
la reprend.

## 6. Cas d'utilisation

- **UC-SUI-02** — Voir l'état des déploiements d'un projet (`projet.lire`).
- **UC-SUI-03** — Voir l'historique des déploiements d'un composant ou d'une application, filtrable par
  cible et par statut.
- **UC-SUI-04** — Depuis une révision, voir où elle est déployée.
