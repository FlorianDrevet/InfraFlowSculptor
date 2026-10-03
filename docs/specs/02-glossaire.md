# 02 — Glossaire

Le terme français est utilisé à l'écran (version française) et dans les specs. Le nom technique est
utilisé dans l'API, le code et l'interface anglaise. Un terme absent de cette liste ne doit pas
apparaître à l'écran.

## 1. Termes

| Terme | Nom technique | Définition |
|---|---|---|
| **Organisation** | Organization | Espace d'un client : ses membres, ses connexions git, ses projets, son journal d'audit. Frontière d'isolation des données. |
| **Membre d'organisation** | OrganizationMember | Utilisateur rattaché à une organisation, avec un rôle d'organisation. |
| **Projet** | Project | Un produit ou un périmètre : ses environnements, ses conventions, ses composants, son plan de publication, son équipe. |
| **Code** (d'un projet, d'un environnement, d'un composant) | Code | Identifiant court, en minuscules, stable, utilisé dans les noms Azure, les dossiers et les noms de pipelines. |
| **Environnement** | Environment | Étape de la chaîne de promotion (dev, test, prod…) : un abonnement, une région, une connexion de déploiement, éventuellement une protection. |
| **Environnement protégé** | ProtectedEnvironment | Environnement dont tout déploiement exige l'approbation d'un approbateur désigné. |
| **Composant** | Component | Unité de déploiement : un ensemble de groupes de ressources et de ressources qui produit son propre code d'infrastructure (une pile, un état) et ses propres pipelines. Exemples : socle partagé, service « commandes ». |
| **Mode de déploiement** | DeploymentMode | `PerEnvironment` : le composant est déployé dans chacun de ses environnements. `Single` : il est déployé une seule fois, dans sa cible propre. |
| **Cible de déploiement** | DeploymentTarget | Ce qui reçoit un déploiement : un environnement (composant `PerEnvironment`) ou la cible propre d'un composant `Single`. Porte abonnement, région, connexion de déploiement et protection. |
| **Langage d'infrastructure** | IacLanguage | Langage du code produit pour un projet : Bicep, Terraform, Pulumi (TypeScript). |
| **Plateforme CI** | CiPlatform | Plateforme qui exécute les pipelines d'un projet : Azure DevOps Pipelines, GitHub Actions, GitLab CI. |
| **Plan de déploiement** | DeploymentPlan | Représentation neutre, calculée par IFS, de ce qui doit être déployé pour chaque composant et chaque cible : ressources, valeurs, noms, rôles, dépendances, étapes de pipeline. |
| **Émetteur** | Emitter | Traducteur du plan de déploiement vers un langage d'infrastructure ou une plateforme CI. Ne prend aucune décision métier. |
| **Connexion de déploiement** | DeploymentConnection | Moyen par lequel les pipelines s'authentifient à Azure pour une cible, toujours par fédération d'identité : service connection (Azure DevOps), identifiant fédéré par environnement (GitHub, GitLab). |
| **Unité de déploiement** | DeploymentUnit | Ce qui suit le cycle de vie d'un composant dans une cible : pile de déploiement (Bicep), état (Terraform), pile Pulumi. |
| **État** | State | Fichier par lequel Terraform ou Pulumi suit les ressources qu'il gère. Stocké chez le client, jamais lu par IFS. |
| **Exécuteurs** | Runners | Machines qui exécutent les pipelines : pool d'agents (Azure DevOps), libellés de runners (GitHub), tags de runners (GitLab). |
| **Magasin de secrets CI** | CiSecretStore | Emplacement des secrets de pipeline : groupe de variables (Azure DevOps), secrets d'environnement (GitHub), variables CI/CD d'environnement (GitLab). |
| **Groupe de ressources** | ResourceGroup | Groupe de ressources Azure d'un composant. |
| **Ressource** | Resource | Instance d'un type du catalogue dans un groupe de ressources. |
| **Ressource existante** | ExistingResource | Ressource déployée hors IFS, désignée par son identifiant Azure dans chaque environnement, pour être la cible de liaisons. |
| **Type de ressource** | ResourceType | Entrée du catalogue (Key Vault, Container App…). |
| **Descripteur** | ResourceTypeDescriptor | Description versionnée d'un type : propriétés, valeurs, contraintes de nom, liaisons, sorties, rôles, exposition réseau. |
| **Catalogue** | Catalog | Ensemble versionné des descripteurs. |
| **Propriété** | Property | Réglage d'une ressource défini par le descripteur (SKU, rétention…). |
| **Surcharge** | EnvironmentOverride | Valeur d'une propriété propre à un environnement, qui remplace la valeur de la ressource. |
| **Valeur effective** | EffectiveValue | Valeur réellement générée pour un environnement : surcharge, sinon valeur de la ressource, sinon défaut du descripteur. |
| **Présence** | Presence | Indique, environnement par environnement, si une ressource y est déployée. |
| **Enfant** | ChildObject | Objet rattaché à une ressource et déployé avec elle (conteneur blob, file Service Bus, base PostgreSQL…). |
| **Nom logique** | LogicalName | Nom court saisi par l'utilisateur (`orders`). |
| **Nom Azure** | AzureName | Nom réel d'une ressource dans un environnement, calculé par le gabarit puis assaini. |
| **Gabarit de nommage** | NamingTemplate | Modèle de nom composé de jetons et de texte fixe. |
| **Abréviation** | Abbreviation | Code court d'un type utilisé dans les noms (`kv`, `ca`…). |
| **Liaison** | Link | Relation typée et orientée d'une ressource source vers une ressource cible (hébergement, tirage d'image, accès…). |
| **Type de liaison** | LinkKind | Nature d'une liaison ; définit les types autorisés et ce que la liaison implique. |
| **Implicite** | Implicit | Se dit d'un élément (rôle, paramètre, identité) déduit par IFS d'une liaison ou d'un paramètre. Non modifiable directement. |
| **Principal** | Principal | Ce qui reçoit un rôle : identité système d'une ressource, identité managée affectée par l'utilisateur, ou groupe Entra. |
| **Attribution de rôle** | RoleAssignment | Principal + rôle + portée. |
| **Accès aux données** | DataAccess | Droit d'une identité dans une base SQL ou PostgreSQL, créé par script après déploiement. |
| **Application** | Application | Ressource déployable par un pipeline applicatif : Web App, Function App, Container App. |
| **Paramètre applicatif** | AppSetting | Valeur fournie à une application : variable d'environnement d'une application, ou clé d'une App Configuration. |
| **Source de valeur** | ValueSource | Origine d'un paramètre applicatif : littérale, sortie de ressource, secret Key Vault. |
| **Sortie** | Output | Propriété d'une ressource lisible par une autre (point de terminaison, nom de serveur…). Une sortie **sensible** ne peut aller que dans un Key Vault. |
| **Secret de pipeline** | PipelineSecret | Secret dont la valeur est saisie par le client dans le magasin de secrets de sa plateforme CI, puis écrite dans un Key Vault lors du déploiement par une étape de pipeline. IFS n'en connaît jamais la valeur. |
| **Exposition réseau** | NetworkExposure | Accès réseau à une ressource : publique, publique restreinte, privée. |
| **Constat** | Finding | Résultat d'une règle de validation : gravité, code, objet, message, correction proposée. |
| **Révision** | Revision | Instantané immuable d'une génération : version du modèle, version du catalogue, fichiers produits, résumé des changements. |
| **Fichier géré** | ManagedFile | Fichier produit par IFS et listé dans le manifeste d'une destination. |
| **Manifeste** | Manifest | Fichier `.ifs/manifest.json` d'une destination : liste des fichiers gérés et leur empreinte. |
| **Point d'extension** | ExtensionPoint | Emplacement prévu pour du contenu écrit par le client et appelé par les fichiers générés (code d'infrastructure additionnel, étapes de pipeline). |
| **Connexion git** | GitConnection | Accès d'une organisation à un fournisseur git (application GitHub, principal de service Azure DevOps, ou jeton en repli). |
| **Dépôt** | Repository | Dépôt git accessible par une connexion. |
| **Destination** | PublishDestination | Dépôt + chemin de base qui reçoit une partie des fichiers d'un projet. |
| **Plan de publication** | PublishPlan | Pour chaque composant : la destination de son infrastructure et celle de ses applications. |
| **Publication** | Publication | Écriture d'une révision dans ses destinations, par branche et pull request. |
| **Kit d'installation** | InstallKit | Script Azure commun, partie propre à la plateforme CI (pipeline d'installation Azure DevOps, script GitHub…) et liste de contrôle, qui préparent Azure et la plateforme CI. |
| **Jeton d'API** | ApiToken | Jeton créé par un utilisateur pour appeler l'API ou le serveur MCP. Ne pas confondre avec un jeton git. |
| **Proposition de modification** | ChangeProposal | Ensemble de modifications du modèle préparé par un agent ou un utilisateur, appliqué après relecture humaine. |
| **Journal d'audit** | AuditLog | Historique horodaté de toutes les modifications et actions sensibles. |

## 2. Termes abandonnés

Ces termes de la v0 ne doivent plus apparaître.

| Ancien terme | Remplacé par | Raison |
|---|---|---|
| Configuration (InfrastructureConfig) | **Composant** | Confusion avec « configuration applicative » et la ressource App Configuration. |
| Topologie, LayoutPreset, ConfigLayoutMode | **Plan de publication** et ses préréglages | La répartition dans les dépôts ne doit pas changer ce qui est généré ([DEC-21](03-decisions.md)). |
| Bootstrap | **Kit d'installation** | Le pipeline seul ne suffit pas ; le kit inclut le script Azure et la liste de contrôle. |
| Privatisation | **Exposition réseau privée** | La privatisation était un booléen qui pouvait rendre une ressource injoignable. |
| Réglages par environnement (liste séparée) | **Surcharges** | Un seul mécanisme pour toutes les propriétés ([DEC-12](03-decisions.md)). |
| Référence cross-config | **Liaison** vers une ressource d'un autre composant | La référence est implicite ([DEC-16](03-decisions.md)). |
| Variable group déclaré | **Magasin de secrets CI** géré par cible de déploiement | IFS crée et nomme les magasins ([23](23-kit-installation.md)). |
| Service connection (terme général) | **Connexion de déploiement** | La service connection n'existe que dans Azure DevOps. |
| Agent pool (terme général) | **Exécuteurs** | Le pool d'agents n'existe que dans Azure DevOps. |
| Bicep additionnel | **Code d'infrastructure additionnel** | Écrit dans le langage du projet. |
| Paramètre sécurisé | Supprimé | Plus de mot de passe ni d'identifiant admin à fournir ([DEC-26](03-decisions.md)). |
| Diagnostic | **Constat** de validation | Un seul moteur, avec des gravités bloquantes ([DEC-19](03-decisions.md)). |
| PAT IFS | **Jeton d'API** | Distinguer clairement du jeton git. |
| Image validée | Supprimé | IFS construit lui-même les images qu'il déploie. |
