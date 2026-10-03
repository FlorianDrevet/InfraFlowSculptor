# 26 — Interface

> Ce document décrit les écrans et les règles d'ergonomie. Le design visuel relève du design system.

## 1. Écrans

| Écran | Contenu principal |
|---|---|
| **Connexion** | Connexion Entra ID ; acceptation d'invitation. |
| **Accueil** | Projets favoris et récents, propositions de modification en attente, constats bloquants de mes projets, dernières publications. |
| **Projets** | Liste filtrable, création par assistant. |
| **Projet — vue d'ensemble** | Composants dans l'ordre de déploiement, environnements, constats par gravité, dernière révision et état de publication, liste de contrôle d'installation. |
| **Projet — graphe** | Vue graphe des ressources et liaisons ([UC-LIA-03](16-liaisons-identites-et-acces.md)), en lecture au lot 1, éditable au lot 2. |
| **Projet — environnements** | Chaîne de promotion, protection, approbateurs, cibles propres des composants `Single`. |
| **Projet — nommage** | Gabarits, abréviations, aperçu en direct sur toutes les ressources. |
| **Projet — publication** | Connexions utilisées, plan de publication, historique des publications. |
| **Projet — historique** | Chronologie des jeux de modifications, étiquettes, consultation à une version, comparaison, annulation, restauration ([31](31-historique-et-versions.md)). |
| **Projet — brouillons** *(lot 2)* | Brouillons personnels et partagés, mise à jour, soumission. |
| **Projet — coûts** *(lot 2)* | Estimation par cible, composant, ressource ; hypothèses d'usage ; budgets ([33 § 3](33-gouvernance-couts-et-supervision.md)). |
| **Projet — déploiements** | Matrice composants × cibles, historique, inventaire des ressources détachées ([28](28-suivi-des-deploiements.md)). |
| **Projet — import** *(lot 3)* | Analyse et écran de revue d'un import ([29](29-import.md)). |
| **Projet — révisions** | Liste, détail (plan de déploiement et fichiers), comparaison, publication, téléchargement. |
| **Projet — membres, paramètres, historique** | Membres et rôles ; langage d'infrastructure et plateforme CI (avec conséquences d'un changement), tags, exécuteurs, relecteurs ; journal d'audit du projet. |
| **Composant** | Groupes de ressources et ressources, dépendances, nommage propre, réglages du composant. |
| **Ressource** | Onglets générés depuis le descripteur (section 2). |
| **Organisation — politiques** *(lot 2)* | Politiques, dérogations demandées et accordées ([33 § 2](33-gouvernance-couts-et-supervision.md)). |
| **Propositions** | Liste et relecture des propositions de modification. |
| **Organisation** | Membres, équipes, invitations, connexions git et Azure, domaines et tenants autorisés, rôles personnalisés *(lot 2)*, accès support, journal d'audit, jetons des membres. |
| **Profil** | Préférences, jetons d'API personnels. |

## 2. Écran d'une ressource

Les onglets affichés sont ceux que le descripteur rend applicables :

| Onglet | Contenu |
|---|---|
| Général | Nom logique, noms Azure par cible avec explication, groupe de ressources, présence, description, tags. |
| Propriétés | Tableau propriétés × environnements : valeur effective, provenance, surcharge en ligne ; valeurs fixes en lecture. |
| Enfants | Conteneurs, files, topics, bases… |
| Liaisons | Sortantes et entrantes, explicites et implicites, avec leurs effets. |
| Identité et accès | Identités, rôles détenus, rôles accordés à d'autres sur cette ressource. |
| Paramètres | Pour les applications et les App Configuration ([17](17-parametres-applicatifs-et-secrets.md)). |
| Application | Build et déploiement ([19](19-applications-build-et-deploiement.md)). |
| Réseau | Exposition par environnement ; *(lot 2)* points de terminaison privés, intégration, domaines. |
| Constats | Constats de la ressource. |
| Historique | Journal d'audit de la ressource. |

Une ressource existante n'affiche que Général (avec ses identifiants par environnement), Liaisons
entrantes et Historique.

## 3. Règles d'ergonomie

**RG-UI-01 — Constats en contexte.** Chaque objet affiche ses constats à côté du champ concerné, avec la
correction proposée ([20](20-validation.md)). Un compteur global par gravité est toujours visible dans
l'en-tête du projet.

**RG-UI-02 — Rien de calculé localement.** Noms, valeurs effectives et éléments implicites viennent du
serveur ([DEC-05](03-decisions.md)). L'écran se met à jour après chaque enregistrement.

**RG-UI-03 — Impact avant action.** Toute action destructrice ou à impact (suppression, changement de
code ou de nom, retrait de liaison, changement d'environnement) passe par une boîte de dialogue qui
liste l'impact calculé par le serveur.

**RG-UI-04 — Implicite visible.** Les éléments implicites sont visuellement distincts et affichent leur
origine au survol, avec un lien vers elle.

**RG-UI-05 — Pas de promesse.** Aucune fonctionnalité non livrée n'apparaît ([P9](01-principes.md)).

**RG-UI-06 — Recherche globale.** Une palette (`Ctrl+K`) cherche dans les projets, composants,
ressources, paramètres et commandes de l'organisation active.

**RG-UI-07 — Préférences côté serveur.** Langue (français, anglais ; défaut : langue du navigateur),
thème (clair, sombre, système ; défaut : système), favoris et récents sont stockés dans le profil et
suivent l'utilisateur d'un poste à l'autre.

**RG-UI-08 — Internationalisation.** Tous les textes passent par les fichiers de traduction ; les
messages de validation aussi. Les noms techniques Azure (SKU, rôles) ne sont pas traduits.

**RG-UI-09 — Accessibilité.** WCAG 2.2 niveau AA ([EXG-12](27-exigences-non-fonctionnelles.md)) : clavier
complet, contrastes, libellés, focus visible ; la vue graphe a un équivalent en liste.

**RG-UI-10 — Conflits.** En cas de conflit de version ([DEC-37](03-decisions.md)), l'écran affiche la
version enregistrée et la saisie de l'utilisateur côte à côte.

**RG-UI-11 — Visualiseur de code.** Coloration Bicep, HCL, TypeScript et YAML, arborescence, recherche dans les fichiers,
diff entre révisions, téléchargement.

## 4. Notifications

**RG-UI-12 — Canaux.** Chaque notification est affichée dans l'application (centre de notifications) et,
selon les préférences de l'utilisateur, envoyée par e-mail. *(Lot 2 : webhooks sortants, Microsoft Teams,
Slack.)*

| Événement | Destinataires | E-mail par défaut |
|---|---|---|
| Invitation à une organisation | Invité | Oui (toujours) |
| Proposition de modification créée | Membres qui ont `propositions.appliquer` sur le projet | Oui |
| Révision publiée (pull request ouverte) | Relecteurs par défaut du projet | Oui |
| Déploiement en échec | Auteur de la révision ; membres qui ont `publier` | Oui |
| Approbation en attente depuis plus de 24 h | Approbateurs de la cible (connus d'IFS) | Oui |
| Cible protégée en retard de plus de 3 révisions | Responsables des déploiements | Non |
| Jeton d'API expirant sous 7 jours | Propriétaire du jeton | Oui (toujours) |
| Jeton git de repli expirant sous 15 et 3 jours | Administrateurs et gestionnaires des connexions | Oui (toujours) |
| Dépréciation du catalogue touchant le projet | Membres qui ont `projet.administrer` ou `composants.gerer` | Oui |
| Demande d'accès support | Administrateurs de l'organisation | Oui (toujours) |
| Dérive détectée | Membres qui ont `publier` sur le projet | Oui |
| Dérogation demandée, accordée, expirant sous 30 jours | Administrateurs ; demandeur | Oui |
| Mention dans un commentaire | Membre mentionné | Oui |
| Incident ou maintenance IFS | Tous les membres des organisations concernées | Selon gravité |

**RG-UI-13 — Préférences.** Chaque utilisateur active ou coupe l'e-mail par type d'événement, sauf ceux
marqués « toujours ». Un résumé quotidien peut remplacer les e-mails unitaires.

**RG-UI-14 — Contenu.** Une notification nomme l'objet, le projet et l'action attendue, avec un lien direct.
Elle ne contient aucune donnée sensible : pas d'identifiant d'abonnement, pas de nom de secret.

