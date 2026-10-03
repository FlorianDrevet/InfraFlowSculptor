# 04 — Périmètre et lots

La priorité absolue est **Bicep + Azure DevOps** ([DEC-62](03-decisions.md)). Le lot 1 est la première
version commerciale : un projet de taille courante, du modèle au premier déploiement réussi puis à sa
maintenance, sans retouche manuelle ([00 § 4](00-vision.md)). Il commence par un **pilote** (jalon 0) qui
prouve la promesse avec quelques équipes avant d'élargir ([DEC-93](03-decisions.md)). Les lots suivants
élargissent sans jamais revenir sur ce socle.

## 1. Matrices de support

La cible est **toujours Azure** ([DEC-43](03-decisions.md)).

| Langage d'infrastructure | Lot |
|---|---|
| Bicep | 1 |
| Terraform | 3 |
| OpenTofu | 4 |
| Pulumi : TypeScript, C#, Python, Go, Java, YAML | 4 |

| Plateforme CI | Lot |
|---|---|
| Azure DevOps Pipelines | 1 |
| GitHub Actions | 2 |
| GitLab CI | 3 |

| Fournisseur git | Lot |
|---|---|
| Azure Repos, GitHub | 1 |
| GitLab | 3 |

| Source des modules ([DEC-54](03-decisions.md)) | Lot |
|---|---|
| AVM registre public (Bicep) | 1 |
| AVM embarqués, modules IFS (Bicep) | 2 |
| Mêmes sources pour Terraform, modules du client | 3 |
| OpenTofu, Pulumi (ressources directes, composants IFS) | 4 |

**Garde-fou du lot 1.** Pour que le plan de déploiement ne soit façonné ni par Bicep ni par Azure DevOps
([DEC-44](03-decisions.md)), le lot 1 inclut deux **prototypes non livrés** : un émetteur Terraform et un
émetteur GitHub Actions, qui doivent produire une sortie déployable pour un **sous-ensemble représentatif**
du projet de référence (un composant `PerEnvironment` et un `Single`, une liaison d'accès entre composants,
un secret de pipeline, un accès aux données, un retrait) ([DEC-93](03-decisions.md)). Tout ce qu'ils ne
peuvent pas traduire sans décision métier révèle un défaut du plan de déploiement, à corriger dans le
lot 1.

## 2. Lot 1 — Premier déploiement réussi (Bicep, Azure DevOps)

### 2.0 Jalon 0 — Pilote

Objectif : prouver, avec 3 à 5 équipes pilotes qualifiées qui créent un nouveau service Azure, qu'une
équipe crée **puis fait évoluer** un service conforme à ses conventions, avec moins de travail manuel et
sans retouche des fichiers gérés ([DEC-93](03-decisions.md)).

| Dimension | Périmètre du pilote |
|---|---|
| Outils | Bicep, Azure DevOps, Azure Repos ; AVM du registre public, épinglés |
| Cibles | Un tenant, deux environnements `dev` et `prd`, approbation en `prd`, exposition publique ou restreinte |
| Dépôts | Un dépôt, publication par pull request uniquement |
| Modèle | Un composant socle et un composant applicatif `PerEnvironment` ; conventions, surcharges, présence, ressources existantes |
| Catalogue | Log Analytics, Application Insights, Key Vault, identité managée, registre de conteneurs, environnement Container Apps, Container App ; Azure SQL si un pilote en a besoin |
| Application | Un conteneur témoin : identité managée, secret Key Vault, contrôle de santé qui vérifie ses dépendances, image identifiée par empreinte |
| Collaboration | Propriétaire, contributeur, lecteur ; audit ; un éditeur coordinateur par projet |
| Livraison | Validation, aperçu puis approbation, kit d'installation, suivi par cible avec états partiels, reprise, révocation effective |
| Sortie | Archive du code et export du modèle ; déploiements autonomes si IFS est indisponible |

**Séquence de démonstration**, mesurée à chaque étape (temps actif, interventions de l'équipe plateforme,
retouches manuelles) : créer le service → livrer une première image → changer un paramètre → ajouter un
accès → retirer cet accès et constater sa révocation → faire échouer une étape d'écriture de plan de
données → relancer la release → relivrer une image précédente.

**Condition de sortie** : les preuves P1 à P4 (section 7) sont obtenues, et les mesures du pilote
justifient l'élargissement. Si les pilotes exigent le réseau privé, des modules internes ou l'import, le
segment visé change avant d'élargir.

### 2.1 Jalon 1 — Tranche verticale

Objectif : le projet de référence ([90](90-projet-de-reference.md)) déployé de bout en bout.

| Domaine | Contenu | Document |
|---|---|---|
| Accès | Organisation, invitations, rôles prédéfinis (portée projet), jetons d'API, journal d'audit (écriture, consultation simple) | [10](10-organisations-et-acces.md) |
| Projet | Assistant, environnements protégés et approbateurs, tags, tags système, préréglages de nommage | [11](11-projets-et-environnements.md), [13](13-nommage.md) |
| Modèle | Composants `PerEnvironment` et `Single`, ressources, surcharges, présence, ressources existantes, liaisons, câblage implicite, accès aux données, paramètres applicatifs, secrets de pipeline, exposition publique et restreinte | [12](12-composants-et-groupes-de-ressources.md) à [18](18-reseau-et-exposition.md) |
| Historique | Jeux de modifications, auteur par objet et par propriété | [31](31-historique-et-versions.md) |
| Catalogue | Les 11 types du projet de référence | [15](15-catalogue.md) |
| Applications | Build conteneur et code, scan d'image, points d'extension, contrôle de santé, stratégie directe | [19](19-applications-build-et-deploiement.md) |
| Production | Validation, révisions, plan de déploiement, émetteur Bicep (AVM), émetteur Azure DevOps, kit d'installation, publication par pull request | [20](20-validation.md) à [24](24-depots-et-publication.md) |
| Suivi | Suivi des déploiements, inventaire des ressources détachées | [28](28-suivi-des-deploiements.md) |
| Exploitation | Cycle de publication du catalogue, accès support consenti, suspension d'organisation | [40](40-exploitation-ifs.md) |

### 2.2 Jalon 2 — Largeur

| Contenu | Document |
|---|---|
| Les 6 autres types du lot 1 : PostgreSQL, Web App, Function App, plan App Service, stockage, Azure Managed Redis | [15](15-catalogue.md) |
| Authentification locale déconseillée et mots de passe générés | [DEC-51](03-decisions.md), [17 § 7](17-parametres-applicatifs-et-secrets.md) |
| Équipes, portée des rôles par composant, abonnement par composant et environnement | [10](10-organisations-et-acces.md), [12](12-composants-et-groupes-de-ressources.md) |
| Versions étiquetées, consultation à une version, comparaison, annulation, restauration, **brouillons** ([DEC-93](03-decisions.md)) | [31](31-historique-et-versions.md) |
| Import de paramètres depuis un fichier, comparaison de révisions | [17](17-parametres-applicatifs-et-secrets.md), [21](21-generation-et-revisions.md) |
| Comptes Microsoft personnels | [DEC-59](03-decisions.md) |

### 2.3 Jalon 3 — Outillage

| Contenu | Document |
|---|---|
| Serveur MCP complet, propositions de modification | [25](25-agent-ia-mcp.md) |
| Vue graphe en lecture, recherche globale, notifications | [26](26-interface.md) |
| Journal d'audit complet (filtres, export) | [10 § 8](10-organisations-et-acces.md) |
| Export et import d'un projet (JSON), convertisseur depuis la v0 | [11 § 6](11-projets-et-environnements.md), [DEC-84](03-decisions.md) |
| Documentation d'architecture générée | [DEC-75](03-decisions.md) |
| Plans Découverte et Équipe, mode découverte | [DEC-78](03-decisions.md) |

## 3. Lot 2 — Production d'entreprise (Bicep, Azure DevOps, GitHub Actions)

Le lot 2 est livré en vagues, dans cet ordre.

| Vague | Contenu | Document |
|---|---|---|
| **A — Réseau privé** | VNet, subnets, appairages hub and spoke, NSG, tables de routage, passerelle NAT, zones DNS privées et publiques, points de terminaison privés, intégration sortante, exécuteurs privés (Managed DevOps Pools, runners GitHub en réseau privé), domaines personnalisés | [18](18-reseau-et-exposition.md) |
| **B — IA** | Microsoft Foundry (compte, projets, déploiements de modèles, connexions, agents en configuration standard), AI Search, Cosmos DB | [32](32-ia-et-foundry.md) |
| **C — Livraison** | Stratégies de déploiement (slot et bascule, bleu/vert, progressive, mise à jour progressive Flex), catalogue d'étapes (.NET, Node/Angular, Python, Java), détection depuis le dépôt, fenêtres de déploiement, Static Web App et son parcours applicatif | [19](19-applications-build-et-deploiement.md) |
| **D — GitHub** | Émetteur GitHub Actions, partie GitHub du kit, suivi des déploiements GitHub, configuration des environnements GitHub par l'application | [22](22-pipelines.md), [23](23-kit-installation.md) |
| **E — Gouvernance et exploitation** | Politiques d'organisation, estimation des coûts, budgets, supervision (alertes recommandées), contrôle de dérive, plan Entreprise (rôles personnalisés, équipes synchronisées avec Entra, publication à deux personnes, politique de liaisons entrantes) | [33](33-gouvernance-couts-et-supervision.md), [10](10-organisations-et-acces.md) |
| **F — Exposition et intégration** | Front Door et WAF, Application Gateway et WAF, API Management, Event Grid, Event Hubs, Container Apps Jobs ; portées RBAC sur les enfants | [15](15-catalogue.md), [16](16-liaisons-identites-et-acces.md) |
| **G — Collaboration et confort** | Commentaires et mentions, modèles de projet et de composant, sources de modules (AVM embarqués, modules IFS), renouvellement des mots de passe, connexion Azure en lecture (disponibilité des noms, sélection des ressources existantes, quotas de modèles), vue graphe éditable, webhooks et notifications Teams/Slack | [31](31-historique-et-versions.md), [11](11-projets-et-environnements.md), [21](21-generation-et-revisions.md) |

## 4. Lot 3 — Ouverture

| Contenu | Document |
|---|---|
| **Émetteur Terraform** et migration assistée Bicep ↔ Terraform | [21](21-generation-et-revisions.md), [DEC-48](03-decisions.md) |
| **GitLab** : dépôts et émetteur GitLab CI | [22](22-pipelines.md), [24](24-depots-et-publication.md) |
| **Import** depuis ARM, Bicep ou un groupe de ressources Azure | [29](29-import.md) |
| Modules du client avec contrat de correspondance | [21 § 6.5](21-generation-et-revisions.md) |
| Catalogue d'étapes PHP et Go | [19 § 6](19-applications-build-et-deploiement.md) |
| Types complémentaires : MySQL, SignalR, Web PubSub, Communication Services, Logic Apps Standard, Container Instances, Azure Firewall, DNS Private Resolver, Bastion, Managed Grafana | [15](15-catalogue.md) |

## 5. Lot 4 — Langages complémentaires et grande échelle

| Contenu | Document |
|---|---|
| **OpenTofu** | [DEC-63](03-decisions.md) |
| **Pulumi** en TypeScript, C#, Python, Go, Java et YAML | [DEC-63](03-decisions.md) |
| Multi-région | [DEC-80](03-decisions.md) |
| AKS (cluster) | [DEC-66](03-decisions.md) |
| Instance dédiée (plan Entreprise) | [DEC-79](03-decisions.md) |

## 6. Hors périmètre (décidé)

- **Autres clouds** (AWS, GCP) et tout modèle multi-cloud.
- Déploiement exécuté par IFS lui-même ; approbation, annulation ou correction automatique depuis IFS.
- Lecture directe de l'état réel Azure ou des états Terraform/Pulumi par IFS (la dérive passe par un
  pipeline du client, [DEC-74](03-decisions.md)).
- Déploiement d'applications dans AKS (Helm, manifestes Kubernetes).
- Rôles Azure personnalisés.
- Édition collaborative en temps réel.
- Gestion des valeurs de secrets par IFS.
- Version installable par le client ([DEC-79](03-decisions.md)).
- Environnements éphémères par pull request pour toute l'infrastructure (seules les préversions natives
  de Static Web Apps sont proposées) : le coût et la durée de création d'une infrastructure complète par
  pull request ne sont pas raisonnables pour la cible du produit.

## 7. Décisions prises et preuves à obtenir

### 7.1 Anciens points ouverts : tous tranchés

| Point | Décision |
|---|---|
| PO-01 Modèle commercial | [DEC-78](03-decisions.md) |
| PO-02 Montée de version du catalogue | [DEC-57](03-decisions.md) |
| PO-03 Instance dédiée | [DEC-79](03-decisions.md) |
| PO-04 Multi-région | [DEC-80](03-decisions.md) |
| PO-05 Rôle « Publicateur » | [DEC-52](03-decisions.md) |
| PO-06 Attributions à portée externe | [DEC-81](03-decisions.md) |
| PO-07 Plusieurs langages par projet | [DEC-82](03-decisions.md) |
| PO-08 OpenTofu | [DEC-63](03-decisions.md) |
| PO-09 Langages Pulumi | [DEC-63](03-decisions.md) |
| PO-10 Télémétrie produit | [DEC-83](03-decisions.md) |

Il ne reste **aucune question sans réponse** dans la spec. Mais une décision documentée clôt un débat,
elle ne prouve pas que le comportement est réalisable ([DEC-97](03-decisions.md)). Les garanties critiques
portent donc un statut : **décidée**, **spécifiée**, **prototypée**, **vérifiée**.

### 7.2 Registre des preuves

Prototypes à réussir avant de développer le pilote, sur les outils retenus (Bicep, Azure DevOps, Azure
Repos), depuis des abonnements de test vides :

| Preuve | Ce qui doit être démontré | Statut |
|---|---|---|
| **P1 — Création depuis zéro** | Kit, puis releases de chaque composant dans l'ordre : succès en une seule exécution par composant, y compris coffre, droits, secret de pipeline, application et accès aux données ([DEC-86](03-decisions.md), [DEC-90](03-decisions.md)) | Spécifiée |
| **P2 — Révocation en production** | Retrait d'une liaison d'accès en cible protégée : l'attribution est supprimée, l'application perd l'accès après propagation, la ressource et ses données restent ([DEC-85](03-decisions.md)) | Spécifiée |
| **P3 — Aperçu puis approbation** | L'approbateur lit l'aperçu avant le stage protégé ; une modification concurrente entre aperçu et déploiement arrête la release sans rien modifier ([DEC-87](03-decisions.md)) | Spécifiée |
| **P4 — Cible en retard** | Une cible qui saute une révision retire bien la ressource retirée entre-temps, et l'inventaire le reflète ([DEC-91](03-decisions.md)) | Spécifiée |
| P5 — Échec partiel et reprise | Une étape d'écriture de plan de données échoue : l'état « partiellement appliquée » s'affiche, la relance termine sans doublon | Spécifiée |
| P6 — Neutralité du plan | Les prototypes Terraform et GitHub Actions traduisent le sous-ensemble représentatif (section 1) | Spécifiée |

Les valeurs du catalogue (SKU, versions, longueurs de noms, codes de région, rôles) restent revérifiées
contre la documentation Microsoft au moment d'implémenter chaque type ([40 § 3](40-exploitation-ifs.md)) :
c'est une tâche de réalisation, pas une décision.
