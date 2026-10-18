# NEXT — où en est l'implémentation

> **À lire en premier en arrivant, à mettre à jour en dernier en partant**, même en pleine étape.
> Ce fichier dit **où l'on est** et **ce que git ne sait pas** (Azure, Entra, Azure DevOps, tests manuels passés).
> Les étapes sont dans [`docs/plan/`](docs/plan/README.md) ; l'historique dans [`docs/plan/JOURNAL.md`](docs/plan/JOURNAL.md).
> Les lignes du tableau ci-dessous sont lues et écrites par `python tools/plan/gate.py` : ne pas renommer leurs libellés.

## En un coup d'œil

| | |
|---|---|
| **Étape courante** | [`P-08`](docs/plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement) — Exécution des preuves (accompagnement) |
| **Statut** | `A_FAIRE` |
| **Dernière étape terminée** | [`P-07`](docs/plan/01-preuves.md#p-07--outillage-des-preuves-et-recette-pas-à-pas) — Outillage des preuves et recette pas à pas (commit `1d6f1be`) |
| **Étape suivante** | `R-03` — Revue des preuves |
| **Verrou** | P-08 : D1a/D1b terminés ; D2/D3 détaillés à l'[étape 0 quinquies](docs/plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement) ; aucun pipeline ou release avant D2a, D2b et les conditions C1 à C8 de D3 |
| **Branche** | `impl/preuves` (empilée sur `impl/socle` jusqu’à sa fusion) |
| **Dernière mise à jour** | 2026-10-18 — Claude |
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
| Organisation / projet Azure DevOps de test | Organisation `floriandrevet0332`, projet privé `shop42` et dépôt `shop42`. PR #74 fusionnée ; CI infra orders #746 réussi. La release infra #748 est arrêtée en DEV avec `FailedBeforeMutation` après divergence entre aperçu et recalcul sous verrou ; PRD ignorée. Aucune release applicative n’est intervenue entre les deux aperçus. Le cas négatif #738 reste conservé ; décisions Entra et groupes restent en place | 2026-10-17 |
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
- P-08 — P1, P9, P3a et P3b sont OK et consignées. P8 reste KO : #738 est conservé comme preuve négative ; #740 est fonctionnellement réussi mais incomplet, car son rapport v1 ne relie pas l’aperçu approuvé. La PR #74 a livré le rapport v2 et le CI #746 a réussi. Au run #748, `approvedPreviewFingerprint` correspond au preview publié, mais le recalcul sous verrou a divergé en DEV : `FailedBeforeMutation`, aucune étape exécutée, PRD ignorée. L’artefact `ifs-preview-dev` est disponible ; les logs de preview #9 et de déploiement #20 ne contiennent pas de JSON What-If recalculé identifiable. Le #748 reste non diagnostiqué. D1a et D1b sont terminés ; cinq aperçus DEV sous l’identité personnelle ont donné quatre comparaisons `Identique` et l’empreinte commune `77dc939f…87b1`. Le détail est dans [`diagnostic-p8-748.md`](docs/plan/preuves/diagnostic-p8-748.md) ; captures et comparaisons restent hors dépôt sous `%LOCALAPPDATA%\InfraFlowSculptor\P08\D1b\20261017T215336Z\`. D1b établit uniquement la répétabilité personnelle. D2/D3 sont détaillés (étape 0 quinquies) : #748 reste définitivement non diagnostiqué ; D2a (code hors ligne) et D2b (clone `shop42`) précèdent un essai unique de release soumis aux conditions C1 à C8 de D3. Le nettoyage Shop42 reste suspendu.
Chaque étape terminée ajoute ici son 🧪 ; vous consignez le résultat dans
[`docs/plan/recettes/suivi.md`](docs/plan/recettes/suivi.md) puis retirez la ligne.

## Questions pour Claude

*(Aucune question en attente.)*

**Arbitrage P-08 du 2026-10-18 (Claude CLI) — D2/D3.** Réponse à la question « suite de 0 quater » : **option 1**. #748 reste définitivement non diagnostiqué (aucune source connue du What-If recalculé ; option 2 écartée, réouverture seulement si l'utilisateur désigne une source absente de l'audit D1a). Protocole complet à l'[étape 0 quinquies](docs/plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement) : **D2a** (Luna, hors ligne, test d'abord) conserve `ifs-preview-recalculated.json` en cas de divergence sans toucher au fail-closed ; **D2b** met à jour le clone `shop42` de façon ciblée (même méthode que 0 ter), la release utilisant le module embarqué dans le CI ; **D3** n'autorise un **unique** essai de release `orders` (CI N construit après D2b, identité de la service connection, DEV puis PRD, approbations par l'utilisateur) que si les conditions C1 à C8 sont vérifiées et consignées ici. Divergence ou autre échec : arrêt, comparaison hors ligne publié/recalculé (diagnostic de **ce** run, pas de #748), `BLOQUE`, pas de second essai sans nouvelle décision. À noter : l'empreinte publiée par #748 (`bec9d7c6…`) diffère de celle de D1b (`77dc939f…`) ; cause non établie.

**Arbitrage P-08 du 2026-10-17 (Claude CLI) — D1a/D1b.** L'étape 0 ter existante (mise à jour ciblée du clone `shop42`, PR #74) reste inchangée. Le diagnostic s'ajoute en 0 quater. D1a est hors ligne : audit des captures/artefacts, comparateur testé sur fixtures synthétiques et protocole. D1b mesure cinq aperçus DEV via le module de la CI #746 sous l'identité Azure CLI personnelle, marquée comme différente de la service connection. Aucun pipeline ni mutation. Ces passes ne prouvent rien causalement sur #748. Si le What-If recalculé du #748 ne peut pas être récupéré, ce run reste non diagnostiqué.

**Statuts décidés par Claude (arbitrage initial du 2026-10-17).** P-08 passe à `EN_COURS` après l'arbitrage. Après D1a, `EN_ATTENTE_DE_RECETTE` ; à réception des captures D1b, `python tools/plan/gate.py resume P-08`, puis P-08 reste `EN_COURS`. Pas de revue intermédiaire via `gate.py` ; Claude relit P-08 au verrou final R-03. Le statut courant est maintenant `BLOQUE`, car le plan ne détaille pas D2/D3.

**Résultat D1b (Luna, 2026-10-17).** Cinq captures et métadonnées validées dans `%LOCALAPPDATA%\InfraFlowSculptor\P08\D1b\20261017T215336Z\`. Les quatre comparaisons hors ligne sont `Identique`, fingerprint `77dc939ffd65b288a6534f5172191753ea05a06bb3d14bdad7d9626e112a87b1` ; `gate.py resume P-08` a repris l’étape le 2026-10-18. La répétabilité personnelle ne diagnostique pas #748.

**Décision P-08 (Claude, 2026-10-17) — option B.** Le refus fail-closed est conservé (aucun changement de code). La recette P8 ([§ 5](docs/plan/recettes/01-preuves.md#5-p8--livraison-applicative-pendant-lattente-dinfrastructure)) est révisée : #738 est le cas négatif conservé (`FailedBeforeMutation` attendu), ni forcé ni rejoué.

Prochaines actions :
1. Luna exécute dans l'ordre D2a, D2b puis D3 de l'étape 0 quinquies ; aucun pipeline ni release hors ce qu'elle autorise (validation de PR et CI `orders/infra` sans connexion Azure, après contrôle des YAML).
2. Aucun lancement de release avant C1 à C8 vérifiées et consignées ici ; un seul essai.
3. Garder #748 non diagnostiqué ; conserver le fail-closed et #738 comme preuve négative.
4. Nettoyage Shop42 suspendu jusqu’à la réussite de P8 et des autres conditions de la procédure T.
