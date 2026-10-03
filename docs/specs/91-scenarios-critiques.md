# 91 — Scénarios critiques

> Chaque garantie de livraison, de droits et de reprise est décrite ici par un scénario complet : état
> initial, acteur et permissions, ordre des effets, interruption possible, état final attendu et preuve.
> Ces scénarios sont normatifs : ils servent de critères d'acceptation ([90 § 4](90-projet-de-reference.md))
> et de base aux preuves du registre ([04 § 7.2](04-perimetre-et-lots.md)). Ils n'ont pas encore été
> exécutés.

Données : le projet de référence ([90](90-projet-de-reference.md)), cibles `dev` (non protégée) et `prd`
(protégée). Sauf mention, le langage est Bicep et la plateforme Azure DevOps.

## T01 — Révoquer un accès en production protégée

- **État initial.** La pile `ifs-shop-orders-prd` gère `ca-shop-api-prd` et l'attribution R (Azure Service
  Bus Data Sender de `id-shop-api-prd` sur `sbns-shop-orders-prd`). La pile porte le refus `denyDelete`,
  l'identité `id-ifs-deploy-shop-prd` exclue ([DEC-99](03-decisions.md)).
- **Acteurs.** Un contributeur retire la liaison ; une personne qui a `publier` publie ; l'approbateur de
  `prd` approuve.
- **Ordre des effets.** Aperçu : R figure dans « accès révoqués ». Déploiement : journal (R `à faire`) →
  mise à jour de la pile, qui détache R → suppression de R par l'identité de déploiement → journal (R
  `faite`) → rapport.
- **Interruption.** Voir T02.
- **État final.** R n'existe plus ; `sbns-shop-orders-prd` et ses messages existent toujours et restent
  protégés ; le contrôle `/health/dependencies` constate l'échec de l'envoi après propagation.
- **Preuve.** Rapport de release ; une tentative de suppression de `sbns-shop-orders-prd` par un
  propriétaire de l'abonnement est refusée par le refus de la pile.

## T02 — Agent tué entre le détachement et la révocation

- **État initial.** Comme T01, release en cours.
- **Interruption.** L'agent disparaît juste après la mise à jour de la pile : R est détachée, pas
  supprimée ; aucun rapport final.
- **Ordre des effets de la reprise.** La release suivante (même révision ou plus récente) lit le journal
  au stage Aperçu : R est `à faire` ; l'aperçu l'affiche dans « opérations reprises » ; après approbation,
  l'étape 7 supprime R avant tout le reste ([DEC-100](03-decisions.md)).
- **État affiché entre les deux.** `prd` : partiellement appliquée (journal : pile déployée, révocation
  restante).
- **État final.** Identique à T01.
- **Preuve.** Le journal montre R `à faire` puis `faite` par la seconde exécution, avec l'identifiant
  d'opération inchangé.

## T03 — Le déploiement de l'unité échoue à mi-chemin

- **État initial.** Une révision ajoute trois ressources ; la troisième échoue (quota).
- **Ordre des effets.** Journal (déploiement `commencé`) → la pile crée deux ressources puis échoue →
  rapport avec les ressources créées et l'erreur.
- **État final.** `prd` : partiellement appliquée ; la fiche d'opération liste deux ressources créées,
  une en échec, les opérations suivantes restantes, et l'action : corriger le quota puis relancer.
- **Preuve.** Relancer la release termine la pile et les opérations suivantes, sans doublon.

## T04 — Livraison applicative entre l'aperçu et le déploiement d'infrastructure

- **État initial.** Image en service `1281`. La release d'infrastructure d'`orders` a terminé son aperçu
  et attend l'approbation de `prd`.
- **Acteurs.** La release applicative livre l'image `1287` en `prd` pendant l'attente (elle prend le verrou
  de la cible, puis le libère).
- **Ordre des effets.** Approbation → étape 5 sous verrou : relit l'image `1287` et la reconduit ;
  l'empreinte des effets est inchangée (l'image n'en fait pas partie, [DEC-102](03-decisions.md)) →
  déploiement.
- **Variante.** Un collègue fusionne entre-temps une nouvelle révision qui change un rôle : la release
  approuvée s'arrête à l'étape 5 sans rien modifier ; la nouvelle release demande une approbation.
- **État final.** `ca-shop-api-prd` sert toujours `1287`.
- **Preuve.** Rapport : image reconduite `1287`, empreinte approuvée égale à l'empreinte appliquée.

## T05 — Demande d'accès entre deux équipes sans droits croisés

- **État initial.** Alice a `modele.modifier` sur `orders` seulement ; Bob sur `core` seulement.
- **Ordre des effets.** Alice crée une liaison « lecture de secret » de `api` vers `kv main` : elle devient
  une demande d'accès. Bob relit l'effet précis (identité `id api`, rôle Key Vault Secrets User, coffre
  `kv main`) et approuve. IFS revérifie les deux droits et applique la seule liaison
  ([DEC-104](03-decisions.md)).
- **État final.** La liaison existe ; ni Alice ni Bob n'ont reçu de droit sur l'autre composant ; la
  demande et ses deux consentements sont journalisés.
- **Preuve.** Les droits effectifs d'Alice et de Bob sont inchangés après l'application.

## T06 — L'effet d'une demande approuvée change avant son application

- **Ordre des effets.** Après l'approbation de Bob, Alice change le rôle demandé en Key Vault Secrets
  Officer.
- **État final.** L'approbation tombe ; la demande revient « à relire » ; rien n'est appliqué.

## T07 — Base et identité consommatrice dans deux composants

- **État initial.** `sqldb invoices` dans le composant `data` ; `api` (identité système) dans `billing`,
  liée par « accès aux données ». Ordre publié : `data` puis `billing`.
- **Ordre des effets.** Release de `data` : serveur et base, aucun utilisateur. Release de `billing` :
  déploiement d'`api` (son identité existe) → étape 11 : création de l'utilisateur par SID et de ses rôles
  dans `sqldb invoices` ([DEC-103](03-decisions.md)).
- **État final.** Premier déploiement réussi en une exécution par composant, sans passe cachée.
- **Preuve.** Le journal de `billing` contient l'opération d'accès aux données ; celui de `data` n'en
  contient aucune.

## T08 — Mot de passe destiné à un coffre existant

- **Ordre des effets.** Un serveur SQL déclare un mot de passe `Généré` dans un Key Vault existant.
- **État final.** La validation produit `VAL-SEC-GENERE-EXISTANT` ; l'écran propose `Secret de pipeline`
  ou `Géré hors IFS`. Avec `Secret de pipeline`, l'aperçu vérifie la variable, et l'étape 8 écrit le
  secret avant le déploiement ([DEC-105](03-decisions.md)).

## T09 — Deux consommateurs d'un secret dans un coffre existant

- **État initial.** `orders` et `billing` consomment `shared-token` du même coffre existant.
- **Ordre des effets.** Les deux le déclarent en « secret de pipeline » : `VAL-PAR-SECRET-PROPRIETAIRE`.
  Le contributeur désigne `orders` comme propriétaire ; `billing` le lit en « géré hors IFS ».
- **Variante.** `billing` retire son paramètre : le secret reste.
- **État final.** Un seul écrivain ; aucune suppression du secret.

## T10 — Supprimer un composant pendant qu'une cible est indisponible

- **Ordre des effets.** `billing` passe à « retrait demandé » ; la publication suivante le remplace par sa
  forme de décommissionnement ; `dev` confirme ; `prd` est inaccessible (exécuteurs en panne).
- **État final.** `billing` : retiré en `dev`, retrait en attente en `prd`, visible ; ses fichiers restent
  publiés tant que `prd` n'a pas confirmé ([DEC-106](03-decisions.md)).

## T11 — Secret expurgé puis vieille révision

- **Ordre des effets.** Une valeur secrète, publiée dans la révision 12, est expurgée.
- **État final.** La révision 12 est contaminée : ses fichiers ne sont plus téléchargeables ni comparables
  dans IFS ; IFS liste les dépôts et commits où elle a été publiée. Après une restauration des sauvegardes
  d'IFS, l'expurgation est réappliquée avant la réouverture ([DEC-107](03-decisions.md)).

## T12 — Correctif du catalogue après publication

- **Ordre des effets.** Un défaut de sécurité est corrigé : `2026.09.1` est publiée, `2026.09` est retirée
  avec une date de refus.
- **État final.** Un projet en `2026.09` voit la montée proposée, puis l'erreur `VAL-CAT-DEPRECIE` à la date
  de refus ; ses révisions anciennes se régénèrent à l'identique pour comparaison, mais ne sont plus
  publiables. Aucune version n'a changé de contenu sous le même numéro.

## T13 — Import d'un clone avec des noms forcés

- **Ordre des effets.** Un projet est exporté puis importé avec un nouveau code ; deux ressources ont des
  noms forcés.
- **État final.** Deux erreurs de collision bloquent la première publication ; pour chacune, l'utilisateur
  renomme ou transforme la ressource en ressource existante ([DEC-108](03-decisions.md)).

## T14 — Aperçu GitHub *(lot 2)*

- **État initial.** Kit GitHub : environnements `shop-prd` (relecteurs requis) et `shop-prd-apercu`
  (branche par défaut, sans relecteur), avec un identifiant fédéré pour chacun.
- **Ordre des effets.** Le job d'aperçu s'exécute dans `shop-prd-apercu` et s'authentifie ; le job de
  déploiement attend les relecteurs de `shop-prd` ([DEC-101](03-decisions.md)).
- **Preuve.** Un job d'aperçu lancé depuis une autre branche échoue à l'authentification.

## T15 — Pipeline applicatif du client pendant une release IFS

- **Ordre des effets.** Le pipeline du client déploie dans l'environnement `shop-prd` : il attend la fin de
  la release d'infrastructure, qui tient le verrou. S'il ne publie pas de rapport de livraison, l'écran
  l'indique et ne garantit pas le verrou ([DEC-109](03-decisions.md)).

## T16 — Reprise avec une révision plus récente, cible en retard

- **État initial.** La révision 3 retire une file et un accès ; sa release `prd` est interrompue après la
  pile (opérations restantes au journal). La révision 4 est publiée.
- **Ordre des effets.** La release de la révision 4 en `prd` reprend d'abord les opérations de la
  révision 3 inscrites au journal, puis applique la révision 4.
- **État final.** L'accès de la révision 3 est révoqué, la file figure à l'inventaire des ressources
  détachées, et l'état final est rattaché au manifeste de la révision 4.
- **Preuve.** Le rapport de la révision 4 cite les opérations reprises avec leur révision d'origine.
