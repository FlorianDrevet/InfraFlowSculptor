# 13 — Nommage

## 1. Objectif

Chaque ressource a un **nom logique** court (`orders`). IFS calcule son **nom Azure** dans chaque cible
de déploiement à partir d'un **gabarit** fixé par le projet ou le composant, puis l'**assainit** selon
les règles Azure du type. Le calcul est unique et côté serveur ([DEC-05](03-decisions.md)) ; son
résultat est écrit en clair dans les fichiers ([DEC-06](03-decisions.md)).

## 2. Gabarit

### 2.1 Jetons

| Jeton | Remplacé par | Exemple |
|---|---|---|
| `{project}` | Code du projet | `shop` |
| `{component}` | Code du composant | `orders` |
| `{name}` | Nom logique de la ressource (ou du groupe de ressources) | `api` |
| `{abbr}` | Abréviation du type ([15](15-catalogue.md)) | `ca` |
| `{env}` | Code de la cible : environnement, ou cible propre d'un composant `Single` | `dev`, `shared` |
| `{region}` | Code court de la région de la ressource ([15 § 5](15-catalogue.md)) | `frc` |

**RG-NOM-01 — Syntaxe.** Un gabarit contient au moins `{name}`. En dehors des jetons, il ne contient que
des minuscules, chiffres, `-`, `_` et `.`. Les jetons sont insensibles à la casse. Un jeton inconnu est
refusé avec la liste des jetons valides. Longueur : 200 caractères max.

**RG-NOM-02 — Un seul contrôle.** Les mêmes règles s'appliquent à tous les gabarits, quel que soit leur
niveau.

### 2.2 Niveaux

| Niveau | Gabarit par défaut | Gabarits par type | Abréviations par type |
|---|---|---|---|
| Projet | Oui (obligatoire) | Oui | Oui |
| Composant | Facultatif | Facultatif | Facultatif |

Un gabarit par type vise un type du catalogue ou `ResourceGroup`.

## 3. Valeurs initiales

À la création d'un projet, l'assistant propose trois préréglages ([DEC-60](03-decisions.md)), avec un
aperçu sur des exemples :

| Préréglage | Gabarit par défaut | Gabarit `ResourceGroup` |
|---|---|---|
| **Compact** (défaut) | `{abbr}-{project}-{name}-{env}` | `rg-{project}-{component}-{name}-{env}` |
| **CAF** (Cloud Adoption Framework) | `{abbr}-{project}-{name}-{env}-{region}` | `rg-{project}-{component}-{name}-{env}-{region}` |
| **Personnalisé** | Saisi | Saisi |

Ces valeurs vivent à un seul endroit (le descripteur du projet par défaut) et sont modifiables dans
l'assistant. Il n'existe pas de gabarit « recommandé » caché : l'assainissement (section 5) suffit à
produire des noms valides pour les types sans tiret (exemple : `cr-shop-main-shared` devient
`crshopmainshared` pour un registre).

## 4. Calcul du nom Azure

Pour une ressource *R* de type *T*, d'un composant *C*, dans une cible *E* :

**Gabarit retenu** — premier trouvé dans cet ordre :
1. nom forcé de *R* pour *E* (section 4.1) : le gabarit n'est pas utilisé ;
2. gabarit de *C* pour *T* ;
3. gabarit par défaut de *C* ;
4. gabarit du projet pour *T* ;
5. gabarit par défaut du projet.

**Abréviation retenue** : celle de *C* pour *T*, sinon celle du projet, sinon celle du catalogue.

**Étapes** : remplacement des jetons → assainissement (section 5) → contrôle de longueur → contrôle
d'unicité (section 6).

**RG-NOM-03 — Explication.** Pour chaque nom affiché, IFS indique le niveau du gabarit retenu, le
gabarit, le résultat avant assainissement et les transformations appliquées.

### 4.1 Nom forcé

**RG-NOM-04 — Nom forcé.** Une ressource ou un groupe de ressources peut porter, pour un environnement,
un nom Azure imposé (cas des noms historiques). Le nom forcé n'est pas assaini : il doit déjà respecter
les contraintes du type, sinon il est refusé.

### 4.2 Ressources sans nom calculé

- Une **ressource existante** est désignée par son identifiant Azure ([DEC-18](03-decisions.md)).
- Les **enfants** (conteneur blob, file, base de données d'un serveur PostgreSQL) portent un nom saisi
  tel quel, contrôlé par le descripteur, unique dans leur parent.
- Les ressources dont Azure impose le nom (zone DNS privée `privatelink.*`) prennent le nom fixé par le
  descripteur.

## 5. Assainissement

**RG-NOM-05 — Règles par type.** Le descripteur de chaque type déclare : caractères autorisés, casse,
séparateurs autorisés, longueur min et max, premier et dernier caractère autorisés. Après remplacement
des jetons, IFS applique dans l'ordre :

1. passage en minuscules si le type l'impose ;
2. suppression des caractères non autorisés (y compris les séparateurs interdits) ;
3. réduction des séparateurs consécutifs à un seul ;
4. suppression des séparateurs et caractères interdits en début et en fin.

**RG-NOM-06 — Pas de troncature.** Si le résultat est trop long ou trop court, c'est une erreur
`VAL-NOM-LONGUEUR` qui affiche le nom obtenu, sa longueur, la limite, et propose de définir un gabarit
pour ce type ou un nom forcé ([DEC-07](03-decisions.md)).

## 6. Unicité

**RG-NOM-07 — Nom logique.** Le nom logique d'une ressource est unique par (composant, type).
Format : `^[a-z][a-z0-9-]{0,39}$`.

**RG-NOM-08 — Nom Azure.** Le descripteur déclare la portée d'unicité du type : `global` (Key Vault,
compte de stockage, registre…), `groupe de ressources`, `parent` (enfants). Deux ressources du même
projet qui produisent le même nom dans la même portée sont en erreur `VAL-NOM-COLLISION`. Pour la portée
`global`, la comparaison porte sur tous les projets de l'organisation.

**RG-NOM-09 — Disponibilité dans Azure.** Pour les types de portée `global`, IFS indique si le nom semble
déjà utilisé dans Azure :
- lot 1 : résolution DNS publique du nom de domaine du service (heuristique). Le résultat est un constat
  `Info` (« déjà résolu : normal si la ressource est déjà déployée »).
- lot 2 : si l'organisation a une connexion Azure en lecture, appel à l'API `checkNameAvailability`
  d'Azure. Un nom indisponible est un `Avertissement`.

## 7. Changements

**RG-NOM-10 — Impact.** Toute modification qui change au moins un nom Azure déjà publié (gabarit,
abréviation, code de projet, de composant ou d'environnement, nom logique, nom forcé) affiche la liste
des noms avant et après, et l'avertissement « Azure créera de nouvelles ressources ; les anciennes
seront détachées ». La modification exige une confirmation explicite.

**RG-NOM-11 — Aperçu.** L'écran de nommage affiche en direct le résultat des gabarits sur toutes les
ressources du projet ou du composant, avec les constats de longueur et de collision.

## 8. Cas d'utilisation

- **UC-NOM-01** — Définir le gabarit par défaut du projet (`conventions.gerer` ; les UC-NOM-02 et 03 au niveau composant exigent `composants.gerer`, UC-NOM-04 `modele.modifier`).
- **UC-NOM-02** — Définir ou retirer un gabarit par type, au niveau projet ou composant.
- **UC-NOM-03** — Définir ou retirer une abréviation par type, au niveau projet ou composant.
- **UC-NOM-04** — Définir ou retirer un nom forcé pour une ressource et un environnement.
- **UC-NOM-05** — Consulter les noms Azure d'une ressource dans toutes ses cibles, avec explication et
  disponibilité.
