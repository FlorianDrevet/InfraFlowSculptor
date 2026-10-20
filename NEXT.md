# NEXT — où en est l'implémentation

> **À lire en premier en arrivant, à mettre à jour en dernier en partant**, même en pleine étape.
> Ce fichier dit **où l'on est** et **ce que git ne sait pas** (Azure, Entra, Azure DevOps, tests manuels passés).
> Les étapes sont dans [`docs/plan/`](docs/plan/README.md) ; l'historique dans [`docs/plan/JOURNAL.md`](docs/plan/JOURNAL.md).
> Les lignes du tableau ci-dessous sont lues et écrites par `python tools/plan/gate.py` : ne pas renommer leurs libellés.

## En un coup d'œil

| | |
|---|---|
| **Étape courante** | [`P-08`](docs/plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement) — Exécution des preuves (accompagnement) |
| **Statut** | `BLOQUE` |
| **Dernière étape terminée** | [`P-07`](docs/plan/01-preuves.md#p-07--outillage-des-preuves-et-recette-pas-à-pas) — Outillage des preuves et recette pas à pas (commit `1d6f1be`) |
| **Étape suivante** | `R-03` — Revue des preuves |
| **Verrou** | D2b.2 — échec du runner à l’étape 4 dû à la conversion locale de `generatedAtUtc` ; diagnostic Sol consigné, nettoyage puis reprise formelle requis |
| **Branche** | `impl/preuves` (empilée sur `impl/socle` jusqu’à sa fusion) |
| **Dernière mise à jour** | 2026-10-20 — Luna |
Statuts : `A_FAIRE` · `EN_COURS` · `EN_ATTENTE_DE_REVUE` (🔒 Luna s'arrête) · `EN_ATTENTE_DE_RECETTE` (Luna attend vos
résultats) · `CORRECTIONS_DEMANDEES` · `BLOQUE` (condition bloquante en attente de résolution).

## Décisions à confirmer avant leur étape

Toutes confirmées le 2026-10-04. Règle pour les suivantes : Luna ne commence pas l'étape tant que la colonne « Confirmée » ne vaut pas **Oui** (sinon : statut `BLOQUE`).

| Décision | Choix proposé | Avant l'étape | Confirmée |
|---|---|---|---|
| [DT-22](docs/technique/01-decisions.md#dt-22--mediator-à-la-place-de-mediatr) | `Mediator` (MIT, génération de source) à la place de MediatR | S-03 | Oui (2026-10-04) |
| [DT-21](docs/technique/01-decisions.md#dt-21--outillage-de-test) | AwesomeAssertions au lieu de FluentAssertions 8 (licence) | S-04 | Oui (2026-10-04) |
| [DT-04](docs/technique/01-decisions.md#dt-04--postgresql-17) | PostgreSQL 17 au lieu de SQL Server | S-06 | Oui (2026-10-04) |
| [DT-07](docs/technique/01-decisions.md#dt-07--authentification--oidc-entra-en-azure-keycloak-en-local) | OIDC générique : Entra en Azure, Keycloak en local ([guide](docs/technique/09-keycloak.md)) | S-07 | Oui (2026-10-04) |
| [DT-08](docs/technique/01-decisions.md#dt-08--worker--service-net-et-service-bus-pas-azure-functions) | Worker .NET + Service Bus à sessions, pas Azure Functions | S-08 | Oui (2026-10-04) |
| [DT-30](docs/technique/01-decisions.md#dt-30--thème-sombre-seul-changement-de-thème-prêt) | Thème sombre seul ; sélecteur de thème prêt et caché tant qu'il n'y a qu'un thème | S-10 | Oui (2026-10-04) |

## Machine

| Outil | Version attendue | Posé ? |
|---|---|---|
| SDK .NET | `10.0.203` | Oui (`10.0.301`) |
| Node | `24.15.0` | Oui (`24.19.0`) |
| Docker | `27.0.0` | Oui (`29.5.3`, daemon répond) |
| CLI Aspire | `13.5.3` | Oui (`13.6.0`) |
| Python | `3.11` | Oui (`3.14.2`) |
| PowerShell 7 | `7.5.0` | Oui (`7.6.6`) |
| Azure CLI | `2.90.0` | Oui (`2.90.0`, vérifié par `pwsh tools/dev/check-prereqs.ps1` le 2026-10-06) |
| Bicep CLI | `0.47.16` | Oui (`0.47.16`) |
| Git / GitHub CLI | présence | Oui (`2.52.0` / `2.88.1`) |

## État hors dépôt 📌

| Élément | État | Depuis |
|---|---|---|
| Abonnements Azure de test (preuves) | Souscription `fc4fa56f-e4e7-41e9-8a0d-559fcd27b827`, tenant `c833b396-517c-465d-bcce-b79702eaf932` ; `francecentral` et les fournisseurs/SKU requis disponibles ; utilisateur connecté Owner au scope souscription et User Access Administrator au scope tenant ; même souscription pour dev/prd/shared | 2026-10-15 |
| Décision de coût et estimation des preuves ([contrôle](docs/plan/recettes/01-preuves.md#01-surveillance-des-coûts-avant-toute-action-sur-les-ressources-azure)) | Florian ne fixe **aucun plafond personnel** et autorise la poursuite sous contrôle des coûts (2026-10-17). Au contrôle Cost Management du 2026-10-17T10:36Z : 84,648182 EUR réels MTD ; prévision REST de 87,427283 EUR du 18 au 31/10, soit 172,075465 EUR projetés. L’API du budget préexistant de 200 EUR/mois indique toutefois 235,765571 EUR prévus et 0,430301 EUR dépensés ; cette divergence est conservée. Ce budget est une alerte, pas un coupe-circuit. Relever le coût réel, la prévision et l’estimation France Central à nouveau avant toute action Azure significative | 2026-10-17 |
| Ressources Azure créées pour les preuves ([inventaire](docs/plan/preuves/inventaires-azure.md)) | Shop42 en France Central. Après #748 : 163 ressources, 26 groupes, 7 piles, 57 rôles et 7 budgets ; aucun ID ajouté ou retiré par rapport au préflight juste avant le run. D2 reste connue saine après #740 ; #748 n’a pas atteint PRD. P8 est KO et le nettoyage suspendu. IDs exacts hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\after-infra-748-20261017T103510Z.json` | 2026-10-17 |
| Organisation / projet Azure DevOps de test | Organisation `floriandrevet0332`, projet privé `shop42` et dépôt `shop42`. PR #74 fusionnée ; CI infra orders #746 réussi. La release infra #748 est arrêtée en DEV avec `FailedBeforeMutation` après divergence entre aperçu et recalcul sous verrou ; PRD ignorée. Aucune release applicative n’est intervenue entre les deux aperçus. Le cas négatif #738 reste conservé ; décisions Entra et groupes restent en place. Garde `shop42-dev` relue : *Pre-check approvals* (`Shop42 Release Approvers`, auto-approbation désactivée) avant l'Exclusive Lock. Le 2026-10-20, les dix définitions sont en révision 9 et `queueStatus=enabled` ; aucune exécution active/en file et aucune PR pour `fix/p08-keep-recalculated-preview`. L'override UI désactive CI, dont le CI `orders/infra` requis pour D2b ; le protocole prévoit maintenant un N manuel après fusion. Le clone original `C:/Users/flori/RiderProjects/shop42` et sa branche distante restent propres à `2875cf3`, basé sur `origin/main` `3f59175`. Sol a arbitré un rejeu dans le clone indépendant `C:/Users/flori/RiderProjects/shop42-d2b-proof`, à créer depuis ce `origin/main`, puis une réconciliation par merge sans réécrire la branche. L'ancienne actualisation de `.ifs/manifest.json` a été restaurée ; son diff est archivé dans `%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-prior-attempt-manifest-diff.patch`. Le transcript complet de 09:35 s'arrête sur `Prepared branch manifest metadata differs from rehearsal` ; le résumé de 09:41 ne remplace pas les deux sorties complètes exigées. Les deux chemins temporaires `ifs-shop42-rehearsal` et `ifs-shop42-fresh` ont été supprimés et vérifiés absents. Aucune écriture Azure DevOps par Luna | 2026-10-20 |
| Droits Azure utilisés pour D1b | `Storage Blob Data Reader` au conteneur `ifs-operations` du compte `stifsshop42dev` et `Key Vault Reader` au coffre `kv-shop42-main-dev`, attribués à l’identité personnelle pour les lectures D1b. Les deux attributions restent en place ; aucune valeur de secret n’a été lue | 2026-10-17 |
| Groupes Entra (`sg-shop42-sql-admins`) et Azure DevOps (« Shop42 Release Approvers ») | Créés dans le tenant fourni ; Florian DREVET est membre des deux groupes | 2026-10-15 |
| Inscriptions Entra d'IFS (`dev`) | — | — |
| Protection de la branche `main` sur GitHub | À activer par vous dans *Settings → Branches* | 2026-10-06 |
| Secret de dépôt GitHub Actions `IFS_E2E_PASSWORD` | Configuré (valeur masquée) | 2026-10-06 |

## Tests manuels en attente de vous

- S-02 — [ouvrir `src/backend/InfraFlowSculptor.slnx` dans Rider](docs/plan/00-socle.md#s-02--squelette-backend-généré-depuis-le-template-cqrs) : vérifier les cinq projets et l'absence de références à `Web.Template.CQRS`.
- S-03 — ouvrir `http://localhost:5257/v1/inexistant` dans un navigateur et vérifier le JSON 404 avec `traceId` ([étape](docs/plan/00-socle.md#s-03--moderniser-le-squelette-évolutions-de-vole-papillon-damour)); l'API a été vérifiée par HTTP, mais Chrome a bloqué la navigation directe.
- S-04 — ouvrir la solution dans Rider, vérifier les cinq projets de test puis lancer *Run All* ([étape](docs/plan/00-socle.md#s-04--projets-de-tests-et-règles-darchitecture)); `dotnet test` passe, mais aucune fenêtre native n'est exposée à l'automatisation.
- S-17 — sur un clone neuf, suivre [README — Démarrer](README.md#démarrer-windows) jusqu'à « Bonjour Alice Martin ». Les prérequis passent ; le démarrage du clone reste à vérifier quand les ports fixes 4200, 8080, 5257 et 3000 seront libres, car l'AppHost de travail reste actif.
- P-08 — P1, P9, P3a et P3b sont OK. P8 reste KO : #738 est la preuve négative conservée ; #740 est incomplet ; #748 a échoué en `FailedBeforeMutation` et reste non diagnostiqué. D1a, D1b et D2a sont terminés. Le détail de #748 est dans [`diagnostic-p8-748.md`](docs/plan/preuves/diagnostic-p8-748.md) ; les captures D1b restent hors dépôt sous `%LOCALAPPDATA%\InfraFlowSculptor\P08\D1b\20261017T215336Z\`. D2b attend les préalables externes listés ci-dessous. Pour D3, C1–C7 et C9 précèdent le lancement ; C8 se vérifie après `Preview_dev` et avant approbation. Le nettoyage Shop42 reste suspendu.
Chaque étape terminée ajoute ici son 🧪 ; vous consignez le résultat dans
[`docs/plan/recettes/suivi.md`](docs/plan/recettes/suivi.md) puis retirez la ligne.

## Questions pour Claude

**Décision active — P-08 / D2b :** Florian a délégué à GPT Sol une autorité d’arbitrage équivalente à Claude. Sol confirme que le résumé
PASS ne remplace pas les sorties complètes : le transcript de 09:35 s’arrête sur `Prepared branch manifest metadata differs from
rehearsal` et `d2b-local-verification.txt` n’est qu’un résumé après rafraîchissement. L’échec initial reste conservé comme preuve.
Son arbitrage du 2026-10-20 autorise un amendement ciblé de D2b.1/.2/.4 : rejouer dans le clone indépendant
`C:/Users/flori/RiderProjects/shop42-d2b-proof`, créé sur `origin/main` `3f59175`, avec la répétition sous `$TEMP/ifs-shop42-rehearsal` ;
préserver le clone original et sa branche `2875cf3`, puis réconcilier le résultat testé par merge et identité de tout l’arbre, sans
réécrire l’historique distant. L’IFS est fixé à `893f2a3dda4d7c8461be9c93f208a9e84d317962` pendant les deux passes. Les assertions
D2b.2 restent intactes et toute nouvelle exception impose `BLOQUE`. Aucun commit, merge ou push nouveau avant la validation de toutes
les sous-étapes D2b.3.1 à 3.6, y compris la confirmation par Florian des huit suspensions et des files vides. La décision précédente
sur le CI N manuel après fusion et l’observation du R0 reste en vigueur.

**Nouvelle exception et arbitrage Sol — 2026-10-20 :** la répétition a interrompu le runner avant publication sur `git rev-parse
origin/main` dans le clone temporaire. La sortie complète a été renommée sans modification et archivée sous
`%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-rehearsal-precondition-failure-20261020T130743Z-transcript.txt` (SHA-256
`91BD51AF4D30FA9F5C152D7FEC6DB598A0852F8FBEB58050146BBC812F6515FA`). La répétition a bien `HEAD=3f5917588…`, la branche attendue
et un arbre propre ; seul `origin/main` manque, ce qui est normal dans ce clone temporaire. Sol confirme que l'exception impose
`BLOQUE`, mais autorise un rejeu complet après suppression de cette exigence superflue pour le clone temporaire. La vérification
`origin/main` du clone de preuve reste obligatoire. Pour reprendre, après le commit de blocage, Luna transcrit cette décision du
propriétaire en passant explicitement le statut `BLOQUE` à `A_FAIRE` ; `gate.py` n'a pas de commande pour cette transition.

**Nouvelle exception du 2026-10-20T13:14:20Z :** après correction de la précondition précédente, le rejeu s'est arrêté à l'étape 0
par `CommandNotFoundException` : `Assert-ProofManifestWorkingTree` n'était pas disponible, car le runner n'avait pas importé
`tools/proofs/ProofTools.psm1`. Aucune publication neuve ni modification du clone de preuve n'a eu lieu. Transcript complet archivé
sous `%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-rehearsal-attempt2-proofmanifest-module-not-imported-20261020T131420Z-transcript.txt`,
SHA-256 `C0E40FB93D3A4B05D24EA845FBC492DBC6517EAEC0F0650A13B930C34F99D955`. Sol confirme que c'est une omission du runner et
autorise à importer explicitement `tools/proofs/ProofTools.psm1` avant l'étape 0, conformément au bloc 0 ter.2. Sous la délégation de
Florian, P-08 reprend en `A_FAIRE` après le commit de cette décision et `gate.py check` ; nettoyer d'abord la copie temporaire restante,
puis refaire les deux passes intégralement avec de nouveaux transcripts. Toute nouvelle exception rebloque immédiatement P-08.

**Nouvelle exception du 2026-10-20T13:16:17Z :** la répétition a passé les étapes 0 à 3 puis échoué à l'étape 4 sur l'assertion
« Le manifeste final diffère du manifeste initial hors module, commit et generatedAtUtc. » Le transcript complet est archivé sous
`%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-rehearsal-attempt3-final-manifest-mismatch-20261020T131617Z-transcript.txt`,
SHA-256 `791FE392BAEFBA26543C6B316AF691A3D1508C9DF545011CA254197030A1966B`. Aucun changement n'a atteint le clone de preuve ou le
clone original ; les deux répertoires temporaires contiennent des artefacts partiels et sont laissés intacts. Sol a diagnostiqué en
lecture seule une erreur du runner : `ConvertFrom-Json` conserve `generatedAtUtc` comme `DateTime`, puis le cast `[string]` le convertit
au format local `10/20/2026 13:16:22`, alors que le manifeste réel conserve `2026-10-20T13:16:22Z`. C'est le premier écart des JSON
canoniques (index 12059 ; longueurs 12540/12541). Sol confirme en lecture seule que les 99 chemins/empreintes sauf le module, les
quatre unités et le module copié correspondent à la publication. Il autorise uniquement le remplacement de
`$expectedManifest.generatedAtUtc = [string]$after.generatedAtUtc` par `$expectedManifest.generatedAtUtc = $after.generatedAtUtc`.
La tentative reste échouée et P-08 `BLOQUE` ; nettoyer les deux chemins par `Remove-TempCopy`, puis rétablir formellement `A_FAIRE`,
passer `gate.py check` et recommencer les deux passes complètes avec de nouveaux transcripts.

Nouveau blocage d'authentification — l'essai de clone Git HTTPS avec le PAT fourni a été refusé par Azure Repos. Le clone original
est resté propre ; son `origin/main` local vaut `3f5917588b83d11fc07d7f9838f5e895e037dd6d`, SHA également visible sur la page
Azure DevOps `shop42` dans le navigateur. Sur décision de GPT Sol du 2026-10-20, D2b.2 peut utiliser un clone indépendant
`--no-hardlinks` depuis le clone original, récupérer localement cette ref, conserver l'URL ADO en `origin` et la recouper avec la
lecture navigateur ; ce repli ne sera pas présenté comme un `fetch origin` réseau. La preuve est conservée dans
`%LOCALAPPDATA%\InfraFlowSculptor\P08\D2b\d2b-git-auth-fallback-evidence.txt`. Un accès Git valide reste obligatoire avant D2b.4 ;
aucun PAT neuf ne sera créé sans consentement spécifique.

L’interface Azure DevOps a été vérifiée en lecture seule le 2026-10-20 pour les dix définitions.
Elles sont toutes en révision 9 avec `queueStatus=enabled`. Pour les dix définitions, l’option UI « Override the YAML
continuous integration trigger from here » est cochée et « Disable continuous integration » est sélectionné ; aucun déclencheur
planifié ni de fin de build n’est configuré. En particulier, CI `orders/infra` (ID 80) a aussi `trigger: main` dans son YAML, mais
l’override UI désactive ce déclencheur : la fusion ne produira pas le run N automatique exigé par D2b.4. Microsoft documente que
l’override UI remplace le déclencheur CI YAML ([Build Azure Repos Git repositories](https://learn.microsoft.com/en-us/azure/devops/pipelines/repos/azure-repos-git?view=azure-devops)).
Les dix définitions n’avaient aucun run actif ou en file ; aucune PR n’existe pour `fix/p08-keep-recalculated-preview`. La garde
`shop42-dev` a été relue et correspond au plan. Aucune définition n’a été suspendue et aucune écriture Azure DevOps n’a été faite.

La définition 80 conserve son override UI désactivant CI ; aucun réglage ADO ne sera modifié pour le contourner. La suspension des
huit définitions collatérales reste obligatoire avant la livraison. Après fusion, Florian vérifiera que `main` correspond toujours
au SHA de fusion, lancera N une seule fois sur `refs/heads/main`, puis vérifiera dans le run `sourceVersion`, `sourceBranch` et
`reason=manual`. La réussite de N devrait déclencher R0 par la ressource pipeline ([déclencheurs de pipeline](https://learn.microsoft.com/en-us/azure/devops/pipelines/process/pipeline-triggers?view=azure-devops)) ;
aucune release ne sera lancée manuellement pour remplacer R0. En cas d’écart, d’échec ou d’absence de R0 après dix minutes, ne pas
relancer N, ne rien déclencher et suivre l’arrêt `BLOQUE` prévu.

Le protocole actif est détaillé dans [P-08, étape 0 quinquies](docs/plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement)
et la [recette § 5](docs/plan/recettes/01-preuves.md#5-p8--livraison-applicative-pendant-lattente-dinfrastructure).

- **Déclencheurs D2b :** livraison sur `main`. Avant le commit/push/PR/merge, Florian relève les dix définitions CI/releases et leurs
  files, puis désactive exactement les huit définitions collatérales (`core`, `data`, `platform`, `orders/apps/api`) avec
  `queueStatus=disabled`. Les deux définitions `orders/infra` restent `enabled`. Aucun YAML du clone n'est changé pour cela.
- **Acteurs :** Luna prépare le commit local, pousse sa branche et ouvre la PR. Florian effectue la fusion, lance manuellement une
  seule fois N sur le SHA fusionné, puis rejette la demande d'approbation du seul R0 connu avant toute tâche `Deploy_dev`. Luna
  n'effectue aucune écriture Azure DevOps.
- **D3 :** C1–C7 et C9 sont des conditions avant le lancement. C8 se vérifie sur l'aperçu de l'essai unique, après `Preview_dev` et
  avant l'approbation de `Deploy_dev`. Pour satisfaire C9, Florian a choisi d'activer lui-même l'auto-approbation après R0 et de
  fournir une nouvelle capture avant D3.
- **Coût :** la décision de Florian du 2026-10-17 est de ne fixer aucun plafond personnel et de poursuivre sous surveillance.
  L'estimation France Central et le coût réel/prévision du 2026-10-17 restent historiques ; les actualiser et consigner une nouvelle
  confirmation avant toute action Azure significative. Le budget préexistant de 200 EUR/mois reste une alerte, pas un coupe-circuit.
- **Historique du statut :** après l’exception du transcript, P-08 est passé à `BLOQUE`. Florian a délégué l’arbitrage à GPT Sol, qui
  a autorisé le clone indépendant et le merge par identité d’arbre ; les amendements ciblés D2b.1/.2/.4 consignent cette méthode.
  La répétition du 2026-10-20T13:07:43Z a échoué avant publication : le clone temporaire possède le HEAD, la branche et l'arbre propres
  attendus, mais pas `origin/main`, car il a été cloné depuis le clone de preuve local. Le runner ajoutait à tort cette exigence à la
  répétition ; `git rev-parse origin/main` a échoué avant toute publication. Transcript complet archivé sous
  `%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-rehearsal-precondition-failure-20261020T130743Z-transcript.txt`, SHA-256
  `91BD51AF4D30FA9F5C152D7FEC6DB598A0852F8FBEB58050146BBC812F6515FA`. Aucun changement n'a été publié dans le clone de preuve.
  L'exception remet P-08 à `BLOQUE`. Sol autorise un nouveau rejeu complet après retrait de cette seule vérification pour le clone
  temporaire ; `origin/main` reste obligatoire pour le clone de preuve. La répétition conserve les assertions `HEAD`, branche et arbre
  propre, et doit repartir après une transition formelle vers `A_FAIRE`. Les commits, merge, push et PR restent interdits jusqu’à la fin
  de D2b.3.1–3.6. `gate.py` n'a ni commande `unblock` ni commande `start` ; `resume` reste réservé à `EN_ATTENTE_DE_RECETTE`.
  Après l'autorisation de reprise, une deuxième exception est survenue à l'étape 0 parce que `ProofTools.psm1` n'était pas chargé ;
  transcript archivé et P-08 rebloqué. Sol a ensuite confirmé l'omission, autorisé l'import explicite et le rejeu complet, puis P-08 a
  été rétabli à `A_FAIRE`. La troisième répétition a passé les étapes 0 à 3, puis échoué sur l'équivalence finale du manifeste ; Sol a
  localisé une conversion locale de `generatedAtUtc` dans le runner et autorisé un correctif d'une ligne. P-08 reste `BLOQUE` jusqu'au
  nettoyage des artefacts temporaires, puis sera rétabli à `A_FAIRE` avant le rejeu intégral.

**Prérequis D2b vérifiés le 2026-10-20 :** le clone original est accessible dans `C:/Users/flori/RiderProjects/shop42` ; sa branche
locale et distante `fix/p08-keep-recalculated-preview` contient le commit de Florian `2875cf3` et l’arbre est propre. Le clone de preuve
est créé à `C:/Users/flori/RiderProjects/shop42-d2b-proof`, avec HEAD et `origin/main` égaux à `3f5917588b83d11fc07d7f9838f5e895e037dd6d`,
branche prévue, arbre propre, sans upstream. Les contrôles locaux du clone sont consignés sous
`%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-local-verification.txt` ; l’inventaire YAML local des dix CI/releases est sous
`%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-yaml-trigger-inventory.txt`. Le diff du précédent rafraîchissement de manifeste a été
archivé puis restauré ; les deux copies temporaires avaient été absentes au début du rejeu. Après la tentative du 2026-10-20T13:16:17Z,
`ifs-shop42-rehearsal` et `ifs-shop42-fresh` contiennent des artefacts partiels, préservés jusqu'à l'arbitrage Sol ; le clone de preuve reste propre. L’audit Azure DevOps a confirmé les dix définitions
en révision 9 et `queueStatus=enabled`, zéro run actif/en file, aucune PR pour la branche, et la garde `shop42-dev` conforme. Il a
aussi révélé l’override CI décrit ci-dessus. Les huit définitions collatérales ne sont pas suspendues. Le rejeu peut commencer après
l’intégration des amendements de Sol ; la livraison reste bloquée jusqu’à la suspension confirmée et aux autres contrôles de D2b.3.
Aucune écriture Azure DevOps n’a été faite par Luna.

Florian a choisi d'activer lui-même l'auto-approbation après R0 et fournira une nouvelle capture avant D3. Si R0 n'apparaît pas
dans les dix minutes après N, cette décision ne précise pas quand activer le réglage : demander une confirmation distincte avant D3.

**D2a terminé (Luna, 2026-10-18).** Commit `bbf1d86` poussé sur `impl/preuves`. Pester : 122/122 ;
`Test-ReferenceBicep.ps1`, `Test-ReferenceDeterminism.ps1` et `Test-ReferencePipelines.ps1` réussis ; manifeste cohérent sur 77 fichiers.

Prochaines actions :

1. Les trois transcripts des tentatives échouées sont conservés avec leurs SHA-256. Sol a autorisé un correctif d'une ligne dans le
   runner ; supprimer les deux copies temporaires uniquement par `Remove-TempCopy`, puis vérifier qu'elles sont absentes.
2. Après nettoyage, rétablir formellement `A_FAIRE`, passer `gate.py check`, puis refaire entièrement répétition et exécution avec de
   nouveaux transcripts distincts. Aucun changement du clone original ou du clone de preuve avant D2b.3.1–3.6.
3. Après ces passes, Florian désactive exactement les huit définitions collatérales (`core` CI 74/release 76, `data` CI 77/release 79,
   `orders/apps/api` CI 83/release 85, `platform` CI 86/release 88) avec `queueStatus=disabled`, puis confirme les huit `disabled`,
   les deux définitions `orders/infra` (CI 80/release 82) toujours `enabled` et des files vides.
4. Après cette confirmation, Luna commite le résultat testé dans le clone de preuve, l’intègre par merge à la branche originale en
   préservant `2875cf3` et en vérifiant l’identité de l’arbre, pousse sans force et ouvre la PR ; Florian fusionne, puis lance
   manuellement un seul N sur le SHA de fusion et rejette R0 avant toute tâche `Deploy_dev`.
5. Le choix C9 est fait : Florian active lui-même l’auto-approbation après R0 et fournit une nouvelle capture avant D3.
6. Avant l'essai D3, vérifier C1–C7 et C9 ; après publication de `Preview_dev`, Florian vérifie C8 avant d'approuver `Deploy_dev`.
7. Garder #748 non diagnostiqué, conserver le fail-closed et #738 comme preuve négative. Le nettoyage Shop42 reste suspendu jusqu'à
   la réussite de P8 et des autres conditions de la procédure T.
