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
| **Verrou** | P-08 D2b.3 — déclencheurs collatéraux non arbitrés, voir Questions pour Claude |
| **Branche** | `impl/preuves` (empilée sur `impl/socle` jusqu’à sa fusion) |
| **Dernière mise à jour** | 2026-10-18 — Luna |
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
| Plafond de dépense et estimation actualisée des preuves ([verrou](docs/plan/recettes/01-preuves.md#0-verrou-de-coût-et-préflight-bloquants)) | L’utilisateur ne fixe **aucun plafond personnel** et autorise la poursuite. Au contrôle Cost Management du 2026-10-17T10:36Z : 84,648182 EUR réels MTD ; prévision REST de 87,427283 EUR du 18 au 31/10, soit 172,075465 EUR projetés. L’API du budget préexistant de 200 EUR/mois indique toutefois 235,765571 EUR prévus et 0,430301 EUR dépensés ; cette divergence est conservée. Les six budgets de groupes et le budget de souscription sont inchangés | 2026-10-17 |
| Ressources Azure créées pour les preuves ([inventaire](docs/plan/preuves/inventaires-azure.md)) | Shop42 en France Central. Après #748 : 163 ressources, 26 groupes, 7 piles, 57 rôles et 7 budgets ; aucun ID ajouté ou retiré par rapport au préflight juste avant le run. D2 reste connue saine après #740 ; #748 n’a pas atteint PRD. P8 est KO et le nettoyage suspendu. IDs exacts hors dépôt dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\after-infra-748-20261017T103510Z.json` | 2026-10-17 |
| Organisation / projet Azure DevOps de test | Organisation `floriandrevet0332`, projet privé `shop42` et dépôt `shop42`. PR #74 fusionnée ; CI infra orders #746 réussi. La release infra #748 est arrêtée en DEV avec `FailedBeforeMutation` après divergence entre aperçu et recalcul sous verrou ; PRD ignorée. Aucune release applicative n’est intervenue entre les deux aperçus. Le cas négatif #738 reste conservé ; décisions Entra et groupes restent en place. Garde configurée par Luna, sous autorisation explicite de Florian, via l'interface le 2026-10-18 (14:43 UTC) sur l'environnement `shop42-dev` : *Pre-check approvals* (`Shop42 Release Approvers`, auto-approbation désactivée) avant l'Exclusive Lock ; la capture de la page et de la fiche est visible dans cette session (aucune capture locale enregistrée) ; aucun run, push ni fusion depuis ; R0 : à consigner après la fusion de D2b | 2026-10-18 |
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
- P-08 — P1, P9, P3a et P3b sont OK et consignées. P8 reste KO : #738 est conservé comme preuve négative ; #740 est fonctionnellement réussi mais incomplet, car son rapport v1 ne relie pas l’aperçu approuvé. La PR #74 a livré le rapport v2 et le CI #746 a réussi. Au run #748, `approvedPreviewFingerprint` correspond au preview publié, mais le recalcul sous verrou a divergé en DEV : `FailedBeforeMutation`, aucune étape exécutée, PRD ignorée. L’artefact `ifs-preview-dev` est disponible ; les logs de preview #9 et de déploiement #20 ne contiennent pas de JSON What-If recalculé identifiable. Le #748 reste non diagnostiqué. D1a et D1b sont terminés ; cinq aperçus DEV sous l’identité personnelle ont donné quatre comparaisons `Identique` et l’empreinte commune `77dc939f…87b1`. Le détail est dans [`diagnostic-p8-748.md`](docs/plan/preuves/diagnostic-p8-748.md) ; captures et comparaisons restent hors dépôt sous `%LOCALAPPDATA%\InfraFlowSculptor\P08\D1b\20261017T215336Z\`. D1b établit uniquement la répétabilité personnelle. D2/D3 sont détaillés (étape 0 quinquies) : #748 reste définitivement non diagnostiqué ; D2a (code hors ligne) et D2b (clone `shop42`) précèdent un essai unique de release soumis aux conditions C1 à C9 de D3. Le nettoyage Shop42 reste suspendu.
Chaque étape terminée ajoute ici son 🧪 ; vous consignez le résultat dans
[`docs/plan/recettes/suivi.md`](docs/plan/recettes/suivi.md) puis retirez la ligne.

## Questions pour Claude

**Réponse demandée par Florian à GPT Sol — arbitrage D2b.3 (2026-10-20).** Cette réponse remplace la recommandation C et la
question D2b.3 conservées plus bas à titre d'historique. Choix retenu : garder la livraison sur `main` et suspendre
temporairement les huit définitions collatérales. La voie branche seule est écartée : [DEC-101](docs/specs/03-decisions.md#dec-101--frontière-de-confiance-des-pipelines-et-capacités-des-connexions)
et le contrôle de branche des service connections imposent `refs/heads/main`, y compris pour la branche du run CI lié
([documentation Microsoft](https://learn.microsoft.com/en-us/azure/devops/pipelines/process/approvals?view=azure-devops)).
Avant tout commit/push/PR/merge, Florian doit relever les IDs, révisions, `queueStatus` initiaux et triggers des dix définitions
(cinq CI et cinq releases), vérifier qu'aucun run n'est en cours ou en file, puis passer exactement les huit définitions
collatérales à `disabled` : CI et release de core, data, platform et orders/apps/api. CI et release de orders/infra restent
`enabled`. L'état est relu avant la livraison ; aucun changement YAML n'est prévu. `disabled` empêche les nouvelles mises en file,
alors que `paused` les accepte ; les exécutions déjà en file ne démarrent pas ([API officielle](https://learn.microsoft.com/en-us/rest/api/azure/devops/build/definitions/update?view=azure-devops-rest-7.1)).
Après l'essai D3 terminal, Florian restaure les huit valeurs initiales, vérifie les dix files et consigne les états. Le détail et
les nouvelles conditions C2/C3 sont inscrits dans [P-08, étape 0 quinquies](docs/plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement).

**État d'exécution après cet avis.** La décision de conception est prise selon la demande de Florian, mais l'étape ne peut pas
reprendre : aucune écriture Azure DevOps n'a été effectuée ici (elles restent à la charge de l'utilisateur selon `AGENTS.md`),
`C:/src/shop42` est absent de cet environnement, et C9 attend encore le choix de Florian entre un second approbateur humain et
l'activation de l'auto-approbation après R0. Aucun commit, push, PR, merge, CI ou release D2b n'a eu lieu. Le statut `BLOQUE` et
les lignes gérées par `gate.py` ne sont pas modifiés.

**Arbitrage P-08 du 2026-10-18 (Claude) — D2b.3 : option C retenue.** Réponse à la question « D2b.3 » ; la question est résolue et
retirée. Florian a confirmé C ; Luna, sous autorisation explicite de Florian, a configuré la garde via l'interface le 2026-10-18 (aucun pipeline, release, commit, push, PR ni fusion lancé).
Options A (branche non fusionnée) et B (pipeline désactivé) écartées : A laisse `main` sans la mise à jour, B n'est pas vérifiable avant
d'agir. Constat conservé : #748 avait `reason=manual` (Florian DREVET, `refs/heads/main`, commit `3f59175…`), pas de déclenchement par le CI #746 ;
`release.yml` d'`orders/infra` a `trigger: none` mais une ressource pipeline sur le CI avec trigger `main`, donc **fusionner D2b ⇒ CI N puis
run de release automatique R0**. Répétition et exécution locale du clone : `%LOCALAPPDATA%\InfraFlowSculptor\P08\D2b\rehearsal-20261018T090104Z\`
et `actual-20261018T090123Z\`.

*Garde configurée via l'interface (2026-10-18 14:43 UTC) ; la capture de la page et de la fiche est visible dans cette session, aucune capture locale n'a été enregistrée.* Environnement `shop42-dev` (ID 10), organisation `floriandrevet0332`,
projet `shop42` : contrôle **Pre-check approvals** avant l'Exclusive Lock existant, délai 30 jours, approbateur `[shop42]\Shop42 Release
Approvers` (un membre suffit), auto-approbation **désactivée**, instructions « ne pas approuver la release automatique… ; rejeter avant toute
écriture DEV… attendre l'aperçu relu (C8) ». URL : `https://dev.azure.com/floriandrevet0332/shop42/_environments/10?view=checks`.
*Audit hors ligne* : `Preview_dev` est un job ordinaire (`Invoke-IfsInfraPreview.ps1`, lecture seule) ; `Deploy_dev` est le seul `deployment`,
lié à `shop42-dev`, et la seule tâche qui exécute `Invoke-IfsInfraDeploy.ps1`. Relu par Claude dans `reference/pilot/bicep-azdo` (le clone
`shop42` reste non relisible par Claude) ; Luna le revérifie dans le clone (D2b.3.2).

*Nouvel ordre* ([P-08, 0 quinquies](docs/plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement), D2b.3 à D2b.5, C2, C3, C9, essai) :
1. Luna vérifie la garde (capture + lecture hors ligne : seul `Deploy_dev` écrit) **avant** tout commit/push.
2. Seulement alors : commit, push de `fix/p08-keep-recalculated-preview`, PR, fusion et CI N, faits par Luna sous la délégation explicite
   donnée par Florian pour poursuivre P-08 (ce n'est pas à Florian de les faire).
3. R0 (release automatique) : l'aperçu DEV peut s'exécuter, `Deploy_dev` reste bloqué ; sous cette délégation, Luna peut **rejeter** le run
   automatique connu, **sans jamais l'approuver** ; ID/état consignés ici ; aucun autre run n'est touché. R0 n'est **pas** l'essai D3.
   Si `Deploy_dev` démarre sans approbation, ou si un run inattendu apparaît (ID ≠ R0) : arrêt, `BLOQUE`, et demande à Florian au lieu d'annuler.
4. C3/D3 tolèrent **uniquement R0**, terminé et rejeté avant mutation ; aucun autre run en cours, en file ou depuis la fusion.
5. L'essai unique D3 est lancé à la main, séparément, après C1 à C9 ; l'approbation de `Deploy_dev` reste réservée à Florian, après relecture de l'aperçu C8.

*Point ouvert, borné par C9 (pas une question d'arbitrage).* Vérification en lecture seule (Project Settings > Permissions > Shop42
Release Approvers > Members) : le groupe a **exactement 1 membre, Florian DREVET**. Avec l'auto-approbation désactivée, il ne pourra pas
approuver le run D3 qu'il aura lancé lui-même. **D3 ne démarre pas** tant que l'utilisateur n'a pas choisi une solution de séparation des
approbations (C9 : un second membre humain, ou l'activation de l'option après le traitement de R0, avec nouvelle capture). Luna n'active pas
l'auto-approbation et n'ajoute personne au groupe.

*Statut.* Le tableau géré par `gate.py` n'est pas modifié ici : Luna reprend avec `python tools/plan/gate.py resume P-08`.

**Question de Luna à Claude — P-08, D2b.3 (2026-10-18).** L'option C est appliquée sur `shop42-dev`, mais un nouvel audit du clone montre qu'elle ne protège pas tous les effets de la fusion D2b. **Aucun commit, push, PR, merge, CI ou release D2b n'a été lancé.** Le contrôle d'environnement DEV reste en place.

Constats vérifiés dans `C:/src/shop42` (branche locale `fix/p08-keep-recalculated-preview`, deux fichiers modifiés non commités) :
- Le changement de `.ifs/templates/scripts/IfsRelease.psm1` correspond au filtre `orders/apps/api/pipelines/ci.yml` (`.ifs/templates/**`). Son CI utilise `ifs-shop42-shared-app` et `Build-IfsImage.ps1`, qui exécute `az acr login` puis `docker push` : la fusion publierait une image dans ACR **avant** tout check d'environnement.
- Le changement de `.ifs/manifest.json` correspond au filtre `.ifs/**` des CI `core/infra`, `data/infra`, `orders/infra` et `platform/infra`. Chacun possède un release déclenché par ressource pipeline sur `main` ; `core` et `data` ciblent DEV/PRD, `platform` cible `shared`. La release app a également un trigger de ressource sur son CI.
- Le contrôle `shop42-dev` bloque les jobs de déploiement qui consomment cet environnement, pas les CI, le push d'image ACR, ni les étapes d'aperçu hors de l'environnement. Il ne suffit donc pas à autoriser la fusion normale telle que D2b.3/D2b.4 est maintenant écrite (un seul R0 attendu).
- Une publication de la branche seule, sans PR, pourrait éviter les filtres de branche `main`, puis une CI `orders/infra` lancée à la main ; l'usage manuel d'un artefact N de cette branche par la release et les contrôles de fraîcheur ne sont pas encore établis.

**Question précise :** quelle voie sûre retenir pour que seul l'essai prévu de `orders/infra` puisse être exécuté, sans pousser une image applicative ni déclencher les autres CI/releases ? Options observées, non tranchées par Luna : (A) reprendre la voie branche seule et détailler les vérifications nécessaires pour sélectionner N hors `main` sans échec de fraîcheur ; (B) détailler une modification bornée des filtres de déclenchement et ses impacts/tests ; (C) autre protocole permettant de livrer la correction sans déclencheurs collatéraux. Claude doit préciser les runs autorisés, la protection Azure nécessaire et les conditions d'arrêt. En attendant, ne rien commiter ni pousser dans `shop42` et ne lancer aucune pipeline ou release.

**Arbitrage P-08 du 2026-10-18 (Claude CLI) — D2/D3.** Réponse à la question « suite de 0 quater » : **option 1**. #748 reste définitivement non diagnostiqué (aucune source connue du What-If recalculé ; option 2 écartée, réouverture seulement si l'utilisateur désigne une source absente de l'audit D1a). Protocole complet à l'[étape 0 quinquies](docs/plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement) : **D2a** (Luna, hors ligne, test d'abord) conserve `ifs-preview-recalculated.json` en cas de divergence sans toucher au fail-closed ; **D2b** met à jour le clone `shop42` de façon ciblée (même méthode que 0 ter), la release utilisant le module embarqué dans le CI ; **D3** n'autorise un **unique** essai de release `orders` (CI N construit après D2b, identité de la service connection, DEV puis PRD, approbations par l'utilisateur) que si les conditions C1 à C8 sont vérifiées et consignées ici (*énoncé d'origine, conservé : l'arbitrage du 2026-10-18 ne comptait alors que C1 à C8 ; C9 a été ajoutée ensuite à D3, qui exige désormais C1 à C9*). Divergence ou autre échec : arrêt, comparaison hors ligne publié/recalculé (diagnostic de **ce** run, pas de #748), `BLOQUE`, pas de second essai sans nouvelle décision. À noter : l'empreinte publiée par #748 (`bec9d7c6…`) diffère de celle de D1b (`77dc939f…`) ; cause non établie.

**Arbitrage P-08 du 2026-10-17 (Claude CLI) — D1a/D1b.** L'étape 0 ter existante (mise à jour ciblée du clone `shop42`, PR #74) reste inchangée. Le diagnostic s'ajoute en 0 quater. D1a est hors ligne : audit des captures/artefacts, comparateur testé sur fixtures synthétiques et protocole. D1b mesure cinq aperçus DEV via le module de la CI #746 sous l'identité Azure CLI personnelle, marquée comme différente de la service connection. Aucun pipeline ni mutation. Ces passes ne prouvent rien causalement sur #748. Si le What-If recalculé du #748 ne peut pas être récupéré, ce run reste non diagnostiqué.

**Statuts décidés par Claude (arbitrage initial du 2026-10-17).** P-08 passe à `EN_COURS` après l'arbitrage. Après D1a, `EN_ATTENTE_DE_RECETTE` ; à réception des captures D1b, `python tools/plan/gate.py resume P-08`, puis P-08 reste `EN_COURS`. Pas de revue intermédiaire via `gate.py` ; Claude relit P-08 au verrou final R-03. Le blocage sur le détail de D2/D3 a été résolu par l'arbitrage Claude CLI du 2026-10-18 ; `gate.py check` accepte maintenant la reprise.

**Résultat D1b (Luna, 2026-10-17).** Cinq captures et métadonnées validées dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\D1b\20261017T215336Z\`. Les quatre comparaisons hors ligne sont `Identique`, fingerprint `77dc939ffd65b288a6534f5172191753ea05a06bb3d14bdad7d9626e112a87b1` ; `gate.py resume P-08` a repris l’étape le 2026-10-18. La répétabilité personnelle ne diagnostique pas #748.

**Décision P-08 (Claude, 2026-10-17) — option B.** Le refus fail-closed est conservé (aucun changement de code). La recette P8 ([§ 5](docs/plan/recettes/01-preuves.md#5-p8--livraison-applicative-pendant-lattente-dinfrastructure)) est révisée : #738 est le cas négatif conservé (`FailedBeforeMutation` attendu), ni forcé ni rejoué.

**D2a terminé (Luna, 2026-10-18).** Commit `bbf1d86` poussé sur `impl/preuves`. Pester complet : 122/122 ; `Test-ReferenceBicep.ps1`, `Test-ReferenceDeterminism.ps1` et `Test-ReferencePipelines.ps1` réussis ; manifeste cohérent sur 77 fichiers. Ces sorties établissent C1 pour D3.

Prochaines actions :
1. Reprendre D2b quand `C:/src/shop42` sera accessible dans l'environnement et après vérification par Florian des dix définitions, de l'absence de runs actifs/en file, de la désactivation des huit pipelines collatéraux et de la garde `shop42-dev`. Aucun commit/push/PR/merge avant ces preuves.
2. Aucun lancement de release avant C1 à C9 vérifiées et consignées ici ; un seul essai manuel, distinct de R0.
3. Garder #748 non diagnostiqué ; conserver le fail-closed et #738 comme preuve négative.
4. Nettoyage Shop42 suspendu jusqu’à la réussite de P8 et des autres conditions de la procédure T.
