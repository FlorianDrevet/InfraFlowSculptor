# Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $RepositoryRoot,
    [Parameter(Mandatory)] [string] $Component
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$pinsPath = Join-Path $RepositoryRoot '.ifs/pins.json'
$pins = Get-Content -LiteralPath $pinsPath -Raw | ConvertFrom-Json -Depth 50
$version = [string]$pins.bicepCliVersion
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Version Bicep absente ou invalide dans $pinsPath." }
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI doit être disponible sur l'agent Ubuntu pour installer et exécuter Bicep."
}

$installOutput = @(& az bicep install --version "v$version" 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Installation de Bicep $version impossible : $($installOutput -join ' ')" }
$versionOutput = @(& az bicep version 2>&1)
if ($LASTEXITCODE -ne 0 -or ($versionOutput -join ' ') -notmatch [regex]::Escape($version)) {
    throw "La CLI Bicep installée ne correspond pas à la version épinglée $version."
}

$infraRoot = Join-Path $RepositoryRoot "$Component/infra"
if (-not (Test-Path -LiteralPath $infraRoot -PathType Container)) { throw "Dossier d'infrastructure introuvable : $infraRoot" }

function Invoke-IfsBicepCheck {
    param([Parameter(Mandatory)] [string[]] $Arguments, [Parameter(Mandatory)] [string] $Description)
    $output = @(& az bicep @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $diagnostics = @($output | Where-Object { "$_" -notmatch '^WARNING: A new Bicep release is available:' })
    $text = $diagnostics -join [Environment]::NewLine
    if ($exitCode -ne 0) { throw "$Description a échoué (code $exitCode) :`n$text" }
    if ($text -match '(?im)(^|\s)warning\b|BCP\d{3}') { throw "$Description a produit un avertissement :`n$text" }
}

Push-Location $infraRoot
try {
    $mainPath = Join-Path $infraRoot 'main.bicep'
    if (-not (Test-Path -LiteralPath $mainPath -PathType Leaf)) { throw "Fichier manquant : $mainPath" }
    [void](Invoke-IfsBicepCheck -Arguments @('build', '--file', 'main.bicep', '--stdout') -Description "$Component/main.bicep build")
    [void](Invoke-IfsBicepCheck -Arguments @('lint', '--file', 'main.bicep') -Description "$Component/main.bicep lint")

    foreach ($file in Get-ChildItem -LiteralPath $infraRoot -Filter '*.bicep' -File -Recurse | Sort-Object FullName) {
        $relativePath = [IO.Path]::GetRelativePath($infraRoot, $file.FullName)
        [void](Invoke-IfsBicepCheck -Arguments @('build', '--file', $relativePath, '--stdout') -Description "$Component/$relativePath build")
    }
    foreach ($file in Get-ChildItem -LiteralPath $infraRoot -Filter '*.bicepparam' -File | Sort-Object Name) {
        [void](Invoke-IfsBicepCheck -Arguments @('build-params', '--file', $file.Name, '--stdout') -Description "$Component/$($file.Name) build")
    }
}
finally {
    Pop-Location
}

Write-Host "Contrôles Bicep réussis pour $Component avec Bicep $version."
