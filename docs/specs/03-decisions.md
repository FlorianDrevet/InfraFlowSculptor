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

> **Remplacée par [DEC-47](#dec-47--plusieurs-plateformes-ci)** : GitHub Actions rejoint Azure DevOps, GitLab CI arrive ensuite (lots fixés par [DEC-62](#dec-62--priorité-absolue-à-bicep-et-azure-devops) : GitHub Actions au lot 2, GitLab CI au lot 3).

**Décision.**
- Les pipelines produits en v1 sont des pipelines YAML Azure DevOps.
- Les dépôts peuvent être sur GitHub ou Azure DevOps Repos : Azure DevOps Pipelines sait lire un dépôt GitHub.
- La description des pipelines dans le modèle ne dépend pas de la plateforme. GitHub Actions arrive au lot 3.

### DEC-25 — Applications : build minimal et points d'extension

> **Remplacée par [DEC-50](#dec-50--pipelines-applicatifs-riches-par-catalogue-détapes)** : les étapes riches reviennent, par un catalogue d'étapes ; les points d'extension sont conservés.

**Constat.** La v0 tentait de gérer tests, couverture, Sonar, lint et analyse de dépendances pour sept piles, avec une détection automatique partielle. C'est un autre produit.

**Décision.**
- IFS construit (image conteneur, ou paquet par pile) et déploie les applications.
- Tests, qualité et analyses sont des **étapes fournies par le client**, que les pipelines générés appellent avant et après le build.
- Seule exception : le scan de l'image conteneur, option visible et activée par défaut.

### DEC-26 — Aucun mot de passe dans le modèle

> **Remplacée par [DEC-51](#dec-51--authentification-locale-permise-déconseillée)** : Entra reste le défaut recommandé ; mots de passe et clés sont permis, signalés comme déconseillés.

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

> **Remplacée par [DEC-49](#dec-49--limport-revient-en-dernier-lot)** : l'import ARM revient au lot 3, avec Bicep et groupe de ressources Azure.

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

> **Lots revus par [DEC-62](#dec-62--priorité-absolue-à-bicep-et-azure-devops)** : les numéros de lot cités ci-dessous sont remplacés par ceux de DEC-62.

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

> **Complétée par [DEC-85](#dec-85--retirer-un-accès-le-révoque-même-en-production) et [DEC-91](#dec-91--preuve-de-livraison-et-référence-des-retraits)** : les objets d'autorisation et de configuration sont toujours supprimés ; les retraits se calculent contre l'unité de déploiement, pas contre la dernière révision publiée.

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

> **Lots revus par [DEC-62](#dec-62--priorité-absolue-à-bicep-et-azure-devops)** : les numéros de lot cités ci-dessous sont remplacés par ceux de DEC-62.

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

> **Lots revus par [DEC-62](#dec-62--priorité-absolue-à-bicep-et-azure-devops)** : les numéros de lot cités ci-dessous sont remplacés par ceux de DEC-62.

**Décision.**
- Le langage d'infrastructure et la plateforme CI se choisissent à la création du projet. En v1, un projet n'en a qu'un de chaque : pas de mélange par composant ([PO-07](04-perimetre-et-lots.md)).
- **Changer de plateforme CI** : la révision suivante produit les pipelines et le kit de la nouvelle plateforme. Les objets de l'ancienne sont listés comme orphelins dans la liste de contrôle.
- **Changer de langage** *(lot 2)* après publication est une **migration assistée**. IFS génère l'adoption des ressources déjà déployées :
  - vers Terraform : blocs `import` ;
  - vers Pulumi : option `import` ;
  - vers Bicep : la pile adopte les ressources existantes.

  La liste de contrôle décrit la bascule : déployer avec le nouveau langage, désactiver les anciens pipelines, retirer l'ancienne pile ou l'ancien état en mode détaché.

**Conséquences.** Un client n'est jamais enfermé dans son premier choix ; la migration réutilise les noms calculés, identiques d'un langage à l'autre.

---

## Arbitrages après revue (2026-10-03)

### DEC-49 — L'import revient, en dernier lot

Remplace [DEC-32](#dec-32--pas-dimport-arm--import-dun-groupe-de-ressources-azure-au-lot-3).

**Décision.**
- Le lot 3 propose l'**import** d'une infrastructure existante depuis trois sources : un modèle ARM JSON, des fichiers Bicep (compilés en ARM par IFS), un groupe de ressources Azure (avec la connexion Azure en lecture).
- Contrairement à la v0, l'import :
  - passe par un écran de revue ;
  - répartit les ressources dans des composants et des groupes de ressources ;
  - reconstitue les surcharges par environnement quand on fournit une source par environnement ;
  - reprend identités, rôles et paramètres ;
  - peut cibler un projet existant.
- Le résultat est une **proposition de modification** ([DEC-31](#dec-31--mcp-dérivé-de-lapi-avec-propositions-de-modification)) relue avant application.

**Conséquences.** Voir [29](29-import.md).

### DEC-50 — Pipelines applicatifs riches, par catalogue d'étapes

Remplace [DEC-25](#dec-25--applications--build-minimal-et-points-dextension).

**Constat.** La v0 gérait tests, couverture, Sonar, lint et analyse des dépendances, mais codés en dur et partiellement. Le besoin client est réel ; c'est la méthode qui ne tenait pas.

**Décision.**
- Les étapes de qualité et de sécurité des pipelines applicatifs (tests, couverture et seuil, lint, analyse des dépendances, SonarQube/SonarCloud, analyse des secrets, scan d'image, SBOM, tests post-déploiement) sont décrites dans un **catalogue d'étapes** versionné, comme le catalogue de ressources ([DEC-13](#dec-13--le-catalogue-est-un-ensemble-de-descripteurs-versionnés)).
- Chaque étape déclare ses paramètres, ses valeurs par défaut par pile et sa traduction par plateforme.
- Chaque étape est d'abord une **commande en ligne** (outil de la pile) ; les fonctions natives de la plateforme ne servent qu'à **publier** les résultats. Cela garde la matrice piles × étapes × plateformes maîtrisable.
- Les points d'extension restent : ils couvrent tout ce que le catalogue ne couvre pas.

**Conséquences.**
- Lot 1 : build, scan d'image, points d'extension.
- Lot 2 : catalogue d'étapes pour .NET, Node/Angular, Python, Java, et suggestions par détection dans le dépôt.
- Lot 3 : PHP, Go.

Voir [19](19-applications-build-et-deploiement.md).

### DEC-51 — Authentification locale permise, déconseillée

Remplace [DEC-26](#dec-26--aucun-mot-de-passe-dans-le-modèle).

**Constat.** Beaucoup d'applications existantes utilisent encore un login SQL, une clé de stockage ou un utilisateur admin de registre. Les interdire bloque des clients ; les proposer sur un pied d'égalité affaiblit la sécurité.

**Décision.**
- Les types qui le supportent ont une propriété **Authentification** dont la valeur par défaut, et recommandée, est Entra seule. Exemples : SQL, PostgreSQL, registre, stockage, Service Bus, App Configuration.
- Activer l'authentification locale (mot de passe, clé, compte admin) :
  - est possible ;
  - est signalé « déconseillé » à l'écran, avec l'alternative Entra ;
  - produit l'avertissement `VAL-SEC-AUTH-LOCALE`, acquittable.
- **IFS ne connaît toujours aucune valeur** ([DEC-04](#dec-04--service-en-ligne-hébergé-dans-lue-sans-secret-applicatif)). Un mot de passe d'administration est :
  - soit **généré par la release** au premier déploiement et stocké dans un Key Vault choisi ;
  - soit fourni comme secret de pipeline.

  Il est transmis au code d'infrastructure comme entrée sensible. En Terraform, IFS utilise les attributs en écriture seule quand le fournisseur les propose (exemple : `administrator_login_password_wo`) ; sinon la valeur est dans l'état protégé et un constat `Info` le dit.
- Les tirages d'image par IFS restent faits par identité ([RG-LIA-09](16-liaisons-identites-et-acces.md)), même si le compte admin du registre est activé pour d'autres usages.

**Conséquences.** Voir [15](15-catalogue.md), [17 § 7](17-parametres-applicatifs-et-secrets.md).

### DEC-52 — Des droits fins : permissions, rôles, portées, équipes

**Constat.** Trois rôles de projet (propriétaire, contributeur, lecteur) obligeaient le propriétaire à tout configurer.

**Décision.**
- Les droits sont des **permissions** élémentaires ([10 § 4](10-organisations-et-acces.md)).
- IFS fournit des **rôles prédéfinis** qui les regroupent : lecteur, auditeur, développeur, contributeur, responsable des déploiements, architecte plateforme, administrateur de projet, propriétaire.
- Un rôle est attribué à un membre ou à une **équipe** de l'organisation.
- Sa **portée** est le projet entier ou une liste de composants : une équipe produit modifie ses composants sans toucher au reste.
- *(Lot 2)* : rôles personnalisés définis par l'organisation ; équipes synchronisées avec des groupes Entra ; publication à deux personnes.

### DEC-53 — Qui écrit la configuration d'une application

**Constat.** Deux pipelines touchent une même application : celui de l'infrastructure, qui la crée et la configure, et le pipeline applicatif, qui livre son code. Si les deux écrivent ses paramètres, le dernier passé gagne :
- un redéploiement d'infrastructure efface un paramètre posé par la livraison applicative, ou l'inverse ;
- on ne sait plus quelle valeur est en service ;
- les références de secrets et les rôles câblés par IFS peuvent être écrasés.

**Décision.**

| Élément de l'application | Propriétaire |
|---|---|
| Existence, plan, SKU, mise à l'échelle, identités, réseau, domaines | Pipeline d'infrastructure |
| Paramètres applicatifs : variables d'environnement, références Key Vault, clés App Configuration | Pipeline d'infrastructure |
| Code (paquet) ou image en service | Pipeline applicatif |
| Valeurs qui doivent changer sans redéploiement (feature flags, réglages fonctionnels) | App Configuration, lue à l'exécution par l'application |

Seule exception : le déploiement d'infrastructure reconduit l'image en service au lieu de la remplacer ([RG-APP-02](19-applications-build-et-deploiement.md)).

**Conséquences.**
- Changer un paramètre = modifier le modèle → révision → pull request → release d'infrastructure. Le code n'est pas relivré.
- Un code qui attend un nouveau paramètre : l'infrastructure est déployée d'abord (le paramètre ajouté est ignoré par l'ancien code), puis le code est livré. Le résumé de révision le signale : « à déployer avant la prochaine livraison de `api` ».
- Un paramètre modifié à la main dans le portail est remplacé au déploiement d'infrastructure suivant ; l'aperçu (what-if, plan) le montre avant.
- L'option inverse (paramètres écrits par le pipeline applicatif) est écartée : les paramètres sortiraient du modèle, de la validation, de la revue et du câblage implicite.

### DEC-54 — Source des modules au choix

> **Lots revus par [DEC-62](#dec-62--priorité-absolue-à-bicep-et-azure-devops)** : les numéros de lot cités ci-dessous sont remplacés par ceux de DEC-62.

Complète [DEC-45](#dec-45--modules-et-fournisseurs-par-langage).

**Décision.** Le projet choisit la source des modules de son code d'infrastructure, avec surcharge possible par type :

| Source | Contenu | Pour qui | Lot |
|---|---|---|---|
| **AVM du registre public** (défaut) | Référence au module vérifié épinglé | La plupart des équipes | 1 (Bicep), 2 (Terraform) |
| **AVM embarqués** | Copie des AVM épinglés dans le dépôt (`modules/avm/`) | Exécuteurs sans accès au registre public, audit, maîtrise totale | 2 |
| **Modules IFS** | Modules compacts écrits par IFS, un par type utilisé, qui couvrent exactement les propriétés du descripteur, dans `modules/` | Équipes qui veulent un code court, lisible et sans dépendance externe | 2 |
| **Modules du client** | Modules du registre privé du client (registre Bicep, registre Terraform privé, dépôt git), branchés par un **contrat de correspondance** déclaré dans IFS : entrées attendues, sorties fournies | Entreprises dont les modules internes sont imposés | 3 |

Pulumi n'a pas de modules vérifiés : sources « ressources directes » (défaut) ou « composants IFS » (`ComponentResource`, lot 3).

**Conséquences.**
- La parité ([EXG-19](27-exigences-non-fonctionnelles.md)) est testée pour chaque source livrée.
- Changer de source après publication : en Terraform, IFS génère des blocs `moved` pour que les ressources ne soient pas recréées ; en Bicep, les ressources gardent leur identifiant Azure et la pile les reprend.

### DEC-55 — IFS suit les déploiements

**Constat.** IFS ne savait pas si ses révisions étaient déployées ni si les déploiements réussissaient. L'indicateur principal de la vision n'était pas mesurable.

**Décision.**
- IFS lit, par les connexions qu'il a déjà, les exécutions des pipelines qu'il gère :
  - application GitHub : permission de lecture sur Actions, événements `workflow_run` ;
  - principal de service Azure DevOps : lecture des builds.
- Chaque pipeline généré porte le numéro de révision. IFS sait donc, par composant et par cible, quelle révision est déployée, laquelle attend une approbation, laquelle a échoué.
- Aucun accès à Azure n'est nécessaire.

**Conséquences.** Voir [28](28-suivi-des-deploiements.md). La liste de contrôle se coche seule quand un premier déploiement réussit.

### DEC-56 — Export, import et modèles de projet

**Décision.**
- Un projet s'**exporte** en un document JSON versionné (modèle complet, sans aucun secret, puisqu'IFS n'en a pas) et s'**importe** dans une organisation.
- *(Lot 2)* Les organisations peuvent enregistrer des **modèles de projet** et des **modèles de composant** (exemple : « API Container App + base SQL + file Service Bus ») réutilisables, et IFS fournit des modèles de départ.

**Conséquences.** Le modèle lui-même est sans verrou, pas seulement la sortie ([P10](01-principes.md)). Voir [11 § 6](11-projets-et-environnements.md).

### DEC-57 — Exploitation d'IFS : catalogue publié par cycle, accès support consenti

> **Modifiée par [DEC-92](#dec-92--version-du-catalogue-figée-par-projet)** : un projet reste sur sa version du catalogue ; la montée est explicite.

**Décision.**
- Le catalogue (ressources et étapes) suit un cycle **brouillon → validation automatique → publication → dépréciation**. La validation automatique consiste à déployer le projet de référence dans toutes les variantes livrées.
- Les projets suivent la dernière version publiée. Un propriétaire peut épingler une version 90 jours au plus. Cela tranche l'ancien [PO-02](04-perimetre-et-lots.md).
- Le personnel d'IFS n'a **aucun accès permanent** aux données des clients. Un accès support :
  - est demandé ;
  - est approuvé par un administrateur de l'organisation ;
  - dure 72 heures au plus ;
  - est en lecture seule par défaut ;
  - est journalisé dans le journal d'audit du client.

**Conséquences.** Voir [40](40-exploitation-ifs.md).

### DEC-58 — Le lot 1 est livré en trois jalons, tranche verticale d'abord

> **Complétée par [DEC-93](#dec-93--un-pilote-avant-le-lot-1-commercial-et-un-modèle-principal-toujours-générable)** : un jalon 0 (pilote) précède le jalon 1 ; les brouillons arrivent au jalon 2.

> **Lots revus par [DEC-62](#dec-62--priorité-absolue-à-bicep-et-azure-devops)** : les numéros de lot cités ci-dessous sont remplacés par ceux de DEC-62.

**Constat.** Le lot 1 est gros. Livrer tout en largeur retarderait la preuve de la promesse.

**Décision.**
- **Jalon 1 — tranche verticale** : le projet de référence de bout en bout en Bicep + Azure DevOps, avec les seuls types qu'il utilise, l'organisation, les rôles prédéfinis, la validation, les révisions, la publication, le kit et le suivi des déploiements.
- **Jalon 2 — largeur** : GitHub Actions, types restants du lot 1, import de paramètres, comparaison de révisions, équipes et portées par composant.
- **Jalon 3 — outillage** : serveur MCP et propositions, vue graphe, recherche globale, journal d'audit complet avec export.

**Conséquences.** Voir [04 § 2](04-perimetre-et-lots.md).

### DEC-59 — Comptes Microsoft personnels acceptés

**Décision.**
- La connexion accepte les comptes professionnels Entra ID **et** les comptes Microsoft personnels. Le coût est faible et les essais individuels deviennent possibles.
- Une organisation peut restreindre ses membres à un ou plusieurs tenants Entra.

### DEC-60 — Préréglages de nommage

**Décision.** L'assistant propose trois préréglages de gabarit par défaut :
- **Compact** (défaut) : `{abbr}-{project}-{name}-{env}` ;
- **CAF** : `{abbr}-{project}-{name}-{env}-{region}`, aligné sur le Cloud Adoption Framework ;
- **Personnalisé**.

### DEC-61 — Les ressources détachées sont suivies

**Décision.** Quand le suivi des déploiements ([DEC-55](#dec-55--ifs-suit-les-déploiements)) confirme qu'une révision qui détache des ressources est déployée, IFS les inscrit dans l'**inventaire des ressources détachées** de la cible :
- nom, type, groupe, date, révision ;
- commande de suppression proposée.

L'utilisateur les marque « supprimées » ou « conservées ». Aucune ressource ne devient orpheline sans trace.

---

## Seconde revue : priorités, historique, IA, réseau, déploiements, et fin des points ouverts (2026-10-03)

### DEC-62 — Priorité absolue à Bicep et Azure DevOps

Remplace les numéros de lot cités dans [DEC-43](#dec-43--azure-seulement-plusieurs-langages-dinfrastructure), [DEC-47](#dec-47--plusieurs-plateformes-ci), [DEC-48](#dec-48--un-langage-et-une-plateforme-par-projet-changeables), [DEC-54](#dec-54--source-des-modules-au-choix) et [DEC-58](#dec-58--le-lot-1-est-livré-en-trois-jalons-tranche-verticale-dabord).

**Décision.**

| Lot | Langages | Plateformes CI | Thème |
|---|---|---|---|
| 1 | Bicep | Azure DevOps | Premier déploiement réussi (3 jalons) |
| 2 | Bicep | Azure DevOps, **GitHub Actions** | Production d'entreprise : réseau privé, IA, stratégies de déploiement, gouvernance, historique |
| 3 | **Terraform** (+ migration Bicep ↔ Terraform) | + GitLab CI | Ouverture, import, ressources complémentaires |
| 4 | **OpenTofu**, **Pulumi** (TypeScript, C#, Python, Go, Java, YAML) | — | Langages complémentaires, multi-région, AKS, instance dédiée |

Le garde-fou du lot 1 est renforcé : un prototype d'émetteur Terraform **et** un prototype d'émetteur GitHub Actions, non livrés, valident la neutralité du plan de déploiement.

### DEC-63 — OpenTofu et tous les langages Pulumi au lot 4

Tranche [PO-08 et PO-09](04-perimetre-et-lots.md).

**Décision.**
- **OpenTofu** est un langage cible à part entière : même HCL que Terraform, mais versions minimales, registre (`registry.opentofu.org`) et tests propres. Les fonctions propres à OpenTofu (chiffrement natif de l'état) sont utilisées : l'état OpenTofu est chiffré par une clé Key Vault.
- **Pulumi** est produit dans les six langages : TypeScript, C#, Python, Go, Java et YAML. Les émetteurs partagent une même traduction du plan de déploiement vers les ressources Azure Native ; seule la syntaxe change.

### DEC-64 — Historique du modèle, versions et restauration

**Constat.** Les révisions historisent la sortie, pas le modèle. Une erreur de saisie ne se défaisait qu'à la main, et on ne pouvait pas voir l'état du modèle à une date passée.

**Décision.**
- Chaque modification du modèle produit un **jeu de modifications** immuable : auteur, date, origine, message, changements avant/après par objet et par propriété. Le modèle a une version qui s'incrémente.
- On peut **étiqueter** une version, **consulter** le modèle tel qu'il était à n'importe quelle version, **comparer** deux versions, **annuler** un jeu de modifications et **restaurer** tout ou partie du modèle à une version.
- Une annulation ou une restauration n'efface rien : elle produit un nouveau jeu de modifications (ou une proposition en cas de conflit).
- *(Lot 2)* **Brouillons** : des espaces de travail où l'on prépare des modifications sans toucher au modèle principal, puis qu'on soumet comme proposition.

**Conséquences.** Voir [31](31-historique-et-versions.md). Revenir en arrière sur l'infrastructure = restaurer le modèle, générer, publier, déployer.

### DEC-65 — Qui a modifié quoi, partout

Complète [DEC-34](#dec-34--journal-daudit).

**Décision.**
- Chaque objet affiche son auteur de création et sa dernière modification.
- Chaque propriété affiche au survol qui l'a modifiée en dernier, quand, et par quel jeu de modifications.
- L'historique fonctionnel (jeux de modifications, restaurables, conservés toute la vie du projet) et le journal d'audit (toutes les actions sensibles, immuable, 13 mois) sont complémentaires et liés : chaque événement d'audit de modification renvoie à son jeu de modifications.

### DEC-66 — Priorités du catalogue

**Décision.** Les types sont classés par importance pour une application d'entreprise courante :

| Priorité | Types | Lot |
|---|---|---|
| Socle applicatif | Log Analytics, Application Insights, Key Vault, identité managée, stockage, plan App Service, Web App, Function App, registre, environnement Container Apps, Container App, SQL (serveur, base), PostgreSQL, Service Bus, App Configuration, **Azure Managed Redis** | 1 |
| Front-end statique | **Static Web App**, avec son parcours applicatif | 2 (vague C) |
| Réseau et sécurité | VNet et subnets, appairage, NSG, table de routage, passerelle NAT, IP publique, zone DNS privée, zone DNS publique, point de terminaison privé (généré), pool d'exécuteurs privé | 2 (vague A) |
| IA | Compte et projets Microsoft Foundry, déploiements de modèles, AI Search, Cosmos DB | 2 (vague B) |
| Exposition, intégration, exploitation | Front Door et WAF, Application Gateway et WAF, API Management, Event Grid, Event Hubs, Container Apps Jobs, groupes d'actions, alertes, tests de disponibilité, budgets | 2 (vagues E et F) |
| Complémentaires | MySQL, SignalR, Web PubSub, Communication Services (e-mail), Logic Apps Standard, Container Instances, Azure Firewall, DNS Private Resolver, Bastion, Managed Grafana | 3 |
| Conteneurs orchestrés | AKS (cluster seulement ; le déploiement d'applications dans AKS reste hors périmètre) | 4 |

Azure Managed Redis entre au lot 1 : un cache fait partie de la plupart des applications. Static Web Apps, prévu au lot 1 à l'origine, passe au lot 2 avec la vague C ([DEC-93](#dec-93--un-pilote-avant-le-lot-1-commercial-et-un-modèle-principal-toujours-générable)) : un type ne sort qu'avec son parcours complet (création, build, droits, livraison, retrait).

### DEC-67 — Applications d'IA : Microsoft Foundry

**Décision.** IFS modélise les applications d'IA générative sur **Microsoft Foundry** :
- ressource Foundry (compte Azure AI Services avec gestion de projets) ;
- projets ;
- déploiements de modèles avec capacité par environnement ;
- connexions identité-à-identité vers AI Search, stockage et Cosmos DB ;
- configuration « standard » des agents (données des agents dans les ressources du client), réseau privé des agents.

Les projets « hub » de l'ancienne génération ne sont pas modélisés. Une application consomme un modèle par une liaison qui accorde le rôle et fournit l'adresse et le nom du déploiement.

**Conséquences.** Voir [32](32-ia-et-foundry.md). Lot 2, vague B.

### DEC-68 — Réseau privé d'entreprise complet

Complète [DEC-28](#dec-28--réseau-privé-complet-ou-rien-lot-2).

**Décision.** Le lot 2 (vague A, en tête) couvre :
- topologies simple et **hub and spoke**, avec **appairages** (y compris vers un hub existant d'un autre abonnement) ;
- **zones DNS privées** centralisées et leurs liens, générées automatiquement à partir des points de terminaison privés ;
- **passerelle NAT**, **tables de routage** (vers un pare-feu existant), NSG ;
- **zone DNS publique** (enregistrements des domaines personnalisés générés) ;
- **exécuteurs privés** pour les pipelines : Managed DevOps Pools (Azure DevOps), réseau privé des runners hébergés GitHub.

Azure Firewall et DNS Private Resolver comme ressources : lot 3.

**Conséquences.** Voir [18](18-reseau-et-exposition.md).

### DEC-69 — Abonnement par composant et par environnement

**Constat.** Un environnement n'a qu'un abonnement. Un composant « connectivité » qui vit, pour chaque environnement, dans l'abonnement de connectivité (modèle Azure Landing Zones) ne pouvait pas être modélisé.

**Décision.** Un composant `PerEnvironment` peut surcharger, pour chaque environnement, l'**abonnement** et la **connexion de déploiement** de la cible. La région reste celle de l'environnement.

### DEC-70 — Stratégies de déploiement applicatif

Complète [DEC-53](#dec-53--qui-écrit-la-configuration-dune-application).

**Décision.** Chaque application choisit une stratégie de livraison :

| Stratégie | Types | Mécanisme Azure |
|---|---|---|
| Directe (défaut) | Tous | Remplacement ; sans coupure si l'application a des sondes de disponibilité (Container Apps) ou plusieurs instances |
| Mise à jour progressive des instances | Function App Flex | Stratégie de mise à jour `RollingUpdate` |
| Slot et bascule | Web App, Function App (plans avec slots) | Déploiement dans un slot, contrôle de santé, échange |
| Bleu/vert | Container App | Révisions multiples, étiquettes, bascule du trafic |
| Progressive (canary) | Container App ; Web App et Function App par routage de trafic vers le slot | Paliers de trafic avec observation et contrôle de santé |

**La répartition du trafic et le contenu des slots appartiennent au pipeline applicatif.** L'infrastructure crée les slots et règle le mode de révision, puis reconduit le trafic en service, comme l'image ([RG-APP-02](19-applications-build-et-deploiement.md)).

**Conséquences.** Voir [19 § 9](19-applications-build-et-deploiement.md). Lot 2.

### DEC-71 — Politiques d'organisation

**Décision.** *(Lot 2)*
- Une organisation définit des **politiques** que tous ses projets respectent : régions autorisées, types ou SKU interdits, exposition publique interdite en cible protégée, authentification locale interdite en cible protégée, tags obligatoires, redondance minimale en cible protégée, préfixe de nommage imposé.
- Une politique produit des constats (`Erreur` ou `Avertissement`).
- Une dérogation par projet est possible, justifiée, datée, approuvée par un administrateur.

**Conséquences.** Voir [33 § 2](33-gouvernance-couts-et-supervision.md).

### DEC-72 — Estimation des coûts et budgets

**Décision.** *(Lot 2)*
- IFS estime le coût mensuel de chaque ressource, composant, environnement et projet à partir de l'API publique des prix Azure (sans accès aux abonnements). Les hypothèses d'usage sont explicites (exemple : Go ingérés par jour).
- Le résumé de chaque révision indique la variation estimée.
- Les **budgets** Azure sont modélisables, avec seuils et alertes.

**Conséquences.** Voir [33 § 3](33-gouvernance-couts-et-supervision.md).

### DEC-73 — Supervision comme du code

**Décision.** *(Lot 2)*
- Groupes d'actions, alertes de métriques et de journaux, tests de disponibilité sont modélisables.
- Chaque type propose des **alertes recommandées** (exemple : erreurs 5xx d'une application, messages en lettres mortes d'une file), activables par composant avec des seuils par environnement.

**Conséquences.** Voir [33 § 4](33-gouvernance-couts-et-supervision.md).

### DEC-74 — Contrôle de dérive par pipeline planifié

Révise le hors périmètre de [04](04-perimetre-et-lots.md) sans revenir sur [DEC-04](#dec-04--service-en-ligne-hébergé-dans-lue-sans-secret-applicatif).

**Décision.** *(Lot 2)*
- Un pipeline planifié, généré par IFS, exécute chaque jour l'aperçu (what-if, plan) de chaque composant dans chaque cible et publie un résumé.
- IFS lit ce résumé par le suivi des déploiements et signale toute **dérive** (écart entre Azure et la dernière révision déployée).
- IFS ne lit toujours pas Azure lui-même et ne corrige rien automatiquement.

### DEC-75 — Documentation d'architecture générée

**Décision.** Chaque révision produit un `README.ifs.md` par destination contenant :
- les composants et leur ordre de déploiement ;
- un **diagramme Mermaid** des ressources et des liaisons ;
- le tableau des noms Azure par environnement, des rôles attribués et des paramètres (sans valeur secrète) ;
- les procédures de déploiement et de retour arrière.

Lot 1, jalon 3.

### DEC-76 — Commentaires et mentions

**Décision.** *(Lot 2)*
- On peut commenter un objet du modèle, une proposition ou une révision, et mentionner un membre (`@nom`), qui est notifié.
- Un fil de commentaires se résout.
- Les commentaires d'une proposition font partie de sa revue.

### DEC-77 — Fenêtres de déploiement

**Décision.** *(Lot 2)*
- Une cible peut déclarer des **fenêtres de déploiement** (jours, heures, fuseau) et un **délai d'attente** avant déploiement.
- Le kit les traduit dans la plateforme : contrôle « heures ouvrées » d'Azure DevOps ; règle de délai d'attente et branche de déploiement de GitHub.

### DEC-78 — Modèle commercial

Tranche [PO-01](04-perimetre-et-lots.md).

**Décision.** Trois plans. Les prix sont des **valeurs de lancement**, révisables par une nouvelle décision :

| Plan | Pour qui | Limites | Prix de lancement |
|---|---|---|---|
| **Découverte** | Essai, individus | 1 projet, 2 environnements, 25 ressources ; génération et téléchargement ; publication vers 1 dépôt ; pas de MCP en écriture | Gratuit |
| **Équipe** | Équipes et PME | 20 projets, 500 ressources par projet ; toutes les fonctions des lots livrés, sauf celles du plan Entreprise | 39 € par membre actif et par mois (3 minimum) |
| **Entreprise** | Grands comptes, intégrateurs | Volumes contractuels ; politiques d'organisation, rôles personnalisés, équipes synchronisées avec Entra, publication à deux personnes, SLA 99,9 %, support prioritaire, instance dédiée en option ([DEC-79](#dec-79--instance-dédiée-et-région-dhébergement)) | Sur devis, à partir de 1 500 € par mois |

- Un **membre actif** est un membre qui a modifié le modèle, généré ou publié dans le mois. Les lecteurs, auditeurs et approbateurs sont gratuits.
- Le **mode découverte** permet de modéliser, valider, générer et télécharger sans aucune connexion git ([RG-VAL-01](20-validation.md)).
- **Règles de facturation** :
  - un membre actif est compté pour le mois civil dès sa première modification, génération ou publication, même s'il part en cours de mois ;
  - l'action d'un agent ou d'un jeton compte pour l'utilisateur qui porte le jeton ;
  - un compteur des membres actifs et des limites du plan est visible par les administrateurs ;
  - un dépassement de limite bloque la création concernée, jamais la lecture, la génération ni l'export ;
  - passer à un plan inférieur au-delà de ses limites rend en lecture seule ce qui dépasse, sans rien supprimer ;
  - un impayé ouvre 14 jours de grâce, puis la suspension ([UC-EXP-03](40-exploitation-ifs.md)) : lecture et export restent possibles ;
  - le coût d'exploitation et de support par organisation active est mesuré pour suivre la marge.

### DEC-79 — Instance dédiée et région d'hébergement

Tranche [PO-03](04-perimetre-et-lots.md).

**Décision.**
- Le service mutualisé est hébergé **uniquement dans l'Union européenne**.
- Au lot 4, le plan Entreprise propose une **instance dédiée** : même version, déployée et opérée par l'équipe IFS dans un abonnement dédié, dans la région choisie par le client.
- IFS ne fournit pas de version installable par le client.

### DEC-80 — Multi-région

Tranche [PO-04](04-perimetre-et-lots.md).

**Décision.** *(Lot 4)*
- Un environnement peut déclarer des **régions secondaires**.
- Un composant marqué **multi-région** est déployé une fois par couple (environnement, région), chaque instance étant une unité de déploiement distincte ; ses gabarits doivent contenir `{region}`.
- Une liaison d'un composant multi-région vers un composant multi-région vise l'instance de la même région ; vers un composant mono-région, la région principale.
- Le routage global (Front Door) et la réplication des données (Cosmos DB multi-région, géo-réplication SQL) se modélisent explicitement.
- Les stages de release déploient la région principale, puis les secondaires.

### DEC-81 — Attributions de rôle à portée externe

Tranche [PO-06](04-perimetre-et-lots.md).

**Décision.** En Bicep, une attribution de rôle dont la portée est dans un autre abonnement que celui de la cible est **toujours** déployée par une **pile dédiée**, créée dans l'abonnement de la portée par la release du composant consommateur, et nommée `ifs-<projet>-<composant>-<cible>-ext-<abonnement court>`. Cette solution fonctionne quel que soit le comportement des piles pour les modules d'un autre abonnement ; le prototype n'est plus nécessaire pour décider.

### DEC-82 — Un seul langage par projet

Tranche [PO-07](04-perimetre-et-lots.md).

**Décision.**
- Un projet a un seul langage d'infrastructure, définitivement : un seul format d'état, un seul kit, un seul jeu de pipelines.
- Deux équipes qui veulent des langages différents font deux projets ; un projet peut référencer les ressources de l'autre comme ressources existantes.
- La migration assistée ([DEC-48](#dec-48--un-langage-et-une-plateforme-par-projet-changeables)) couvre le changement complet.

### DEC-83 — Télémétrie produit

Tranche [PO-10](04-perimetre-et-lots.md).

**Décision.**
- IFS mesure l'usage de ses fonctions (événements, durées, erreurs) avec un identifiant d'utilisateur pseudonymisé, sans aucun contenu du modèle (ni noms, ni valeurs, ni identifiants Azure). Conservation 13 mois.
- Un administrateur d'organisation peut la désactiver ; seuls les journaux nécessaires à la sécurité et à la facturation restent.

### DEC-84 — Reprise des projets de l'ancienne application

**Décision.** Un **convertisseur ponctuel** lit la base de l'ancienne application (v0) et produit, pour chaque projet, un export au format `ifs-project/v1` ([DEC-56](#dec-56--export-import-et-modèles-de-projet)), importable dans la nouvelle. La conversion fait de son mieux : ce qui n'a pas d'équivalent est listé dans un rapport. Outil interne, livré au jalon 3 du lot 1, pas une fonction du produit.

---

## Troisième revue : cycle de vie, sécurité des effets et preuve de livraison (2026-10-03)

> Ces décisions traitent la revue externe [docs/reviews/2026-10-03-revue-fonctionnelle-v1.md](../reviews/2026-10-03-revue-fonctionnelle-v1.md).
> Le traitement constat par constat est dans [docs/reviews/2026-10-03-traitement-revue-v1.md](../reviews/2026-10-03-traitement-revue-v1.md).

### DEC-85 — Retirer un accès le révoque, même en production

Complète [DEC-46](#dec-46--cycle-de-vie-et-état-par-langage).

**Constat.** « Détacher » protège les données, mais une attribution de rôle est aussi une ressource : détachée, elle resterait active alors que le modèle la montre retirée.

**Décision.**
- Le descripteur classe chaque ressource générée dans l'une de deux familles :
  - **ressources à données ou à état** (stockage, bases, coffres, registres, files, applications…) : elles suivent la règle du composant, détacher ou supprimer ([DEC-46](#dec-46--cycle-de-vie-et-état-par-langage)) ;
  - **objets d'autorisation et de configuration** (attributions de rôle, politiques d'accès Redis, utilisateurs de base créés par IFS, clés App Configuration, paramètres de diagnostic, règles de pare-feu, identifiants fédérés) : ils sont **toujours supprimés** quand ils sortent du modèle, y compris en cible protégée.
- En Bicep, la pile détache tout ; une étape de la release supprime ensuite explicitement les objets d'autorisation et de configuration sortis de la pile, et inscrit chaque suppression dans le rapport de la release. En Terraform, ces objets ne reçoivent jamais de bloc `removed`. En Pulumi, jamais `retainOnDelete`.
- Le résumé de chaque révision et l'aperçu de chaque release distinguent « accès révoqués » et « ressources détachées ».

**Conséquences.** La révocation est prouvée par le rapport de la release ([DEC-91](#dec-91--preuve-de-livraison-et-référence-des-retraits)). Le projet de référence vérifie qu'une application perd effectivement l'accès retiré ([90](90-projet-de-reference.md)).

### DEC-86 — Séquencement d'une release d'infrastructure

**Constat.** Au premier déploiement, un coffre, ses droits, ses secrets et leurs consommateurs dépendent les uns des autres ; la spec ne fixait pas l'ordre.

**Décision.**
- Une release d'un composant dans une cible enchaîne, dans un ordre fixe : contrôle de l'aperçu approuvé, lecture des secrets nécessaires, déploiement de l'unité (pile, état), révocations, écritures de plan de données (secrets, mots de passe générés, clés App Configuration, accès aux données), domaines ([22 § 3](22-pipelines.md)).
- **Les coffres alimentés par la release précèdent leurs consommateurs.** Un Key Vault qui reçoit un secret de pipeline ou un mot de passe généré appartient à un composant déployé avant tout composant qui consomme ce secret, ou est une ressource existante. Sinon : erreur `VAL-SEC-ORDRE`. L'assistant crée par défaut un composant socle qui porte les coffres.
- Chaque étape est idempotente. Relancer une release reprend tout depuis le début et ne refait que ce qui manque. Les droits tout juste attribués sont attendus par réessais, 10 minutes au plus ; les autres erreurs ne sont pas réessayées.

**Conséquences.** Le premier déploiement réussit depuis un abonnement vide sans seconde exécution corrective ; c'est un critère d'acceptation ([90](90-projet-de-reference.md)).

### DEC-87 — L'approbateur voit l'aperçu de ce qui sera appliqué

**Constat.** Azure DevOps évalue les approbations **avant** de démarrer un stage : un aperçu calculé dans le stage approuvé arrive trop tard. Le what-if n'est pas disponible pour les piles de déploiement.

**Décision.**
- Chaque cible a deux stages : **Aperçu**, qui n'utilise pas l'environnement protégé, puis **Déploiement**, sur l'environnement, avec l'approbation.
- L'aperçu Bicep combine le what-if ARM du modèle à la portée de l'abonnement et la comparaison entre les ressources gérées par la pile et celles du nouveau modèle (ressources qui seront détachées ou supprimées, accès révoqués). Ses limites sont écrites dans le résumé. Terraform conserve le plan ; Pulumi conserve l'aperçu.
- L'aperçu produit une **empreinte**. Le stage Déploiement recalcule l'aperçu ; si l'empreinte diffère de celle qui a été approuvée, il s'arrête sans rien modifier et demande de relancer la release.
- Les stages d'infrastructure et d'applications d'une même cible partagent le verrou exclusif de l'environnement, en mode **séquentiel** : aucune exécution n'est abandonnée, et un déploiement d'application ne peut pas s'intercaler entre l'aperçu et le déploiement d'infrastructure.

### DEC-88 — Identités séparées pour l'infrastructure et la livraison des applications

**Décision.**
- Le kit crée deux identités par cible : `id-ifs-deploy-<projet>-<cible>` (infrastructure : Contributor et administration RBAC conditionnée) et `id-ifs-app-<projet>-<cible>` (livraison des applications), chacune avec sa connexion de déploiement.
- L'identité applicative ne reçoit que : `AcrPush` sur le registre de build, et le droit de mettre à jour chaque application qu'elle livre, à la portée de cette application. Ces droits sont attribués par le déploiement d'infrastructure.
- Chaque connexion n'est autorisée que pour ses pipelines : infrastructure ou applications. Les pipelines de pull request n'ont **aucune** connexion Azure.
- Les points d'extension du client s'exécutent avec la connexion du pipeline qui les appelle, jamais avec une connexion plus large.

### DEC-89 — Les effets indirects d'une commande exigent les permissions correspondantes

**Constat.** Une liaison créée dans un composant peut ouvrir un accès à une ressource d'un autre composant ; une suppression ou une restauration peut toucher des objets hors de la portée de son auteur.

**Décision.**
- IFS calcule tous les effets d'une commande avant de l'appliquer, y compris les éléments implicites, et vérifie la permission de l'auteur sur **chaque** objet touché.
- Une liaison qui produit un accès (rôle, accès aux données, lecture de secret, lecture de configuration, tirage d'image, stockage hôte, usage d'IA) vers une ressource d'un autre composant exige `modele.modifier` sur le composant cible. Sinon elle devient une **demande d'accès** : une proposition que relit une personne qui a cette permission. *(Lot 2 : un composant peut ouvrir ses liaisons entrantes sans demande, à tous ou à une liste.)*
- Une suppression en cascade qui toucherait un objet hors de la portée de l'auteur est refusée ; IFS liste les objets et leurs responsables.
- Appliquer une proposition, une annulation ou une restauration exige, pour chaque effet, la permission qu'exigerait la modification directe. `propositions.appliquer` ne donne aucun droit supplémentaire.
- On n'attribue qu'un rôle dont on détient toutes les permissions. Seul un propriétaire attribue le rôle Propriétaire.

### DEC-90 — Accès aux données sans permission d'annuaire

**Constat.** `CREATE USER … FROM EXTERNAL PROVIDER` exécuté par une identité de service oblige l'identité du serveur SQL à lire l'annuaire Entra (Microsoft Graph), un prérequis lourd que la spec ne décrivait pas.

**Décision.**
- Azure SQL : le script crée l'utilisateur avec `CREATE USER [<nom>] WITH SID = <identifiant client converti>, TYPE = E`, forme documentée qui ne consulte pas l'annuaire. L'identifiant client de chaque identité est une sortie du déploiement.
- PostgreSQL : `pgaadauth_create_principal_with_oid`, avec l'identifiant d'objet, pour la même raison.
- Seul prérequis restant : l'identité de déploiement est membre du groupe administrateur Entra du serveur (étape du kit, constat `VAL-LIA-SQL-ADMIN` tant qu'elle n'est pas confirmée).
- Retirer un accès aux données retire les rôles **et** supprime l'utilisateur créé par IFS ([DEC-85](#dec-85--retirer-un-accès-le-révoque-même-en-production)). Un utilisateur qu'IFS n'a pas créé n'est jamais touché.

### DEC-91 — Preuve de livraison et référence des retraits

**Décision.**
- Chaque release produit un **rapport** (`ifs-report.json`, artefact du run) : révision, commit, empreinte du manifeste, cible, étapes terminées, ressources détachées, accès révoqués, résultat. IFS le lit par la connexion de l'organisation. La correspondance entre une exécution et une révision repose sur le commit et l'empreinte du manifeste, jamais sur le seul nom du run.
- L'état affiché par composant et cible distingue : **déployée** (toutes les étapes terminées), **partiellement appliquée** (le déploiement a modifié Azure, une étape suivante a échoué), **en échec avant modification**, **inconnu** (information indisponible), avec la date de la dernière lecture.
- Les ressources retirées sont calculées au moment de l'application, contre ce que l'unité de déploiement gère réellement (pile Bicep, état Terraform, pile Pulumi), pas contre la dernière révision publiée. Une cible qui saute des révisions retire donc tout ce qui doit l'être.
- Terraform : un bloc `removed` reste généré tant qu'au moins une cible du composant n'a pas rapporté de déploiement réussi d'une révision qui le contenait.
- L'inventaire des ressources détachées est alimenté par les rapports, pas par une déduction.
- Le suivi des pull requests (commit, fusion, fermeture) passe au lot 1.

### DEC-92 — Version du catalogue figée par projet

Remplace le point « les projets suivent la dernière version publiée » de [DEC-57](#dec-57--exploitation-difs--catalogue-publié-par-cycle-accès-support-consenti).

**Décision.**
- Un projet reste sur sa version du catalogue. Une nouvelle version est **proposée** avec la liste des fichiers et des valeurs qu'elle changerait ; la montée est une action explicite (`projet.administrer`), journalisée, qui produit un jeu de modifications d'origine `Mise à jour du catalogue`.
- Une version reste utilisable 12 mois après la publication de la suivante. Ensuite la génération exige la montée.
- Exception : un correctif de sécurité ou un retrait imposé par Azure est appliqué à toutes les versions supportées, annoncé aux administrateurs, et cité dans le résumé de la révision suivante.
- Une révision ancienne se régénère à l'identique avec sa version du catalogue, tant que celle-ci est supportée.

### DEC-93 — Un pilote avant le lot 1 commercial, et un modèle principal toujours générable

**Décision.**
- Le lot 1 commence par un **jalon 0 — pilote** ([04 § 2.0](04-perimetre-et-lots.md)) : Bicep, Azure DevOps, Azure Repos, un dépôt, deux environnements, une application conteneur, un catalogue réduit, avec 3 à 5 équipes pilotes qualifiées. Il démontre la séquence : création, première livraison, modification, ajout d'un accès, révocation, échec partiel, reprise.
- Le passage aux jalons suivants est conditionné aux mesures du pilote (temps actif, interventions, retouches manuelles, support nécessaire).
- Les prototypes Terraform et GitHub Actions du garde-fou ([DEC-62](#dec-62--priorité-absolue-à-bicep-et-azure-devops)) couvrent un sous-ensemble représentatif du projet de référence, pas tout le projet.
- Au jalon 0 et au jalon 1, un projet a un éditeur coordinateur qui intègre les changements. Les **brouillons** passent du lot 2 au jalon 2 du lot 1 ; dès qu'ils existent, le modèle principal n'accepte plus une application qui ajoute une erreur de validation : le travail incomplet reste dans son brouillon et ne bloque ni ne pollue la livraison des autres.

### DEC-94 — Des fichiers générés stables

**Décision.** L'en-tête des fichiers générés ne contient pas le numéro de révision. Une génération ne modifie que les fichiers dont le contenu change réellement ; la révision et les empreintes vivent dans le manifeste. Un composant sans changement fonctionnel garde des fichiers identiques et ne déclenche aucun pipeline.

### DEC-95 — Secret saisi par erreur

**Décision.**
- Avant tout enregistrement, IFS analyse les valeurs littérales (motifs de jetons connus, chaînes de connexion, forte entropie). Une valeur détectée est **refusée** à la saisie, avec la conversion en secret de pipeline proposée. L'utilisateur peut la forcer en déclarant qu'elle n'est pas secrète ; c'est journalisé.
- Un administrateur d'organisation peut **expurger** une valeur déjà enregistrée : elle est remplacée par une marque dans le modèle, l'historique, les révisions non publiées et les propositions ; l'audit garde l'acte, pas la valeur. IFS rappelle que la valeur doit être renouvelée chez le client et qu'elle a pu être publiée dans un dépôt git, qu'IFS ne réécrit pas.

### DEC-96 — Opérations sensibles des agents confirmées par un humain, côté serveur

**Décision.**
- Les commandes d'administration (membres, rôles, jetons, connexions, politiques, export, suppression de projet) ne sont pas exposées au MCP.
- `publish_revision` crée une **demande de publication** qu'une personne qui a `publier` confirme dans l'écran, après avoir vu le résumé de la révision. Un projet peut autoriser la publication directe par agent (`projet.administrer`), uniquement en mode pull request.
- Par défaut, les agents ont le mode proposition ; le mode direct ([RG-MCP-10](25-agent-ia-mcp.md)) s'active par projet.

### DEC-97 — Statut des décisions et registre des preuves

**Constat.** Une décision documentée clôt un débat ; elle ne démontre pas que le comportement est réalisable.

**Décision.**
- Chaque garantie critique porte un statut : **décidée**, **spécifiée**, **prototypée**, **vérifiée**.
- Le registre des preuves ([04 § 7](04-perimetre-et-lots.md)) liste les prototypes à réussir avant le développement : création depuis un abonnement vide, révocation d'un accès en production, aperçu puis approbation, cible qui a sauté des révisions.
- En cas de conflit entre documents : les décisions les plus récentes priment, puis les principes, puis les règles de gestion. Un conflit détecté est corrigé dans tous les documents par la même modification.

### DEC-98 — Un propriétaire unique pour chaque objet dérivé

**Décision.**
- Une attribution de rôle est déployée par le composant de l'identité qui la reçoit. Une identité affectée ne sert qu'aux ressources de son composant (`VAL-LIA-IDENTITE-PARTAGEE`) : deux unités de déploiement ne gèrent jamais la même attribution.
- Les droits des identités de déploiement et de livraison (créées par le kit, hors composants) sont déployés par le composant de la ressource visée.
- Un secret (secret de pipeline, mot de passe généré) est écrit par le composant qui porte son Key Vault, ou par le composant consommateur si le coffre est une ressource existante ; une clé App Configuration par le composant qui porte son magasin ; un utilisateur de base par le composant de la base.
- Une dépendance de **création** (hébergement, accès, données, lecture d'une sortie connue seulement après déploiement) ordonne les composants et ne doit pas former de cycle. Une sortie **calculable depuis le nom** (adresse d'un Key Vault, d'un Service Bus, d'un serveur SQL, domaine personnalisé) ne crée pas de dépendance d'ordre. Deux applications qui s'appellent mutuellement par de telles adresses restent possibles ; sinon, le cycle est signalé avec cette alternative.
