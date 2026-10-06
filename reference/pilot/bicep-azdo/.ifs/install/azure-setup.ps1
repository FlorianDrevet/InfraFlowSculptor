# Généré par InfraFlowSculptor. Ne pas modifier : la prochaine publication remplacera ce fichier.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $AzureDevOpsOrganization,
    [Parameter(Mandatory)] [string] $AzureDevOpsProject,
    [Parameter(Mandatory)] [string] $TenantId,
    [switch] $WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$installRoot = Split-Path -Parent $PSCommandPath
$repositoryRoot = (Resolve-Path (Join-Path $installRoot '../..')).Path
$modulePath = Join-Path $installRoot 'scripts/IfsInstall.psm1'
Import-Module $modulePath -Force

if (-not $WhatIf) {
    Write-Information -MessageData 'Le script va creer ou reconciler des ressources Azure et des service connections Azure DevOps.' -InformationAction Continue
    Write-Information -MessageData 'Aucune ressource portant des donnees ne sera supprimee. Les identifiants federes ADO obsoletes seront revoques.' -InformationAction Continue
    $confirmation = Read-Host 'Tapez INSTALLER pour continuer'
    if ($confirmation -cne 'INSTALLER') { throw 'Installation annulee.' }
}

$null = Invoke-IfsAzureSetup -RepositoryPath $repositoryRoot -AzureDevOpsOrganization $AzureDevOpsOrganization -AzureDevOpsProject $AzureDevOpsProject -TenantId $TenantId -WhatIf:$WhatIf
