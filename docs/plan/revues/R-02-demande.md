# Demande de revue R-02 — Sortie de référence du pilote

- **Segment** : P-01 → P-06
- **Branche / pull request** : `impl/preuves` → `impl/socle` ([PR #2](https://github.com/FlorianDrevet/InfraFlowSculptor/pull/2), empilée sur la PR #1 encore ouverte)
- **Commits** : `origin/impl/socle..HEAD`, de `884b323` à `1c7e2d8`
  - `884b323` — application témoin et contrôle de ses dépendances
  - `7e4fba3` — P-01 terminée
  - `aa9da86` — injection du secret E2E dans le royaume jetable
  - `c5d4397` — correction du traitement des notifications DCP périmées
  - `784d7c9` — code Bicep du projet pilote de référence
  - `e34cd33` — P-02 terminée
  - `02cf2b2` — module de release avec journal d'opérations et reprise
  - `5cb1050` — P-03 terminée
  - `784be86` — pipelines Azure DevOps du projet pilote
  - `27fcbb3` — P-04 terminée
  - `639f3c3` — kit d'installation du projet pilote
  - `5aba790` — documentation générée, manifeste et contrôles en CI
  - `1c7e2d8` — prise en compte des fichiers cachés dans les contrôles

## Vérifications exécutées

| Commande | Résultat |
|---|---|
| `dotnet build src/backend/InfraFlowSculptor.slnx --configuration Release --no-restore --warnaserror -m:1` | 15 projets, 0 avertissement, 0 erreur |
| `dotnet test src/backend/InfraFlowSculptor.slnx --configuration Release --no-build --no-restore -m:1 --filter "Category!=Acceptance"` | 66 réussis, 0 échec, 6 projets |
| `npm run lint` | Réussi ; les 46 clés i18n sont présentes et utilisées |
| `npm test` | 41 tests réussis (35 Angular, 3 tokens, 3 icônes) |
| `npm run build` | Réussi |
| `npm run e2e` local | 2 scénarios publics réussis ; 4 scénarios authentifiés ignorés car `IFS_E2E_PASSWORD` n'est pas disponible dans ce processus |
| CI GitHub, run `37588979668` | Tous les jobs réussis, dont Acceptance tests et End-to-end tests |
| `Invoke-Pester reference/release-module/tests -CI` | 38 tests réussis, 0 échec |
| PSScriptAnalyzer sur les cinq scripts concernés | 0 erreur, 0 avertissement ; `PSUseBOMForUnicodeEncodedFile` est exclu car les artefacts imposent UTF-8 sans BOM |
| `Test-ReferenceDeterminism.ps1` et `Update-ManifestExample.ps1 -Check` | 73 fichiers gérés conformes ; manifeste cohérent |
| `Test-ReferencePipelines.ps1` | 24 pipelines valides contre le schéma épinglé |
| `Test-ReferenceBicep.ps1` | Les 4 composants compilent, sont lintés et formatés sans avertissement |
| `python tools/plan/gate.py lint` | 105 étapes, 18 verrous, aucun problème |

Le run GitHub valide également les scénarios authentifiés avec le secret de dépôt configuré ; aucune valeur de secret n'est stockée dans ce dépôt.

## Écarts au plan

| Étape | Plan | Réalisé | Raison |
|---|---|---|---|
| P-01 à P-06 | Vérifications locales et sortie de référence avant toute preuve Azure | Aucun déploiement Azure ni exécution distante Azure DevOps | Conforme au périmètre R-02, explicitement « avant Azure » ; aucun abonnement ni projet Azure DevOps de test n'est configuré |
| R-02 | Vérifications backend/frontend/E2E | Les quatre scénarios authentifiés sont ignorés lors de l'exécution E2E locale ; ils passent dans le run CI GitHub `37588979668` | Le secret E2E est configuré dans GitHub Actions, mais n'est pas disponible dans le processus local |
| Vérification backend | Build sans avertissement | Build effectué en `Release` | L'AppHost Aspire actif conserve les DLL `Debug`; la configuration `Release` a permis de vérifier sans arrêter les services en cours |
| P-06 / CI | Énumération déterministe de tous les fichiers générés | Un premier run CI Linux a révélé que les chemins cachés `.ifs/` n'étaient pas inclus par l'énumération PowerShell par défaut ; correction et régression ajoutées dans `1c7e2d8`, puis CI verte |

## Dette de test ouverte (`.github/test-debt.md`)

La dette S-08 reste ouverte et non bloquante selon la décision R-01 : les branches de settlement du `SessionJobProcessorService` (budget épuisé, escalade réessai/dead-letter) et la boucle de renouvellement du runner ne sont pas exercées de bout en bout. La résolution reste affectée à une étape dédiée aux tests du Worker.

## Tests manuels préparés

- **P-01** — image témoin exécutée localement : `/health` renvoie `version: local`, les contrôles non configurés sont ignorés, et une clé de paiement vide donne 503 sans exposer de valeur secrète.
- **P-02** — comparaison de `orders/infra/main.dev.bicepparam` avec `reference-pilote.md` § 2.1 ; noms exacts, aucune valeur `dev` dans `main.bicep`, image `caApi` laissée vide.
- **P-03** — import du module et `Get-Help Invoke-IfsPreview -Full` : aide française présente.
- **P-04** — lecture de la release `orders` et du template de stages : aperçu de production sans environnement, déploiement lié à `shop-prd`, secret `MAIN_PAYMENTS_API_KEY` présent uniquement dans le composant `core`.
- **P-05** — lecture de `SETUP.md` comme un client : chaque action identifie le rôle responsable et la commande ou l'écran requis. La validation distante reste hors du périmètre avant Azure.
- **P-06** — aperçu GitHub public de `README.ifs.md` : diagramme Mermaid rendu, noms conformes à `reference-pilote.md` § 2.1, captures aux deux largeurs ci-dessous.
- **Recette R-02** — aucune recette Azure utilisateur n'est requise à ce verrou ; le plan demande la lecture de `SETUP.md`, effectuée pour P-05.

## Captures

- `captures/R-02/reference-1440.png`
- `captures/R-02/reference-390.png`

La capture mobile conserve le diagramme Mermaid dans l'aperçu GitHub ; ses commandes d'agrandissement ouvrent la vue détaillée.

## Points d'attention pour la relecture

- Vérifier les portées minimales des rôles, la condition RBAC, les fédérations d'identité, `denyDelete` et les exclusions du kit d'installation.
- Confirmer qu'aucun secret ni accès Azure n'est présent dans les fichiers ou pipelines de pull request.
- Vérifier que le journal de release est écrit avant les mutations, que l'empreinte couvre les effets et que les relances sont idempotentes.
- Évaluer la lisibilité pour un client qui ne connaît pas IFS et la conformité aux formes de `docs/specs/21-generation-et-revisions.md` § 6.2.
- Examiner les limites de la validation hors ligne : le RBAC et les pipelines sont simulés ou validés statiquement, sans abonnement Azure ni organisation Azure DevOps de test.
