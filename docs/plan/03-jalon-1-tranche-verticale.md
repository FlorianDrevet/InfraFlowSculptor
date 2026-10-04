# Jalon 1 — Tranche verticale

> **Niveau : découpé.** Chaque étape dit quoi construire, avec ses références, ses tests et sa recette. Claude la
> **détaille au niveau d'exécution** (fichiers, classes, commandes exactes) au verrou [`R-08`](02-jalon-0-pilote.md#-r-08--sortie-du-jalon-0),
> sur le code réel du jalon 0. Luna n'exécute rien de ce fichier avant ce détail : `gate.py check` l'en empêche
> (R-08 non approuvé).

**But** ([04 § 2.1](../specs/04-perimetre-et-lots.md), [DEC-58](../specs/03-decisions.md)) : le projet de référence
([90](../specs/90-projet-de-reference.md)) déployé de bout en bout, avec tout ce que le jalon 1 promet.

**Critère de sortie** : critères 1 à 18 de [90 § 4](../specs/90-projet-de-reference.md) sur la variante Bicep + Azure
DevOps ; scénarios T01–T04, T07, T16 ([91](../specs/91-scenarios-critiques.md)) réussis ; preuve P6 obtenue.

**Écart à arbitrer au détail (R-08)** : [04 § 2.1](../specs/04-perimetre-et-lots.md) place « build conteneur **et code** »
au jalon 1, mais les onze types du jalon 1 sont tous des conteneurs ; Web App et Function App arrivent au jalon 2.
Proposition : profils de build Code au jalon 2, avec leurs types (J2-01).

---

## Segment « modèle complet » — branche `impl/j1-modele`

### J1-01 — Catalogue 2026.10 : les onze types du projet de référence, montée de version

| | |
|---|---|
| **Spécifications** | [15 § 3](../specs/15-catalogue.md) (App Configuration, Service Bus et ses enfants), [DEC-92](../specs/03-decisions.md), [40 § 3](../specs/40-exploitation-ifs.md) (RG-EXP-05), `VAL-CAT-MISE-A-JOUR`, `VAL-CAT-FIN-SUPPORT` |
| **Maquette** | [ResourceChildren](../design/maquette-v1/preview/ResourceChildren.html), [ProjectSettings](../design/maquette-v1/preview/ProjectSettings.html) (version du catalogue, montée) |
| **Commit** | `feat(catalogue): version 2026.10 et montée de version des projets` |

🎯 **Objectif.** Les onze types de [90](../specs/90-projet-de-reference.md) ; un projet monte de version explicitement.
🔧 **À faire.** Version `catalog/2026.10/` (11 types), enfants Service Bus (files, topics, abonnements), commande de
montée avec la liste des fichiers et valeurs qui changeraient, constats de version, écran des enfants.
✅ **Vérification automatique.** Schémas ; correspondance AVM ; montée simulée du projet pilote → seuls les changements
annoncés.
🧪 **Test manuel.** Monter le projet pilote de `2026.10-pilot` à `2026.10` : l'écran liste ce qui change ; après montée,
ajouter la file `order-created` à `sbns orders`.

### J1-02 — Clés App Configuration, lecture de configuration, sorties sensibles

| | |
|---|---|
| **Spécifications** | [17 § 2-5](../specs/17-parametres-applicatifs-et-secrets.md) (clés, RG-PAR-02, 09, 23, sortie sensible), [16 § 3](../specs/16-liaisons-identites-et-acces.md) (Lecture de configuration), [22 § 3.1](../specs/22-pipelines.md) étape 11 |
| **Commit** | `feat(modele): clés App Configuration et synchronisation par la release` |

🎯 **Objectif.** La configuration partagée par App Configuration, synchronisée par la release, jamais dans le code d'infrastructure.
🔧 **À faire.** Destination « clé », fichier `appconfig/<magasin>.<cible>.json`, étape de synchronisation du module de
release (ajout, mise à jour, suppression des seules clés IFS), alimentation « sortie sensible », rôles implicites.
✅ **Vérification automatique.** Moteur (implicites de [90 § 2.3](../specs/90-projet-de-reference.md)) ; Pester de la
synchronisation (clé créée hors IFS jamais touchée).
🧪 **Test manuel.** Clé `orders:maxItemsPerOrder` = 50 (dev : 500) ; l'application témoin la lit (`appconfig` de `/health/dependencies`).

### J1-03 — Historique du modèle : jeux de modifications et auteur par propriété

| | |
|---|---|
| **Spécifications** | [31 § 2-3](../specs/31-historique-et-versions.md) : RG-HIS-01 à 06 ; [DEC-64, 65](../specs/03-decisions.md) ; [EXG-22](../specs/27-exigences-non-fonctionnelles.md) |
| **Maquette** | [ProjectHistory](../design/maquette-v1/preview/ProjectHistory.html) — **exclus** : étiquettes, comparaison, restauration (J2) |
| **Commit** | `feat(historique): jeux de modifications et provenance des valeurs` |

🎯 **Objectif.** Qui a changé quoi, quand, pour chaque objet et chaque valeur.
🔧 **À faire.** `ChangeSet` produit par `UnitOfWorkBehavior` (une commande = un jeu), différences par objet et par
environnement, lien audit ↔ jeu, provenance au survol/focus/toucher, onglet Historique des objets, chronologie du projet.
✅ **Vérification automatique.** Un jeu par commande ; reconstruction du modèle à une version donnée en < 3 s pour 300
ressources (préparation de J2-04).
🧪 **Test manuel.** bob modifie une surcharge ; survoler la cellule → « bob, il y a 1 min, jeu n° 42 » ; chronologie du projet.

### J1-04 — Rôles prédéfinis complets et jetons d'API

| | |
|---|---|
| **Spécifications** | [10 § 4.2](../specs/10-organisations-et-acces.md), [10 § 7](../specs/10-organisations-et-acces.md) : RG-ORG-17 à 19, UC-ORG-16 à 18 ; [EXG-02](../specs/27-exigences-non-fonctionnelles.md) |
| **Maquette** | [ProjectMembers](../design/maquette-v1/preview/ProjectMembers.html) (8 rôles), [ApiTokens](../design/maquette-v1/preview/ApiTokens.html), [ApiTokenCreate](../design/maquette-v1/preview/ApiTokenCreate.html) — **exclu** : panneau OAuth MCP (J3) |
| **Commit** | `feat(acces): rôles prédéfinis et jetons d'API` |

🎯 **Objectif.** Scripts et outils agissent avec les droits de l'utilisateur, limités par les portées.
🔧 **À faire.** `ApiToken` (préfixe `ifs_`, 256 bits, empreinte seule), `ApiTokenAuthenticationHandler`, portées par
commande, révocation, expiration et e-mail à J-7, liste administrateur ; les huit rôles dans l'écran.
✅ **Vérification automatique.** Jeton jamais stocké en clair ; portée insuffisante → 403 ; isolation avec jeton.
🧪 **Test manuel.** Créer un jeton `read` ; `curl -H "Authorization: Bearer ifs_…" …/v1/projects` → 200 ; une commande → 403.

### J1-05 — Composants : duplication, décommissionnement, code additionnel, exécuteurs

| | |
|---|---|
| **Spécifications** | [12 § 4](../specs/12-composants-et-groupes-de-ressources.md) UC-CMP-03, UC-CMP-04 ; [DEC-106](../specs/03-decisions.md) ; [21 § 7](../specs/21-generation-et-revisions.md) ; [11 § 2, 4](../specs/11-projets-et-environnements.md) (exécuteurs) ; [91 T10](../specs/91-scenarios-critiques.md) |
| **Commit** | `feat(composant): duplication, décommissionnement et code additionnel` |

🎯 **Objectif.** Faire évoluer la structure d'un projet déployé.
🔧 **À faire.** Duplication ; retrait demandé → forme de décommissionnement générée → retiré cible par cible ; point
d'extension « code d'infrastructure additionnel » (contrat de RG-GEN-16, bouchon au contrôle) ; exécuteurs.
✅ **Vérification automatique.** Sortie de décommissionnement (instantané) ; T10 en test d'acceptation (Gitea + rapports simulés).
🧪 **Test manuel.** Dupliquer `orders` en `billing`, publier, puis supprimer `billing` : état « retrait demandé », forme de
décommissionnement dans la pull request.

### J1-06 — Changement de langage ou de plateforme (écran des conséquences)

| | |
|---|---|
| **Spécifications** | [UC-PRJ-06](../specs/11-projets-et-environnements.md), [DEC-48](../specs/03-decisions.md), `VAL-GEN-LANGAGE` |
| **Commit** | `feat(projet): conséquences d'un changement d'outil` |

🎯 **Objectif.** Le mécanisme existe, même si seuls Bicep et Azure DevOps sont livrés : rien n'est proposé qui n'existe pas
([P9](../specs/01-principes.md)) ; le moteur sait refuser ce qu'un émetteur ne traduit pas.
🔧 **À faire.** Matrice de prise en charge par langage dans les descripteurs, `VAL-GEN-LANGAGE`, commande de changement
(désactivée dans l'interface tant qu'un seul choix existe).
✅ **Vérification automatique.** Moteur avec un descripteur de test non pris en charge → constat.
🧪 **Test manuel.** Aucun visible au jalon 1 (P9) ; vérification par Scalar.

### 🔒 R-09 — Revue du modèle complet

**Périmètre** : `J1-01` à `J1-06`. **Recette** : `recettes/03-jalon-1.md` § Modèle. **Après approbation** : `impl/j1-livraison`.

---

## Segment « livraison complète » — branche `impl/j1-livraison`

### J1-07 — Publication complète : préréglages, relecteurs, mode direct, GitHub

| | |
|---|---|
| **Spécifications** | [24](../specs/24-depots-et-publication.md) (préréglages, RG-PUB-05, 06, 07, 12, 13, 14, 17), [24 § 2](../specs/24-depots-et-publication.md) (application GitHub), [04 § 1](../specs/04-perimetre-et-lots.md) (Azure Repos et GitHub au lot 1) |
| **Maquette** | [ProjectPublishPlan](../design/maquette-v1/preview/ProjectPublishPlan.html), [OrgConnections](../design/maquette-v1/preview/OrgConnections.html) (GitHub), [PublishDialog](../design/maquette-v1/preview/PublishDialog.html) (mode direct) |
| **Commit** | `feat(publication): préréglages, plusieurs dépôts et GitHub` |

🎯 **Objectif.** Les trois préréglages, plusieurs dépôts publiés indépendamment, des dépôts GitHub pour des pipelines Azure DevOps.
🔧 **À faire.** `GitHubProvider` (application GitHub, Octokit), préréglages, relecteurs par défaut, mode direct, résultat par dépôt.
✅ **Vérification automatique.** WireMock GitHub ; publication du projet de référence en « infra et code séparés » (Gitea).
🧪 **Test manuel.** Projet de référence publié dans `shop-infra` et `shop-app` : deux pull requests ; une en échec (dépôt
fermé) republiée seule.

### J1-08 — Projet de référence complet et sortie de référence `reference/shop/`

| | |
|---|---|
| **Spécifications** | [90](../specs/90-projet-de-reference.md) § 1 à 4 ; [EXG-16](../specs/27-exigences-non-fonctionnelles.md), [EXG-17](../specs/27-exigences-non-fonctionnelles.md) |
| **Commit** | `test(reference): projet de référence complet et sa sortie` |

🎯 **Objectif.** Le test d'acceptation officiel du lot 1, généré à chaque build.
🔧 **À faire.** Données de démonstration du projet [90](../specs/90-projet-de-reference.md) ; sortie générée, **relue par Claude**
au verrou, figée dans `reference/shop/bicep-azdo/` ; `Engine.Tests` sur les résultats de [90 § 2](../specs/90-projet-de-reference.md) ;
recette Azure des critères 3 à 18.
✅ **Vérification automatique.** `ReferenceOutputTests` sur les deux projets de référence.
🧪 **Test manuel.** Recette `recettes/03-jalon-1.md` § Référence (critères 3 à 18 de [90 § 4](../specs/90-projet-de-reference.md)).

### J1-09 — Suivi : notifications, retards, décommissionnement

| | |
|---|---|
| **Spécifications** | [28](../specs/28-suivi-des-deploiements.md) (RG-SUI-06, états de retrait), [26 § 4](../specs/26-interface.md) (e-mails du suivi) |
| **Commit** | `feat(suivi): notifications et états de décommissionnement` |

🎯 **Objectif.** Être prévenu d'un échec, d'une approbation qui attend, d'une cible en retard.
🔧 **À faire.** Notifications e-mail du suivi, états retrait demandé/retiré, indicateurs agrégés.
✅ **Vérification automatique.** Rapports enregistrés → notifications attendues (MailPit en acceptation).
🧪 **Test manuel.** Faire échouer une release (secret vide) → e-mail à l'auteur de la révision.

### 🔒 R-10 — Revue de la livraison complète

**Périmètre** : `J1-07` à `J1-09`. **Recette** : `recettes/03-jalon-1.md` § Référence. **Après approbation** : `impl/j1-exploitation`.

---

## Segment « exploitation et garde-fous » — branche `impl/j1-exploitation`

### J1-10 — Back-office : catalogue, accès support, organisations

| | |
|---|---|
| **Spécifications** | [40](../specs/40-exploitation-ifs.md) entier ; [DEC-57](../specs/03-decisions.md) ; [91 T11, T12](../specs/91-scenarios-critiques.md) |
| **Maquette** | [BackofficeCatalog](../design/maquette-v1/preview/BackofficeCatalog.html), [BackofficeSupport](../design/maquette-v1/preview/BackofficeSupport.html) ; zone accès support de [OrgSettings](../design/maquette-v1/preview/OrgSettings.html) |
| **Commit** | `feat(exploitation): back-office IFS` |

🎯 **Objectif.** Opérer IFS sans jamais lire les données d'un client sans son accord.
🔧 **À faire.** Rôles internes (politique `Internal`), cycle de vie du catalogue et validation avant publication (RG-EXP-04 :
génération de toutes les variantes, déploiement de test déclenché manuellement), versions de correctif et retrait,
accès support consenti et tracé dans l'audit du client, suspension, réactivation, purge, récupération, accès anticipé,
bandeau d'incident, dossier de diagnostic.
✅ **Vérification automatique.** Aucun accès aux données client sans accès approuvé (tests d'isolation avec `ops`).
🧪 **Test manuel.** `ops` demande un accès à Contoso ; alice approuve ; `ops` lit le projet ; l'audit de Contoso le montre.

### J1-11 — Garde-fou P6 : prototypes Terraform et GitHub Actions (non livrés)

| | |
|---|---|
| **Spécifications** | [04 § 1](../specs/04-perimetre-et-lots.md) (garde-fou), [DEC-44](../specs/03-decisions.md), [DEC-93](../specs/03-decisions.md), [91 T14](../specs/91-scenarios-critiques.md) |
| **Commit** | `test(prototypes): émetteurs Terraform et GitHub Actions du sous-ensemble représentatif` |

🎯 **Objectif.** Prouver que le plan de déploiement n'est façonné ni par Bicep ni par Azure DevOps (P6).
🔧 **À faire.** Émetteurs prototypes (hors de l'interface, derrière une option interne) pour le sous-ensemble de
[04 § 1](../specs/04-perimetre-et-lots.md) ; déploiement réel ; **toute décision métier qu'ils doivent prendre est un défaut du
plan**, corrigé dans le moteur.
✅ **Vérification automatique.** Compilation et `terraform validate` / schéma GitHub.
🧪 **Test manuel.** Recette P6 (`recettes/03-jalon-1.md` § P6), y compris l'aperçu GitHub (T14).

### J1-12 — Exigences transverses avant le pilote élargi

| | |
|---|---|
| **Spécifications** | [EXG-03, 06, 07, 08, 10, 21](../specs/27-exigences-non-fonctionnelles.md) ; [UC-ORG-14, 15](../specs/10-organisations-et-acces.md) ; [RG-EXP-08, 11](../specs/40-exploitation-ifs.md) |
| **Commit** | `feat(plateforme): limites distribuées, RGPD, sauvegarde et performance` |

🎯 **Objectif.** Tenir les engagements de sécurité, de performance et de conformité.
🔧 **À faire.** Limites par Redis, performance 1 000 ressources / 5 000 liaisons, suppression de compte et pseudonymisation,
suppression d'organisation (30 jours), restauration testée et reprise après restauration, page d'état, liste ASVS niveau 2.
✅ **Vérification automatique.** Tests de performance ; tests RGPD ; exercice de restauration scripté.
🧪 **Test manuel.** Supprimer le compte de chloe → profil effacé, audit pseudonymisé ; restaurer une sauvegarde de `dev`.

### 🔒 R-11 — Revue de l'exploitation et des garde-fous

**Périmètre** : `J1-10`, `J1-11`. **Recette** : back-office et P6.

### 🔒 R-12 — Sortie du jalon 1

**Périmètre** : `J1-12` et le jalon entier. **Claude** : critères de [90 § 4](../specs/90-projet-de-reference.md), puis
**détaille le jalon 2** (skill `detailler-jalon`). **Après approbation** : premier segment du jalon 2.
