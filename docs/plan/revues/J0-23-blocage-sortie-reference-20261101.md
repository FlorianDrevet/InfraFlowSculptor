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
- Une consultation demandée à Claude CLI avec --restricted --model sonnet a échoué sur la limite de quota de cinq heures. Aucun appel à Opus n’a été fait.
- Aucune recette Azure ou Azure DevOps, écriture de ressource externe ou dépense n’a été exécutée. Sur demande explicite de Florian, cet état partiel est commité et poussé dans l’attente de la revue Claude; le gate reste BLOQUE.

## Condition bloquante

Le contenu demandé par J0-23 ne peut pas être produit simultanément à partir du seul plan neutre et rester identique à la référence actuelle : celle-ci contient des valeurs P-08 propres à un run, tandis que les modèles intégrés appellent des fichiers qui sont exclus du périmètre de sortie de §3. Les étapes du catalogue n’ont pas de corps YAML ni d’emplacement d’injection documenté.

## Options observées, sans arbitrage

1. Élargir la sortie de référence et J0-23 pour inclure les assets réellement requis, ajouter les modèles d’étapes et leurs points d’insertion, puis retirer ou déplacer les paramètres P-08 figés hors du pipeline généré.
2. Garder la liste de fichiers J0-23 inchangée et définir les paramètres P-08 et les assets exclus comme éléments client/hors génération, en ajustant la preuve de parité en conséquence et en reportant les étapes configurables.

## Question à Florian

Quelle option dois-je appliquer pour lever ce blocage J0-23 ? Je recommande l’option 1 afin que l’arborescence générée soit exécutable et pilotée par le modèle, mais cela implique de mettre à jour la référence et le périmètre du plan.
