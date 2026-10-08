# Vérifie les pipelines de référence Azure DevOps contre leur schéma épinglé et contrôle leurs références locales.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$referenceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$pilotRoot = Join-Path $referenceRoot 'pilot'
$pipelineRoot = Join-Path $pilotRoot 'bicep-azdo'
$pinsPath = Join-Path $pilotRoot 'pins.json'
$schemaPath = Join-Path $PSScriptRoot 'schemas/azure-pipelines.json'
$runtimePinsPath = Join-Path $pipelineRoot '.ifs/pins.json'
$pins = Get-Content -LiteralPath $pinsPath -Raw | ConvertFrom-Json -Depth 100
$yamlVersion = [string]$pins.powerShellModules.'powershell-yaml'
$schemaPin = $pins.pipelineValidation.azurePipelinesSchema
$schemaHash = (Get-FileHash -LiteralPath $schemaPath -Algorithm SHA256).Hash.ToLowerInvariant()

if ([string]::IsNullOrWhiteSpace($yamlVersion)) { throw 'pins.json ne fixe pas la version de powershell-yaml.' }
if ($schemaHash -ne [string]$schemaPin.sha256) { throw 'Le schéma Azure Pipelines local ne correspond pas au SHA-256 épinglé.' }
if ([string]$schemaPin.source -notmatch [regex]::Escape([string]$schemaPin.commit)) { throw 'La source du schéma Azure Pipelines doit référencer son commit épinglé.' }

$yamlModule = Get-Module -ListAvailable -Name powershell-yaml | Where-Object { $_.Version.ToString() -eq $yamlVersion } | Select-Object -First 1
if (-not $yamlModule) { throw "Installer powershell-yaml $yamlVersion pour exécuter cette validation." }
Import-Module powershell-yaml -RequiredVersion $yamlVersion -ErrorAction Stop

if (-not (Test-Path -LiteralPath $runtimePinsPath -PathType Leaf)) { throw "Fichier runtime absent : $runtimePinsPath" }
if ((Get-FileHash -LiteralPath $runtimePinsPath -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $pinsPath -Algorithm SHA256).Hash) {
    throw '.ifs/pins.json doit rester synchronisé avec reference/pilot/pins.json.'
}

# Les modèles de release embarquent leurs schémas pour rester autonomes après publication.
$sourceSchemas = Join-Path $referenceRoot 'release-module/schemas'
$runtimeSchemas = Join-Path $pipelineRoot '.ifs/templates/schemas'
foreach ($sourceSchema in Get-ChildItem -LiteralPath $sourceSchemas -Filter '*.json' -File | Sort-Object Name) {
    $runtimeSchema = Join-Path $runtimeSchemas $sourceSchema.Name
    if (-not (Test-Path -LiteralPath $runtimeSchema -PathType Leaf)) { throw "Schéma embarqué manquant : $runtimeSchema" }
    if ((Get-FileHash -LiteralPath $sourceSchema.FullName -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $runtimeSchema -Algorithm SHA256).Hash) {
        throw "Le schéma embarqué diverge de sa source : $($sourceSchema.Name)"
    }
}

$officialSchema = Get-Content -LiteralPath $schemaPath -Raw | ConvertFrom-Json -AsHashtable -Depth 100
$templateSchemaPath = Join-Path ([IO.Path]::GetTempPath()) "ifs-azure-pipelines-stages-$PID.schema.json"
$templateSchema = [ordered]@{
    '$schema' = $officialSchema['$schema']
    oneOf = @($officialSchema.definitions.stagesTemplate)
    definitions = $officialSchema.definitions
}
[IO.File]::WriteAllText($templateSchemaPath, (ConvertTo-Json -InputObject $templateSchema -Depth 100 -Compress), [Text.UTF8Encoding]::new($false))

function ConvertTo-IfsSchemaDocument {
    param([Parameter(Mandatory)] [object] $Node, [string] $Context)

    if ($Node -is [System.Collections.IDictionary]) {
        if ($Context -eq 'env' -and @($Node.Keys | Where-Object { "$_" -match '^\$\{\{\s*each\b' }).Count -gt 0) {
            return [ordered]@{ IFS_SCHEMA_SECRET = '$(IFS_SCHEMA_SECRET)' }
        }
        $result = [ordered]@{}
        foreach ($key in $Node.Keys) {
            $childContext = if ([string]$key -eq 'env') { 'env' } else { $null }
            $normalizedKey = [regex]::Replace([string]$key, '\$\{\{.*?\}\}', 'sample')
            $result[$normalizedKey] = ConvertTo-IfsSchemaDocument -Node $Node[$key] -Context $childContext
        }
        return $result
    }

    if ($Node -is [System.Collections.IList] -and $Node -isnot [string]) {
        $result = [System.Collections.Generic.List[object]]::new()
        foreach ($item in $Node) {
            if ($item -is [System.Collections.IDictionary] -and $item.Count -eq 1) {
                $dynamicKey = [string]@($item.Keys)[0]
                if ($dynamicKey -match '^\$\{\{\s*(if|each)\b' -and $item[$dynamicKey] -is [System.Collections.IList]) {
                    foreach ($expandedItem in $item[$dynamicKey]) {
                        $result.Add((ConvertTo-IfsSchemaDocument -Node $expandedItem -Context $Context))
                    }
                    continue
                }
            }
            $result.Add((ConvertTo-IfsSchemaDocument -Node $item -Context $Context))
        }
        return ,$result.ToArray()
    }

    if ($Node -is [string]) { return [regex]::Replace($Node, '\$\{\{.*?\}\}', 'sample') }
    return $Node
}

function Get-TemplateReferences {
    param([Parameter(Mandatory)] [object] $Node)
    if ($Node -is [System.Collections.IDictionary]) {
        foreach ($key in $Node.Keys) {
            if ([string]$key -eq 'template' -and $Node[$key] -is [string]) { [string]$Node[$key] }
            Get-TemplateReferences -Node $Node[$key]
        }
    }
    elseif ($Node -is [System.Collections.IList] -and $Node -isnot [string]) {
        foreach ($item in $Node) { Get-TemplateReferences -Node $item }
    }
}

function Test-LocalReference {
    param([Parameter(Mandatory)] [string] $Path, [Parameter(Mandatory)] [string] $Description)
    $relativePath = $Path.TrimStart('/', '\')
    $candidate = [IO.Path]::GetFullPath((Join-Path $pipelineRoot $relativePath))
    $rootPrefix = [IO.Path]::GetFullPath($pipelineRoot) + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description sort du dossier de référence : $Path"
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "$Description introuvable : $Path" }
}

$yamlFiles = @(Get-ChildItem -LiteralPath $pipelineRoot -Filter '*.yml' -File -Recurse -Force | Sort-Object FullName)
if ($yamlFiles.Count -ne 24) { throw "24 fichiers YAML sont attendus après P-05 ; trouvé : $($yamlFiles.Count)." }
$expectedFiles = @(
    '.ifs/templates/infra-pr.yml', '.ifs/templates/infra-ci.yml', '.ifs/templates/infra-release.yml', '.ifs/templates/infra-target-stages.yml',
    '.ifs/templates/app-pr.yml', '.ifs/templates/app-ci.yml', '.ifs/templates/app-release.yml', '.ifs/templates/app-target-stage.yml',
    'core/infra/pipelines/pr.yml', 'core/infra/pipelines/ci.yml', 'core/infra/pipelines/release.yml',
    'data/infra/pipelines/pr.yml', 'data/infra/pipelines/ci.yml', 'data/infra/pipelines/release.yml',
    'platform/infra/pipelines/pr.yml', 'platform/infra/pipelines/ci.yml', 'platform/infra/pipelines/release.yml',
    'orders/infra/pipelines/pr.yml', 'orders/infra/pipelines/ci.yml', 'orders/infra/pipelines/release.yml',
    'orders/apps/api/pipelines/pr.yml', 'orders/apps/api/pipelines/ci.yml', 'orders/apps/api/pipelines/release.yml',
    '.ifs/install/install.pipeline.yml'
)
foreach ($expectedFile in $expectedFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $pipelineRoot $expectedFile) -PathType Leaf)) { throw "Pipeline P-04 manquante : $expectedFile" }
}

foreach ($file in $yamlFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    if (-not $text.StartsWith('# Généré par InfraFlowSculptor', [StringComparison]::Ordinal)) {
        throw "En-tête RG-GEN-08 absent : $($file.FullName)"
    }
    $document = ConvertFrom-Yaml -Yaml $text -Ordered
    if ($null -eq $document) { throw "Le YAML est vide : $($file.FullName)" }

    foreach ($template in @(Get-TemplateReferences -Node $document)) {
        Test-LocalReference -Path $template -Description 'Modèle Azure DevOps'
    }
    foreach ($match in [regex]::Matches($text, '\.ifs/templates/scripts/([A-Za-z0-9_.-]+\.ps1)')) {
        Test-LocalReference -Path ('.ifs/templates/scripts/{0}' -f $match.Groups[1].Value) -Description 'Script de pipeline'
    }
    foreach ($match in [regex]::Matches($text, '\.ifs/install/scripts/([A-Za-z0-9_.-]+\.ps1)')) {
        Test-LocalReference -Path ('.ifs/install/scripts/{0}' -f $match.Groups[1].Value) -Description 'Script du kit installation'
    }

    $isTemplate = $file.FullName.StartsWith((Join-Path $pipelineRoot '.ifs/templates'), [StringComparison]::OrdinalIgnoreCase)
    $schema = if ($isTemplate) { $templateSchemaPath } else { $schemaPath }
    $schemaDocument = if ($isTemplate) { ConvertTo-IfsSchemaDocument -Node $document -Context $null } else { $document }
    $json = ConvertTo-Json -InputObject $schemaDocument -Depth 100 -Compress
    try { $isValid = Test-Json -Json $json -SchemaFile $schema -ErrorAction Stop }
    catch { throw "Le schéma Azure Pipelines refuse $($file.FullName) : $($_.Exception.Message)" }
    if (-not $isValid) { throw "Le schéma Azure Pipelines refuse $($file.FullName)." }

    if ($file.Name -eq 'pr.yml') {
        $templatePath = @($document.extends.template)[0]
        $templateFullPath = Join-Path $pipelineRoot ([string]$templatePath)
        $templateText = Get-Content -LiteralPath $templateFullPath -Raw
        if ($text -match '(?i)AzureCLI@|AzurePowerShell@|azureSubscription\s*:' -or
            $templateText -match '(?i)AzureCLI@|AzurePowerShell@|azureSubscription\s*:') {
            throw "La pipeline PR ne doit pas posséder de connexion Azure : $($file.FullName)"
        }
    }
    Write-Host ('OK {0}' -f [IO.Path]::GetRelativePath($pipelineRoot, $file.FullName).Replace('\', '/'))
}

# Le dépôt pilote a un seul écrivain de secret : core. Les autres composants ne doivent pas connaître sa variable.
$corePipeline = Get-Content -LiteralPath (Join-Path $pipelineRoot 'core/infra/pipelines/release.yml') -Raw
if ($corePipeline -notmatch '(?m)^\s*- name:\s*MAIN_PAYMENTS_API_KEY\s*$' -or
    $corePipeline -notmatch '(?m)^\s*value:\s*\$\(MAIN_PAYMENTS_API_KEY\)\s*$') {
    throw 'La pipeline core doit mapper explicitement MAIN_PAYMENTS_API_KEY à son étape de release.'
}
foreach ($component in @('data', 'platform', 'orders')) {
    $componentPipeline = Get-Content -LiteralPath (Join-Path $pipelineRoot "$component/infra/pipelines/release.yml") -Raw
    if ($componentPipeline -match 'MAIN_PAYMENTS_API_KEY|ifs-shop-(dev|prd)') {
        throw "Le composant consommateur $component ne doit pas recevoir le groupe ni le mapping du secret core."
    }
}

# Le build publie vers le registre partagé; chaque déploiement utilise l'identité de sa cible.
$appCiTemplate = ConvertFrom-Yaml -Yaml (Get-Content -LiteralPath (Join-Path $pipelineRoot '.ifs/templates/app-ci.yml') -Raw) -Ordered
$appCiTask = @($appCiTemplate.stages[0].jobs[0].steps | Where-Object { $_ -is [System.Collections.IDictionary] -and $_.Contains('task') -and $_['task'] -eq 'AzureCLI@2' }) | Select-Object -First 1
if ($null -eq $appCiTask -or $appCiTask.inputs.azureSubscription -ne '${{ parameters.registryServiceConnection }}') {
    throw 'Le build applicatif doit utiliser la connexion applicative de la cible du registre.'
}
$appCiPipeline = ConvertFrom-Yaml -Yaml (Get-Content -LiteralPath (Join-Path $pipelineRoot 'orders/apps/api/pipelines/ci.yml') -Raw) -Ordered
if ($appCiPipeline.extends.parameters.registryServiceConnection -ne 'ifs-shop-shared-app') {
    throw "Le build de l'application témoin doit publier vers le registre de la cible shared."
}
$appTargetTemplate = ConvertFrom-Yaml -Yaml (Get-Content -LiteralPath (Join-Path $pipelineRoot '.ifs/templates/app-target-stage.yml') -Raw) -Ordered
$appDeployJob = $appTargetTemplate.stages[0].jobs[0]
if ($appDeployJob.environment -ne '${{ parameters.project }}-${{ parameters.target.name }}') {
    throw "Le déploiement applicatif doit cibler l'environnement de sa cible."
}
$appDeployTask = @($appDeployJob.strategy.runOnce.deploy.steps | Where-Object { $_ -is [System.Collections.IDictionary] -and $_.Contains('task') -and $_['task'] -eq 'AzureCLI@2' }) | Select-Object -First 1
if ($null -eq $appDeployTask -or $appDeployTask.inputs.azureSubscription -ne 'ifs-${{ parameters.project }}-${{ parameters.target.name }}-app') {
    throw 'Chaque déploiement applicatif doit utiliser la connexion de sa cible, distincte de celle du registre.'
}
$appReleasePipelineText = Get-Content -LiteralPath (Join-Path $pipelineRoot 'orders/apps/api/pipelines/release.yml') -Raw
if ($appReleasePipelineText -match 'appServiceConnection|registryServiceConnection') {
    throw 'La pipeline de livraison ne doit pas fixer une connexion shared pour toutes les cibles.'
}

$stageTemplate = ConvertFrom-Yaml -Yaml (Get-Content -LiteralPath (Join-Path $pipelineRoot '.ifs/templates/infra-target-stages.yml') -Raw) -Ordered
if ($stageTemplate.stages.Count -ne 2 -or $stageTemplate.stages[0].stage -notmatch '^Preview_' -or $stageTemplate.stages[1].stage -notmatch '^Deploy_') {
    throw 'Le modèle infrastructure doit émettre Preview puis Deploy pour chaque cible.'
}
if ($stageTemplate.stages[0].jobs[0].Contains('environment')) { throw 'Le stage Preview ne doit déclarer aucun environnement protégé.' }
if ($stageTemplate.stages[1].jobs[0].environment -ne '${{ parameters.project }}-${{ parameters.target.name }}') {
    throw "Le stage Deploy doit cibler l'environnement associe au projet et a la cible."
}
if ($stageTemplate.stages[1].lockBehavior -ne 'sequential') { throw 'Le stage Deploy doit conserver le verrou séquentiel de la cible.' }
if ([regex]::Matches((Get-Content -LiteralPath (Join-Path $pipelineRoot '.ifs/templates/infra-target-stages.yml') -Raw), '(?m)^\s+- group: ifs-').Count -ne 2) {
    throw 'Le groupe de variables doit être lié aux stages Preview et Deploy des seuls composants écrivains.'
}

Remove-Item -LiteralPath $templateSchemaPath -Force
Write-Host "Tous les $($yamlFiles.Count) fichiers Azure Pipelines sont valides contre le schéma épinglé."
