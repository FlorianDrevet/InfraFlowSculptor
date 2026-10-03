# 00 — Vision

## 1. Le produit en une phrase

InfraFlowSculptor (IFS) permet à une équipe de **décrire une fois** son infrastructure Azure et ses
conventions, puis produit et tient à jour, dans ses propres dépôts git, le **code d'infrastructure**
(Bicep, Terraform ou Pulumi), les **pipelines** (Azure DevOps, GitHub Actions, GitLab CI) et le **kit
d'installation** nécessaires pour la déployer, avec le câblage de sécurité (identités, rôles, secrets)
déduit automatiquement.

**Azure seulement, mais dans les outils de l'équipe** : IFS connaît Azure en profondeur et s'adapte au
langage d'infrastructure et à la plateforme CI que l'équipe utilise déjà ([DEC-43](03-decisions.md),
[DEC-47](03-decisions.md)).

## 2. Le problème

Une équipe qui déploie sur Azure refait sans cesse le même travail, et le refait différemment :

| Douleur | Conséquence |
|---|---|
| Chaque nouveau service démarre par un copier-coller de Bicep ou de Terraform, et de YAML. | Les conventions (nommage, tags, réseau, TLS) dérivent d'un service à l'autre. |
| Le câblage de sécurité est manuel : identité managée, rôle `AcrPull`, rôle `Key Vault Secrets User`, utilisateur SQL. | Un rôle oublié se découvre au déploiement, souvent en production. |
| Préparer la plateforme CI est artisanal : connexions fédérées à Azure, environnements, approbations, secrets, état Terraform, définitions de pipelines. | Des jours de mise en route, une procédure que personne ne documente. |
| Les liens entre services (le service B lit le Key Vault du socle A) vivent dans la tête des gens. | Impossible de savoir ce qui casse si on supprime une ressource. |
| Les générateurs IA écrivent du code d'infrastructure plausible, ressource par ressource. | Pas de cohérence de projet, pas de tenue dans le temps, pas de câblage vérifié. |

## 3. Pour qui

| Persona | Rôle dans IFS | Ce qu'il attend |
|---|---|---|
| **Architecte plateforme** (acheteur principal) | Définit les environnements, les conventions de nommage, le socle partagé, le plan de publication. | Des conventions appliquées partout, sans police manuelle. |
| **Développeur d'une équipe produit** | Ajoute son service, sa base, ses paramètres, ses accès. | Déployer sans maîtriser le langage d'infrastructure ni RBAC, et sans attendre l'équipe plateforme. |
| **Intégrateur / ESN** | Livre des infrastructures chez plusieurs clients. | Une organisation par client, un livrable propre et lisible chez chacun. |
| **Agent IA** | Modifie le modèle à la demande d'un humain, via MCP. | Une surface complète et des garde-fous (validation, propositions). |

## 4. La promesse

1. **Du modèle au premier déploiement réussi en moins d'une journée**, pour un projet de taille
   courante (2 à 4 environnements, 10 à 40 ressources).
2. **Ce qui est affiché est ce qui est déployé** : noms, valeurs et droits sont calculés une seule
   fois, par le serveur, et écrits en clair dans les fichiers.
3. **Chaque modification devient une pull request lisible**, que l'équipe relit comme du code écrit à
   la main.
4. **Aucun verrou** : le code produit est du Bicep, du Terraform ou du Pulumi idiomatique, et des
   pipelines standards de la plateforme. Un client qui quitte IFS garde des fichiers qu'il peut
   maintenir seul. Un client qui change d'outil est accompagné par une migration assistée
   ([DEC-48](03-decisions.md)).

## 5. Face aux alternatives

| Alternative | Ce qu'elle fait bien | Ce qu'IFS apporte en plus |
|---|---|---|
| Bicep ou Terraform écrit à la main + Azure Verified Modules | Contrôle total, modules de qualité. | Conventions imposées, câblage RBAC déduit, environnements et pipelines générés, vue des dépendances. IFS **utilise** les AVM ([DEC-45](03-decisions.md)). |
| Azure Developer CLI (`azd`) | Démarrage rapide d'une application. | Plusieurs composants et équipes, socle partagé, environnements protégés, gouvernance de projet. |
| Terraform ou Pulumi + modules internes | Écosystème mûr, multi-cloud. | IFS **produit** du Terraform ou du Pulumi : l'équipe garde son outil, et gagne le modèle Azure, le câblage et la cohérence de projet. Les équipes produit n'ont pas à apprendre le langage. |
| Portail développeur (Backstage) | Catalogue et gabarits de création. | IFS maintient le service **après** sa création ; un gabarit ne fait que le démarrer. |
| Génération par IA (Copilot, Claude) | Rapide pour un fichier isolé. | Modèle persistant, validation, cohérence de projet. L'IA devient un client d'IFS via MCP. |

## 6. Ce qu'IFS n'est pas

- **Pas un outil de déploiement.** Ce sont les pipelines du client qui déploient, avec ses identités.
  IFS n'a besoin d'aucun droit sur les abonnements Azure du client.
- **Pas un outil multi-cloud.** Azure uniquement, définitivement. Le choix porte sur le langage et la
  plateforme CI, jamais sur le cloud.
- **Pas une CMDB ni un outil de supervision.** IFS connaît le modèle voulu, pas l'état réel.
- **Pas un générateur de code applicatif ni une plateforme CI.** IFS construit et déploie les
  applications de façon minimale ; tests et qualité restent chez le client, via des points d'extension
  ([DEC-25](03-decisions.md)).

## 7. Mise à disposition

IFS est un **service en ligne (SaaS) multi-organisations**, hébergé dans l'Union européenne
([DEC-04](03-decisions.md)). IFS ne stocke **aucune valeur de secret applicatif** du client. Les seuls
secrets conservés sont des identifiants de connexion git, et seulement en mode de repli.

## 8. Indicateurs de succès

| Indicateur | Cible v1 |
|---|---|
| Temps **actif** entre la création du projet et le premier déploiement réussi du projet de référence ([90](90-projet-de-reference.md)), hors attente des droits chez le client, mesuré séparément | < 1 jour |
| Temps d'obtention des droits chez le client (consentements, connexions git et Azure, approbateurs) | Mesuré, pour réduire les frictions ; pas de cible |
| **Activation** : premier déploiement réussi **et** contrôle des dépendances de l'application témoin au vert **et** deuxième modification livrée en production | > 70 % des projets créés par des équipes pilotes |
| Part des révisions dont le premier déploiement réussit sans retouche manuelle des fichiers | > 90 % |
| Abandons avant la première génération, modifications manuelles des fichiers gérés, demandes au support par projet | Mesurés par cohorte mensuelle |
| Étapes manuelles restantes après le kit d'installation | Exécuter le script Azure, exécuter la partie plateforme du kit, saisir les valeurs de secrets |
| Écarts entre le nom affiché et le nom déployé | 0, par construction |
