# 29 — Import d'une infrastructure existante *(lot 3)*

## 1. Objectif

Démarrer depuis une infrastructure déjà décrite ou déjà déployée, au lieu de tout modéliser à la main
([DEC-49](03-decisions.md)). Le résultat n'est jamais appliqué directement : c'est une **proposition de
modification** ([25 § 4](25-agent-ia-mcp.md)), relue avant application.

## 2. Sources

| Source | Entrée | Prérequis |
|---|---|---|
| **Modèle ARM** | Un ou plusieurs fichiers JSON (modèle et paramètres), ou un export de groupe de ressources du portail | — |
| **Bicep** | Fichiers `.bicep` et `.bicepparam` ; IFS les compile en ARM puis traite comme un modèle ARM | Modules référencés accessibles (registre public ou fichiers fournis) |
| **Groupe de ressources Azure** | Un ou plusieurs groupes, par environnement | Connexion Azure en lecture de l'organisation ([04 § 3](04-perimetre-et-lots.md)) |

**RG-IMP-01 — Une source par environnement.** L'utilisateur peut fournir une source par environnement
(exemple : le modèle avec `dev.parameters.json`, puis avec `prod.parameters.json` ; ou le groupe de
ressources de dev, puis celui de prod). IFS rapproche les ressources d'un environnement à l'autre par
type et par nom, après retrait des parties propres à l'environnement (section 4.2).

## 3. Étapes

| Étape | Contenu |
|---|---|
| 1. Source | Choix de la source, du projet cible (nouveau ou existant) et des environnements. |
| 2. Analyse | IFS lit les ressources, les rapproche entre environnements, les associe aux types du catalogue et calcule les écarts. |
| 3. Revue | Écran de revue (section 5) : répartition, noms, valeurs, liaisons, écarts. |
| 4. Proposition | IFS crée une proposition de modification avec toutes les commandes. |
| 5. Application | Relecture et application par une personne qui a `propositions.appliquer` ; la validation s'exécute. |

## 4. Analyse

### 4.1 Association au catalogue

**RG-IMP-02 — Types.** Chaque ressource ARM est associée au type du catalogue de même type ARM. Une
ressource sans type correspondant est un écart « non pris en charge » ; elle peut être reprise comme
**ressource existante** (cible de liaisons) ou ignorée.

**RG-IMP-03 — Propriétés.** Chaque propriété ARM est rapprochée d'une propriété du descripteur :
- valeur égale au défaut du descripteur : rien n'est saisi ;
- valeur différente et acceptée : elle devient la valeur de la ressource ;
- valeur refusée par le descripteur (obsolète, hors liste) : écart « valeur non acceptée », avec la valeur
  la plus proche proposée ;
- propriété ARM sans équivalent dans le descripteur : écart « propriété non reprise ». Elle ne sera pas
  gérée ; le constat le dit, rien n'est ignoré en silence.

### 4.2 Environnements, noms et surcharges

**RG-IMP-04 — Rapprochement.** Pour chaque ressource, IFS cherche dans les autres environnements la
ressource de même type dont le nom ne diffère que par les parties propres à l'environnement (code,
région, suffixes connus). L'utilisateur corrige les rapprochements à la revue.

**RG-IMP-05 — Surcharges.** Pour une ressource rapprochée, une propriété de même valeur partout devient la
valeur de la ressource ; une propriété qui diffère devient une surcharge par environnement. Une ressource
trouvée dans certains environnements seulement y est présente, absente ailleurs.

**RG-IMP-06 — Nommage.** IFS propose le gabarit de nommage qui reproduit le plus de noms existants. Pour
chaque ressource dont le nom ne suit pas le gabarit retenu, il propose un **nom forcé** par environnement
([RG-NOM-04](13-nommage.md)), pour que l'import ne renomme rien dans Azure.

### 4.3 Câblage

**RG-IMP-07 — Reprise du câblage.**
- Identités : identités affectées reprises comme ressources, attachements repris.
- Attributions de rôles : celles dont le principal et la portée sont dans l'import deviennent des liaisons
  « accès ». Les autres deviennent des écarts.
- Paramètres applicatifs : littéraux, références Key Vault (`@Microsoft.KeyVault(...)`, `secretRef`) et
  valeurs égales à une sortie connue (exemple : le nom d'hôte d'un Key Vault importé) deviennent des
  paramètres du bon type.
- Liens d'hébergement : plan d'une Web App, environnement d'une Container App, serveur d'une base.
- Valeurs secrètes rencontrées en clair dans un modèle : **jamais reprises**. Elles deviennent des secrets de
  pipeline à saisir, et un écart de gravité `Avertissement` le signale.

## 5. Écran de revue

| Zone | Contenu |
|---|---|
| Répartition | Arborescence proposée composants → groupes de ressources → ressources. Par défaut, un composant par groupe de ressources source ; l'utilisateur déplace, regroupe, renomme. |
| Ressource | Type retenu, nom logique, noms par environnement (calculés ou forcés), valeurs et surcharges, présence. |
| Liaisons | Liaisons reprises, à confirmer. |
| Écarts | Liste filtrable : non pris en charge, propriété non reprise, valeur non acceptée, rôle hors périmètre, secret en clair. Chacun a une action : accepter, corriger, ignorer (avec commentaire). |
| Résumé | Nombre de ressources, de liaisons, d'écarts par gravité ; aperçu de la validation. |

**RG-IMP-08 — Pas de recréation.** Avant de créer la proposition, IFS vérifie que les noms Azure calculés
(ou forcés) de chaque environnement sont identiques aux noms d'origine. Un nom qui changerait produit une
erreur bloquante de l'import, car le premier déploiement recréerait la ressource.

## 6. Premier déploiement après import

**RG-IMP-09 — Adoption.** La première révision après un import adopte les ressources existantes :
- Bicep : la pile de déploiement prend en charge les ressources de même identifiant ;
- Terraform : IFS génère des blocs `import` ;
- Pulumi : option `import`.

La liste de contrôle demande de vérifier l'aperçu (what-if, plan) avant la première approbation : il ne
doit montrer que des mises à jour, aucune création ni suppression de ressource importée.

**RG-IMP-10 — Une seule chaîne de gestion.** Le contrôle des noms ne suffit pas à garantir l'absence de
perte. Avant la première release, la proposition d'import liste les propriétés non reprises, que le
premier déploiement remettrait à la valeur du descripteur, et exige que l'ancienne chaîne de gestion
(pile, état, pipeline existant) soit désactivée : deux chaînes ne gèrent jamais les mêmes ressources.

## 7. Cas d'utilisation

- **UC-IMP-01** — Importer depuis une source (`composants.gerer` sur le projet cible, ou droit de créer
  un projet).
- **UC-IMP-02** — Reprendre une analyse interrompue (conservée 30 jours).
- **UC-IMP-03** — Importer par un agent IA : outils MCP `preview_import`, `create_import_proposal`.
