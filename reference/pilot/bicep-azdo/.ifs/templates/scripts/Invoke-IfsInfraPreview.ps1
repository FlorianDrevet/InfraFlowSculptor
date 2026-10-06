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
Invoke-IfsInfraPreview -ReleasePath $ReleasePath -TemplateFile $TemplateFile -ParameterFile $ParameterFile -OutputDirectory $OutputDirectory | Out-Null
