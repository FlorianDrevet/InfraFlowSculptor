# Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ReleasePath,
    [Parameter(Mandatory)] [string] $ImageManifestPath,
    [Parameter(Mandatory)] [string] $Application,
    [Parameter(Mandatory)] [string] $Target,
    [Parameter(Mandatory)] [string] $OutputPath,
    [ValidateRange(1, 300)] [int] $HealthTimeoutSeconds = 300
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'IfsRelease.psm1') -Force
$data = Read-IfsReleaseData -Path $ReleasePath
if ($data.target -ne $Target) { throw "La donnée de release $ReleasePath ne cible pas $Target." }
$appState = @($data.appOwnedState | Where-Object { $_.kind -eq 'containerAppImage' })
if ($appState.Count -ne 1) { throw "La release doit définir exactement une Container App à livrer ; trouvé : $($appState.Count)." }

$imageManifest = Get-Content -LiteralPath $ImageManifestPath -Raw | ConvertFrom-Json -Depth 20
if ($imageManifest.schema -ne 'ifs-image/v1' -or $imageManifest.application -ne $Application -or $imageManifest.component -ne $data.component) {
    throw 'Le manifeste image ne correspond pas à cette application et à cette release.'
}
if ([string]$imageManifest.image -notmatch '^.+@sha256:[a-fA-F0-9]{64}$') { throw 'Le manifeste image ne contient pas une référence immuable avec empreinte SHA-256.' }

$resourceId = [string]$appState[0].resourceId
$fqdn = [string](& az containerapp show --ids $resourceId --query properties.configuration.ingress.fqdn --output tsv 2>&1)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($fqdn) -or $fqdn -match '^(None|null)$') {
    throw "Lecture du domaine de santé de la Container App impossible : $resourceId"
}
$healthUrl = [uri]('https://{0}/health' -f $fqdn.Trim())
Invoke-IfsAppDeploy `
    -Application $Application `
    -Component ([string]$data.component) `
    -Target $Target `
    -ResourceId $resourceId `
    -Image ([string]$imageManifest.image) `
    -HealthUrl $healthUrl `
    -OutputPath $OutputPath `
    -HealthTimeoutSeconds $HealthTimeoutSeconds | Out-Null
Write-Output ('##vso[task.uploadfile]{0}' -f $OutputPath)
