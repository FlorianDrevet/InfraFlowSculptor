# J0-23 — blocage sur le périmètre de sortie Azure DevOps — 2026-11-01

Blocage confirmé : CONFIRMÉ

## Vérifications effectuées

- J0-23 est l’étape courante sur impl/j0-generation. Le plan demande l’arborescence de reference-pilote.md §3 identique à la référence, des pipelines générés depuis DeploymentPlan, des modèles partagés embarqués, le kit d’installation et README.ifs.md.
- Le test ajouté pour la parité de l’arborescence complète échoue sur orders/infra/pipelines/release.yml : la référence contient un bloc parameters: de reprise P-08 avec des valeurs propres à une exécution antérieure, tandis que le plan neutre ne porte pas ces valeurs. Les autres fichiers inclus dans le périmètre de comparaison sont identiques.
- Les assets utilisés par les modèles ne sont pas tous dans l’arborescence J0-23 : azure-setup.ps1 importe .ifs/install/scripts/IfsInstall.psm1; le pipeline d’installation appelle .ifs/install/scripts/Install-Azdo.ps1 et .ifs/install/readiness.bicep; les modèles app-ci.yml et infra-ci.yml appellent plusieurs scripts qui ne figurent pas dans la liste J0-23. Le modèle infra-release.yml référence aussi conditionnellement le modèle de reprise et son script, absents de cette liste.
- Les descripteurs du catalogue DependencyCache et ImageScan pointent vers .ifs/templates/steps/*.yml, mais ces fichiers n’existent ni dans la référence ni dans le catalogue. Le plan neutre conserve désormais les étapes configurées et un traducteur Azure DevOps résout leurs paramètres, mais l’émetteur ne peut pas produire une étape exécutable ni l’injecter dans les modèles partagés sans ces ressources et points d’insertion.
- Le test moteur ApplicationPlanPreservesConfiguredPipelineStepsAndParameters a d’abord échoué parce que les étapes disparaissaient du plan, puis passe après ajout de ApplicationPlan.Steps. Les chemins des nouveaux pipelines, modèles et fichiers d’installation sont générés de façon déterministe.
- Suite backend complète : 743 réussis, 1 échec, 1 ignoré. L’unique échec est le test de parité de l’arborescence, sur les paramètres P-08 de orders/infra/pipelines/release.yml. Le test Bicep CLI reste ignoré par défaut.
- Build strict dotnet build src/backend/InfraFlowSculptor.slnx --no-restore --configuration Release -warnaserror : 0 avertissement, 0 erreur.
- pwsh -File reference/tools/Test-ReferencePipelines.ps1 : 25/25 fichiers Azure Pipelines de la référence conformes au schéma épinglé.
- pwsh -File reference/tools/Test-ReferenceDeterminism.ps1 : 79 fichiers de référence en UTF-8 sans BOM, LF, saut final et en-tête requis.
- git diff --check passe. python -m graphify update . a extrait 1 613 fichiers puis échoué à produire le rendu HTML, car le graphe de 5 777 nœuds dépasse la limite de visualisation de 5 000.
- Après le renouvellement du quota, Claude CLI Sonnet a analysé les specs, le plan, la référence et les émetteurs en lecture seule (`claude -p --restricted --model sonnet`). Claude n’a modifié aucun fichier et aucun appel à Opus n’a été fait. L’avis détaillé et les vérifications Codex figurent ci-dessous.
- Aucune recette Azure ou Azure DevOps, écriture de ressource externe ou dépense n’a été exécutée. L’implémentation partielle reste committée et poussée ; le gate reste BLOQUE.

## Condition bloquante

Le contenu demandé par J0-23 ne peut pas être produit simultanément à partir du seul plan neutre et rester identique à la référence actuelle : celle-ci contient des valeurs P-08 propres à un run, tandis que les modèles intégrés appellent des fichiers qui sont exclus du périmètre de sortie de §3. Les étapes du catalogue n’ont pas de corps YAML ni d’emplacement d’injection documenté.

## Options observées, sans arbitrage

1. Élargir la sortie de référence et J0-23 pour inclure les assets réellement requis, ajouter les modèles d’étapes et leurs points d’insertion, puis retirer ou déplacer les paramètres P-08 figés hors du pipeline généré.
2. Garder la liste de fichiers J0-23 inchangée et définir les paramètres P-08 et les assets exclus comme éléments client/hors génération, en ajustant la preuve de parité en conséquence et en reportant les étapes configurables.

## Question à Florian

Quelle option dois-je appliquer pour lever ce blocage J0-23 ? Claude recommande une variante ciblée de l’option 1 afin que l’arborescence générée soit exécutable et pilotée par le modèle, mais cela implique de mettre à jour la référence et le périmètre du plan. La décision reste à Florian conformément à AGENTS.md.

## Consultation Claude Sonnet et vérification Codex — 2026-11-01

### Recommandation de Claude

Claude recommande une extension ciblée de l’option 1. L’option 2 laisserait les pipelines et le kit incomplets alors que J0-23 exige l’arborescence entière, l’étape de référence exige une sortie exécutable et la spec demande que le comportement CI découle du plan. En revanche, les paramètres et la logique de reprise `shop42/orders/recover-prd-762` sont des résidus de la preuve P-08 : ils ne doivent pas entrer dans la génération.

Pour `DependencyCache` et `ImageScan`, Claude recommande de ne pas créer des modèles YAML absents de la référence ni de changer le catalogue immuable pour conserver des chemins de templates fictifs. Les étapes connues seraient traduites vers des paramètres explicites du pipeline applicatif et du script de build ; une étape sans traduction/support doit provoquer un échec explicite. Le paramètre de sévérité du scan doit venir du plan.

### Constats vérifiés indépendamment

- **Fermeture de ressources — confirmé.** Le projet Azure DevOps embarque huit modèles et quatre scripts, sans les schémas appelés par `IfsRelease.psm1`, `pins.json` lu par le module, ni tous les scripts/Bicep appelés par les modèles et le kit. `AzureDevOpsTemplateResources.ReadAll()` ne peut donc pas produire une sortie autonome conforme aux références de chemins.
- **Résidus P-08 — confirmé.** `orders/infra/pipelines/release.yml`, `infra-data-access-recovery.yml`, `infra-release.yml`, `infra-target-stages.yml` et `IfsRelease.psm1` contiennent des valeurs/gardes propres à `shop42`, `orders` et à l’opération de reprise #762. Elles ne sont pas portées par le plan neutre.
- **Étapes applicatives — confirmé.** Le dépôt ne contient aucun appel de `IStepTranslator` depuis l’émetteur Azure DevOps. Les descripteurs pointent vers `.ifs/templates/steps/*.yml`, absent de la référence ; `ApplicationStepPlan.Enabled` n’est pas pris en compte par `AzureDevOpsStepTranslator`, et celui-ci ne vérifie pas que le fichier de template existe.
- **Chemins d’application — confirmé statiquement.** `ApplicationAggregate.IsRelativePath` rejette les chemins absolus, segments parent et caractères de contrôle, mais accepte notamment les guillemets, `#`, `$(` et `${{`. `AzureDevOpsPipelineEmitter` écrit ces chemins dans des scalaires YAML non quotés. Aucun essai d’injection n’a été exécuté ; un test de rejet/échappement reste nécessaire.
- **Codes de projet — confirmé.** Les émetteurs du kit remplacent chaque sous-chaîne `shop` sans jetons dédiés, tandis que les ressources Azure DevOps embarquées gardent l’en-tête « projet shop ». L’installation de référence fixe également les cibles et la région France Central et refuse un manifeste autre que `shop`. La portée pilote de cette limite doit être explicite avant d’en faire une garantie générique.
- **Parité — confirmé.** `ReferenceOutputTests` utilise une liste manuelle de fichiers pipeline/installation/README plutôt qu’un inventaire exhaustif de la fermeture décrite par le plan ; un nouveau fichier peut donc être omis silencieusement du test.

### Décision encore requise

La recommandation ne constitue pas une autorisation de modifier la référence ou le texte du plan. Florian doit confirmer l’extension ciblée, notamment l’archivage des artefacts de reprise P-08, l’inventaire complet des assets, la traduction des deux étapes du catalogue et la limite pilote/générique du kit. Aucun changement de code ou de référence dépendant de cet arbitrage n’est engagé avant confirmation.
