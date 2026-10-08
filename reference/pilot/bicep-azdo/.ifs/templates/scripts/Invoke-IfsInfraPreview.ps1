# Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ReleasePath,
    [Parameter(Mandatory)] [string] $TemplateFile,
    [Parameter(Mandatory)] [string] $ParameterFile,
    [Parameter(Mandatory)] [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'IfsRelease.psm1') -Force
$data = Read-IfsReleaseData -Path $ReleasePath
$serviceConnectionClientId = [string]$env:servicePrincipalId
if ([string]::IsNullOrWhiteSpace($serviceConnectionClientId)) {
    throw 'La tâche AzureCLI doit exposer servicePrincipalId pour vérifier l''identité de déploiement.'
}
$deploymentIdentity = Get-IfsManagedIdentity -ReleaseData $data -Kind deploy
$applicationIdentity = Get-IfsManagedIdentity -ReleaseData $data -Kind app
if ([string]$deploymentIdentity.clientId -ne $serviceConnectionClientId) {
    throw "La connexion Azure authentifiée ne correspond pas à l'identité de déploiement attendue pour $($data.project)/$($data.target)."
}
Invoke-IfsInfraPreview `
    -ReleasePath $ReleasePath `
    -TemplateFile $TemplateFile `
    -ParameterFile $ParameterFile `
    -OutputDirectory $OutputDirectory `
    -DeploymentIdentityObjectId ([string]$deploymentIdentity.principalId) `
    -ApplicationIdentityObjectId ([string]$applicationIdentity.principalId) | Out-Null
