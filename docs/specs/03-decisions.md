# 03 — Journal des décisions

Chaque décision indique le constat qui l'a motivée (souvent un écart de la v0, voir
[A1](A1-traitement-des-ecarts-v0.md)), ce qui est décidé et ce que cela entraîne. Une décision ne se
modifie pas : on la remplace par une nouvelle qui la cite.

---

## Fondations

### DEC-01 — IFS est propriétaire des fichiers qu'il gère

**Constat.** En v0, IFS poussait des fichiers et supprimait les anciens sans définir qui en était
propriétaire. Une retouche manuelle du client était écrasée sans avertissement au push suivant.

**Décision.**
- Le modèle est la source de vérité.
- Chaque destination contient un manifeste (`.ifs/manifest.json`) qui liste les fichiers gérés et leur empreinte.
- Avant chaque publication, IFS compare le contenu du dépôt au manifeste. Toute modification manuelle d'un fichier géré est présentée sous forme de diff, et l'utilisateur doit confirmer qu'il l'écrase.
- La personnalisation passe par des points d'extension : un code d'infrastructure additionnel par composant (dans le langage du projet), des étapes de pipeline fournies par le client.

**Conséquences.**
- Chaque fichier généré porte un en-tête « généré par IFS ».
- Les fichiers hors manifeste ne sont jamais touchés.
- Voir [21](21-generation-et-revisions.md) et [24](24-depots-et-publication.md).

### DEC-02 — Le « composant » remplace la « configuration »

**Constat.** « Configuration » désignait à la fois l'unité de déploiement, la configuration applicative et la ressource App Configuration.

**Décision.** L'unité de déploiement s'appelle **composant** (`Component`). Elle a un **code** unique dans le projet, qui sert de nom de dossier et de jeton de nommage.

**Conséquences.** Deux composants ne peuvent plus écrire au même endroit (l'ancien problème des configurations homonymes).

### DEC-03 — Une organisation au-dessus des projets

**Constat.** Il n'y avait pas de niveau client. La liste des utilisateurs exposait tous les comptes de la plateforme.

**Décision.** L'**organisation** est la frontière d'isolation. Elle porte les membres, les invitations, les connexions git, les jetons d'API et le journal d'audit. Un utilisateur peut appartenir à plusieurs organisations.

**Conséquences.** Toute donnée appartient à exactement une organisation. Toute lecture vérifie l'appartenance ([EXG-01](27-exigences-non-fonctionnelles.md)).

### DEC-04 — Service en ligne hébergé dans l'UE, sans secret applicatif

**Constat.** Le modèle de mise à disposition n'était pas défini, alors qu'il conditionne la sécurité.

**Décision.**
- IFS est un SaaS multi-organisations hébergé dans l'Union européenne.
- IFS ne stocke **aucune** valeur de secret applicatif. Les seuls secrets conservés sont les jetons git du mode de repli ([DEC-23](#dec-23--connexions-git-par-application-jeton-en-repli)), dans un coffre dédié, supprimés avec la connexion.
- IFS n'a besoin d'aucun accès aux abonnements Azure du client.

**Conséquences.** Les fonctions qui exigeraient un accès Azure sont soit déplacées dans les pipelines du client (exemple : la vérification des domaines), soit rendues facultatives (lot 2 : connexion Azure en lecture pour la disponibilité des noms).

### DEC-05 — Un seul moteur de calcul, côté serveur

**Constat.** En v0, l'aperçu des noms et la génération utilisaient deux calculs différents. Le nom affiché n'était pas celui déployé.

**Décision.** Le serveur calcule une seule fois les noms Azure, les valeurs effectives, les éléments implicites, l'ordre des composants et les constats. L'écran, l'API, le MCP et la génération lisent ce résultat.

**Conséquences.** L'interface ne contient aucune logique de calcul métier.

---

## Modèle

### DEC-06 — Les noms Azure sont écrits en clair dans les fichiers

> **Étendue par [DEC-44](#dec-44--génération-en-deux-étages)** : la règle vaut pour tous les langages (fichier de paramètres Bicep, fichier de variables Terraform, configuration de pile Pulumi).

**Constat.** Le Bicep v0 contenait des fonctions de nommage évaluées au déploiement : illisible, invérifiable, et divergent de l'écran.

**Décision.** Le fichier de paramètres de chaque environnement contient le nom Azure calculé de chaque ressource. Le Bicep ne contient aucune fonction de nommage.

**Conséquences.** Un changement de nom apparaît dans la pull request. Les références entre composants utilisent le nom calculé.

### DEC-07 — Les noms sont assainis, jamais tronqués

**Constat.** Les gabarits par défaut produisaient des noms invalides, par exemple un tiret dans le nom d'un compte de stockage.

**Décision.**
- Après application du gabarit, le nom est assaini selon le descripteur du type : caractères interdits retirés, casse imposée, séparateurs doublés ou en bord retirés.
- Un nom trop long ou trop court est une **erreur**. Aucune troncature automatique : elle créerait des collisions invisibles.

**Conséquences.** Voir [13](13-nommage.md).

### DEC-08 — Les environnements sont identifiés par identifiant

**Constat.** En v0, les valeurs par environnement étaient rattachées au nom texte de l'environnement. Le renommer les rendait orphelines.

**Décision.**
- Toute donnée par environnement est rattachée à son identifiant.
- Le nom et le code sont uniques dans le projet.
- Supprimer un environnement supprime ses données, après affichage de l'impact.

### DEC-09 — La région descend de la cible vers la ressource

**Constat.** En v0, trois régions indépendantes coexistaient (environnement, groupe de ressources, ressource), et la région d'un groupe de ressources était identique dans tous les environnements.

**Décision.**
- La **cible de déploiement** porte la région.
- Un groupe de ressources suit la région de la cible, sauf région fixée explicitement.
- Une ressource est **toujours** dans la région de son groupe de ressources, sauf les types que le descripteur déclare globaux.

**Conséquences.** Un environnement = une région en v1. Le multi-région est hors périmètre ([04](04-perimetre-et-lots.md)).

### DEC-10 — Deux modes de déploiement : par environnement ou unique

**Constat.** En v0, tout était déployé dans chaque environnement. Un registre partagé entre environnements, ou un socle dans un abonnement plateforme, était impossible à modéliser.

**Décision.**
- Un composant `PerEnvironment` est déployé dans les environnements qu'il cible (tous par défaut, ou un sous-ensemble).
- Un composant `Single` est déployé une fois, dans sa **cible propre** : code, abonnement, région, service connection, protection.
- Une ressource d'un composant `PerEnvironment` peut se lier à une ressource d'un composant `Single`. L'inverse est interdit.

### DEC-11 — Présence par environnement

**Constat.** On ne pouvait pas dire « pas de Redis en dev ».

**Décision.**
- Chaque ressource a une présence par environnement, présente partout par défaut.
- Une ressource absente n'est pas déployée dans cet environnement.
- La validation interdit qu'une ressource présente dépende d'une ressource absente.

### DEC-12 — Un seul mécanisme : valeur + surcharge par environnement

**Constat.** En v0, chaque type séparait arbitrairement des « propriétés » fixes et des « réglages par environnement » (purge protection fixe, Always On en double, API Cosmos par environnement).

**Décision.**
- Toute propriété a une valeur sur la ressource et, si le descripteur la déclare surchargeable, une surcharge facultative par environnement.
- Le descripteur déclare aussi les propriétés **verrouillées après publication** (exemple : système d'exploitation d'un plan) et **irréversibles** (exemple : purge protection, qu'on peut activer mais pas désactiver).

**Conséquences.** Toutes les valeurs effectives sont écrites explicitement dans les fichiers de paramètres : aucun défaut caché de module.

### DEC-13 — Le catalogue est un ensemble de descripteurs versionnés

**Constat.** En v0, chaque type était codé à la main dans huit couches. Les listes de valeurs divergeaient entre l'écran et le serveur, et beaucoup étaient périmées.

**Décision.** Un descripteur par type, dans un catalogue versionné, unique source pour l'écran, l'API, le MCP, la validation et la génération.

**Conséquences.**
- Une mise à jour du catalogue peut déprécier une valeur : un constat d'avertissement apparaît sur les projets concernés.
- Une révision enregistre la version du catalogue utilisée.

### DEC-14 — Les modules Bicep sont les Azure Verified Modules

> **Remplacée par [DEC-45](#dec-45--modules-et-fournisseurs-par-langage)**, qui l'étend à tous les langages.

**Décision.** La génération utilise les Azure Verified Modules du registre public, chacun épinglé à une version fixée par le descripteur. Un module local n'est écrit que si aucun module vérifié ne couvre le type.

**Conséquences.**
- La qualité et la maintenance des modules reposent sur Microsoft.
- Monter la version d'un module est une mise à jour du catalogue, testée sur le projet de référence.

### DEC-15 — Déploiement par Azure Deployment Stacks

> **Remplacée par [DEC-46](#dec-46--cycle-de-vie-et-état-par-langage)** : la règle « détacher par défaut » est conservée pour tous les langages ; les piles de déploiement restent le mécanisme Bicep.

**Constat.** Retirer une ressource du modèle n'avait aucun effet sur Azure, et rien ne le disait.

**Décision.**
- Chaque composant est déployé dans chaque cible comme une **pile de déploiement** de portée abonnement.
- Une ressource retirée du modèle est **détachée** par défaut : elle reste dans Azure mais n'est plus gérée.
- Un composant peut choisir la **suppression** des ressources retirées, mais jamais dans une cible protégée.

### DEC-16 — Toutes les relations sont des liaisons typées

**Constat.** La v0 avait six mécanismes de liens, plus deux modélisés mais morts. Les références inter-configurations n'étaient branchées que pour six propriétés et ignorées ailleurs sans message.

**Décision.**
- Une relation est une **liaison** : source, cible, type, paramètres.
- Une liaison peut viser une ressource d'un autre composant du même projet : la référence `existing` est déduite.
- Les dépendances entre composants, l'analyse d'impact et la vue graphe sont calculées depuis les liaisons.

### DEC-17 — Câblage implicite

**Constat.** Les rôles nécessaires (`AcrPull`, `Key Vault Secrets User`) faisaient l'objet d'avertissements que l'utilisateur devait corriger à la main.

**Décision.** Une liaison ou un paramètre applicatif qui exige un droit crée l'attribution de rôle, l'identité système ou le paramètre nécessaire, marqués implicites. Ces éléments ne se suppriment pas directement : ils disparaissent avec leur origine.

### DEC-18 — Une ressource existante est désignée par son identifiant Azure

**Décision.** Une ressource existante porte, pour chaque environnement où elle est présente, son identifiant Azure complet (abonnement, groupe de ressources, nom). Elle ne passe pas par le calcul de nom. Elle peut être cible de liaisons, jamais source.

---

## Contrôle et production

### DEC-19 — Un moteur de validation serveur, avec des erreurs bloquantes

**Constat.** En v0, les contrôles étaient éclatés entre l'écran et le serveur, jamais bloquants, et en partie absents.

**Décision.**
- Un moteur unique produit des constats `Erreur`, `Avertissement` ou `Info`.
- Une erreur bloque la génération.
- L'écran, l'API et le MCP obtiennent les mêmes constats.

### DEC-20 — La génération produit une révision immuable du projet entier

**Constat.** Il existait deux niveaux de génération (projet, configuration) selon la topologie, sans historique. On pouvait pousser une génération périmée.

**Décision.**
- Générer produit une **révision** numérotée, immuable, du projet **entier**.
- Une publication publie une révision, vers tout ou partie de ses destinations.
- Une révision dont le modèle a changé depuis est signalée comme périmée.

### DEC-21 — Le plan de publication est indépendant de la génération

**Constat.** En v0, la topologie changeait ce qui était généré, triplait les cas à tester, et le sous-chemin de dépôt n'était jamais appliqué.

**Décision.**
- La génération produit une arborescence logique unique.
- Le **plan de publication** associe chaque partie (infrastructure ou applications d'un composant) à une destination : dépôt + chemin de base.
- Les anciennes topologies deviennent des **préréglages** qui remplissent ce plan.

**Conséquences.** Les modèles de pipeline partagés sont copiés dans chaque destination qui en a besoin : aucune dépendance entre dépôts.

### DEC-22 — Publication par pull request par défaut

**Décision.** Par défaut, une publication écrit, dans chaque dépôt, sur une branche dédiée (`ifs/<projet>/revision-<n>`) et ouvre ou met à jour une pull request vers la branche par défaut. L'écriture directe sur une branche reste possible.

### DEC-23 — Connexions git par application, jeton en repli

**Constat.** En v0, chaque dépôt avait son jeton personnel, sans date d'expiration connue, et les jetons restaient orphelins après suppression du dépôt.

**Décision.**
- GitHub : application GitHub installée par le client.
- Azure DevOps : principal de service Entra d'IFS ajouté à l'organisation Azure DevOps.
- Jeton personnel en repli uniquement, avec date d'expiration obligatoire et alerte.
- Les connexions vivent au niveau organisation.

### DEC-24 — Azure DevOps en v1, modèle de pipeline neutre

> **Remplacée par [DEC-47](#dec-47--plusieurs-plateformes-ci)** : GitHub Actions rejoint Azure DevOps au lot 1, GitLab CI arrive au lot 3.

**Décision.**
- Les pipelines produits en v1 sont des pipelines YAML Azure DevOps.
- Les dépôts peuvent être sur GitHub ou Azure DevOps Repos : Azure DevOps Pipelines sait lire un dépôt GitHub.
- La description des pipelines dans le modèle ne dépend pas de la plateforme. GitHub Actions arrive au lot 3.

### DEC-25 — Applications : build minimal et points d'extension

**Constat.** La v0 tentait de gérer tests, couverture, Sonar, lint et analyse de dépendances pour sept piles, avec une détection automatique partielle. C'est un autre produit.

**Décision.**
- IFS construit (image conteneur, ou paquet par pile) et déploie les applications.
- Tests, qualité et analyses sont des **étapes fournies par le client**, que les pipelines générés appellent avant et après le build.
- Seule exception : le scan de l'image conteneur, option visible et activée par défaut.

### DEC-26 — Aucun mot de passe dans le modèle

**Décision.**
- Les applications tirent leurs images avec une identité managée affectée par l'utilisateur ; l'utilisateur admin des registres est toujours désactivé.
- Azure SQL et PostgreSQL fonctionnent en authentification Entra seule.

**Conséquences.** La notion de « paramètre sécurisé » disparaît. L'accès aux bases passe par des **accès aux données** créés par script après déploiement ([16](16-liaisons-identites-et-acces.md)).

### DEC-27 — Paramètres applicatifs unifiés ; sorties sensibles vers Key Vault seulement

**Constat.** En v0, les app settings et les clés App Configuration avaient des mécanismes proches mais distincts, et les clés n'étaient jamais générées.

**Décision.**
- Un **paramètre applicatif** a une destination (variable d'environnement d'une application, ou clé + label d'une App Configuration) et une source (littérale, sortie, secret Key Vault).
- Une sortie sensible ne peut alimenter qu'un secret Key Vault.

### DEC-28 — Réseau privé complet ou rien *(lot 2)*

**Constat.** En v0, une ressource privatisée sans Private Endpoint devenait injoignable, la zone DNS privée n'était jamais créée et les consommateurs n'étaient pas intégrés au réseau.

**Décision.** L'exposition privée exige :
- un subnet pour le point de terminaison privé ;
- une stratégie DNS ;
- pour chaque application consommatrice, une intégration réseau sortante.

La validation refuse toute configuration incomplète.

### DEC-29 — Domaines personnalisés vérifiés au déploiement *(lot 2)*

**Constat.** En v0, « Valider le DNS » ne vérifiait rien.

**Décision.**
- IFS n'a pas accès à Azure ([DEC-04](#dec-04--service-en-ligne-hébergé-dans-lue-sans-secret-applicatif)), donc c'est le pipeline de release qui vérifie les enregistrements DNS avant de lier le domaine.
- Si les enregistrements manquent, le domaine n'est pas lié, et le pipeline affiche les enregistrements exacts à créer.

### DEC-30 — Un kit d'installation complet et réconciliant

> **Étendue par [DEC-47](#dec-47--plusieurs-plateformes-ci)** : le script Azure est commun ; la partie « plateforme CI » existe pour chaque plateforme (pipeline d'installation Azure DevOps, script GitHub, script GitLab).

**Constat.** En v0, le bootstrap ne créait ni service connections, ni approbations, ni valeurs, et ne mettait jamais à jour l'existant.

**Décision.** Chaque révision contient un kit :
- un **script Azure** (identités de déploiement, identifiants fédérés, rôles, service connections) ;
- un **pipeline d'installation** Azure DevOps qui **réconcilie** les objets dont IFS est propriétaire : il les crée et les met à jour, sans jamais rien supprimer ;
- une **liste de contrôle** des étapes restantes.

### DEC-31 — MCP dérivé de l'API, avec propositions de modification

**Constat.** En v0, les outils MCP étaient écrits à la main, avec une couverture partielle et quatre groupes d'outils non enregistrés.

**Décision.**
- Chaque commande et requête de l'API est exposée comme outil MCP, depuis le même catalogue de commandes.
- Authentification OAuth (Entra) ou jeton d'API.
- Un agent peut préparer une **proposition de modification** qu'un humain relit et applique.

### DEC-32 — Pas d'import ARM ; import d'un groupe de ressources Azure au lot 3

**Constat.** L'import ARM v0 n'avait pas d'écran, perdait les réglages par environnement et mettait tout dans un seul groupe de ressources.

**Décision.**
- L'import ARM est supprimé.
- Le lot 1 permet de **référencer** des ressources existantes ([DEC-18](#dec-18--une-ressource-existante-est-désignée-par-son-identifiant-azure)).
- Le lot 3 propose un import depuis un groupe de ressources Azure, avec écran de revue.

### DEC-33 — Tags système automatiques

**Décision.**
- IFS ajoute à chaque ressource et groupe de ressources les tags `ifs-project`, `ifs-component`, `ifs-environment` et `managed-by=infraflowsculptor`.
- Ils ne sont pas surchargeables.
- Le projet peut les désactiver, avec un avertissement.

### DEC-34 — Journal d'audit

**Décision.** Chaque modification du modèle, chaque génération, publication, changement de droits et création de jeton est journalisé : qui, quand, quoi, avant et après. Le journal est consultable par les administrateurs d'organisation et les propriétaires de projet.

### DEC-35 — Aucune fonctionnalité annoncée

**Décision.** Aucune section « bientôt disponible ». Voir [P9](01-principes.md).

### DEC-36 — Un seul parcours de création de projet

**Constat.** En v0, la création simple et l'assistant appliquaient des règles différentes, et le brouillon était stocké dans le navigateur.

**Décision.** Un seul assistant, avec un brouillon enregistré côté serveur par utilisateur.

### DEC-37 — Concurrence optimiste

**Décision.**
- Chaque objet porte une version.
- Une modification fondée sur une version périmée est refusée avec un message clair et le contenu à jour.
- Pas d'édition simultanée en temps réel en v1.

### DEC-38 — Portées et principaux RBAC ; pas de rôles privilégiés

**Décision.**
- Principaux : identité système d'une ressource, identité affectée par l'utilisateur, groupe Entra (identifiant par environnement).
- Portées : la ressource cible ou son groupe de ressources. La portée « enfant » (un conteneur, une file) arrive au lot 2.
- Owner, User Access Administrator et Role Based Access Control Administrator ne sont jamais attribuables depuis le modèle.

### DEC-39 — Approbateurs modélisés

**Constat.** En v0, « approbation requise » produisait seulement un message demandant de configurer l'approbation à la main.

**Décision.** Une cible protégée porte une liste d'approbateurs, exprimés dans les identités de la plateforme CI du projet (utilisateurs ou groupes Azure DevOps, utilisateurs ou équipes GitHub, utilisateurs ou groupes GitLab). Le kit d'installation crée la règle d'approbation correspondante ([DEC-47](#dec-47--plusieurs-plateformes-ci)).

### DEC-40 — L'ordre entre composants est déduit ; les cycles sont interdits

**Décision.**
- Le graphe des liaisons entre composants détermine l'ordre de déploiement.
- Un cycle est une erreur.
- Chaque release vérifie au démarrage que les ressources des composants dont elle dépend existent dans l'environnement, et échoue avec un message explicite sinon.

### DEC-41 — Un catalogue aligné sur l'Azure actuel

**Constat.** La v0 proposait des valeurs retirées ou obsolètes : TLS 1.0 et 1.1, Application Insights classique, Redis 4, Functions in-process, désactivation du soft delete de Key Vault, SKU Log Analytics hérités.

**Décision.**
- Le catalogue ne propose que des valeurs supportées pour une nouvelle ressource.
- Une valeur qui devient obsolète est marquée dépréciée, avec une date. La validation l'avertit, puis la refuse après cette date.

### DEC-42 — Journalisation par défaut vers Log Analytics

**Décision.**
- Un composant peut désigner un espace Log Analytics par défaut.
- Toute ressource qui le supporte reçoit alors un paramètre de diagnostic (tous les journaux, toutes les métriques) vers cet espace, sauf exclusion explicite.
- Ces paramètres sont des liaisons implicites.

---

## Langages d'infrastructure et plateformes CI

### DEC-43 — Azure seulement, plusieurs langages d'infrastructure

**Constat.** La première version de la v1 limitait la sortie au Bicep ([DEC-14](#dec-14--les-modules-bicep-sont-les-azure-verified-modules), [DEC-15](#dec-15--déploiement-par-azure-deployment-stacks)). Beaucoup d'équipes Azure travaillent en Terraform, certaines en Pulumi. Imposer Bicep exclut ces clients ou les oblige à changer d'outil.

**Décision.**
- La **cible reste Azure, et seulement Azure**. AWS, GCP et le multi-cloud sont définitivement hors périmètre.
- Le **langage d'infrastructure** est un choix du projet :

  | Langage | Lot |
  |---|---|
  | Bicep | 1 |
  | Terraform (HCL) | 2 |
  | Pulumi (TypeScript) | 3 |

- Le modèle ne contient rien de propre à un langage.

**Conséquences.**
- Le catalogue décrit des **ressources Azure** (types ARM et leurs propriétés), pas des modules.
- Chaque descripteur déclare sa prise en charge par langage. Un type ou une valeur non pris en charge par le langage du projet n'est pas proposé.
- Rester sur Azure seul garde l'avantage du produit : connaissance fine des services, des rôles, des noms et du câblage. Le multi-langage ne coûte que des émetteurs ([DEC-44](#dec-44--génération-en-deux-étages)) ; le multi-cloud coûterait le modèle entier.

### DEC-44 — Génération en deux étages

**Décision.**
- **Étage 1 — Plan de déploiement.** Pour chaque composant et chaque cible, IFS calcule une représentation neutre :
  - ressources Azure avec nom, groupe, région, valeurs effectives, présence ;
  - identités, attributions de rôles, enfants ;
  - références externes, dépendances, secrets attendus, scripts post-déploiement ;
  - étapes logiques des pipelines.

  Tous les calculs et toutes les règles métier vivent dans cet étage.
- **Étage 2 — Émetteurs.** Un émetteur par langage d'infrastructure et un par plateforme CI traduisent le plan de déploiement en fichiers. Un émetteur ne prend **aucune** décision métier : il traduit.

**Conséquences.**
- Ajouter un langage ou une plateforme revient à écrire un émetteur.
- Le résumé des changements d'une révision se calcule sur le plan de déploiement : il est identique quel que soit le langage.
- Le plan de déploiement est consultable (écran, API, MCP).
- La **parité** entre langages est testée : le projet de référence, émis dans chaque langage, doit produire les mêmes ressources Azure ([EXG-19](27-exigences-non-fonctionnelles.md)).

### DEC-45 — Modules et fournisseurs par langage

Remplace [DEC-14](#dec-14--les-modules-bicep-sont-les-azure-verified-modules).

**Décision.**

| Langage | Base de génération | Épinglage |
|---|---|---|
| Bicep | Azure Verified Modules Bicep (registre public `br/public`) | Version de module par descripteur |
| Terraform | Azure Verified Modules Terraform (registre Terraform), sur les fournisseurs `azurerm` et `azapi` | Versions des modules, des fournisseurs et version minimale de Terraform par catalogue |
| Pulumi | Fournisseur Azure Native (il n'existe pas d'AVM Pulumi) | Version du paquet `@pulumi/azure-native` par catalogue |

Sans module vérifié pour un type, l'émetteur écrit la ressource directement (ressource Bicep, ressource `azurerm`/`azapi`, ressource Azure Native).

**Conséquences.** Monter une version est une mise à jour du catalogue, testée par le projet de référence dans chaque langage.

### DEC-46 — Cycle de vie et état par langage

Remplace [DEC-15](#dec-15--déploiement-par-azure-deployment-stacks).

**Décision.**
- Règle commune : une ressource retirée du modèle est **détachée** par défaut (elle reste dans Azure, non gérée). Un composant peut choisir la **suppression**, jamais dans une cible protégée.
- Mise en œuvre par langage :

| Langage | Unité de déploiement | État | Détacher | Supprimer |
|---|---|---|---|---|
| Bicep | Pile de déploiement de portée abonnement, par composant × cible | Tenu par Azure (la pile) | `actionOnUnmanage: detach` | `actionOnUnmanage: delete` |
| Terraform | Configuration racine par composant, un état par composant × cible | Compte de stockage Azure du client créé par le kit : accès Entra uniquement, versioning, verrouillage par bail | Blocs `removed` avec `destroy = false`, générés pour les ressources retirées depuis la dernière révision publiée | Absence de bloc `removed` : Terraform détruit |
| Pulumi | Pile Pulumi par composant × cible | Conteneur blob Azure du client créé par le kit (défaut) ou Pulumi Cloud (choix du projet) ; secrets de l'état chiffrés par une clé Key Vault | Option `retainOnDelete` sur toutes les ressources | Pas d'option `retainOnDelete` |

**Conséquences.**
- Les états Terraform et Pulumi contiennent, par nature, des attributs sensibles des ressources : le kit les protège ([23](23-kit-installation.md)).
- IFS ne lit jamais un état.
- Les secrets de pipeline ne passent jamais par le code d'infrastructure : ils sont écrits dans Key Vault par une étape de pipeline ([RG-PAR-15](17-parametres-applicatifs-et-secrets.md)). Ils n'apparaissent donc dans aucun état.

### DEC-47 — Plusieurs plateformes CI

Remplace [DEC-24](#dec-24--azure-devops-en-v1-modèle-de-pipeline-neutre).

**Décision.**
- La **plateforme CI** est un choix du projet :

  | Plateforme | Lot |
  |---|---|
  | Azure DevOps Pipelines | 1 |
  | GitHub Actions | 1 |
  | GitLab CI | 3 |

- L'authentification des pipelines auprès d'Azure se fait **toujours** par fédération d'identité (OIDC), jamais par secret de principal de service.

| Concept IFS | Azure DevOps | GitHub Actions | GitLab CI *(lot 3)* |
|---|---|---|---|
| Dépôts compatibles | Azure Repos, GitHub | GitHub | GitLab |
| Connexion de déploiement | Service connection ARM (fédération) | Identifiant fédéré sur `repo:<dépôt>:environment:<env>` + variables d'environnement | Identifiant fédéré sur le jeton `id_tokens` |
| Cible protégée | Environnement + contrôle d'approbation | Environnement + relecteurs requis | Environnement protégé + approbations |
| Magasin de secrets | Groupe de variables | Secrets d'environnement | Variables CI/CD limitées à l'environnement |
| Modèles partagés | Modèles YAML | Actions composites | `include` |
| Exécuteurs | Pool d'agents | Libellés de runners | Tags de runners |
| Exclusivité de déploiement | Contrôle de verrou exclusif sur l'environnement | `concurrency` | `resource_group` |

**Conséquences.** Le kit d'installation a une partie Azure commune et une partie propre à chaque plateforme ([DEC-30](#dec-30--un-kit-dinstallation-complet-et-réconciliant)).

### DEC-48 — Un langage et une plateforme par projet, changeables

**Décision.**
- Le langage d'infrastructure et la plateforme CI se choisissent à la création du projet. En v1, un projet n'en a qu'un de chaque : pas de mélange par composant ([PO-07](04-perimetre-et-lots.md)).
- **Changer de plateforme CI** : la révision suivante produit les pipelines et le kit de la nouvelle plateforme. Les objets de l'ancienne sont listés comme orphelins dans la liste de contrôle.
- **Changer de langage** *(lot 2)* après publication est une **migration assistée**. IFS génère l'adoption des ressources déjà déployées :
  - vers Terraform : blocs `import` ;
  - vers Pulumi : option `import` ;
  - vers Bicep : la pile adopte les ressources existantes.

  La liste de contrôle décrit la bascule : déployer avec le nouveau langage, désactiver les anciens pipelines, retirer l'ancienne pile ou l'ancien état en mode détaché.

**Conséquences.** Un client n'est jamais enfermé dans son premier choix ; la migration réutilise les noms calculés, identiques d'un langage à l'autre.
