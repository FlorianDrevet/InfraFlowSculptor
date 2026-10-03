# 31 — Historique du modèle, versions et traçabilité

## 1. Objectif

Savoir à tout moment **qui a changé quoi, quand et pourquoi** dans le modèle. Pouvoir revoir le modèle tel
qu'il était, comparer deux états, et revenir en arrière sans jamais effacer l'histoire
([DEC-64](03-decisions.md), [DEC-65](03-decisions.md)).

## 2. Jeu de modifications

Toute modification du modèle, quelle que soit son origine, produit un **jeu de modifications** immuable.

| Champ | Contenu |
|---|---|
| Numéro | Version du modèle produite par ce jeu (entier croissant par projet). |
| Auteur | Utilisateur, avec le jeton d'API, l'agent MCP ou l'opérateur support s'il y en a un. |
| Date | UTC. |
| Origine | `Écran`, `API`, `MCP`, `Proposition`, `Import`, `Annulation`, `Restauration`, `Mise à jour du catalogue`, `Conversion v0`. |
| Message | Facultatif ; résumé automatique sinon (exemple : « 2 propriétés modifiées sur `ca api` »). |
| Changements | Par objet : création, suppression, ou liste des propriétés avec valeur avant et après (pour chaque environnement concerné). |
| Liens | Proposition d'origine, révisions générées depuis cette version. |

**RG-HIS-01 — Granularité.** Un enregistrement dans l'écran, une commande de l'API, l'application d'une
proposition ou d'un import produit **un** jeu de modifications, même s'il touche plusieurs objets.

**RG-HIS-02 — Ce qui est historisé.** Tout l'état du modèle d'un projet : projet, environnements,
composants, groupes de ressources, ressources, enfants, surcharges, présences, liaisons, paramètres
applicatifs, nommage, tags, plan de publication, réglages des applications et étapes de pipeline, groupes
Entra. Ne sont pas historisés ici, mais au journal d'audit : membres, rôles, jetons, connexions.

**RG-HIS-03 — Conservation.** Les jeux de modifications sont conservés toute la vie du projet. Ils suivent
l'export ([11 § 6](11-projets-et-environnements.md)) s'il est demandé avec l'historique.

## 3. Qui a modifié quoi

**RG-HIS-04 — Sur chaque objet.** L'écran d'un objet affiche son auteur de création, sa dernière
modification (auteur, date), et un onglet **Historique** : la chronologie de ses jeux de modifications.

**RG-HIS-05 — Sur chaque propriété.** Au survol, au focus clavier ou au toucher d'une valeur (y compris d'une surcharge d'environnement),
l'écran indique qui l'a fixée en dernier, quand, et par quel jeu de modifications, avec un lien.

**RG-HIS-06 — Lien avec l'audit.** Chaque événement d'audit de modification du modèle
([10 § 8](10-organisations-et-acces.md)) renvoie à son jeu de modifications. L'historique sert à comprendre
et à restaurer ; l'audit sert à prouver. L'audit couvre en plus les actions hors modèle.

## 4. Versions étiquetées

**UC-HIS-01 — Étiqueter une version** (`modele.modifier`). Nom (1 à 80 caractères, unique dans le projet)
et description. Exemples : « avant migration réseau », « livraison T3 ».

**RG-HIS-07 — Étiquettes automatiques.** Chaque révision générée référence sa version de modèle ; chaque
révision publiée et chaque révision déployée en cible protégée ([28](28-suivi-des-deploiements.md))
apparaissent comme repères dans la chronologie.

## 5. Consulter et comparer

**UC-HIS-02 — Voir le modèle à une version.** Tout l'écran du projet bascule en **lecture seule** sur l'état
du modèle à la version choisie (ou à une date, ou à une étiquette, ou à la version d'une révision). Un
bandeau rappelle qu'on regarde le passé.

**UC-HIS-03 — Comparer deux versions.** Différence par composant, ressource et propriété (valeurs par
environnement), liaisons ajoutées et retirées, noms Azure modifiés. Les comparaisons courantes sont
proposées en un clic : « depuis la dernière révision publiée », « depuis ce qui est déployé en prod ».

## 6. Revenir en arrière

**UC-HIS-04 — Annuler un jeu de modifications** (`modele.modifier` sur les objets concernés). IFS calcule
les modifications inverses.
- Si aucune modification postérieure ne touche les mêmes propriétés : elles s'appliquent en un nouveau jeu
  d'origine `Annulation`.
- Sinon IFS crée une **proposition** qui montre les conflits ; l'utilisateur choisit propriété par propriété.

**UC-HIS-05 — Restaurer une version.** Portée : tout le projet, un composant ou une ressource. IFS crée une
**proposition** contenant toutes les modifications qui ramènent la portée à son état passé, avec le résumé
et l'impact (noms Azure qui changent, ressources qui seront recréées ou détachées, accès ouverts ou
révoqués). L'appliquer exige, pour chaque modification, la permission qu'exigerait la modification directe
([DEC-89](03-decisions.md)) : par exemple `environnements.proteges.gerer` pour une cible protégée. Une fois
appliquée, elle produit un jeu d'origine `Restauration`.

**RG-HIS-12 — Expurger une valeur** (administrateur d'organisation, [DEC-95](03-decisions.md)). Une valeur
enregistrée par erreur (un secret collé dans une valeur littérale) est remplacée par une marque dans le
modèle, tous les jeux de modifications, les révisions non publiées et les propositions. Les révisions qui
contenaient la valeur, publiées ou non, sont marquées **contaminées** : leurs fichiers ne sont plus
téléchargeables ni comparables dans IFS ; leurs métadonnées et l'incident restent visibles
([DEC-107](03-decisions.md)). L'audit garde l'acte, l'auteur et l'objet, pas la valeur. L'écran rappelle que
la valeur doit être renouvelée chez le client, et liste les dépôts, branches et commits où ces révisions
ont été publiées, qu'IFS ne réécrit pas. Les expurgations sont réappliquées après toute restauration des
sauvegardes d'IFS ([RG-EXP-11](40-exploitation-ifs.md)).

**RG-HIS-08 — On n'efface jamais.** Annuler ou restaurer ajoute des jeux de modifications ; l'historique
reste intact.

**RG-HIS-09 — Ce que « revenir » veut dire.**

| Je veux revenir en arrière sur… | Comment |
|---|---|
| Le modèle | Restaurer une version (UC-HIS-05). |
| L'infrastructure déployée | Restaurer le modèle, générer, publier, déployer. Avant d'appliquer la restauration, IFS calcule un **plan de retour** qui classe chaque changement : applicable ; partiel ; impossible, pour une propriété irréversible déjà franchie (attribut **I** du catalogue, exemple : protection contre la purge activée) ; avec intervention, pour une ressource recréée. L'aperçu de la release montre ensuite ce qui change dans Azure. |
| Les données | Jamais par IFS : restaurer le modèle ne restaure ni le contenu d'une base, ni des fichiers, ni une ressource supprimée avec ses données. L'écran le dit et renvoie aux sauvegardes du client. |
| Le code d'une application | Pas par IFS : relancer la release du build précédent, ou utiliser la stratégie de déploiement (bascule inverse du slot, retour du trafic sur la révision bleue, [19 § 9](19-applications-build-et-deploiement.md)). |
| Une ressource supprimée par erreur | Restaurer sa version : la ressource revient dans le modèle avec ses liaisons ; si elle avait été détachée, elle sort de l'inventaire des ressources détachées et la pile ou l'état la reprend au déploiement suivant. |

## 7. Brouillons *(lot 1, jalon 2)*

Avant les brouillons (jalons 0 et 1), un projet a un éditeur coordinateur qui intègre les changements
([DEC-93](03-decisions.md)).

**RG-HIS-13 — Modèle principal générable.** Dès que les brouillons existent, une application au modèle
principal (brouillon soumis, proposition, commande directe) qui ajouterait une `Erreur` de validation est
refusée : le travail incomplet reste dans son brouillon et ne bloque pas la livraison des autres. Un
correctif urgent se prépare dans un brouillon court, appliqué dès qu'il est valide.

**UC-HIS-06 — Créer un brouillon.** Un brouillon est un espace de travail nommé, personnel ou partagé, qui
part de la version courante du modèle. Les modifications faites dans un brouillon :
- n'affectent pas le modèle principal ;
- sont validées comme le modèle principal (constats du brouillon) ;
- peuvent être générées en **révision d'essai**, téléchargeable mais pas publiable.

**UC-HIS-07 — Soumettre un brouillon.** Le brouillon devient une proposition de modification
([25 § 4](25-agent-ia-mcp.md)). Les propositions des agents IA sont des brouillons soumis.

**RG-HIS-10 — Mise à jour d'un brouillon.** Si le modèle principal a changé, l'utilisateur **met à jour** son
brouillon. Les modifications sans conflit sont reportées automatiquement. Les conflits (même propriété
modifiée des deux côtés) se résolvent propriété par propriété.

**RG-HIS-11 — Durée.** Un brouillon sans activité depuis 90 jours est archivé (récupérable 30 jours).
