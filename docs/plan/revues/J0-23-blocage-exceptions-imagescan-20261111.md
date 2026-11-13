# J0-23 — format des exceptions ImageScan — 2026-11-11

Blocage confirmé : CONFIRMÉ

## Question à Florian

Quel format le paramètre `ImageScan.exceptions` doit-il accepter ?

- des identifiants CVE séparés par des virgules, convertis par le template en exclusions Trivy ;
- le chemin, dans le dépôt, du fichier d'exclusions Trivy ;
- le contenu brut d'un fichier d'exclusions Trivy YAML.

La spécification 19 § 6.1 nomme les « exceptions », et le descripteur de catalogue les représente par une chaîne vide par défaut, sans définir leur format ni leur traduction. Le template échoue volontairement si la valeur n'est pas vide ; ignorer silencieusement une valeur non vide rendrait le contrôle de sécurité trompeur.

La sortie complète reste également à revalider avec Docker : les trois tests d'acceptation du worker démarrent Aspire/Testcontainers, mais le moteur Linux de Docker Desktop n'est pas disponible dans cette session. Le service `com.docker.service` est arrêté ; `docker info` ne répond pas. Après lancement de Docker Desktop dans la session utilisateur, l'interface affiche « An unexpected error occurred » pendant l'initialisation de l'Inference manager : le fichier `C:\Users\flori\AppData\Local\Docker\run\dockerInference` est inaccessible. Le démarrage direct du service a également été refusé par les droits Windows. Aucun rapport de diagnostic n'a été envoyé et aucune réinitialisation n'a été lancée.

## Vérifications effectuées

- `dotnet build src/backend/InfraFlowSculptor.slnx -c Release --no-restore --verbosity minimal` : réussi, 0 avertissement, 0 erreur.
- `dotnet test src/backend/tests/InfraFlowSculptor.Emitters.Tests/InfraFlowSculptor.Emitters.Tests.csproj -c Release --no-restore` : 55 réussis, 1 test CLI Bicep ignoré par défaut. Le test TDD `InstallKitAndReadmeRenderProjectCodeAcrossAllAssets` a échoué avant le correctif puis passe ; il couvre le code `shop42` dans le kit, le README et les assets Azure DevOps.
- Suite .NET complète : échec de trois tests d'acceptation au démarrage Docker ; les tests du projet d'émetteurs passent.
- `reference/tools/Test-ReferencePipelines.ps1` : les 26 YAML passent le schéma épinglé, les références locales et les assertions de séquencement/sécurité PR.
- `reference/tools/Test-ReferenceBicep.ps1` : les quatre composants passent, sans avertissement.
- `reference/tools/Test-ReferenceDeterminism.ps1` : 79 fichiers conformes.
- `reference/tools/Update-ManifestExample.ps1 -Check` : manifeste cohérent avec 79 fichiers.
- Pester des tests actifs : 139 réussis ; le seul test restant refuse de publier depuis une source Git modifiée, précondition attendue du test P-08.
- Pester des preuves archivées P-08 : 76/76 réussis.
- Aucune écriture Azure ou Azure DevOps.

## Réévaluation du runtime et de la recette — 2026-11-13

Podman Desktop est disponible, le CLI Podman 6.2.0 fonctionne et sa machine WSL2 `podman-machine-default` est
démarrée. La variable utilisateur `ASPIRE_CONTAINER_RUNTIME=podman` est effective ; `aspire doctor` confirme Podman
comme runtime sélectionné (6 contrôles
passés, 2 avertissements liés à Docker arrêté et à l'extension VS Code absente, aucun échec). `aspire run` démarre
l'AppHost IFS avec les dépendances Podman, le Worker, l'API et le Web. Web, `/alive`, Scalar, découverte OIDC Keycloak
et Gitea répondent 200.

Le contrôle `tools/dev/check-prereqs.ps1` choisit désormais le runtime d'après `ASPIRE_CONTAINER_RUNTIME` ; README et
guide local sont alignés. Le test `test_selected_podman_runtime_passes_when_docker_engine_is_unavailable` a été observé
rouge avant l'implémentation, puis les trois tests `test_check_prereqs.py` passent. Le contrôle réel retourne 10/10 OK.
Build Release avec `--warnaserror` : 0 avertissement/erreur. Suite backend : 752 réussis, 1 ignoré (test Bicep CLI
opt-in), 0 échec ; tests d'acceptation : 3/3. Docker Desktop reste arrêté, mais n'empêche plus ces validations sous
Podman.

Le blocage fonctionnel reste uniquement le format de `ImageScan.exceptions` ; aucune conversion n'a été inventée et
aucun `gate.py unblock`, commit, push ou accès Azure/Azure DevOps n'a été réalisé.
