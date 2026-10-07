# Revue R-02 — Sortie de référence du pilote

Verdict : APPROUVÉ

- **Relu** : l'intégralité du segment `P-01` → `P-06`, commits `884b323..823a815` (31 commits, `origin/impl/socle`
  → `823a815`, 142 fichiers, 43 815 insertions / 38 suppressions) — PR [#2](https://github.com/FlorianDrevet/InfraFlowSculptor/pull/2),
  empilée sur `impl/socle` (PR #1, toujours ouverte). Relecture ciblée en détail du delta depuis le verdict `CORRECTIONS`
  du tour précédent (`7e27458..f1021f5`, 6 fichiers, 387 insertions / 44 suppressions) : `docs/plan/01-preuves.md`,
  `docs/specs/23-kit-installation.md`, `reference/pilot/bicep-azdo/.ifs/install/SETUP.md`,
  `reference/pilot/bicep-azdo/.ifs/install/scripts/IfsInstall.psm1`, `reference/pilot/manifest.example.json`,
  `reference/release-module/tests/Kit.Tests.ps1` — ligne par ligne, code puis tests. Confirmé que les deux commits
  suivants (`701bcde`, `823a815`) ne touchent que `docs/plan/revues/R-02-demande.md`, `NEXT.md`, `docs/plan/JOURNAL.md`
  et `docs/plan/recettes/suivi.md` (aucun code, aucun test). Confirmé qu'aucun autre fichier du segment n'a changé
  depuis le tour précédent : les fichiers portant les correctifs déjà vérifiés résolus (`IfsRelease.psm1`,
  `Install-Azdo.ps1`, les modules Bicep de rôle, `IfsRelease.Tests.ps1`, `InstallAzdo.Tests.ps1`) sont absents du diff
  `7e27458..823a815`. Relus aussi : `docs/plan/revues/R-02-demande.md`, `docs/plan/revues/R-02-revue.md` (verdict
  précédent), `docs/plan/revues/README.md` (modèle), `docs/plan/recettes/suivi.md`, `.github/test-debt.md`,
  `AGENTS.md`, les quatre `main.<cible>.bicepparam` de `core`/`data`/`orders`/`platform` (noms de groupes de
  ressources), et les quatre `main.bicep` (confirmation que chacun crée toujours son groupe de ressources via
  `resource rg_main 'Microsoft.Resources/resourceGroups@2024-03-01'`).
- **Vérifications relancées (par moi, ce tour)** :

  | Commande | Résultat |
  |---|---|
  | `Invoke-Pester -Path ./reference/release-module/tests -CI` | 58 tests réussis, 0 échec, 0 ignoré (4 fichiers) |
  | `Invoke-ScriptAnalyzer` sur les 5 scripts listés dans `ci.yml` (`azure-setup.ps1`, `IfsInstall.psm1`, `Install-Azdo.ps1`, `Test-ReferenceDeterminism.ps1`, `Update-ManifestExample.ps1`), règle `PSUseBOMForUnicodeEncodedFile` exclue comme en CI | 0 erreur, 0 avertissement |
  | `./reference/tools/Update-ManifestExample.ps1 -Check` | OK : manifeste cohérent avec 76 fichiers gérés |
  | `./reference/tools/Test-ReferenceDeterminism.ps1` | OK : 76 fichiers UTF-8 sans BOM, LF, saut final et en-tête requis |
  | `./reference/tools/Test-ReferencePipelines.ps1` | OK : 24/24 fichiers Azure Pipelines valides contre le schéma épinglé |
  | `./reference/tools/Test-ReferenceBicep.ps1` | OK : `core`, `data`, `orders`, `platform` compilent, lintent et formatent sans avertissement |
  | `python tools/plan/gate.py lint` | ✔ 105 étapes dont 18 verrous, cohérent |
  | `gh run view 37612167671` (commit `f1021f5`) | `success` — 7/7 jobs verts (Backend, Frontend, Acceptance, End-to-end, Release module, Supply chain, CI artifact sanitization) |
  | `gh run view 37612968488` (commit `823a815`, HEAD) | `success` — 7/7 jobs verts (mêmes jobs) |

  Toutes ces commandes, je les ai exécutées moi-même dans cette session (pas de résultat recopié d'une annonce) ; les
  chiffres correspondent exactement à ceux de `R-02-demande.md`. Contrairement au tour précédent, je disposais cette
  fois d'un accès shell : aucune limite de lecture seule à signaler.
- **Recette** : aucune recette Azure n'est prescrite à R-02 (rien n'est déployé). `docs/plan/recettes/suivi.md`
  consigne P-05 « OK, vérification locale et lecture de SETUP » daté du 2026-10-07, avec une phrase dédiée à la
  nouvelle section de `SETUP.md` (« les exigences de portée et la responsabilité de Bicep sont explicites »), donc
  bien relue après l'ajout de ces deux lignes par `f1021f5` — la recette n'est pas restée figée sur une version
  antérieure du fichier.
- **Limites** : comme au tour précédent, aucun abonnement Azure ni organisation Azure DevOps de test n'est configuré ;
  la validation reste hors ligne (RBAC et pipelines simulés ou validés statiquement). Les identifiants de rôle intégrés
  Azure utilisés par le code (`Key Vault Secrets Officer`, `Container Apps Contributor`, `AcrPush`, etc.) ne sont pas
  revérifiés contre une source live ce tour — ils l'ont été au tour précédent et le diff ne les touche pas.

## Constats précédents — vérifiés résolus

- **BLOQUANT-1, MAJEUR-1 à MAJEUR-7**, et les trois constats reclassés (terminologie SID, fédérations étrangères,
  portée RBAC) : confirmés résolus au tour précédent (voir l'historique de ce fichier) ; aucun des fichiers qui portent
  ces correctifs n'a changé depuis (`IfsRelease.psm1`, `Install-Azdo.ps1`, les modules Bicep de rôle, les tests
  associés) — rien à revérifier, rien n'a pu régresser sans diff.
- **MAJEUR-8 / R-02-C1 (le kit pré-créait les groupes de ressources des composants sans spécification ni test
  dédié)** — résolu. Vérifié précisément contre les six points demandés :
  1. *Seules les portées retournées par `Get-IfsRbacScopePlan` sont préparées.* `Invoke-IfsAzureSetup`
     (`IfsInstall.psm1:776-783`) calcule `$requiredScopes` en unissant, pour **chaque** cible, les `.Scope` renvoyés
     par `Get-IfsRbacScopePlan`, puis appelle `Initialize-IfsComponentResourceGroup -Releases $plan.Releases
     -RequiredScopes @($requiredScopes)` **avant** toute autre action. `Initialize-IfsComponentResourceGroup`
     (`IfsInstall.psm1:328-399`) construit un `HashSet` à partir de ce paramètre et ignore (`continue`, ligne 346)
     toute release dont la portée `/subscriptions/<sub>/resourceGroups/<name>` n'y figure pas. Confirmé en exécution
     réelle : le log `WHATIF` de `Invoke-Pester` ne contient que les 5 créations attendues
     (`rg-shop-core-main-{dev,prd}`, `rg-shop-orders-main-{dev,prd}`, `rg-shop-platform-main-shared`), jamais
     `rg-shop-data-main-*`.
  2. *Nom, région et tags viennent de `resourceGroups.main`.* `Get-IfsBicepResourceGroup` (`IfsInstall.psm1:267-295`)
     lit `resourceGroups.main.{name,location,tags}` depuis `bicep build-params`, et `Invoke-IfsAzureSetup`
     (`IfsInstall.psm1:761-766`) attache ces trois valeurs à chaque release (`Add-Member ResourceGroupName/
     ResourceGroupLocation/ResourceGroupTags`) avant tout calcul de portée ou d'initialisation — aucune autre
     source (pas de nom dérivé du composant, pas de valeur câblée).
  3. *Tous les groupes existants sont validés avant la première création.* Dans `Initialize-IfsComponentResourceGroup`,
     la boucle de lecture (`group list` par abonnement puis vérification région/tags via
     `Assert-IfsComponentResourceGroupTag`, lignes 377-392) s'exécute **intégralement** — pour tous les groupes
     désirés, toutes cibles confondues — avant la boucle de création (lignes 394-398) : un groupe non conforme lève
     une erreur avant qu'un seul `group create` n'ait pu partir, quel que soit l'ordre d'itération.
  4. *La seconde exécution est idempotente.* Test dédié « ne recrée pas les groupes conformes à la seconde
     initialisation » (`Kit.Tests.ps1:380-411`) : deux appels successifs à `Initialize-IfsComponentResourceGroup`
     sur le même état simulé, 2 créations au premier appel, 0 créations supplémentaires au second. Vérifié vert par
     ma propre exécution de Pester.
  5. *`data` reste créé par Bicep.* `Get-IfsRbacScopePlan` (`IfsInstall.psm1:79-100`) n'a pas de branche `'data'`
     dans son `switch` : ce composant n'ajoute donc jamais sa propre portée. Test dédié « crée seulement les groupes
     utilisés comme portées RBAC... » (`Kit.Tests.ps1:340-378`) l'assure explicitement
     (`$createdNames | Should -Not -Contain 'rg-shop-data-main-dev'`), et les quatre `main.bicep` déclarent chacun
     `resource rg_main 'Microsoft.Resources/resourceGroups@2024-03-01'` — y compris `data` — donc Bicep réconcilie
     bien ce groupe au déploiement, comme documenté dans `docs/specs/23-kit-installation.md` § 2 (ligne « 1 bis »).
  6. *Les tests `WhatIf` utilisent des noms différents.* Le test complet « ne fait aucune mutation Azure pendant le
     WhatIf du kit complet » (`Kit.Tests.ps1:525-594`) dérive désormais le nom simulé de `resourceGroups.main` à
     partir du composant et de la cible lus dans le chemin du fichier (`rg-shop-$component-main-$target`), au lieu
     du `rg-test` unique partagé par tous les composants utilisé avant la correction ; les trois nouveaux tests
     ciblés (`Kit.Tests.ps1:340-378`, `:380-411`, `:413-467`) utilisent chacun au moins deux groupes distincts
     (`core`/`dev`, `orders`/`prd`, `data`/`dev`).

  Documentation : `docs/specs/23-kit-installation.md` § 2 porte la nouvelle ligne « 1 bis. Groupes des composants
  utilisés comme portées RBAC », `docs/plan/01-preuves.md` (P-05 🔧 point 1 et 🔧 point 4) et
  `.ifs/install/SETUP.md` décrivent la même règle en langage client — plus de décision de conception non
  documentée. Le troisième scénario de test (`Kit.Tests.ps1:413-467`) couvre les 9 combinaisons région/4 tags ×
  {absent, incorrect} et vérifie `0` appel `group create` après le `Should -Throw`, pour chaque cas. `data` dans
  `manifest.example.json` et les hachages régénérés sont cohérents (`Update-ManifestExample.ps1 -Check` vert).

## Constats

Aucun constat bloquant, majeur ou mineur nouveau ce tour.

## Décisions prises pendant la revue

Aucune nouvelle `DT-nn` : la correction R-02-C1 était explicitement scopée par la revue précédente à une
documentation en spec/plan (pas à une décision technique transverse), et c'est ce qui a été livré. Aucun statut de
preuve de [04 § 7.2](../specs/04-perimetre-et-lots.md) à mettre à jour à ce verrou (ce sera R-03, sur les résultats
réels des preuves P1–P9).

## Pour la suite

- Vous pouvez fusionner la PR #2 (`impl/preuves` → `impl/socle`) dès que la PR #1 (base) est elle-même fusionnée, ou
  la laisser empilée en l'état : le plan prévoit que la pull request reste ouverte et que Luna continue sur la même
  branche.
- Luna reprend avec `P-07` — Outillage des preuves et recette pas à pas — sur `impl/preuves`.
- Rien à reporter sur `P-07` : aucun mineur n'a été identifié ce tour, `gate.py lint` n'a donc pas eu besoin d'être
  relancé après une modification du plan.
