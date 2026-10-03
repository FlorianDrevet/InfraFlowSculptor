# 04 — Périmètre et lots

Le lot 1 est le plus petit produit qui tient la promesse de [00 § 4](00-vision.md) : un projet de
taille courante, du modèle au premier déploiement réussi, sans retouche manuelle. Tout ce qui n'y
contribue pas directement est repoussé.

## 1. Matrice de support

La cible est **toujours Azure** ([DEC-43](03-decisions.md)). Les langages et plateformes arrivent par lot :

| | Azure DevOps Pipelines | GitHub Actions | GitLab CI |
|---|---|---|---|
| **Bicep** | Lot 1 | Lot 1 | Lot 3 |
| **Terraform** | Lot 2 | Lot 2 | Lot 3 |
| **Pulumi (TypeScript)** | Lot 3 | Lot 3 | Lot 3 |

| Fournisseur git | Lot |
|---|---|
| GitHub | 1 |
| Azure Repos | 1 |
| GitLab | 3 |

**Pourquoi cet ordre.**
- Deux plateformes CI dès le lot 1 obligent l'abstraction des pipelines à être réelle dès le départ.
  GitHub Actions est aussi la plateforme la plus demandée avec des dépôts GitHub, déjà supportés.
- Terraform au lot 2 couvre la majorité des équipes Azure qui ne sont pas sur Bicep.
- Pulumi et GitLab touchent des publics plus restreints : lot 3.

**Garde-fou du lot 1.** Pour que le plan de déploiement ne soit pas façonné par Bicep
([DEC-44](03-decisions.md)), le lot 1 inclut un **prototype d'émetteur Terraform**, non livré, qui doit
produire depuis le projet de référence un code Terraform déployable. Tout ce que ce prototype ne peut pas
traduire sans décision métier révèle un défaut du plan de déploiement, à corriger dans le lot 1.

## 2. Lot 1 — Premier déploiement réussi

| Domaine | Contenu | Document |
|---|---|---|
| Accès | Organisations, membres, invitations, rôles d'organisation et de projet, jetons d'API, journal d'audit | [10](10-organisations-et-acces.md) |
| Projet | Assistant de création (langage, plateforme), environnements (protégés, approbateurs), tags, tags système | [11](11-projets-et-environnements.md) |
| Composants | Modes `PerEnvironment` et `Single`, groupes de ressources, ordre de déploiement, duplication | [12](12-composants-et-groupes-de-ressources.md) |
| Nommage | Gabarits à deux niveaux, assainissement, unicité, nom forcé, explication du nom | [13](13-nommage.md) |
| Ressources | Propriétés et surcharges, présence, ressources existantes, enfants, suppression avec impact | [14](14-modele-des-ressources.md) |
| Catalogue | 16 types du lot 1 ([15 § 3](15-catalogue.md)), descripteurs avec prise en charge par langage | [15](15-catalogue.md) |
| Câblage | Liaisons, câblage implicite, RBAC, accès aux données SQL et PostgreSQL, journalisation par défaut | [16](16-liaisons-identites-et-acces.md) |
| Paramètres | Variables d'environnement, clés App Configuration, secrets Key Vault, secrets de pipeline, import depuis fichier | [17](17-parametres-applicatifs-et-secrets.md) |
| Réseau | Exposition publique et publique restreinte (liste d'adresses IP) | [18](18-reseau-et-exposition.md) |
| Applications | Build conteneur et code, promotion d'image, points d'extension, contrôle de santé | [19](19-applications-build-et-deploiement.md) |
| Validation | Moteur et règles du lot 1 | [20](20-validation.md) |
| Génération | Révisions, plan de déploiement, émetteur Bicep (AVM, piles de déploiement), diff entre révisions | [21](21-generation-et-revisions.md) |
| Pipelines | Émetteurs Azure DevOps et GitHub Actions : infrastructure (PR, CI, release avec aperçu) et applications | [22](22-pipelines.md) |
| Installation | Script Azure, pipeline d'installation Azure DevOps, script GitHub, liste de contrôle | [23](23-kit-installation.md) |
| Publication | Connexions GitHub et Azure DevOps, plan de publication, manifeste, pull requests | [24](24-depots-et-publication.md) |
| Agent IA | Serveur MCP complet, propositions de modification | [25](25-agent-ia-mcp.md) |
| Interface | Écrans du lot 1, vue graphe en lecture, recherche globale | [26](26-interface.md) |

## 3. Lot 2 — Production d'entreprise et Terraform

| Contenu | Document |
|---|---|
| **Émetteur Terraform** (AVM Terraform, `azurerm`/`azapi`, état dans Azure Storage, blocs `removed`) | [21](21-generation-et-revisions.md), [22](22-pipelines.md), [23](23-kit-installation.md) |
| **Migration assistée de langage** (Bicep ↔ Terraform), changement de plateforme CI | [DEC-48](03-decisions.md) |
| Réseau privé : VNet, subnets, NSG, zones DNS privées, points de terminaison privés, intégration sortante | [18](18-reseau-et-exposition.md) |
| Domaines personnalisés avec vérification au déploiement | [18](18-reseau-et-exposition.md) |
| Types : Cosmos DB, Event Hubs, Azure Managed Redis, Azure AI Services | [15](15-catalogue.md) |
| Portées RBAC sur les enfants (conteneur blob, file, topic) | [16](16-liaisons-identites-et-acces.md) |
| Connexion Azure facultative en lecture : disponibilité des noms par l'API Azure, sélection des ressources existantes | [13](13-nommage.md), [14](14-modele-des-ressources.md) |
| Configuration des environnements GitHub par l'application GitHub d'IFS | [23](23-kit-installation.md) |
| Slots de déploiement App Service | [19](19-applications-build-et-deploiement.md) |
| Suivi de l'état des pull requests publiées | [24](24-depots-et-publication.md) |
| Vue graphe éditable (création de liaisons par glisser-déposer) | [26](26-interface.md) |

## 4. Lot 3 — Extension

| Contenu |
|---|
| **Émetteur Pulumi TypeScript** (Azure Native, état dans Azure Storage ou Pulumi Cloud) |
| **GitLab** : dépôts et émetteur GitLab CI |
| Import depuis un groupe de ressources Azure, avec écran de revue ([DEC-32](03-decisions.md)) |
| Types : Front Door, API Management, Static Web Apps, Container Instances |

## 5. Hors périmètre (décidé)

- **Autres clouds** (AWS, GCP) et tout modèle multi-cloud.
- Déploiement exécuté par IFS lui-même.
- Lecture de l'état réel Azure ou des états Terraform/Pulumi pour détecter la dérive.
- Multi-région (plusieurs régions pour un même environnement, reprise sur sinistre active).
- Rôles Azure personnalisés.
- Édition collaborative en temps réel.
- Gestion des valeurs de secrets par IFS.

## 6. Points ouverts

Ces points n'ont pas encore de décision. Ils n'empêchent pas de démarrer le lot 1.

| # | Question | Échéance |
|---|---|---|
| PO-01 | Modèle commercial : plans, limites (nombre de projets, de ressources), essai gratuit. | Avant ouverture commerciale |
| PO-02 | Politique de montée de version des modules et fournisseurs : automatique à chaque version du catalogue, ou choisie par projet ? | Avant fin du lot 1 |
| PO-03 | Instance dédiée (hébergée chez un client) : demande réelle, et à quel prix ? | Après les premiers clients |
| PO-04 | Multi-région : quel modèle (environnement multi-région, ou composant décliné par région) ? | Lot 3 |
| PO-05 | Faut-il un rôle de projet « Publicateur » (peut publier sans modifier le modèle) ? | Retours des premiers clients |
| PO-06 | Les attributions de rôle à portée externe (autre abonnement, [RG-LIA-17](16-liaisons-identites-et-acces.md)) peuvent-elles être gérées par la pile de déploiement Bicep du composant, ou faut-il un déploiement séparé ? À vérifier par un prototype sur le projet de référence. Terraform et Pulumi le permettent par fournisseur avec alias. | Début du lot 1 |
| PO-07 | Mélanger les langages par composant dans un même projet (exemple : socle en Terraform, services en Bicep) ? Techniquement possible, puisque les références entre composants passent par les noms calculés ; à décider sur demande réelle. | Après le lot 2 |
| PO-08 | Compatibilité OpenTofu de la sortie Terraform : garantie testée, ou simple meilleur effort ? | Lot 2 |
| PO-09 | Pulumi : autres langages que TypeScript (C#, Python) ? | Après le lot 3 |
