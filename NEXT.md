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
| **Verrou** | D2b — le déclencheur UI de `orders/infra` CI empêche le N automatique prévu après fusion ; arbitrage demandé à Claude |
| **Branche** | `impl/preuves` (empilée sur `impl/socle` jusqu’à sa fusion) |
| **Dernière mise à jour** | 2026-10-20 — Luna |
Statuts : `A_FAIRE` · `EN_COURS` · `EN_ATTENTE_DE_REVUE` (🔒 Luna s'arrête) · `EN_ATTENTE_DE_RECETTE` (Luna attend vos
résultats) · `CORRECTIONS_DEMANDEES` · `BLOQUE` (question d'arbitrage en attente).

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
| Organisation / projet Azure DevOps de test | Organisation `floriandrevet0332`, projet privé `shop42` et dépôt `shop42`. PR #74 fusionnée ; CI infra orders #746 réussi. La release infra #748 est arrêtée en DEV avec `FailedBeforeMutation` après divergence entre aperçu et recalcul sous verrou ; PRD ignorée. Aucune release applicative n’est intervenue entre les deux aperçus. Le cas négatif #738 reste conservé ; décisions Entra et groupes restent en place. Garde `shop42-dev` relue : *Pre-check approvals* (`Shop42 Release Approvers`, auto-approbation désactivée) avant l'Exclusive Lock. Le 2026-10-20, les dix définitions sont en révision 9 et `queueStatus=enabled` ; aucune exécution active/en file et aucune PR pour `fix/p08-keep-recalculated-preview`. L'override UI désactive CI, dont le CI `orders/infra` requis pour D2b ; détails et arbitrage ci-dessous. Clone local : `C:/Users/flori/RiderProjects/shop42`, basé sur `3f591d7`. Audit local D2b réussi ; manifeste recalculé avec `Write-ProofManifest` vers la source IFS `dbcaa9c`. Fichier de travail non commité : `.ifs/manifest.json` seulement ; les 99 empreintes de fichiers gérés restent inchangées. Aucune écriture Azure DevOps par Luna | 2026-10-20 |
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

**Arbitrage demandé — P-08 / D2b :** l’interface Azure DevOps a été vérifiée en lecture seule le 2026-10-20 pour les dix définitions.
Elles sont toutes en révision 9 avec `queueStatus=enabled`. Pour les dix définitions, l’option UI « Override the YAML
continuous integration trigger from here » est cochée et « Disable continuous integration » est sélectionné ; aucun déclencheur
planifié ni de fin de build n’est configuré. En particulier, CI `orders/infra` (ID 80) a aussi `trigger: main` dans son YAML, mais
l’override UI désactive ce déclencheur : la fusion ne produira pas le run N automatique exigé par D2b.4. Microsoft documente que
l’override UI remplace le déclencheur CI YAML ([Build Azure Repos Git repositories](https://learn.microsoft.com/en-us/azure/devops/pipelines/repos/azure-repos-git?view=azure-devops)).
Les dix définitions n’avaient aucun run actif ou en file ; aucune PR n’existe pour `fix/p08-keep-recalculated-preview`. La garde
`shop42-dev` a été relue et correspond au plan. Aucune définition n’a été suspendue et aucune écriture Azure DevOps n’a été faite.

**Question pour Claude :** amender le contrat D2b.4 et les préconditions D3 pour lever cette contradiction. Faut-il (1) conserver le N
automatique en demandant à Florian de réactiver le déclencheur CI dans l’interface pour la seule définition 80 avant la fusion, ou
(2) autoriser un unique lancement manuel de N sur le SHA fusionné de `main`, puis vérifier si sa réussite déclenche R0 par la
ressource pipeline ([déclencheurs de pipeline](https://learn.microsoft.com/en-us/azure/devops/pipelines/process/pipeline-triggers?view=azure-devops)) ?
GPT Sol recommande l’option 2 comme changement ADO minimal, mais cette nouvelle action et le plan correspondant doivent être
explicitement arbitrés. Dans les deux cas, garder la suspension prévue des huit définitions collatérales. Jusque-là,
aucune suspension, livraison D2b dans `shop42` (commit/push/PR/fusion), ni lancement de N/R0.

Le protocole actif, sous réserve de cet arbitrage, est détaillé dans [P-08, étape 0 quinquies](docs/plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement)
et la [recette § 5](docs/plan/recettes/01-preuves.md#5-p8--livraison-applicative-pendant-lattente-dinfrastructure).

- **Déclencheurs D2b :** livraison sur `main`. Avant le commit/push/PR/merge, Florian relève les dix définitions CI/releases et leurs
  files, puis désactive exactement les huit définitions collatérales (`core`, `data`, `platform`, `orders/apps/api`) avec
  `queueStatus=disabled`. Les deux définitions `orders/infra` restent `enabled`. Aucun YAML du clone n'est changé pour cela.
- **Acteurs :** Luna prépare le commit local, pousse sa branche et ouvre la PR. Florian effectue la fusion et rejette la demande
  d'approbation du seul R0 connu avant toute tâche `Deploy_dev`. Luna n'effectue aucune écriture Azure DevOps.
- **D3 :** C1–C7 et C9 sont des conditions avant le lancement. C8 se vérifie sur l'aperçu de l'essai unique, après `Preview_dev` et
  avant l'approbation de `Deploy_dev`. Pour satisfaire C9, Florian a choisi d'activer lui-même l'auto-approbation après R0 et de
  fournir une nouvelle capture avant D3.
- **Coût :** la décision de Florian du 2026-10-17 est de ne fixer aucun plafond personnel et de poursuivre sous surveillance.
  L'estimation France Central et le coût réel/prévision du 2026-10-17 restent historiques ; les actualiser et consigner une nouvelle
  confirmation avant toute action Azure significative. Le budget préexistant de 200 EUR/mois reste une alerte, pas un coupe-circuit.
- **Historique du statut :** à la demande de Florian, le précédent blocage d'arbitrage a été levé et P-08 repris `EN_COURS`. Le nouvel
  audit D2b du 2026-10-20 révèle la contradiction décrite ci-dessus ; le statut courant est désormais `BLOQUE`. `gate.py` n'a ni
  commande `unblock` ni commande `start` ; `resume` reste réservé à `EN_ATTENTE_DE_RECETTE`.

**Prérequis D2b vérifiés le 2026-10-20 :** le clone est accessible dans `C:/Users/flori/RiderProjects/shop42` ; la branche distante
`fix/p08-keep-recalculated-preview` contient le commit de Florian `2875cf3`. Les contrôles locaux du clone sont consignés sous
`%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-local-verification.txt` ; l’inventaire YAML local des dix CI/releases est sous
`%LOCALAPPDATA%/InfraFlowSculptor/P08/D2b/d2b-yaml-trigger-inventory.txt`. Le manifeste local a été rafraîchi par l’outil prévu ;
seul `.ifs/manifest.json` est modifié dans le clone et n’est ni commité ni poussé. L’audit Azure DevOps a confirmé les dix définitions
en révision 9 et `queueStatus=enabled`, zéro run actif/en file, aucune PR pour la branche, et la garde `shop42-dev` conforme. Il a
aussi révélé l’override CI décrit ci-dessus. Les huit définitions collatérales ne sont pas suspendues. Toute livraison D2b attend la
réponse de Claude et l’application du protocole amendé ; aucun changement de pipeline, run, PR ou fusion Azure DevOps n’a été fait.

Florian a choisi d'activer lui-même l'auto-approbation après R0 et fournira une nouvelle capture avant D3. Si R0 n'apparaît pas
dans les dix minutes après N, cette décision ne précise pas quand activer le réglage : demander une confirmation distincte avant D3.

**D2a terminé (Luna, 2026-10-18).** Commit `bbf1d86` poussé sur `impl/preuves`. Pester : 122/122 ;
`Test-ReferenceBicep.ps1`, `Test-ReferenceDeterminism.ps1` et `Test-ReferencePipelines.ps1` réussis ; manifeste cohérent sur 77 fichiers.

Prochaines actions :

1. Attendre l’arbitrage de Claude sur le déclencheur de CI `orders/infra` ; ne pas suspendre les huit définitions et ne pas livrer D2b
   avant la mise à jour du plan.
2. Après résolution, reprendre les préalables de déclenchement D2b. Le choix C9 est fait : Florian active lui-même l’auto-approbation
   après R0 et fournit une nouvelle capture avant D3.
3. Une fois les préalables amendés satisfaits, Luna vérifie le clone et finalise le manifeste local encore non commité, puis pousse la
   branche et ouvre la PR ; Florian fusionne et rejette R0 avant toute écriture DEV.
4. Avant l'essai D3, vérifier C1–C7 et C9 ; après publication de `Preview_dev`, Florian vérifie C8 avant d'approuver `Deploy_dev`.
5. Garder #748 non diagnostiqué, conserver le fail-closed et #738 comme preuve négative. Le nettoyage Shop42 reste suspendu jusqu'à
   la réussite de P8 et des autres conditions de la procédure T.
