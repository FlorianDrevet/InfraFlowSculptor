# 10 — Organisations, utilisateurs et accès

## 1. Objectif

Isoler les données de chaque client, donner à chacun les droits justes, permettre à des outils (scripts,
agents IA) d'agir au nom d'un utilisateur avec des droits limités, et garder la trace de tout.

## 2. Authentification

**RG-ORG-01 — Microsoft Entra ID.** Les utilisateurs se connectent avec un compte professionnel Entra ID,
quel que soit le tenant. IFS n'a ni compte local ni mot de passe.

**RG-ORG-02 — Profil.** À chaque connexion, IFS met à jour le profil depuis les revendications Entra :
identifiant d'objet et tenant (clé stable), nom d'affichage, adresse e-mail. Le profil n'est pas
modifiable dans IFS.

**RG-ORG-03 — Clients non interactifs.** L'API et le serveur MCP acceptent aussi un **jeton d'API**
(section 6) et, pour le MCP, un jeton OAuth Entra ([25](25-agent-ia-mcp.md)).

## 3. Organisation

| Champ | Règles |
|---|---|
| Nom | 2 à 80 caractères. |
| Domaines autorisés | Liste facultative de domaines e-mail (`contoso.com`). Si renseignée, seules ces adresses peuvent être invitées. |
| Membres | Au moins un administrateur (RG-ORG-05). |

**UC-ORG-01 — Créer une organisation.** Tout utilisateur connecté peut créer une organisation et en
devient administrateur.

**UC-ORG-02 — Changer d'organisation.** Un utilisateur membre de plusieurs organisations choisit
l'organisation active ; l'écran ne montre que ses données.

### 3.1 Rôles d'organisation

| Rôle | Peut |
|---|---|
| **Administrateur** | Gérer les membres et invitations, les connexions git, les domaines autorisés ; consulter tout le journal d'audit ; voir tous les projets (en lecture) et en devenir propriétaire. |
| **Membre** | Créer des projets (il en devient propriétaire), accéder aux projets dont il est membre. |

**RG-ORG-04 — Pas de doublon.** Un utilisateur est au plus une fois membre d'une organisation, et au
plus une fois membre d'un projet.

**RG-ORG-05 — Dernier administrateur.** Le dernier administrateur d'une organisation ne peut ni partir,
ni être retiré, ni être rétrogradé.

### 3.2 Invitations

**UC-ORG-03 — Inviter.** Un administrateur invite une adresse e-mail avec un rôle d'organisation et,
facultativement, des rôles de projet. L'invité reçoit un e-mail avec un lien valable 7 jours.

**UC-ORG-04 — Accepter.** L'invité se connecte avec Entra ; l'adresse de son compte doit correspondre
à l'invitation. Il devient membre avec les rôles prévus.

**UC-ORG-05 — Révoquer ou renvoyer une invitation.**

**RG-ORG-06 — Domaines.** Une invitation vers un domaine non autorisé est refusée quand la liste des
domaines autorisés est renseignée.

**RG-ORG-07 — Annuaire limité.** La recherche d'utilisateurs ne porte que sur les membres de
l'organisation active. Aucune liste globale des utilisateurs d'IFS n'existe.

## 4. Droits sur un projet

### 4.1 Rôles de projet

| Rôle | Résumé |
|---|---|
| **Propriétaire** | Tout, y compris les environnements, le plan de publication, les membres du projet, la suppression. |
| **Contributeur** | Modéliser (composants, ressources, liaisons, paramètres), générer, publier. |
| **Lecteur** | Consulter le modèle, les constats, les révisions et leurs fichiers. |

### 4.2 Matrice des droits

| Action | Lecteur | Contributeur | Propriétaire |
|---|:-:|:-:|:-:|
| Consulter projet, composants, ressources, constats, révisions | ✓ | ✓ | ✓ |
| Créer, modifier, supprimer composants, groupes de ressources, ressources, liaisons, paramètres applicatifs | | ✓ | ✓ |
| Modifier nommage et tags du projet | | ✓ | ✓ |
| Générer une révision, télécharger | | ✓ | ✓ |
| Publier une révision | | ✓ | ✓ |
| Créer, modifier, supprimer un environnement ; changer protection et approbateurs | | | ✓ |
| Modifier le plan de publication | | | ✓ |
| Gérer les membres du projet | | | ✓ |
| Supprimer ou restaurer le projet | | | ✓ |

L'administrateur d'organisation a les droits de lecteur sur tous les projets et peut se nommer
propriétaire d'un projet (action journalisée).

**RG-ORG-08 — Héritage.** Les droits sur un composant, un groupe de ressources ou une ressource sont ceux
du projet.

**RG-ORG-09 — Invisibilité.** Un projet auquel l'utilisateur n'a pas accès répond « introuvable », pour
ne pas révéler son existence. Une action interdite sur un projet visible répond « interdit ».

**RG-ORG-10 — Dernier propriétaire.** Un projet garde toujours au moins un propriétaire.

**RG-ORG-11 — Contrôle systématique.** Toute lecture et toute écriture, sans exception, vérifie
l'appartenance à l'organisation et le rôle sur le projet ([EXG-01](27-exigences-non-fonctionnelles.md)).
Un objet référencé dans une requête (cible de liaison, Key Vault, dépôt) doit appartenir au même projet,
ou à la même organisation pour une connexion git.

**UC-ORG-06 — Ajouter un membre au projet.** Le propriétaire choisit un membre de l'organisation et un rôle.
**UC-ORG-07 — Changer le rôle d'un membre du projet.**
**UC-ORG-08 — Retirer un membre du projet.**

## 5. Membres d'organisation

**UC-ORG-09 — Changer le rôle d'organisation d'un membre** (administrateur).

**UC-ORG-10 — Retirer un membre de l'organisation** (administrateur). Il perd l'accès à tous les projets
de l'organisation ; ses jetons d'API liés à l'organisation sont révoqués. S'il est dernier propriétaire
d'un projet, le retrait est refusé jusqu'à désignation d'un autre propriétaire.

**UC-ORG-11 — Quitter une organisation.** Mêmes règles.

## 6. Jetons d'API

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

**RG-ORG-12 — Portées.**

| Portée | Autorise |
|---|---|
| `read` | Toutes les lectures. |
| `propose` | Créer des propositions de modification ([25](25-agent-ia-mcp.md)), sans les appliquer. Implique `read`. |
| `write` | Modifier le modèle. Implique `read` et `propose`. |
| `generate` | Générer des révisions. Implique `read`. |
| `publish` | Publier des révisions dans les dépôts. Implique `read`. |

**RG-ORG-13 — Les portées limitent, elles n'élargissent pas.** Un jeton agit avec les droits de son
propriétaire, limités par ses portées et sa liste de projets.

**RG-ORG-14 — Expiration.** Un jeton expiré ou révoqué est refusé. Un e-mail prévient 7 jours avant
l'expiration.

**UC-ORG-12 — Créer un jeton.** **UC-ORG-13 — Révoquer un jeton.** **UC-ORG-14 — Lister ses jetons**
(actifs, expirés, révoqués). Un administrateur peut lister et révoquer les jetons des membres de son
organisation.

## 7. Journal d'audit

**RG-ORG-15 — Événements journalisés.**
- Toute création, modification ou suppression d'objet du modèle, avec les valeurs avant et après.
- Générations, publications, téléchargements de révision.
- Changements de membres, de rôles, d'invitations.
- Création et révocation de jetons, connexions git ajoutées, modifiées ou retirées.
- Prise de propriété d'un projet par un administrateur.

**RG-ORG-16 — Contenu d'un événement.**
- Date (UTC) et auteur (utilisateur ; jeton d'API ou agent MCP s'il y en a un).
- Organisation et projet.
- Action, objet concerné, différence.

**RG-ORG-17 — Conservation et accès.**
- Conservation 13 mois, sans modification ni suppression possible.
- Consultable par les administrateurs (toute l'organisation) et les propriétaires (leur projet).
- Filtrable par auteur, objet, période ; exportable en CSV.

**UC-ORG-15 — Consulter l'historique d'un objet** depuis son écran (ressource, composant, environnement).
