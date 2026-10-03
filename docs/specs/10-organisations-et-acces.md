# 10 — Organisations, utilisateurs et accès

## 1. Objectif

Isoler les données de chaque client, donner à chacun **exactement** les droits dont il a besoin
([DEC-52](03-decisions.md)), permettre à des outils (scripts, agents IA) d'agir au nom d'un utilisateur
avec des droits limités, et garder la trace de tout.

## 2. Authentification

**RG-ORG-01 — Comptes Microsoft.** Les utilisateurs se connectent avec un compte professionnel Entra ID,
quel que soit le tenant, ou avec un compte Microsoft personnel ([DEC-59](03-decisions.md)). IFS n'a ni
compte local ni mot de passe.

**RG-ORG-02 — Profil.** À chaque connexion, IFS met à jour le profil depuis les revendications :
identifiant d'objet et tenant (clé stable), nom d'affichage, adresse e-mail. Le profil n'est pas
modifiable dans IFS.

**RG-ORG-03 — Clients non interactifs.** L'API et le serveur MCP acceptent aussi un **jeton d'API**
(section 7) et, pour le MCP, un jeton OAuth Entra ([25](25-agent-ia-mcp.md)).

## 3. Organisation

| Champ | Règles |
|---|---|
| Nom | 2 à 80 caractères. |
| Domaines autorisés | Liste facultative de domaines e-mail. Si renseignée, seules ces adresses peuvent être invitées. |
| Tenants autorisés | Liste facultative de tenants Entra. Si renseignée, seuls les comptes de ces tenants peuvent être membres ; les comptes personnels sont alors exclus. |
| Création de projets | `Tous les membres` (défaut) ou `Rôles désignés`. |
| Plan | `Découverte`, `Équipe`, `Entreprise` ([DEC-78](03-decisions.md)) ; fixe les limites et les fonctions disponibles. Un dépassement de limite bloque la création concernée, avec un message qui cite la limite et le plan supérieur. |
| Politiques | Politiques d'organisation ([33 § 2](33-gouvernance-couts-et-supervision.md)), plan Entreprise. |
| Télémétrie produit | Activée (défaut) ou désactivée ([DEC-83](03-decisions.md)). |
| Membres, équipes | Sections 3.2 et 3.3. |

**UC-ORG-01 — Créer une organisation.** Tout utilisateur connecté peut créer une organisation et en
devient administrateur.

**UC-ORG-02 — Changer d'organisation.** Un utilisateur membre de plusieurs organisations choisit
l'organisation active ; l'écran ne montre que ses données.

### 3.1 Rôles d'organisation

| Rôle | Peut |
|---|---|
| **Administrateur** | Tout au niveau organisation : membres, invitations, équipes, connexions git et Azure, domaines et tenants autorisés, rôles personnalisés *(lot 2)*, journal d'audit complet, approbation des accès support ([DEC-57](03-decisions.md)), lecture de tous les projets et prise de propriété d'un projet (journalisée). |
| **Gestionnaire des connexions** | Ajouter, tester, modifier, retirer les connexions git et Azure ([24 § 2](24-depots-et-publication.md)). |
| **Auditeur d'organisation** | Lire le journal d'audit complet et tous les projets. |
| **Membre** | Accéder aux projets où il a un rôle ; créer des projets si l'organisation le permet. |

Un membre peut cumuler plusieurs rôles d'organisation.

**RG-ORG-04 — Pas de doublon.** Un utilisateur est au plus une fois membre d'une organisation.

**RG-ORG-05 — Dernier administrateur.** Le dernier administrateur d'une organisation ne peut ni partir,
ni être retiré, ni être rétrogradé.

### 3.2 Invitations

**UC-ORG-03 — Inviter.** Un administrateur invite une adresse e-mail avec des rôles d'organisation et,
facultativement, des équipes et des rôles de projet. L'invité reçoit un e-mail avec un lien valable
7 jours.

**UC-ORG-04 — Accepter.** L'invité se connecte ; l'adresse de son compte doit correspondre à l'invitation.

**UC-ORG-05 — Révoquer ou renvoyer une invitation.**

**RG-ORG-06 — Domaines et tenants.** Une invitation vers un domaine ou un tenant non autorisé est refusée.

**RG-ORG-07 — Annuaire limité.** La recherche d'utilisateurs ne porte que sur les membres de
l'organisation active. Aucune liste globale des utilisateurs d'IFS n'existe.

### 3.3 Équipes

| Champ | Règles |
|---|---|
| Nom | 1 à 80 caractères, unique dans l'organisation. |
| Description | 500 caractères max. |
| Membres | Membres de l'organisation. |
| Groupe Entra lié *(lot 2)* | Si renseigné, les membres de l'équipe sont synchronisés depuis le groupe (toutes les heures et à la connexion) ; l'ajout manuel est alors désactivé. |

**UC-ORG-06 — Gérer les équipes** (administrateur). Une équipe reçoit des rôles de projet comme un membre.
Un membre a l'union des droits accordés à lui-même et à ses équipes.

## 4. Droits sur un projet

### 4.1 Permissions

Les droits sont des permissions élémentaires. Certaines peuvent être limitées à des composants
(colonne « Portée composant »).

| Permission | Autorise | Portée composant |
|---|---|:-:|
| `projet.lire` | Consulter le projet, ses composants, ressources, constats, révisions, fichiers, déploiements. | |
| `audit.lire` | Consulter le journal d'audit du projet. | |
| `modele.modifier` | Créer, modifier, supprimer groupes de ressources, ressources, liaisons sortantes, paramètres applicatifs ; dupliquer des ressources. | ✓ |
| `applications.gerer` | Régler build, étapes de pipeline et déploiement des applications ([19](19-applications-build-et-deploiement.md)). | ✓ |
| `composants.gerer` | Créer, dupliquer, supprimer des composants ; changer leur mode, code et réglages. | |
| `conventions.gerer` | Nommage et tags du projet, groupes Entra du projet. | |
| `environnements.gerer` | Créer, modifier, réordonner, supprimer les environnements **non protégés** ; exécuteurs. | |
| `environnements.proteges.gerer` | Protection, approbateurs, abonnement et connexion de déploiement des cibles protégées. | |
| `publication.gerer` | Plan de publication, relecteurs par défaut. | |
| `generer` | Générer une révision, télécharger. | |
| `publier` | Publier une révision. | |
| `installation.gerer` | Marquer les étapes de la liste de contrôle, gérer l'inventaire des ressources détachées. | |
| `propositions.appliquer` | Appliquer ou rejeter les propositions de modification ([25](25-agent-ia-mcp.md)). | ✓ |
| `membres.gerer` | Attribuer et retirer des rôles de projet. | |
| `projet.administrer` | Langage d'infrastructure, plateforme CI, source des modules, version du catalogue épinglée, export, suppression et restauration du projet. | |

**RG-ORG-08 — Lecture implicite.** Toute permission implique `projet.lire`.

**RG-ORG-09 — Portée composant.** Une permission accordée sur des composants ne vaut que pour eux.
- Un membre limité au composant `orders` modifie ses ressources et peut créer des liaisons **vers** les
  ressources d'autres composants (il les lit), mais ne modifie pas ces autres composants.
- *(Lot 2)* Un composant peut restreindre ses liaisons entrantes : de tous les composants (défaut), d'une
  liste, ou sur approbation de son responsable.

### 4.2 Rôles prédéfinis

| Rôle | Pour qui | Permissions |
|---|---|---|
| **Lecteur** | Toute personne qui doit voir | `projet.lire` |
| **Auditeur** | Sécurité, conformité | `projet.lire`, `audit.lire` |
| **Développeur** | Membre d'une équipe produit, souvent limité à ses composants | `modele.modifier`, `applications.gerer`, `generer` |
| **Contributeur** | Membre actif du projet | `modele.modifier`, `applications.gerer`, `composants.gerer`, `generer`, `publier`, `propositions.appliquer` |
| **Responsable des déploiements** | Release manager | `generer`, `publier`, `installation.gerer`, `environnements.gerer` |
| **Architecte plateforme** | Équipe plateforme | `conventions.gerer`, `composants.gerer`, `modele.modifier`, `applications.gerer`, `environnements.gerer`, `environnements.proteges.gerer`, `publication.gerer`, `generer`, `publier`, `installation.gerer`, `propositions.appliquer` |
| **Administrateur du projet** | Gestion du projet et de son équipe | Toutes sauf `projet.administrer` |
| **Propriétaire** | Responsable du projet | Toutes |

**RG-ORG-10 — Attribution.** Un rôle de projet s'attribue à un membre ou à une équipe, avec une portée :
le projet, ou une liste de composants (seules les permissions « portée composant » sont alors
accordées ; les autres exigent la portée projet).

**RG-ORG-11 — Dernier propriétaire.** Un projet garde toujours au moins un propriétaire, désigné
nominativement (pas seulement par une équipe).

**RG-ORG-12 — Créateur.** Le créateur d'un projet en devient propriétaire.

**RG-ORG-13 — Invisibilité.** Un projet auquel l'utilisateur n'a pas accès répond « introuvable ». Une
action sans la permission requise sur un projet visible répond « interdit », en nommant la permission.

**RG-ORG-14 — Contrôle systématique.** Toute lecture et toute écriture, sans exception, vérifie
l'appartenance à l'organisation et la permission sur le projet ou le composant ([EXG-01](27-exigences-non-fonctionnelles.md)).
Un objet référencé dans une requête (cible de liaison, Key Vault, dépôt) doit appartenir au même projet,
ou à la même organisation pour une connexion.

**RG-ORG-15 — Lecture des permissions dans les specs.** Dans les autres documents, chaque cas
d'utilisation indique la permission requise entre parenthèses, par exemple « (`publier`) ».

### 4.3 Rôles personnalisés et séparation des tâches *(lot 2)*

**UC-ORG-07 — Créer un rôle personnalisé** (administrateur d'organisation) : nom, description,
permissions choisies dans la liste de la section 4.1. Il est utilisable dans tous les projets de
l'organisation.

**RG-ORG-16 — Publication à deux personnes.** Un projet peut exiger que la personne qui publie une
révision ne soit pas celle qui l'a générée, ni l'auteur des modifications qu'elle contient.

### 4.4 Cas d'utilisation

- **UC-ORG-08** — Attribuer un rôle de projet à un membre ou à une équipe, avec sa portée (`membres.gerer`).
- **UC-ORG-09** — Retirer un rôle (`membres.gerer`).
- **UC-ORG-10** — Voir les droits effectifs d'un membre sur un projet, avec leur provenance (directe ou
  par équipe).

## 5. Membres d'organisation

**UC-ORG-11 — Changer les rôles d'organisation d'un membre** (administrateur).

**UC-ORG-12 — Retirer un membre de l'organisation** (administrateur). Il perd l'accès à tous les projets
de l'organisation ; ses jetons d'API liés à l'organisation sont révoqués. S'il est dernier propriétaire
d'un projet, le retrait est refusé jusqu'à désignation d'un autre propriétaire.

**UC-ORG-13 — Quitter une organisation.** Mêmes règles.

## 6. Cycle de vie des comptes

**UC-ORG-14 — Supprimer une organisation** (administrateur). L'administrateur saisit le nom pour
confirmer.
- L'organisation est désactivée aussitôt : plus d'accès, jetons révoqués, connexions suspendues.
- Pendant 30 jours, elle est restaurable par un administrateur.
- Les données sont ensuite purgées, sauf le journal d'audit, conservé jusqu'à sa fin de conservation.
- Un export de tous les projets ([DEC-56](03-decisions.md)) est proposé avant la suppression.

**UC-ORG-15 — Supprimer son compte.** Un utilisateur qui n'est dernier administrateur ou dernier
propriétaire nulle part peut supprimer son compte :
- son profil est effacé ;
- ses jetons sont révoqués ;
- son nom est pseudonymisé dans les journaux ([EXG-06](27-exigences-non-fonctionnelles.md)).

## 7. Jetons d'API

| Donnée | Règles |
|---|---|
| Nom | Obligatoire, 1 à 100 caractères, unique parmi les jetons actifs de l'utilisateur. |
| Organisation | Un jeton est lié à une seule organisation. |
| Projets | Tous les projets accessibles (défaut) ou une liste restreinte. |
| Portées | Au moins une parmi `read`, `propose`, `write`, `generate`, `publish`. Une portée inconnue est une erreur. |
| Expiration | Obligatoire, entre 1 et 365 jours. |
| Valeur | `ifs_` + secret aléatoire de 256 bits, affichée une seule fois. |
| Stockage | Empreinte et préfixe affichable uniquement. |
| Suivi | Création, dernière utilisation, expiration, révocation. |

**RG-ORG-17 — Portées.**

| Portée | Autorise, dans la limite des permissions de l'utilisateur |
|---|---|
| `read` | Toutes les lectures. |
| `propose` | Créer des propositions de modification, sans les appliquer. Implique `read`. |
| `write` | Modifier le modèle. Implique `read` et `propose`. |
| `generate` | Générer des révisions. Implique `read`. |
| `publish` | Publier des révisions. Implique `read`. |

**RG-ORG-18 — Les portées limitent, elles n'élargissent pas.** Un jeton agit avec les permissions de son
propriétaire, limitées par ses portées et sa liste de projets.

**RG-ORG-19 — Expiration.** Un jeton expiré ou révoqué est refusé. Un e-mail prévient 7 jours avant
l'expiration ([26 § 4](26-interface.md)).

**UC-ORG-16 — Créer un jeton.** **UC-ORG-17 — Révoquer un jeton.** **UC-ORG-18 — Lister ses jetons**
(actifs, expirés, révoqués). Un administrateur peut lister et révoquer les jetons des membres.

## 8. Journal d'audit

**RG-ORG-20 — Événements journalisés.**
- Toute création, modification ou suppression d'objet du modèle, avec les valeurs avant et après.
- Générations, publications, téléchargements, exports.
- Changements de membres, d'équipes, de rôles, d'invitations.
- Création et révocation de jetons ; connexions ajoutées, modifiées ou retirées.
- Prise de propriété d'un projet par un administrateur.
- Accès support demandés, approuvés, utilisés ([DEC-57](03-decisions.md)).

**RG-ORG-21 — Contenu d'un événement.**
- Date (UTC), auteur (utilisateur ; jeton d'API, agent MCP ou opérateur IFS s'il y en a un).
- Organisation, projet.
- Action, objet, différence.

**RG-ORG-22 — Conservation et accès.**
- Conservation 13 mois, sans modification ni suppression possible.
- Consultable avec `audit.lire` (projet), par l'administrateur ou l'auditeur d'organisation (tout).
- Filtrable par auteur, objet, période ; exportable en CSV.

**UC-ORG-19 — Consulter l'historique d'un objet** depuis son écran.
