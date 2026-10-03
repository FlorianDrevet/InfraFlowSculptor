# A1 — Traitement des écarts de la v0

La rétro-spécification v0 (ancien dépôt `infra-pipeline-editor`, `docs/specs/90-ecarts.md`) relevait
121 écarts. Cette annexe indique, pour chacun, comment la v1 le traite.

| Statut | Sens |
|---|---|
| **Résolu** | Une décision ou une règle de la v1 traite l'écart. « Résolu » veut dire **spécifié**, pas encore prouvé : les garanties critiques suivent le registre des preuves ([04 § 7.2](04-perimetre-et-lots.md), [DEC-97](03-decisions.md)). |
| **Supprimé** | La fonctionnalité concernée n'existe plus en v1. |
| **Lot 2 / Lot 3** | Traité par une fonctionnalité spécifiée, livrée plus tard. |
| **Hors périmètre** | Écarté volontairement ([04 § 6](04-perimetre-et-lots.md)). |

## Accès (ACC)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-ACC-01 Lectures sans contrôle d'appartenance | Résolu | [RG-ORG-14](10-organisations-et-acces.md), [EXG-01](27-exigences-non-fonctionnelles.md) : contrôle systématique, testé route par route. |
| EC-ACC-02 Liste de tous les utilisateurs | Résolu | [DEC-03](03-decisions.md), [RG-ORG-07](10-organisations-et-acces.md) : annuaire limité à l'organisation. |
| EC-ACC-03 Dernier Owner | Résolu | [RG-ORG-05](10-organisations-et-acces.md), [RG-ORG-10](10-organisations-et-acces.md). |
| EC-ACC-04 Doublon de membre | Résolu | [RG-ORG-04](10-organisations-et-acces.md). |
| EC-ACC-05 Portées inconnues | Résolu | [10 § 6](10-organisations-et-acces.md) : portée inconnue = erreur. |
| EC-ACC-06 Invitation impossible | Résolu | Invitations par e-mail ([10 § 3.2](10-organisations-et-acces.md)). |

## Projet (PRJ)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-PRJ-01 Deux créations, deux règles | Résolu | [DEC-36](03-decisions.md) : un seul assistant. |
| EC-PRJ-02 Jeton non testé à la création | Résolu | Connexions git au niveau organisation, testées à l'ajout ([RG-PUB-01](24-depots-et-publication.md)) ; la création de projet ne saisit plus de jeton. |
| EC-PRJ-03 Renommer un environnement casse des liens | Résolu | [DEC-08](03-decisions.md), [RG-ENV-01](11-projets-et-environnements.md). |
| EC-PRJ-04 Supprimer un environnement laisse des données | Résolu | [UC-ENV-03](11-projets-et-environnements.md) : impact affiché, suppression complète. |
| EC-PRJ-05 Jetons orphelins | Résolu | [DEC-23](03-decisions.md) : plus de jeton par dépôt ; jeton de repli supprimé avec sa connexion. |
| EC-PRJ-06 Défauts de nommage dupliqués | Résolu | [13 § 3](13-nommage.md) : un seul endroit. |
| EC-PRJ-07 Région par défaut | Résolu | Aucune région présaisie : choix explicite obligatoire. |

## Dépôts (DEP)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-DEP-01 Remplacement de jeton isolé sans écran | Supprimé | Connexions git au niveau organisation. |
| EC-DEP-02 Secret fantôme | Supprimé | Idem. |
| EC-DEP-03 Sous-chemin de dépôt jamais honoré | Résolu | [RG-PUB-04](24-depots-et-publication.md) : chemin de base de chaque destination. |
| EC-DEP-04 Droits par niveau incohérents | Résolu | Matrice [10 § 4.2](10-organisations-et-acces.md) ; règle par défaut du plan ([RG-PUB-05](24-depots-et-publication.md)). |
| EC-DEP-05 Fournisseurs annoncés | Résolu | Fournisseurs listés explicitement ; GitLab au lot 3. |
| EC-DEP-06 Validation réelle MultiRepo manquante | Résolu | [EXG-17](27-exigences-non-fonctionnelles.md) : tests d'intégration sur de vrais fournisseurs, trois préréglages. |
| EC-DEP-07 Navigation de dépôt partielle | Résolu | [UC-PUB-05](24-depots-et-publication.md) : navigation dans le dépôt de code de chaque composant. |
| EC-DEP-08 Changement de topologie destructif | Résolu | [RG-PUB-07](24-depots-et-publication.md) : rien n'est supprimé implicitement ; impact affiché. |

## Composants, ex-configurations (CFG)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-CFG-01 Références partiellement câblées | Résolu | [DEC-16](03-decisions.md) : toute liaison peut viser un autre composant. |
| EC-CFG-02 Région libre | Résolu | [DEC-09](03-decisions.md). |
| EC-CFG-03 Renommer ou déplacer un groupe de ressources | Résolu | [RG-CMP-03](12-composants-et-groupes-de-ressources.md) : la ressource suit la région de son groupe. |
| EC-CFG-04 Mode des pipelines applicatifs inatteignable | Supprimé | Mode `Combined` retiré. |
| EC-CFG-05 Paramètres de configuration morts | Supprimé | — |
| EC-CFG-06 Pas d'alias ni d'usage déclaré | Résolu | La liaison porte son type et ses paramètres. |
| EC-CFG-07 Route mal rangée | Supprimé | Le plan de publication est au niveau projet. |
| RG-CFG-01 (v0) Pas d'unicité du nom | Résolu | [DEC-02](03-decisions.md) : code unique par projet. |

## Nommage (NOM)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-NOM-01 Deux calculs différents | Résolu | [DEC-05](03-decisions.md), [DEC-06](03-decisions.md). |
| EC-NOM-02 Vérification DNS au lieu de l'API Azure | Lot 2 | [RG-NOM-09](13-nommage.md) : DNS en `Info` au lot 1, API Azure au lot 2. |
| EC-NOM-03 `{location}` différent entre écran et Bicep | Résolu | Jeton `{region}` unique, codes du catalogue. |
| EC-NOM-04 Nom personnalisé mort | Résolu | [RG-NOM-04](13-nommage.md) : nom forcé par environnement. |
| EC-NOM-05 Contrôles inégaux | Résolu | [RG-NOM-02](13-nommage.md). |
| EC-NOM-06 Le gabarit recommandé ne protège que l'écran | Résolu | [DEC-07](03-decisions.md) : assainissement. |
| EC-NOM-07 Pas de contrôle à la génération | Résolu | `VAL-NOM-LONGUEUR`, `VAL-NOM-COLLISION` bloquants. |
| EC-NOM-08 VirtualNetwork sans abréviation | Résolu | Chaque descripteur a une abréviation et des règles de nom. |

## Ressources (RES)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-RES-01 Noms en double | Résolu | [RG-NOM-07](13-nommage.md), [RG-NOM-08](13-nommage.md). |
| EC-RES-02 Dépendances explicites mortes | Résolu | Remplacées par les liaisons. |
| EC-RES-03 Event Hub sans réglages à l'écran | Résolu | L'écran est généré depuis le descripteur ([P7](01-principes.md)) ; Event Hubs au lot 2. |
| EC-RES-04 Liens d'entrée/sortie morts | Résolu | Remplacés par les liaisons et les sources « sortie ». |
| EC-RES-05 Dépendants incomplets | Résolu | [UC-LIA-02](16-liaisons-identites-et-acces.md) : impact calculé sur tout le graphe. |
| EC-RES-06 Modifications ignorées sans message | Résolu | [RG-RES-10](14-modele-des-ressources.md) : une ressource existante n'a rien à ignorer ; [P3](01-principes.md). |
| EC-RES-07 Trois régions par défaut | Résolu | [DEC-09](03-decisions.md). |

## Catalogue (CAT)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-CAT-01 Politique d'éviction Redis non générée | Résolu | [RG-RES-02](14-modele-des-ressources.md) : toute valeur est générée ; Managed Redis au lot 1, jalon 2. |
| EC-CAT-02 Pas de contrôle d'adressage | Lot 2 | [RG-NET-04](18-reseau-et-exposition.md). |
| EC-CAT-03 Réglages Document Intelligence perdus | Résolu | Descripteur unique ; AI Services au lot 2. |
| EC-CAT-04 Textes libres côté serveur | Résolu | [RG-RES-03](14-modele-des-ressources.md). |
| EC-CAT-05 Valeurs serveur sans écran | Résolu | Une seule source : le descripteur. |
| EC-CAT-06 Pas de sortie pour trois types | Résolu | Sorties définies pour chaque type ([15](15-catalogue.md)), dont `loginServer`. |
| EC-CAT-07 Versions figées | Résolu | [DEC-13](03-decisions.md), [DEC-41](03-decisions.md) : catalogue versionné, dépréciations datées. |

## RBAC (RBA)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-RBA-01 Cible hors projet | Résolu | [RG-LIA-01](16-liaisons-identites-et-acces.md). |
| EC-RBA-02 Source sans identité utile | Résolu | [RG-LIA-07](16-liaisons-identites-et-acces.md). |
| EC-RBA-03 Diagnostic Key Vault strict | Résolu | [RG-LIA-15](16-liaisons-identites-et-acces.md) : groupes d'équivalence ; câblage implicite. |
| EC-RBA-04 Retrait de l'identité silencieux | Résolu | [RG-LIA-11](16-liaisons-identites-et-acces.md). |
| EC-RBA-05 Portée unique | Résolu / Lot 2 | Ressource et groupe de ressources au lot 1 ; enfants au lot 2 ([DEC-38](03-decisions.md)). |

## Paramètres et pipelines applicatifs (APP)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-APP-01 Clés App Configuration jamais générées | Résolu | [17 § 5](17-parametres-applicatifs-et-secrets.md). |
| EC-APP-02 Clé unique sans label | Résolu | Unicité (clé, label). |
| EC-APP-03 Doublon en erreur technique | Résolu | [RG-PAR-03](17-parametres-applicatifs-et-secrets.md). |
| EC-APP-04 Modification limitée | Résolu | [RG-PAR-07](17-parametres-applicatifs-et-secrets.md). |
| EC-APP-05 Lectures sans contrôle | Résolu | [EXG-01](27-exigences-non-fonctionnelles.md). |
| EC-APP-06 Pas d'output cross-config explicite | Résolu | [RG-PAR-06](17-parametres-applicatifs-et-secrets.md). |
| EC-APP-07 `Combined` ne combine rien | Supprimé | — |
| EC-APP-08 Deux énumérations du mode | Supprimé | — |
| EC-APP-09 Options générées mais pas saisissables | Résolu | Promotion déduite du modèle ([RG-APP-07](19-applications-build-et-deploiement.md)) ; options riches par catalogue d'étapes au lot 2 ([DEC-50](03-decisions.md)) ; pas d'option cachée ([22 § 5](22-pipelines.md)). |
| EC-APP-10 Scans de sécurité forcés | Résolu | Étape « scan de l'image » du catalogue d'étapes : visible, activée par défaut, bloquante ou non. |
| EC-APP-11 Détection partielle | Lot 2 | [DEC-50](03-decisions.md) : détection limitée au chemin du code source, valeurs par défaut par pile ([UC-APP-01](19-applications-build-et-deploiement.md)). |
| EC-APP-12 « Image validée » non vérifiée | Supprimé | IFS construit les images qu'il déploie. |
| EC-APP-13 Premier environnement implicite | Résolu | Registre de build explicite et affiché ([RG-APP-07](19-applications-build-et-deploiement.md)) ; plus de variable de build par environnement. |
| EC-APP-14 Navigation de dépôt au niveau projet seulement | Résolu | [UC-APP-02](19-applications-build-et-deploiement.md). |

## Réseau (NET)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-NET-01 Privatisée sans Private Endpoint | Résolu (lot 2) | [RG-NET-06](18-reseau-et-exposition.md) : impossible à enregistrer. |
| EC-NET-02 L'onglet Réseau ne charge pas | Supprimé | Écran réécrit, généré depuis le descripteur. |
| EC-NET-03 Mode DNS ignoré | Lot 2 | [RG-NET-07](18-reseau-et-exposition.md). |
| EC-NET-04 Plusieurs sous-ressources dans un point de terminaison | Lot 2 | Un point de terminaison par `groupId`. |
| EC-NET-05 Branchement des modules non vérifié | Résolu | [RG-GEN-04](21-generation-et-revisions.md), [EXG-16](27-exigences-non-fonctionnelles.md). |
| EC-NET-06 Validation DNS fictive | Lot 2 | [DEC-29](03-decisions.md). |
| EC-NET-07 Profil réseau V2 mort | Supprimé | — |
| EC-NET-08 Aucun outil MCP réseau | Résolu | [RG-MCP-05](25-agent-ia-mcp.md) : parité par construction. |
| EC-NET-09 Pas de NSG ni de pare-feu IP | Résolu / Lot 2 | Exposition restreinte au lot 1 ; NSG au lot 2. |

## Génération (GEN)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-GEN-01 Génération par configuration sans écran | Supprimé | [DEC-20](03-decisions.md) : génération toujours au niveau projet. |
| EC-GEN-02 Nom généré différent | Résolu | [DEC-06](03-decisions.md). |
| EC-GEN-03 Réglages Document Intelligence perdus | Résolu | Descripteur unique. |
| EC-GEN-04 Clés App Configuration jamais générées | Résolu | [17 § 5](17-parametres-applicatifs-et-secrets.md). |
| EC-GEN-05 Six propriétés cross-config seulement | Résolu | [DEC-16](03-decisions.md). |
| EC-GEN-06 Pas d'historique | Résolu | [DEC-20](03-decisions.md), [RG-GEN-05](21-generation-et-revisions.md). |
| EC-GEN-07 Pas de validation du Bicep produit | Résolu | [RG-GEN-04](21-generation-et-revisions.md) ; what-if dans les pipelines. |

## Pipelines et bootstrap (PIP)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-PIP-01 Approbations non créées | Résolu | [DEC-39](03-decisions.md). |
| EC-PIP-02 Service connections non créées | Résolu | Script Azure ([23 § 2](23-kit-installation.md)). |
| EC-PIP-03 Variables secrètes vides | Résolu | Contrôle avant déploiement ([RG-PAR-14](17-parametres-applicatifs-et-secrets.md)) et liste de contrôle ; IFS ne connaît pas les valeurs par conception. |
| EC-PIP-04 Pas de mise à jour | Résolu | Réconciliation ([23 § 3](23-kit-installation.md)). |
| EC-PIP-05 Chemins de base ignorés | Résolu | [RG-PUB-04](24-depots-et-publication.md). |
| EC-PIP-06 Validation MultiRepo réelle | Résolu | [EXG-17](27-exigences-non-fonctionnelles.md). |
| EC-PIP-07 Azure DevOps uniquement | Résolu | GitHub Actions au lot 2, GitLab CI au lot 3 ([DEC-47](03-decisions.md), [DEC-62](03-decisions.md)). |
| EC-PIP-08 Pas de what-if | Résolu | [22 § 3](22-pipelines.md). |

## Livraison git (GIT)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-GIT-01 Le bouton de push ne pousse que le Bicep | Résolu | Une publication publie toute la révision. |
| EC-GIT-02 Pas de pull request | Résolu | [DEC-22](03-decisions.md). |
| EC-GIT-03 Fichiers à la racine non nettoyés | Résolu | Manifeste ([RG-PUB-08](24-depots-et-publication.md)). |
| EC-GIT-04 Sous-chemin ignoré | Résolu | [RG-PUB-04](24-depots-et-publication.md). |
| EC-GIT-05 Validation réelle MultiRepo | Résolu | [EXG-17](27-exigences-non-fonctionnelles.md). |
| EC-GIT-06 Écran mort de renouvellement du jeton | Supprimé | — |
| EC-GIT-07 Génération et push non liés | Résolu | [RG-GEN-02](21-generation-et-revisions.md), [RG-PUB-10](24-depots-et-publication.md). |

## Diagnostics (DIA)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-DIA-01 Règle Key Vault stricte | Résolu | Câblage implicite et équivalences. |
| EC-DIA-02 Diagnostics éclatés | Résolu | [DEC-19](03-decisions.md). |
| EC-DIA-03 Contrôles absents | Résolu | Catalogue de règles [20 § 4](20-validation.md). |
| EC-DIA-04 Disponibilité par DNS public | Lot 2 | [RG-NOM-09](13-nommage.md). |
| EC-DIA-05 Avant génération projet seulement | Résolu | [RG-VAL-01](20-validation.md) : toute génération est validée. |

## Import (IMP)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-IMP-01 Pas d'écran | Lot 3 | [29](29-import.md) : écran de revue de l'import ([DEC-49](03-decisions.md)). |
| EC-IMP-02 Nouveau projet uniquement | Lot 3 | Import vers un nouveau projet ou un projet existant, sous forme de proposition. |
| EC-IMP-03 Un seul resource group | Lot 3 | Répartition en composants et groupes de ressources à la revue. |
| EC-IMP-04 ARM JSON seulement | Lot 3 | Sources ARM JSON, Bicep (compilé), groupe de ressources Azure. |
| EC-IMP-05 Réglages par environnement perdus | Lot 3 | Une source par environnement ; différences converties en surcharges. Identités, rôles et paramètres repris. |
| EC-IMP-06 Abonnement vide | Lot 3 | Environnements choisis ou créés explicitement à la revue. |

## MCP

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-MCP-01 Outils non enregistrés | Résolu | [RG-MCP-05](25-agent-ia-mcp.md). |
| EC-MCP-02 Couverture partielle | Résolu | Idem. |
| EC-MCP-03 Génération projet seulement | Résolu | La génération est toujours projet, et la publication couvre toutes les destinations. |
| EC-MCP-04 Brouillons non persistés | Résolu | Propositions persistées ([RG-MCP-09](25-agent-ia-mcp.md)) ; brouillons de projet côté serveur ([RG-PRJ-02](11-projets-et-environnements.md)). |
| EC-MCP-05 Lecture limitée | Résolu | `get_project_model` et parité des requêtes. |

## Interface (UI)

| Écart v0 | Statut | Traitement v1 |
|---|---|---|
| EC-UI-01 Paramètres de projet fictifs | Résolu | [DEC-35](03-decisions.md). |
| EC-UI-02 Préférences non synchronisées | Résolu | [RG-UI-07](26-interface.md). |
| EC-UI-03 Pas de vue graphe | Résolu / Lot 2 | Lecture au lot 1, édition au lot 2. |
| EC-UI-04 Pas d'écran d'import ARM | Lot 3 | Voir IMP. |
| EC-UI-05 Pas de recherche globale | Résolu | [RG-UI-06](26-interface.md). |

## Problèmes de fond relevés à la revue (hors liste v0)

| Problème | Traitement v1 |
|---|---|
| Pas de propriétaire défini pour les fichiers poussés | [DEC-01](03-decisions.md) |
| Pas de ressource partagée entre environnements, pas d'abonnement plateforme | [DEC-10](03-decisions.md) |
| Pas de ressource absente d'un environnement | [DEC-11](03-decisions.md) |
| Séparation arbitraire propriétés / réglages par environnement (API Cosmos par environnement…) | [DEC-12](03-decisions.md) |
| Ressource existante identifiée par calcul de nom | [DEC-18](03-decisions.md) |
| Pas d'ordre de déploiement entre configurations | [DEC-40](03-decisions.md) |
| Réseau privé dangereux (pas d'intégration sortante, pas de DNS) | [DEC-28](03-decisions.md) |
| Mots de passe SQL et identifiants admin de registre | [DEC-51](03-decisions.md) : permis, déconseillés, jamais connus d'IFS |
| Accès aux bases de données à créer à la main | [16 § 6](16-liaisons-identites-et-acces.md) |
| Valeurs Azure obsolètes au catalogue | [DEC-41](03-decisions.md) |
| Pas de journalisation des ressources (paramètres de diagnostic) | [DEC-42](03-decisions.md) |
| Le déploiement d'infrastructure peut écraser l'image de l'application | [RG-APP-02](19-applications-build-et-deploiement.md), [DEC-53](03-decisions.md) |
| Pas de niveau organisation, pas d'audit, pas de modèle SaaS défini | [DEC-03](03-decisions.md), [DEC-34](03-decisions.md), [DEC-04](03-decisions.md) |
| Vocabulaire surchargé (« configuration ») | [DEC-02](03-decisions.md), [02](02-glossaire.md) |
| Sortie limitée à Bicep et à Azure DevOps | [DEC-43](03-decisions.md) à [DEC-48](03-decisions.md) : Bicep, Terraform, Pulumi ; Azure DevOps, GitHub Actions, GitLab CI ; Azure seulement |
