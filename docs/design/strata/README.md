# Strata

Strata est le design system d'**InfraFlowSculptor** (IFS). IFS modélise une infrastructure Azure, la valide, en génère des **révisions** (code d'infrastructure, pipelines, kit d'installation), les **publie** par pull request dans les dépôts du client, puis **suit les déploiements** que les pipelines du client exécutent. Le nom vient des couches que l'on manipule : organisation, projet, composant, groupe de ressources, ressource.

Il sert de source unique pour deux usages :
- **la maquette v1** (canvas « InfraFlowSculptor v1 », 72 écrans), qui l'installe et en monte les composants ;
- **le code** (frontend Angular), qui en reprend les tokens et les règles. Voir « Passer au code » en bas.

Le vocabulaire est celui des spécifications fonctionnelles v1 (`docs/specs/02-glossaire.md`). Strata est sombre uniquement pour l'instant. Un thème clair viendra comme second thème des mêmes tokens, sans renommage.

## Principes

1. **Le chemin d'abord.** Chaque écran dit où l'on en est sur le chemin modéliser → générer → publier → déployer : constats restants, révision périmée ou non, pull requests fusionnées ou non, cibles à jour ou en retard.
2. **Aperçu = résultat.** Un nom, une valeur effective ou un fichier affiché est exactement ce que la génération écrira. Un aperçu approximatif est un bug. Quand Azure impose un ajustement (pas de tirets dans un nom de compte de stockage), l'interface le dit.
3. **Couleur = signal.** `signal` (cyan) pour agir, `ember` (ambre) pour ce qui a changé ou demande attention. Tout le reste est en niveaux d'ardoise.
4. **Densité calme.** Des écrans d'ingénieurs, utilisés longtemps : beaucoup d'information, peu de bruit. Pas de dégradé, pas de verre dépoli, pas d'ombre sur les panneaux.
5. **Ne jamais mentir.** Un élément implicite montre son origine. Un état partiel est dit partiel, une information périmée est dite « inconnue » avec sa date. Une donnée déclarée et non vérifiée porte la mention « déclaré, non vérifié ». Un bouton « Publier » publie vraiment, et dit dans quels dépôts.

## Écriture

- Interface en français. Vouvoiement ou tournure impersonnelle (« Saisissez le secret », « Accepter la demande d'accès »), jamais de tutoiement.
- Les boutons disent ce qui se passe : « Générer la révision 16 », « Publier dans 2 dépôts », « Ajouter la liaison », pas « OK » ni « Valider ».
- Les erreurs disent ce qui s'est passé et comment corriger : « La release a échoué à l'étape secrets : la variable `MAIN_PAYMENTS_API_KEY` est vide dans `ifs-shop-prd`. Saisissez-la puis relancez la release. » Pas d'excuse, pas de code d'erreur brut.
- Les identifiants (noms Azure, codes, dépôts, branches, chemins) sont écrits en mono, tels qu'ils seront générés, en minuscules.
- Les termes Azure restent en anglais quand c'est le nom du service ou du concept (Key Vault, Container App, private endpoint, service connection). Les notions d'IFS restent en français : composant, cible, liaison, surcharge, présence, révision, constat, publication.
- Les dates de l'interface sont relatives quand elles sont récentes (« il y a 2 h »), absolues sinon (« 29/09 16:12 »).
- Pas d'emoji.

## Couleur

| Rôle | Tokens | Règle |
| --- | --- | --- |
| Fond et surfaces | `bg` → `surface-sidebar` → `surface-1` → `surface-2` → `surface-3` | Chaque niveau monte d'un cran. Un panneau est en `surface-1` sur `bg`. Pas plus de trois niveaux visibles à la fois. |
| Lignes | `line`, `line-subtle`, `line-strong` | `line` autour des panneaux, `line-subtle` entre lignes de tableau, `line-strong` autour des champs (3:1). |
| Texte | `text`, `text-2`, `text-3` | `text-3` est le plancher de contraste (4.9:1 sur `surface-1`) : rien de plus pâle pour du texte lisible. |
| Action | `signal`, `signal-ink`, `signal-text`, `signal-soft` | Un seul bouton `signal` par écran. Onglet actif, focus, sélection, « en cours », « en attente d'approbation ». |
| Changement / attention | `ember`, `ember-text`, `ember-soft` | Modifié depuis la dernière révision, surcharge d'environnement, avertissement, état partiellement appliqué, déconseillé. |
| États | `success*`, `danger*` | Réussi / échoué. Jamais pour décorer. |
| Identité et accès | `edge-identity`, `identity-text`, `identity-soft` | Identités, rôles, références Key Vault, dérive. |
| Catégories | `cat-*` | Repli quand l'icône Azure n'est pas affichée, légende du graphe. |
| Graphe | `edge-dependency` (hébergement, valeur), `signal` (accès), `edge-identity` (identité), `ember` (demande d'accès, à corriger) | Les liens se distinguent aussi par le trait : plein pour un lien explicite, pointillé pour un lien implicite. |
| Dialogue | `overlay-scrim` | Fond derrière un dialogue. |

Deux états qui doivent se distinguer diffèrent aussi par la luminosité ou par le texte, pas seulement par la teinte.

## Typographie

- **Instrument Sans** pour l'interface (Google Fonts, 400 / 500 / 600). Pas de 700.
- **JetBrains Mono** pour les noms générés, codes, dépôts, chemins, extraits de code, en-têtes de colonne.
- Échelle : `page-title` 28, `dialog-title` 18, `section-title` 16, `body` 14, `body-dense` 13, `caption` 12, `mono` 12, `overline` 11 (capitales, +0.08em).
- Le nom logique d'une ressource ou d'un composant est un identifiant : titre en `mono-title`, pas en sans.

## Espace, rayons, profondeur

- Grille de 4 px : `space-1` (4) à `space-12` (48). Les groupes se mettent en page en flex ou grid avec `gap`.
- Rayons : `radius-md` (6) pour les contrôles, `radius-lg` (10) pour les panneaux, `radius-xl` (12) pour les dialogues, `radius-pill` pour les puces.
- Une seule ombre, `shadow-overlay`, pour ce qui flotte (dialogue, menu). Les panneaux se séparent par la couleur et la bordure.

## Iconographie

Deux familles, jamais mélangées pour le même rôle :

1. **Icônes de ressources Azure** : les icônes officielles Microsoft (pack Azure Public Service Icons V24), via le composant `ResourceIcon` ou le groupe d'assets « Azure ». Règles Microsoft : ne pas modifier l'icône, écrire le nom du service à côté, ne l'utiliser que pour le service concerné. Un type sans icône officielle dans le groupe (Microsoft Foundry, AI Search, PostgreSQL, Azure Managed Redis…) utilise la tuile d'abréviation (`variant="tile"`). Voir la note de licence plus bas.
2. **Icônes d'interface** : au trait, 1.75 px, grille 24, en `currentColor`, via le composant `Icon`. Pour les actions et la navigation uniquement.

Le logo IFS est le symbole des trois couches (couche du haut en `signal`, deux couches au trait en `text-3` et `line-strong`) suivi du mot « InfraFlow » en `text` et « Sculptor » en `text-3`. Pas encore de fichier logo dans ce système : il est dessiné en SVG en ligne dans la maquette.

## Mise en page

- **Coquille** : barre latérale `sidebar-width` (236) sur `surface-sidebar` ; barre supérieure `topbar-height` (56) avec le fil d'Ariane en mono à gauche et les actions de page à droite ; contenu limité à `content-max` (1280), marges `space-8`.
- **Navigation latérale**, selon la portée :
  - espace de travail : Accueil, Projets, Propositions, Notifications ;
  - projet : Vue d'ensemble, Graphe, Composants (avec la liste des composants), Environnements, Nommage, Constats, Révisions, Déploiements, Installation, Historique, Brouillons, Coûts, Publication, Membres, Paramètres ;
  - organisation : Membres, Équipes, Rôles, Connexions, Politiques, Audit, Réglages ;
  - back-office IFS : Catalogue, Organisations et support.
  Une pastille `ember` à côté d'un composant modifié depuis la dernière révision.
- **Pages denses** : colonne principale (tableau, liste) + colonne latérale de 300–320 px (constats, présence, commentaires, explications).
- **Dialogue** : boîte centrée en `surface-1`, bordure `line-strong`, `radius-xl`, `shadow-overlay`, sur `overlay-scrim`. Titre qui dit l'action, corps qui dit l'impact, deux boutons au plus en pied.
- Sous 760 px, la barre latérale disparaît ; les grilles passent à une colonne. Sur mobile, l'interface sert à consulter et à décider, pas à modéliser.

## Motifs propres au produit

- **Depuis la dernière révision** : chaque écran de modélisation montre ce qui a changé depuis la dernière révision (`Modifiée depuis la révision 14`) et propose de générer.
- **Propriétés × environnements** : les propriétés d'une ressource s'éditent dans un tableau, une colonne par environnement. Trois aspects, toujours avec une légende : **surcharge** de l'environnement (fond `ember-soft`, bordure `ember`) ; **valeur de la ressource** (bordure `line-strong`) ; **défaut du catalogue** (bordure pointillée, texte `text-3`). Une propriété verrouillée après publication porte l'icône `lock`.
- **Implicite visible** : un élément déduit (rôle, paramètre, liaison) est en italique `text-3` ou en trait pointillé, et montre son origine au survol, au focus et au toucher, avec un lien vers elle. Il ne se modifie pas directement.
- **Constats** : un compteur par gravité dans l'en-tête du projet ; chaque constat à côté de son objet, avec sa correction. Erreur `danger`, erreur à la publication `danger` avec la mention, avertissement `ember`, info `neutral`.
- **Aperçu puis approbation** : l'aperçu d'une release sépare ressources créées ou modifiées, ressources détachées, **accès révoqués** et écritures prévues (secrets, clés, utilisateurs de base). Un accès révoqué n'est jamais présenté comme une ressource détachée.
- **États d'une cible** : `success` Déployée ; `ember` Partiellement appliquée, En retard ; `signal` En attente d'approbation, En cours ; `danger` En échec ; `identity` Dérive ; `neutral` Inconnu (avec la date de dernière lecture), Jamais déployée, Non applicable. Le texte dit toujours l'état.
- **Publication par dépôt** : un résultat par dépôt (pull request, commit, fichiers), une publication partielle est dite comme telle ; une modification manuelle détectée est montrée en diff et exige une confirmation.
- **Demande d'accès** : une liaison qui ouvre un accès à un autre composant devient une demande, affichée en `ember` jusqu'à sa décision.
- **Liste de contrôle** : chaque étape porte un état : Automatique (`neutral`), À faire (`ember`), Fait (`success`) ; une commande exacte s'affiche en mono, prête à copier.

## Composants

| Composant | Rôle |
| --- | --- |
| `Button` | Actions ; `primary` une seule fois par écran |
| `Icon` | Icônes d'interface au trait |
| `Badge` | Puces d'état et d'attribut |
| `TextField` | Champ à une ligne, mono pour les identifiants, état « surcharge » |
| `Segmented` | Choix exclusif court (environnement, exposition, stratégie) |
| `Toggle` | Interrupteur à effet immédiat |
| `Tabs` | Sections d'une page avec compteurs |
| `ResourceIcon` | Icône Azure officielle d'un type de ressource |
| `ResourceRow` | Ligne de ressource avec nom généré et état |
| `GeneratedName` | Nom Azure calculé pour un environnement |
| `GoldenPathRail` | Avancement en quatre étapes |
| `Banner` | Message de section (avertissement, échec, explication) |
| `Panel` | Bloc de contenu titré |

Les composants sont des composants React (`window.Strata`), chargés avec React 18. Chaque fichier `components/<Nom>/README.md` donne les règles d'usage. Les puces d'état des tableaux (constats, déploiements, liste de contrôle) suivent la géométrie de `Badge` : hauteur 22, padding 0 8, `radius-pill`.

## Passer au code

Le frontend IFS est en Angular ; ce système n'est pas une librairie à importer telle quelle, c'est la spécification que le code suit.

1. **Tokens.** `tokens.json` est la source. Générer les variables CSS et la configuration Tailwind à partir de ce fichier, avec le préfixe du projet : `--ifs-surface-1`, `--ifs-signal`, etc. Les noms de tokens ne changent pas d'un côté à l'autre.
2. **Styles.** `components/bundle.css` donne la géométrie exacte de chaque composant (classes `st-*`, chaque `var()` avec sa valeur de repli). Les composants Angular `app-ds-*` reprennent ces valeurs.
3. **Correspondance avec un DS Angular** :

| Strata | Angular |
| --- | --- |
| `Button` | `app-ds-button` (variants primary / secondary / ghost / danger) |
| `Badge` | `app-ds-chip`, `app-ds-status-dot` |
| `TextField` | `app-ds-text-field` (+ `app-ds-ip-input` pour IP / CIDR) |
| `Segmented` | `app-ds-segmented-control` |
| `Toggle` | `app-ds-toggle` |
| `Tabs` | `app-ds-tabs` |
| `Banner` | `app-ds-banner`, `app-ds-alert` |
| `Panel` | `app-ds-card` |
| `ResourceIcon`, `ResourceRow`, `GeneratedName`, `GoldenPathRail` | à créer |

4. **Icônes Azure.** Copier les SVG du groupe « Azure » et les indexer par type de ressource (la table `Strata.resourceTypes` donne la correspondance type → fichier → nom du service).

## Licence des icônes Azure

Microsoft autorise ces icônes « dans des diagrammes d'architecture, des supports de formation ou de la documentation ». Leur usage dans l'interface d'un produit vendu n'est pas explicitement couvert. Avant la commercialisation d'IFS, faire valider ce point (ou demander l'accord de Microsoft) ; `ResourceIcon variant="tile"` permet de basculer sur les tuiles d'abréviation sans toucher aux écrans.
