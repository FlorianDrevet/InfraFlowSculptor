# 01 — Principes

Ces principes tranchent les cas que les specs n'ont pas prévus. En cas de conflit entre une règle et
un principe, le principe l'emporte et la règle doit être corrigée.

## P1 — Le modèle est la source de vérité

Le modèle IFS décrit l'infrastructure voulue. Les fichiers produits en sont une projection. IFS est
propriétaire des fichiers qu'il gère dans les dépôts du client. Une modification manuelle de ces
fichiers est détectée et signalée avant d'être écrasée ([DEC-01](03-decisions.md)). La personnalisation
passe par des **points d'extension** explicites, jamais par l'édition des fichiers générés.

## P2 — Ce qui est affiché est ce qui est généré

Les noms, les valeurs effectives par environnement, les rôles et l'ordre de déploiement sont calculés
**une seule fois**, par le serveur. L'écran, l'API, le serveur MCP, la validation et la génération
lisent le même résultat. Aucun calcul métier n'est refait dans l'interface.

## P3 — Rien de silencieux

Toute donnée saisie a un effet visible dans les fichiers produits, ou elle est refusée. Une option
sans effet n'existe pas. Une valeur ignorée est une erreur, pas un comportement. Une opération qui ne
fait rien le dit.

## P4 — Sécurisé par défaut

- Identités managées partout. Pas de mot de passe d'administration, pas d'identifiants admin de registre.
- Les secrets ne transitent jamais en clair dans les fichiers ; ils sont lus depuis Key Vault.
- TLS 1.2 minimum, HTTPS seul, FTP désactivé, accès public anonyme désactivé.
- Les rôles privilégiés (Owner, User Access Administrator, Role Based Access Control Administrator)
  ne sont jamais attribuables depuis le modèle.

Un utilisateur peut relâcher certaines de ces valeurs quand le catalogue le permet. Il le fait
explicitement et la validation le signale.

## P5 — Câblage implicite

Quand une intention en implique d'autres, IFS les déduit. « Mon application lit ce secret » implique
le rôle `Key Vault Secrets User` pour l'identité de l'application. « Mon application tire son image de
ce registre » implique `AcrPull`. Les éléments déduits sont visibles, marqués « implicite », avec leur
origine. Ils disparaissent avec elle.

## P6 — Une notion, un mécanisme

Un même besoin ne doit pas avoir deux mécanismes. Tous les liens entre ressources sont des
**liaisons**. Toute valeur qui varie par environnement est une **surcharge**. Tout paramètre d'une
application est un **paramètre applicatif**, quelle que soit sa destination.

## P7 — Le catalogue est de la donnée

Ce qu'un type de ressource accepte (propriétés, valeurs, contraintes de nom, rôles, sorties, liaisons)
est décrit par un **descripteur** versionné. L'écran, l'API, le MCP, la validation et la génération
lisent ce descripteur. Ajouter une valeur, ou un type qui n'utilise que des capacités existantes, ne
demande pas de modifier plusieurs couches. Une **nouvelle capacité** (un nouveau type de liaison, une
politique d'accès hors RBAC, un nouveau parcours de livraison) demande du code, et un type qui en dépend
ne sort qu'avec elle.

## P8 — Les contrôles sont côté serveur

Toute règle est vérifiée par le serveur. L'écran peut l'anticiper pour le confort, jamais la remplacer.
Une erreur bloquante bloque pour tous les clients : écran, API et MCP.

## P9 — Pas de promesse non tenue

Aucun écran n'affiche une fonctionnalité qui n'existe pas (« bientôt disponible », bouton grisé sans
raison). Une fonction des lots suivants est absente de l'interface tant qu'elle n'est pas livrée.

## P10 — Sortie lisible et sans verrou

Le code produit est idiomatique dans son langage (Bicep, Terraform, Pulumi) et sur sa plateforme CI,
formaté, déterministe et commenté. Les noms y figurent en clair. Une personne qui ne connaît pas IFS doit
pouvoir le relire, le comprendre et le maintenir.

## P11 — Le modèle ignore les outils

Le modèle décrit Azure, pas un langage ni une plateforme CI. Une notion n'entre dans le modèle que si elle
s'exprime dans tous les langages et toutes les plateformes supportés, **avec le même comportement**
(création, mise à jour, retrait), pas seulement la même syntaxe. Quand ce n'est pas le cas, le descripteur
le déclare dans sa matrice de prise en charge par langage, et la validation le signale
(`VAL-GEN-LANGAGE`) : la parité est garantie sur ce sous-ensemble déclaré. Ce qui est propre à un outil vit
dans son émetteur ([DEC-44](03-decisions.md)) et n'y prend aucune décision métier.
