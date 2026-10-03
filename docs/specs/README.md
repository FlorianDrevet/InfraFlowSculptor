# Spécifications fonctionnelles — InfraFlowSculptor v1

> **Statut : v1 cible (2026-10-03).** Ces documents décrivent le produit **voulu**, pas un existant.
> Ils remplacent la rétro-spécification v0 de l'ancien dépôt `infra-pipeline-editor`, dont chaque
> écart est traité dans l'annexe [A1](A1-traitement-des-ecarts-v0.md).
>
> Ils décrivent ce que l'utilisateur peut faire, les données manipulées, les règles appliquées et ce
> qui est produit. Ils ne décrivent pas l'implémentation (langages, couches, classes), sauf quand une
> contrainte technique change le comportement visible.

## Comment lire

1. [00 — Vision](00-vision.md) : pourquoi le produit existe, pour qui, contre quoi.
2. [01 — Principes](01-principes.md) : les onze règles qui tranchent les cas non prévus.
3. [02 — Glossaire](02-glossaire.md) : le vocabulaire, à respecter partout (écran, API, code).
4. [03 — Décisions](03-decisions.md) : le journal des choix structurants et leur raison.
5. [05 — Modèle de données](05-modele-de-donnees.md) et [06 — Parcours](06-parcours-utilisateur.md) : la vue d'ensemble.
6. Les domaines (10 à 33), dans l'ordre du parcours utilisateur, puis l'exploitation (40).
7. [90 — Projet de référence](90-projet-de-reference.md) : un exemple complet avec la sortie attendue.
   Il sert de test d'acceptation.

## Sommaire

| # | Document | Contenu |
|---|---|---|
| 00 | [Vision](00-vision.md) | Problème, personas, promesse, alternatives, indicateurs |
| 01 | [Principes](01-principes.md) | Règles de conception non négociables |
| 02 | [Glossaire](02-glossaire.md) | Termes, noms techniques, termes abandonnés |
| 03 | [Décisions](03-decisions.md) | Journal des décisions DEC-01 à DEC-84 |
| 04 | [Périmètre et lots](04-perimetre-et-lots.md) | Matrices de support, lot 1 en trois jalons, lots 2 à 4, hors périmètre, points ouverts tous tranchés |
| 05 | [Modèle de données](05-modele-de-donnees.md) | Entités, relations, invariants transverses |
| 06 | [Parcours utilisateur](06-parcours-utilisateur.md) | Parcours de bout en bout : premier déploiement, nouveau service, changement de paramètre… |
| 10 | [Organisations et accès](10-organisations-et-acces.md) | Organisation, équipes, permissions, rôles et portées, invitations, jetons d'API, journal d'audit, cycle de vie des comptes |
| 11 | [Projets et environnements](11-projets-et-environnements.md) | Projet, création, outils, environnements, tags, export et modèles |
| 12 | [Composants et groupes de ressources](12-composants-et-groupes-de-ressources.md) | Composant, mode de déploiement, groupes de ressources, ordre entre composants |
| 13 | [Nommage](13-nommage.md) | Gabarits, jetons, assainissement, unicité, disponibilité |
| 14 | [Modèle des ressources](14-modele-des-ressources.md) | Propriétés, surcharges, présence, ressources existantes, cycle de vie |
| 15 | [Catalogue](15-catalogue.md) | Descripteurs de types et types disponibles par lot |
| 16 | [Liaisons, identités et accès](16-liaisons-identites-et-acces.md) | Graphe de liaisons, câblage implicite, RBAC, accès aux données |
| 17 | [Paramètres applicatifs et secrets](17-parametres-applicatifs-et-secrets.md) | Variables d'environnement, clés App Configuration, secrets |
| 18 | [Réseau et exposition](18-reseau-et-exposition.md) | Exposition, VNet et subnets, hub and spoke, appairage, NAT, routage, NSG, points de terminaison privés, DNS privé et public, exécuteurs privés, domaines |
| 19 | [Applications : build, qualité et déploiement](19-applications-build-et-deploiement.md) | Qui fait quoi (infra / application), build, catalogue d'étapes, promotion d'image, points d'extension |
| 20 | [Validation](20-validation.md) | Moteur de règles, gravités, catalogue des règles |
| 21 | [Génération et révisions](21-generation-et-revisions.md) | Révision immuable, plan de déploiement, émetteurs, conventions Bicep / Terraform / Pulumi |
| 22 | [Pipelines](22-pipelines.md) | Pipelines d'infrastructure et applicatifs ; traduction Azure DevOps / GitHub Actions / GitLab CI |
| 23 | [Kit d'installation](23-kit-installation.md) | Script Azure commun, partie propre à chaque plateforme, liste de contrôle |
| 24 | [Dépôts et publication](24-depots-et-publication.md) | Connexions git, plan de publication, manifeste, pull requests |
| 25 | [Agent IA (MCP)](25-agent-ia-mcp.md) | Serveur MCP, propositions de modification |
| 26 | [Interface](26-interface.md) | Écrans, règles d'ergonomie, notifications |
| 27 | [Exigences non fonctionnelles](27-exigences-non-fonctionnelles.md) | Sécurité, isolation, performance, disponibilité, conformité, parité |
| 28 | [Suivi des déploiements](28-suivi-des-deploiements.md) | Révision déployée par cible, échecs, approbations, ressources détachées |
| 29 | [Import](29-import.md) | Reprise d'une infrastructure ARM, Bicep ou Azure (lot 3) |
| 31 | [Historique et versions](31-historique-et-versions.md) | Jeux de modifications, qui a modifié quoi, versions, comparaison, restauration, brouillons |
| 32 | [IA et Foundry](32-ia-et-foundry.md) | Comptes et projets Foundry, déploiements de modèles, AI Search, agents |
| 33 | [Gouvernance, coûts et supervision](33-gouvernance-couts-et-supervision.md) | Politiques d'organisation, coûts, budgets, alertes, dérive |
| 90 | [Projet de référence](90-projet-de-reference.md) | Exemple complet et sortie attendue, par variante |
| 40 | [Exploitation d'IFS](40-exploitation-ifs.md) | Back-office : catalogue, accès support, organisations, incidents |
| A1 | [Traitement des écarts v0](A1-traitement-des-ecarts-v0.md) | Chaque écart de l'ancienne version et sa résolution |

## Conventions

### Identifiants

| Préfixe | Sens | Exemple |
|---|---|---|
| `DEC-nn` | Décision structurante (voir [03](03-decisions.md)) | `DEC-06` |
| `RG-<DOM>-nn` | Règle de gestion : toujours vraie, vérifiée côté serveur | `RG-NOM-04` |
| `UC-<DOM>-nn` | Cas d'utilisation : un parcours avec un résultat | `UC-PUB-03` |
| `VAL-<CODE>` | Règle de validation du moteur (voir [20](20-validation.md)) | `VAL-NOM-LONGUEUR` |
| `EXG-nn` | Exigence non fonctionnelle | `EXG-04` |

Codes de domaine : `ORG` organisation et accès, `PRJ` projet, `ENV` environnement, `CMP` composant,
`NOM` nommage, `RES` ressources, `CAT` catalogue, `LIA` liaisons et RBAC, `PAR` paramètres
applicatifs, `NET` réseau, `APP` applications, `VAL` validation, `GEN` génération, `PIP` pipelines,
`INS` installation, `PUB` publication, `MCP` agent IA, `UI` interface, `SUI` suivi des déploiements,
`IMP` import, `DON` modèle de données, `EXP` exploitation, `SEC` sécurité (validation), `HIS` historique,
`IA` intelligence artificielle, `GOV` gouvernance.

### Vocabulaire normatif

- **doit** : obligatoire ; le serveur le garantit.
- **peut** : possibilité offerte à l'utilisateur.
- **par défaut** : valeur proposée, modifiable.
- *(lot 2)*, *(lot 3)* : la fonctionnalité est spécifiée mais livrée plus tard (voir [04](04-perimetre-et-lots.md)).
  Sans mention, elle fait partie du lot 1.

### Règle d'évolution

Une spec ne décrit que ce qui sera livré, avec son lot. Une idée non décidée va dans les points
ouverts de [04](04-perimetre-et-lots.md), jamais dans le corps d'un document. Toute nouvelle décision
structurante ajoute une entrée `DEC-nn` ; on ne réécrit pas une décision, on la remplace par une
nouvelle qui la cite.
