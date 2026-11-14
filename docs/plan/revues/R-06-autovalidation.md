# Auto-validation R-06 — Revue de la génération

Verdict : VALIDÉ

- **Segment** : J0-21 à J0-25 — verrou R-06.
- **Branche / pull request** : impl/j0-generation — sans PR.
- **Base du segment** : origin/impl/j0-modele, commit d786478dc11d6974ae4681f835ec5c173f29fe47.
- **HEAD avant clôture** : a283b4423dc447367ea580586ceaf796a4da81d8.

## Vérifications exécutées

| Commande / contrôle | Résultat |
|---|---|
| dotnet build src/backend/InfraFlowSculptor.slnx --configuration Release --no-restore --warnaserror -m:1 | Réussi : 0 avertissement, 0 erreur. |
| dotnet test src/backend/InfraFlowSculptor.slnx --configuration Release --no-restore -m:1 avec IFS_RUN_BICEP_CLI_TESTS=1 et IFS_RUN_AZURE_BICEP_CLI_TESTS=1 définis pour le processus | 847/847 réussis, aucun échec ni saut : Acceptance 3, API 143, Application 83, Architecture 9, Catalog 41, Domain 136, Emitters 99, Engine 233, Infrastructure 100. |
| Tests ciblés RevisionOutputCheckerTests | 15/15. |
| pwsh -NoProfile -File reference/tools/Test-ReferencePipelines.ps1 | 26/26 fichiers YAML valides selon le schéma épinglé. |
| pwsh -NoProfile -File reference/tools/Test-ReferenceBicep.ps1 | core, data, orders et platform valides, formatés et sans avertissement. |
| pwsh -NoProfile -File reference/tools/Test-ReferenceDeterminism.ps1 | 80 fichiers UTF-8, sans BOM, LF et avec saut final conformes. |
| Invoke-Pester reference/release-module/tests/InvokeIfsImageScan.Tests.ps1 -CI -Output Minimal | 26/26, aucun échec ni saut. |
| python -m graphify update . | Rapport actualisé : 13 457 nœuds, 39 779 arêtes, 491 communautés. Trois fichiers SQL ne sont pas extraits, faute de tree_sitter_sql. |
| AppHost Aspire sous Podman et endpoints locaux | aspire describe confirme Worker/API/Web sains; HTTP 200 sur Web, API /alive, Scalar, Gitea et découverte OIDC Keycloak. Volumes persistants conservés. |
| python tools/plan/gate.py lint et git diff --check | Exécutés après ajout de cette auto-validation, avant le commit de clôture. |

Les tests ciblés ImageScan vérifient l'image locale seule, les sévérités cumulatives au seuil, les chemins d'exceptions et l'absence d'héritage de configuration Trivy du dépôt. Les tests du vérificateur Worker couvrent les fichiers cachés, sorties PowerShell et YAML, erreurs de schéma, chemins dupliqués, séparateurs, chemins racine et échecs de nettoyage.

## Revue Claude CLI

- Revue du diff R-06 en lecture seule, base origin/impl/j0-modele, modèle Sonnet, CLI en mode restreint. Des suivis ciblés ont relu le vérificateur Worker, ses tests et le scanner ImageScan.
- La dernière revue, après corrections, ne relève **aucun défaut actionnable restant**. Claude n'a exécuté ni tests ni Trivy.
- Constats confirmés puis corrigés :
  - Trivy traite --severity comme une liste de filtrage, pas comme un seuil minimal. Le script transmet maintenant toutes les sévérités égales ou supérieures au seuil (HIGH inclut aussi CRITICAL, etc.); les tests vérifient chaque seuil.
  - Trivy pouvait charger un trivy.yaml ou .trivyignore du dépôt. Le scan s'exécute dans un répertoire temporaire vierge et les variables de configuration/ignore héritées sont supprimées pour le processus, puis restaurées.
  - L'analyse devait être locale : --image-src docker l'impose et évite un téléchargement distant.
  - Les contrôles de l'application et du Build ID acceptaient auparavant la casse ou le saut de ligne final; les tests rouges ont conduit à des expressions ancrées et sensibles à la casse.
  - Les validations récursives omettaient les fichiers cachés; Get-ChildItem -Force les inclut.
  - Un échec de nettoyage pouvait masquer l'erreur initiale de validation; le vérificateur conserve désormais l'exception principale et rattache l'échec de nettoyage.
  - Les chemins de sortie dupliqués sans tenir compte de la casse et les séparateurs inverses sont refusés; les cas sont couverts par des tests.

## Recette exécutée

La recette manuelle G1–G6 a été exécutée : révision 1 générée, paramètres PRD consultés, archive comparée à la référence sur 80/80 fichiers après normalisation des marqueurs (manifeste exclu), absence de nouvelle révision après une génération inchangée, plan de déploiement inspecté, puis génération bloquée par VAL-LIA-OBLIGATOIRE après retrait d'Hosting. La liaison a été restaurée.

G7 Alice a été exécutée sur bureau 1440×900 et mobile 390×844 : export réel ifs-project.json, schéma ifs-project/v1, aucun membre ni secret dans le contenu inspecté, événement « Export du projet shop » présent dans l'audit. Le panneau de paramètres a été comparé à la preview à 1440 px; le décalage de grille a été corrigé et son test Playwright rouge/vert passe sur bureau et mobile (2/2). Captures et comparaison : captures/R-06/.

La suite E2E complète reste partielle : 30/36 passent. Les deux parcours links ne trouvent pas la fixture shop15, absente des projets actifs et archivés lors de l'inspection; les deux parcours resources expirent pendant GET /delete-impact. La cause de l'absence de shop15 n'est pas établie; aucune fixture n'a été recréée. Après nettoyage des seules fixtures r14desk/r14mob identifiées, le parcours ciblé des constats passe 2/2. Ces limites sont détaillées dans docs/plan/recettes/suivi.md.

## Écarts

- La recette utilise le projet et les volumes locaux persistants déjà présents; elle n'a pas modifié la base pour remplacer les fixtures absentes.
- L'exécution E2E complète n'est pas entièrement verte pour les deux causes ci-dessus. Les scénarios ciblés pertinents et la recette G1–G7 sont passés; aucun résultat global E2E n'est présenté comme intégralement réussi.
- Le rapport Graphify n'extrait pas trois SQL sans tree_sitter_sql; l'extraction des autres sources et sa mise à jour réussissent.

## Contrôles de coût et sécurité

- Aucune écriture Azure, Azure DevOps, Entra ou dépôt externe; aucun coût incrémental.
- Aucun secret, jeton ou mot de passe n'a été ajouté au dépôt, à la documentation ou à la mémoire.
- L'AppHost et les volumes Podman persistants ont été conservés.

## Risques résiduels

- Le vérificateur Worker n'inclut pas les stdout/stderr générés dans les incidents afin de ne pas divulguer de données client.
- Les chemins générés depuis les slugs contraints et les fichiers de modèles fixes ne couvrent pas explicitement tous les cas spécifiques à Windows (noms de périphériques, flux alternatifs, points/espaces finaux); les émetteurs actuels utilisent des composants connus et ces cas ne bloquent pas R-06.
- L'arrêt d'un arbre de processus lors d'une annulation peut encore rencontrer une course rare dans les runners Bicep/PowerShell; le test d'annulation existant passe. Aucun défaut actionnable n'a été confirmé pour le verrou.
