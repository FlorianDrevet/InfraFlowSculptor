# 21 — Génération et révisions

## 1. Objectif

Transformer le modèle validé en fichiers déployables (code d'infrastructure, pipelines, kit
d'installation), dans le **langage d'infrastructure** et pour la **plateforme CI** du projet, sous la forme
d'une **révision** immuable, traçable et comparable ([DEC-20](03-decisions.md), [DEC-43](03-decisions.md),
[DEC-47](03-decisions.md)).

## 2. Révision

| Champ | Contenu |
|---|---|
| Numéro | Entier croissant par projet (1, 2, 3…). |
| Auteur, date | Utilisateur ou jeton/agent, horodatage UTC. |
| Version du modèle | Empreinte du modèle au moment de la génération. |
| Version du catalogue | Version des descripteurs, des modules, des fournisseurs et des outils utilisés. |
| Langage, plateforme | Langage d'infrastructure et plateforme CI du projet à ce moment. |
| Plan de déploiement | Représentation neutre calculée à l'étage 1 (section 3). |
| Constats | Avertissements et infos présents (aucune erreur, par construction). |
| Fichiers | Arborescence complète (section 5). |
| Résumé des changements | Différence de **plan de déploiement** avec la révision précédente : ressources ajoutées, modifiées (propriétés, environnements), retirées, noms Azure modifiés, rôles ajoutés ou retirés ; puis fichiers modifiés. |
| Publications | Destinations où la révision a été publiée, avec résultat ([24](24-depots-et-publication.md)). |

**UC-GEN-01 — Générer une révision** (`generer`, ou jeton de portée `generate`). Toujours sur le projet
entier. Refusé s'il y a une erreur de validation. Si le modèle, le catalogue, le langage et la plateforme
n'ont pas changé depuis la dernière révision, aucune révision n'est créée et la dernière est renvoyée.

**RG-GEN-01 — Immuable.** Une révision ne change jamais. Corriger = générer une nouvelle révision.

**RG-GEN-02 — Périmée.** Une révision est « périmée » dès que le modèle, le catalogue, le langage ou la
plateforme a changé depuis sa génération. L'écran le signale et propose de générer.

**RG-GEN-03 — Déterminisme.** Une même entrée produit des fichiers identiques octet pour octet : ordre
stable, fins de ligne `LF`, encodage UTF-8 sans BOM.

**RG-GEN-04 — Contrôle de la sortie.** Avant d'enregistrer une révision, IFS exécute les contrôles du
langage (section 6) et vérifie la syntaxe des pipelines. Un point d'extension du client, qu'IFS ne voit
pas, est remplacé pendant le contrôle par un bouchon vide qui respecte son contrat (RG-GEN-16). Un échec
est un défaut d'IFS : la révision est marquée « en échec », n'est pas publiable, et l'incident est remonté
à l'équipe IFS.

**RG-GEN-05 — Conservation.** Les métadonnées de toutes les révisions sont conservées tant que le projet
existe. Les fichiers sont conservés pour toute révision publiée, et 90 jours pour les autres.

**UC-GEN-02 — Consulter une révision** : plan de déploiement, arborescence, visualiseur avec coloration
(Bicep, HCL, TypeScript, YAML), téléchargement d'un fichier ou de l'archive complète.

**UC-GEN-03 — Comparer deux révisions** : résumé des changements et diff fichier par fichier.

## 3. Deux étages ([DEC-44](03-decisions.md))

### 3.1 Étage 1 — Plan de déploiement

Pour chaque composant et chaque cible, le plan de déploiement contient :

| Élément | Contenu |
|---|---|
| Cible | Code, abonnement, région, connexion de déploiement, protection. |
| Groupes de ressources | Nom Azure, région, tags effectifs. |
| Ressources | Type ARM, nom Azure, groupe, région, présence, valeurs effectives de toutes les propriétés (exprimées dans le vocabulaire Azure du descripteur), valeurs fixes, enfants, identités, exposition. |
| Références externes | Ressources d'autres composants (nom, groupe, abonnement) et ressources existantes (identifiant). |
| Attributions de rôles | Principal, rôle, portée, origines. |
| Paramètres applicatifs | Destination, forme Azure ([17 § 5](17-parametres-applicatifs-et-secrets.md)). |
| Secrets attendus | Secrets de pipeline à écrire dans Key Vault. |
| Scripts post-déploiement | Accès aux données. |
| Dépendances | Composants à déployer avant celui-ci. |
| Ressources retirées | À titre d'information : ressources présentes dans la dernière révision publiée et absentes de celle-ci, avec leur traitement (détacher, supprimer, révoquer). La référence qui fait foi au déploiement est l'unité de déploiement elle-même (RG-GEN-19). |
| Pipelines | Étapes logiques ([22 § 2](22-pipelines.md)). |

**RG-GEN-06 — Seul lieu des décisions.** Toute valeur, tout nom, tout rôle, tout ordre est décidé à
l'étage 1. Le plan de déploiement est exposé par l'API et le MCP.

### 3.2 Étage 2 — Émetteurs

**RG-GEN-07 — Traduction pure.** Un émetteur traduit le plan de déploiement dans un langage ou pour une
plateforme. Il ne lit pas le modèle, ne calcule ni nom ni valeur, et ne filtre rien. Ce qu'il ne sait pas
traduire est connu d'avance : le descripteur déclare la prise en charge par langage, et la validation
refuse ce qui n'est pas pris en charge (`VAL-GEN-LANGAGE`).

| Émetteur | Lot |
|---|---|
| Bicep | 1 |
| Terraform | 3 |
| OpenTofu | 4 |
| Pulumi (TypeScript, C#, Python, Go, Java, YAML) | 4 |
| Azure DevOps Pipelines | 1 |
| GitHub Actions | 2 |
| GitLab CI | 3 |

Lots selon [DEC-62](03-decisions.md) et [04](04-perimetre-et-lots.md).

## 4. Ce qui est généré

| Partie | Contenu |
|---|---|
| Infrastructure d'un composant | Code d'infrastructure dans le langage du projet, un fichier de valeurs par cible, scripts post-déploiement, pipelines d'infrastructure. |
| Applications d'un composant | Pipelines applicatifs par application ([22](22-pipelines.md)). |
| Commun | Modèles de pipeline partagés, kit d'installation ([23](23-kit-installation.md)), manifeste, `README.ifs.md`. |

## 5. Arborescence logique

La génération produit une arborescence **unique**, indépendante des dépôts. Le plan de publication la
répartit dans les destinations ([DEC-21](03-decisions.md), [24](24-depots-et-publication.md)).

```
<composant>/
  infra/
    …                                (fichiers du langage, section 6)
    scripts/                         (scripts post-déploiement : accès aux données)
    pipelines/                       (Azure DevOps uniquement)
  apps/
    <application>/
      pipelines/                     (Azure DevOps uniquement)
.ifs/
  templates/                         (Azure DevOps : modèles YAML partagés)
  actions/                           (GitHub Actions : actions composites partagées)
  install/                           (kit d'installation, voir 23)
  manifest.json                      (écrit par la publication, par destination)
.github/workflows/                   (GitHub Actions uniquement, toujours à la racine du dépôt, voir RG-PUB-17)
README.ifs.md                        (architecture, diagramme, noms par environnement, déploiement, retour arrière, points d'extension ; DEC-75)
```

**RG-GEN-08 — En-tête.** Chaque fichier généré commence par un commentaire : « Généré par
InfraFlowSculptor — projet <code>. Ne pas modifier : les modifications seront signalées puis remplacées
à la prochaine publication. Personnalisation : voir README.ifs.md. » (Les formats sans commentaire, comme
JSON, en sont dispensés et sont listés au manifeste.) L'en-tête ne contient **pas** le numéro de révision :
un fichier dont le contenu ne change pas reste identique, ne fait pas de bruit dans la pull request et ne
déclenche aucun pipeline ([DEC-94](03-decisions.md)). La révision et les empreintes sont dans le
manifeste.

## 6. Conventions du code d'infrastructure

### 6.1 Règles communes à tous les langages

**RG-GEN-09 — Rien de propre à une cible dans le code.** Toute valeur qui dépend de la cible est dans le
**fichier de valeurs** de la cible :
- noms Azure ([DEC-06](03-decisions.md)) ;
- valeurs effectives de toutes les propriétés ([RG-RES-02](14-modele-des-ressources.md)) ;
- présence de chaque ressource ;
- tags effectifs, région, abonnement, identifiants des ressources existantes.

**RG-GEN-10 — Entrées typées et documentées.** Les entrées du code sont typées (types utilisateur Bicep,
objets typés Terraform avec validations, interfaces TypeScript) et chaque entrée porte une description.

**RG-GEN-11 — Références externes.** Une cible d'un autre composant est déclarée par son nom Azure
calculé, son groupe et son abonnement ; une ressource existante par son identifiant. Rien n'est lu dans
l'état d'un autre composant.

**RG-GEN-12 — Aucun secret.** Aucune valeur secrète n'entre dans le code ni dans les fichiers de valeurs.
Les secrets de pipeline sont écrits dans Key Vault par une étape de pipeline
([RG-PAR-15](17-parametres-applicatifs-et-secrets.md)).

**RG-GEN-13 — Lisibilité.** Identifiants dérivés du type et du nom logique (`caApi`, `ca_api`), sections
commentées par groupe de ressources, aucune sortie inutilisée, formatage officiel du langage.

**RG-GEN-14 — Contrôles propres.** La sortie passe sans erreur **ni avertissement** les contrôles de son
langage. Un avertissement est un défaut d'IFS ([RG-GEN-04](#2-révision)).

### 6.2 Bicep (lot 1)

| Fichier | Rôle |
|---|---|
| `main.bicep` | Portée abonnement ; crée les groupes de ressources, puis un module par ressource à la portée de son groupe. |
| `main.<cible>.bicepparam` | Fichier de valeurs de la cible. |
| `types.bicep` | Types utilisateur des paramètres. |
| `modules/` | Modules locaux, seulement si aucun AVM ne couvre le type. |

- Modules : AVM du registre public, version épinglée (`br/public:avm/res/<…>:<version>`).
- Présence : paramètre booléen par ressource et déploiement conditionnel.
- Références externes : `existing`.
- Contrôles : `bicep build`, `bicep build-params`, linter avec sa configuration par défaut, `bicep format`
  sans différence.

### 6.3 Terraform (lot 3) et OpenTofu (lot 4)

| Fichier | Rôle |
|---|---|
| `versions.tf` | Version minimale de Terraform (au moins celle qui supporte les blocs `removed`), fournisseurs `azurerm` et `azapi` épinglés. |
| `providers.tf` | Fournisseur principal sur l'abonnement de la cible ; fournisseurs avec alias pour les abonnements externes. Authentification OIDC fournie par le pipeline. |
| `main.tf` | Groupes de ressources, puis un module AVM (ou une ressource) par ressource. |
| `variables.tf` | Variables typées, avec descriptions et validations. |
| `outputs.tf` | Sorties consommées uniquement. |
| `removed.tf` | Blocs `removed` des ressources détachées ([DEC-46](03-decisions.md)). |
| `imports.tf` | Blocs `import` d'une migration assistée ([DEC-48](03-decisions.md)). |
| `targets/<cible>.tfvars` | Fichier de valeurs de la cible. |
| `targets/<cible>.backend.hcl` | Configuration de l'état : compte de stockage, conteneur, clé `<projet>/<composant>/<cible>.tfstate`, authentification Entra. |

- Présence : `count` (ou `for_each`) piloté par la valeur de présence.
- Références externes : sources de données (`data`).
- Contrôles : `terraform fmt -check`, `terraform init -backend=false`, `terraform validate`, `tflint` avec
  l'ensemble de règles Azure.
- OpenTofu ([DEC-63](03-decisions.md)) : même structure ; registre `registry.opentofu.org`, version minimale
  d'OpenTofu, chiffrement de l'état par une clé Key Vault (bloc `encryption`), contrôles `tofu fmt`,
  `tofu validate`.

### 6.4 Pulumi (lot 4)

| Fichier | Rôle |
|---|---|
| `Pulumi.yaml` | Projet Pulumi du composant, environnement d'exécution `nodejs`. |
| `Pulumi.<cible>.yaml` | Configuration de la pile de la cible : fichier de valeurs (noms en clair, valeurs effectives) et fournisseur de chiffrement des secrets (`azurekeyvault://…`). |
| `index.ts` | Groupes de ressources, puis les ressources Azure Native. |
| `config.ts` | Interfaces typées et lecture de la configuration. |
| `package.json` | Dépendances épinglées en version exacte (`@pulumi/pulumi`, `@pulumi/azure-native`). |
| `tsconfig.json` | Configuration TypeScript stricte. |

- Présence : création conditionnelle selon la configuration.
- Références externes : fonctions `get`.
- Détacher : option `retainOnDelete` sur toutes les ressources si la règle du composant est « détacher ».
- Contrôles : `tsc --noEmit` en mode strict, `prettier --check`.

Le tableau ci-dessus décrit TypeScript. Les autres langages Pulumi ([DEC-63](03-decisions.md)) suivent la
même découpe (projet, configuration par cible, programme, configuration typée, dépendances épinglées) :

| Langage | Programme | Dépendances | Contrôles |
|---|---|---|---|
| C# | `Program.cs`, `Config.cs` | `.csproj` (`Pulumi`, `Pulumi.AzureNative`) | `dotnet build` avec avertissements traités comme erreurs, `dotnet format --verify-no-changes` |
| Python | `__main__.py`, `config.py` | `requirements.txt` épinglé | `mypy --strict`, `ruff check` |
| Go | `main.go`, `config.go` | `go.mod` | `go vet`, `gofmt -l` |
| Java | `App.java`, `Config.java` | `pom.xml` | `mvn -B compile` |
| YAML | `Pulumi.yaml` (ressources déclarées) | — | `pulumi preview` sur une pile de test |

### 6.5 Source des modules ([DEC-54](03-decisions.md))

| Source | Ce que contient la destination | Lot |
|---|---|---|
| AVM registre public | Références aux modules vérifiés épinglés (`br/public:…`, `registry.terraform.io/Azure/avm-res-…`) | 1 (Bicep), 2 (Terraform) |
| AVM embarqués | Copie des modules vérifiés épinglés sous `modules/avm/`, référencés par chemin relatif ; fichiers gérés comme les autres | 2 |
| Modules IFS | Un module compact par type utilisé, sous `modules/ifs/`, qui expose exactement les propriétés du descripteur ; fichiers gérés | 2 |
| Modules du client | Références aux modules du client (registre privé ou dépôt git), selon un **contrat de correspondance** déclaré par type : entrée du module pour chaque propriété du descripteur, sorties fournies, version | 3 |

**RG-GEN-21 — Surcharge par type.** La source s'applique à tout le projet, avec surcharge possible par type
(exemple : AVM partout, sauf Key Vault en module du client).

**RG-GEN-22 — Contrat des modules du client** *(lot 3)*. IFS ne voit pas le code des modules du client. Le
contrat déclaré est vérifié par le pipeline de PR (compilation, `validate`). Une propriété du descripteur
sans entrée correspondante dans le contrat est une erreur `VAL-GEN-CONTRAT` : rien n'est ignoré en silence
([P3](01-principes.md)).

**RG-GEN-23 — Changement de source.** Après publication, changer la source ne recrée aucune ressource :
- Terraform : IFS génère les blocs `moved` d'une adresse de module à l'autre ;
- Bicep : les ressources gardent leur identifiant Azure et la pile les reprend.

**RG-GEN-24 — Accès au registre.** Avec la source « AVM registre public », les exécuteurs doivent joindre
le registre public (`mcr.microsoft.com` pour Bicep, `registry.terraform.io` pour Terraform). Si les
exécuteurs n'ont pas d'accès Internet, choisir « AVM embarqués » ou « Modules IFS ».

## 7. Code d'infrastructure additionnel (point d'extension)

**RG-GEN-15 — Principe.** Un composant peut déclarer un code d'infrastructure écrit par le client, **dans
le langage du projet**, situé hors des fichiers gérés, dans la même destination que son infrastructure.

| Langage | Forme attendue | Appel |
|---|---|---|
| Bicep | Fichier `.bicep` de portée abonnement | Module appelé par `main.bicep` |
| Terraform | Dossier de module | Bloc `module` dans `main.tf` |
| Pulumi | Fichier TypeScript qui exporte une fonction `extend(context)` | Appelée en fin de `index.ts` |

**RG-GEN-16 — Contrat.** Le code additionnel est appelé après toutes les ressources du composant et
reçoit :
- `target` : code, abonnement, région ;
- `resourceGroups` : nom Azure de chaque groupe, par nom logique ;
- `resources` : nom Azure et identifiant de chaque ressource présente, par nom logique ;
- `tags` : tags effectifs du composant.

Le contrat est versionné et documenté dans `README.ifs.md`, identique dans tous les langages.

**RG-GEN-17 — Vérification.** À la publication, IFS vérifie que le code additionnel existe dans la branche
cible ; sinon la publication est refusée pour cette destination, avec un modèle vide proposé.

## 8. Cycle de vie des ressources ([DEC-46](03-decisions.md))

**RG-GEN-18 — Unité de déploiement.** Chaque composant est déployé dans chaque cible comme une unité
indépendante, nommée `ifs-<projet>-<composant>-<cible>` : pile de déploiement (Bicep), état
(Terraform), pile Pulumi.

**RG-GEN-19 — Ressources retirées** ([DEC-85](03-decisions.md), [DEC-91](03-decisions.md)).
- Elles sont calculées **au moment de l'application**, contre ce que l'unité de déploiement gère
  réellement dans la cible (pile Bicep, état Terraform, pile Pulumi). Une cible qui a sauté des révisions
  retire donc tout ce qui doit l'être.
- Les ressources à données ou à état sont détachées ou supprimées selon la règle du composant ; une cible
  protégée détache toujours.
- Les objets d'autorisation et de configuration sont toujours supprimés : un accès retiré du modèle est
  révoqué dans Azure.
- Terraform : un bloc `removed` reste généré tant qu'au moins une cible du composant n'a pas rapporté le
  déploiement réussi d'une révision qui le contenait.

**RG-GEN-25 — Famille de cycle de vie.** Le descripteur classe chaque ressource générée : ressource à
données ou à état, ou objet d'autorisation et de configuration (attribution de rôle, politique d'accès,
utilisateur de base créé par IFS, clé App Configuration, paramètre de diagnostic, règle de pare-feu,
identifiant fédéré).

**RG-GEN-20 — Pas de blocage.** IFS ne pose aucun refus de modification dans Azure (piles Bicep sans
`denySettings`). Les verrous `CanNotDelete` des cibles protégées viennent de l'option du composant.
