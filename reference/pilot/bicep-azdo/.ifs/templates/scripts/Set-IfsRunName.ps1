# Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ManifestPath,
    [Parameter(Mandatory)] [string] $Component,
    [Parameter(Mandatory)] [string] $Target
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Le manifeste de dépôt est introuvable : $ManifestPath"
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json -Depth 50
$project = if ($manifest.project) { [string]$manifest.project } else { [string]$manifest.projectCode }
$revision = if ($null -ne $manifest.revision) { [string]$manifest.revision } else { [string]$manifest.rev }
if ([string]::IsNullOrWhiteSpace($project) -or [string]::IsNullOrWhiteSpace($revision)) {
    throw 'Le manifeste doit définir le projet et la révision pour nommer le run.'
}

$runName = '{0} · {1} · rev {2} · {3}' -f $project, $Component, $revision, $Target
Write-Output ('##vso[build.updatebuildnumber]{0}' -f $runName)
