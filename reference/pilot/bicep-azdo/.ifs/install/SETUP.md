<!-- Généré par InfraFlowSculptor — projet shop. Ne pas modifier : la prochaine publication remplacera ce fichier. Personnalisation : voir README.ifs.md. -->

# Installation du pilote shop

Ce kit prépare trois cibles Azure : dev, prd et shared. Il utilise Azure CLI, Azure DevOps Pipelines et des connexions fédérées Microsoft Entra. Aucune clé client n'est créée.

## 1. Préparer les comptes

Il faut :

- être Owner ou User Access Administrator sur les abonnements A, B et C, pour attribuer les rôles Azure ;
- pouvoir créer des identités managées et des comptes de stockage dans ces abonnements ;
- avoir Azure CLI et l'extension Azure DevOps installées ;
- être administrateur du projet Azure DevOps, ou disposer des droits de création de pipelines, environnements, groupes de variables, contrôles et politiques de branche ;
- être autorisé à ajouter des membres aux groupes Entra. Si ce droit manque, le script affiche la commande à faire exécuter par un administrateur Entra.

Connectez-vous au tenant qui possède les abonnements :

    az login --tenant <tenant-id>
    az extension add --name azure-devops
    az devops configure --defaults organization=https://dev.azure.com/<organisation> project=shop

Le dépôt publié doit contenir .ifs/manifest.json et les fichiers release.*.json avec de vrais GUID d'abonnements. Les valeurs de démonstration <A>, <B> et <C> doivent avoir été remplacées avant de continuer.

## 2. Prévisualiser puis créer les ressources Azure

Depuis la racine du dépôt :

    ./.ifs/install/azure-setup.ps1 -AzureDevOpsOrganization https://dev.azure.com/<organisation> -AzureDevOpsProject shop -TenantId <tenant-id> -WhatIf
    ./.ifs/install/azure-setup.ps1 -AzureDevOpsOrganization https://dev.azure.com/<organisation> -AzureDevOpsProject shop -TenantId <tenant-id>

La première commande lit Azure et Azure DevOps, puis affiche les opérations prévues sans écrire. La seconde demande de taper INSTALLER avant de créer les ressources.

Pour chaque cible, le script prépare le groupe rg-ifs-shop-<cible>, les identités id-ifs-deploy-shop-<cible> et id-ifs-app-shop-<cible>, le compte de stockage technique, le conteneur ifs-operations, les rôles et les connexions fédérées ifs-shop-<cible> et ifs-shop-<cible>-app.

Avant d'attribuer les rôles délégués, il lit les paramètres Bicep et prépare seulement les groupes de composants utilisés comme portées RBAC. Chaque groupe est créé avec le nom, la région et les tags de `resourceGroups.main`. Un groupe déjà présent doit avoir la région et les tags `managed-by`, `ifs-project`, `ifs-component` et `ifs-environment` attendus ; sinon le script s'arrête avant de créer un groupe. Les autres groupes, dont celui de `data`, sont créés par Bicep. Les déclarations de `main.bicep` restent la source de l'état désiré.

Les identités de déploiement reçoivent Contributor, Azure Deployment Stack Owner et un rôle RBAC Administrator avec une condition limitée aux rôles utilisés par le pilote. Les identités applicatives n'obtiennent aucun rôle à l'abonnement ; l'identité app de shared reçoit AcrPush uniquement sur le registre partagé. Le stockage désactive les clés partagées et l'accès anonyme, et active versioning et suppression réversible.

Les objets existants sans marque managed-by: infraflowsculptor restent intacts et provoquent une erreur explicite. Un kit plus ancien que la révision déjà installée est refusé. Les seules suppressions portent sur les identifiants fédérés Azure DevOps ifs-ado-* obsolètes ; aucune ressource contenant des données n'est supprimée.

Le rapport final liste chaque cible et toute action Entra à faire. Si le groupe `sg-shop-sql-admins` n'existe pas, un administrateur Entra autorisé à créer des groupes le crée, puis relance le script :

    az ad group create --display-name sg-shop-sql-admins --mail-nickname shop-sql-admins

Si l'ajout d'une identité au groupe est refusé, un administrateur Entra ayant le droit de gérer les membres exécute la commande `az ad group member add` exacte affichée par le rapport. Elle utilise l'identifiant d'objet de l'identité de déploiement, pas son identifiant client.

## 3. Créer et lancer le pipeline d'installation

Dans Azure DevOps, ouvrez Pipelines → New pipeline → Azure Repos Git, choisissez le dépôt shop, puis Existing Azure Pipelines YAML file et le chemin .ifs/install/install.pipeline.yml. Enregistrez le pipeline et lancez-le depuis la branche main.

Le pipeline crée ou réaligne :

- les environnements shop-dev, shop-prd et shop-shared ;
- l'approbation du groupe Azure DevOps Shop Release Approvers sur les environnements protégés et les connexions applicatives protégées ;
- un verrou exclusif sur chaque environnement ;
- les groupes de variables ifs-shop-dev et ifs-shop-prd ;
- les pipelines PR, CI et Release sous \shop\<composant>[\<application>] ;
- les contrôles de branche et de modèle requis sur les connexions ;
- les autorisations limitées aux pipelines qui utilisent chaque connexion, environnement et groupe de variables ;
- les politiques Build Validation filtrées par chemins sur main.

Le groupe Azure DevOps Shop Release Approvers doit exister avant cette étape. Si le pipeline ne le trouve pas, un administrateur du projet disposant du droit de créer des groupes de sécurité le crée dans Project settings → Permissions → New group, avec exactement ce nom, puis relance le pipeline.

Pendant ce run, le pipeline d'installation reçoit temporairement accès aux connexions pour vérifier les trois identités de déploiement et lancer un what-if sans ressource. La phase finale retire cet accès temporaire. L'artefact ifs-install-report contient les résultats par cible.

## 4. Saisir le secret de déploiement

Le responsable du secret fournit la valeur à une personne autorisée à gérer la bibliothèque de variables Azure Pipelines. Cette personne ouvre Pipelines → Library, puis `ifs-shop-dev` et `ifs-shop-prd`. Dans chaque groupe, elle édite `MAIN_PAYMENTS_API_KEY`, saisit la valeur et active Keep this value secret avant d'enregistrer. Elle ne copie pas cette valeur dans un fichier, une commande ou les journaux.

Le kit crée la variable absente avec une valeur vide. Il ne remplace jamais une valeur déjà saisie et conserve les variables devenues inutiles en les signalant dans le rapport.

## 5. Lire le résultat

Téléchargez ifs-install-report depuis le run du pipeline :

- reussi / Succeeded : la vérification automatique indiquée est passée ;
- Failed : consultez les journaux de la tâche de readiness et corrigez le point nommé ;
- a confirmer : un administrateur Entra vérifie l'adhésion de l'identité au groupe SQL ; la personne autorisée sur la Library vérifie qu'une valeur a été saisie pour le secret.

Azure DevOps masque les valeurs des variables secrètes dans son API. Le rapport peut confirmer les noms attendus, mais la vérification qu'une valeur secrète a bien été saisie reste humaine.
