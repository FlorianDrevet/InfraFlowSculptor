# 14 — Modèle des ressources

## 1. Objectif

Définir ce que **toute** ressource possède et les règles qui s'appliquent quel que soit son type. Le
détail par type vient du descripteur ([15](15-catalogue.md)).

## 2. Données communes

| Champ | Règles |
|---|---|
| Type | Un type du catalogue, fixé à la création. |
| Nom logique | [RG-NOM-07](13-nommage.md). |
| Groupe de ressources | Un groupe du composant. Change par déplacement (UC-CMP-06). |
| Description | 500 caractères max. |
| Existante | Booléen fixé à la création (section 6). |
| Propriétés | Valeurs de la ressource (section 3). |
| Surcharges | Valeurs par environnement (section 3). |
| Présence | Par environnement (section 4). |
| Enfants | Selon le descripteur (section 5). |
| Identités | Identité système (oui/non) et identités affectées ([16 § 4](16-liaisons-identites-et-acces.md)). |
| Liaisons sortantes | [16](16-liaisons-identites-et-acces.md). |
| Paramètres applicatifs | Pour les types destinataires ([17](17-parametres-applicatifs-et-secrets.md)). |
| Exposition réseau | Pour les types qui la supportent ([18](18-reseau-et-exposition.md)). |
| Journalisation | Hérite de l'espace par défaut du composant, ou exclusion explicite ([DEC-42](03-decisions.md)). |
| Tags | [11 § 5](11-projets-et-environnements.md). |
| Nom forcé | Par environnement ([13 § 4.1](13-nommage.md)). |
| Version | Concurrence optimiste ([DEC-37](03-decisions.md)). |

## 3. Propriétés et surcharges

Chaque propriété est définie par le descripteur :

| Attribut du descripteur | Sens |
|---|---|
| Type de valeur | Booléen, entier (min, max, pas), décimal, énumération, texte (motif, longueur), liste. |
| Défaut | Valeur si rien n'est saisi. |
| Obligatoire | La ressource doit avoir une valeur effective dans chaque environnement de présence. |
| Surchargeable | Peut prendre une valeur différente par environnement. |
| Verrouillée après publication | Modifiable jusqu'à la première publication d'une révision qui contient la ressource ; ensuite refusée avec l'explication « Azure ne permet pas ce changement sans recréer la ressource ». |
| Irréversible | Ne peut évoluer que dans un sens (exemple : purge protection `false → true`). |
| Dépréciation | Valeurs dépréciées et date ([DEC-41](03-decisions.md)). |
| Condition | Propriété applicable seulement si une autre a une valeur donnée (exemple : capacité Premium). |
| Contraintes croisées | Règles entre propriétés (exemple : CPU et mémoire d'une Container App). |

**RG-RES-01 — Valeur effective.** Pour un environnement : surcharge de cet environnement, sinon valeur
de la ressource, sinon défaut du descripteur.

**RG-RES-02 — Tout est écrit.** La valeur effective de **chaque** propriété applicable est écrite dans
le fichier de paramètres de l'environnement, y compris quand elle vaut le défaut ([DEC-12](03-decisions.md)).
Aucune valeur n'est laissée au défaut implicite d'un module.

**RG-RES-14 — Vide, absent, hérité.** Trois cas distincts, partout (écran, API, import de fichier) :
**hérité** (aucune surcharge : la valeur de la ressource, ou le défaut du catalogue, s'applique) ;
**vide** (une chaîne vide saisie volontairement, seulement si le descripteur l'accepte) ; **absent**
(propriété facultative explicitement non renseignée). Retirer une surcharge revient à « hérité » ; l'API
exige pour cela une commande explicite, jamais une valeur `null` implicite. Aucun réglage n'est perdu en
silence : un import de fichier qui omettrait une valeur existante la laisse inchangée.

**RG-RES-03 — Contrôle à la saisie.** Une valeur hors du descripteur est refusée par le serveur, quel que
soit le client (écran, API, MCP). Les contraintes croisées sont contrôlées sur chaque valeur effective.

**RG-RES-04 — Affichage.** L'écran d'une ressource présente un tableau propriétés × environnements, avec
la valeur effective et sa provenance (surcharge, ressource, défaut).

## 4. Présence par environnement

**RG-RES-05 — Défaut.** Une nouvelle ressource est présente dans tous les environnements ciblés par son
composant. Une ressource existante est absente tant que son identifiant n'est pas saisi pour
l'environnement.

**RG-RES-06 — Effet.** Une ressource absente d'un environnement n'est pas déployée dans cet
environnement. Ses liaisons, paramètres et rôles implicites y sont ignorés.

**RG-RES-07 — Cohérence.** Si la ressource source d'une liaison est présente dans un environnement, la
cible doit y être présente (ou appartenir à un composant `Single`) : sinon erreur `VAL-RES-PRESENCE`.

## 5. Enfants

Certains types ont des objets enfants déployés avec eux (conteneurs blob, files, topics, bases
PostgreSQL, voir [15](15-catalogue.md)).

**RG-RES-08 — Enfants.** Un enfant a un nom (saisi tel quel, contrôlé par le descripteur, unique dans
son parent) et des propriétés, éventuellement surchargeables. Il suit la présence de son parent.

## 6. Ressource existante

**RG-RES-09 — Désignation.** Une ressource existante porte, pour chaque environnement où elle est
présente, un identifiant Azure complet :
`/subscriptions/<id>/resourceGroups/<rg>/providers/<fournisseur>/<type>/<nom>`. Le type doit
correspondre au type IFS choisi.

**RG-RES-10 — Rôle.** Une ressource existante n'a ni propriétés, ni enfants, ni identités, ni paramètres
applicatifs. Elle peut seulement être **cible** de liaisons. Elle peut porter des **propriétés déclarées**
utiles aux contrôles : exposition (publique, restreinte, privée), zone DNS privée, tenant. Elles sont
affichées « déclaré, non vérifié » : IFS ne lit pas Azure pour les confirmer. L'écran n'affiche que ce qui
s'applique.

**RG-RES-11 — Génération.** Elle est déclarée comme référence à une ressource existante à partir de son
identifiant (`existing` en Bicep, source de données en Terraform, fonction `get` en Pulumi) ; elle n'est jamais
déployée ni modifiée. Une attribution de rôle sur une ressource existante est déployée à la portée de
cette ressource, ce qui exige que l'identité de déploiement y ait les droits (signalé dans la liste de
contrôle).

**RG-RES-12 — Sélection** *(lot 2)*. Avec une connexion Azure, l'utilisateur choisit la ressource dans
une liste au lieu de saisir l'identifiant.

## 7. Cycle de vie

**UC-RES-01 — Ajouter une ressource** (`modele.modifier`) :
1. type (catalogue filtré par recherche et catégorie) ;
2. nom logique, groupe de ressources, existante ou non ;
3. propriétés obligatoires et liaisons obligatoires du type (exemple : plan d'hébergement) ;
4. facultatif : surcharges, présence.

L'écran montre aussitôt les noms Azure calculés.

**UC-RES-02 — Modifier une ressource** : propriétés, surcharges, présence, enfants, identités, liaisons,
paramètres, exposition, tags, nom forcé.

**UC-RES-03 — Dupliquer une ressource** dans le même composant ou un autre : nouveau nom logique
obligatoire ; propriétés, surcharges, enfants, liaisons sortantes et paramètres sont copiés.

**UC-RES-04 — Consulter une ressource** : valeurs effectives, noms Azure, liaisons entrantes et
sortantes (explicites et implicites), paramètres, constats, historique.

**UC-RES-05 — Supprimer une ressource** (`modele.modifier`) :
- IFS liste les liaisons entrantes, les paramètres applicatifs qui la citent et les éléments implicites
  qui disparaîtront.
- Si une liaison entrante est **obligatoire** pour sa source (exemple : le plan d'une Web App), la
  suppression est refusée tant que la source n'est pas modifiée.
- Sinon, après confirmation, la ressource et tout ce qui la cite sont supprimés ensemble.
- Dans Azure, la ressource est détachée ou supprimée selon la règle de son composant
  ([DEC-46](03-decisions.md)) ; l'écran le rappelle.

**RG-RES-13 — Concurrence.** Une modification fondée sur une version périmée est refusée ; l'écran
affiche les deux versions et laisse l'utilisateur reprendre.
