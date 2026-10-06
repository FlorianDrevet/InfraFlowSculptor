# Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ReleasePath,
    [Parameter(Mandatory)] [string] $ManifestPath,
    [Parameter(Mandatory)] [string] $PreviewPath,
    [Parameter(Mandatory)] [string] $TemplateFile,
    [Parameter(Mandatory)] [string] $ParameterFile,
    [Parameter(Mandatory)] [string] $OutputPath,
    [Parameter(Mandatory)] [string] $DefaultBranch,
    [Parameter(Mandatory)] [string] $ManifestPathInRepository,
    [string] $SqlScript,
    [string] $RunId = $env:BUILD_BUILDID
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'IfsRelease.psm1') -Force
$data = Read-IfsReleaseData -Path $ReleasePath
if (@($data.dataAccess).Count -gt 0 -and [string]::IsNullOrWhiteSpace($SqlScript)) {
    $SqlScript = Join-Path (Split-Path -Parent $ReleasePath) 'scripts/data-access.sql'
}
if (@($data.dataAccess).Count -gt 0 -and -not (Test-Path -LiteralPath $SqlScript -PathType Leaf)) {
    throw "Le script SQL d'accès aux données est introuvable : $SqlScript"
}

$serviceConnectionClientId = [string]$env:servicePrincipalId
if ([string]::IsNullOrWhiteSpace($serviceConnectionClientId)) {
    throw "La tâche AzureCLI doit exposer servicePrincipalId pour vérifier l'identité fédérée de déploiement."
}
$identityName = 'id-ifs-deploy-{0}-{1}' -f $data.project, $data.target
$resourceGroup = 'rg-ifs-{0}-{1}' -f $data.project, $data.target
$identityJson = @(& az identity show --name $identityName --resource-group $resourceGroup --subscription $data.subscriptionId --query '{clientId:clientId,principalId:principalId}' --output json 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Lecture de l'identité de déploiement $identityName impossible : $($identityJson -join ' ')" }
$identity = ($identityJson -join [Environment]::NewLine) | ConvertFrom-Json -Depth 10
if ([string]$identity.clientId -ne $serviceConnectionClientId) {
    throw "La connexion Azure authentifiée ne correspond pas à l'identité attendue $identityName."
}
if ([string]$identity.principalId -notmatch '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$') {
    throw "L'identité $identityName ne possède pas de principalId valide."
}

Invoke-IfsInfraDeploy `
    -ReleasePath $ReleasePath `
    -ManifestPath $ManifestPath `
    -PreviewPath $PreviewPath `
    -TemplateFile $TemplateFile `
    -ParameterFile $ParameterFile `
    -SqlScript $SqlScript `
    -OutputPath $OutputPath `
    -DefaultBranch $DefaultBranch `
    -ManifestPathInRepository $ManifestPathInRepository `
    -DeploymentIdentityObjectId ([string]$identity.principalId) `
    -RunId $RunId | Out-Null
Write-Output ('##vso[task.uploadfile]{0}' -f $OutputPath)
