# Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsLinux) { throw "Le scanner Trivy du pilote est installé uniquement sur l'agent Ubuntu." }

$pinsPath = Join-Path $PSScriptRoot '../../pins.json'
$pins = Get-Content -LiteralPath $pinsPath -Raw | ConvertFrom-Json -Depth 50
$version = [string]$pins.pipelineValidation.trivyVersion
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Version Trivy absente ou invalide dans $pinsPath." }
$tempRoot = Join-Path $env:AGENT_TEMPDIRECTORY "ifs-trivy-$version"
$installRoot = Join-Path $tempRoot 'bin'
$archiveName = "trivy_${version}_Linux-64bit.tar.gz"
$baseUrl = "https://github.com/aquasecurity/trivy/releases/download/v$version"
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null

if (-not (Test-Path -LiteralPath (Join-Path $installRoot 'trivy') -PathType Leaf)) {
    $archivePath = Join-Path $tempRoot $archiveName
    $checksumsPath = Join-Path $tempRoot "trivy_${version}_checksums.txt"
    Invoke-WebRequest -Uri "$baseUrl/$archiveName" -OutFile $archivePath
    Invoke-WebRequest -Uri "$baseUrl/trivy_${version}_checksums.txt" -OutFile $checksumsPath
    $checksumLine = Get-Content -LiteralPath $checksumsPath | Where-Object { $_ -match "\s\*?$([regex]::Escape($archiveName))$" } | Select-Object -First 1
    if (-not $checksumLine -or $checksumLine -notmatch '^([a-fA-F0-9]{64})\s+') { throw "Empreinte officielle absente pour $archiveName." }
    $expected = $Matches[1].ToLowerInvariant()
    $actual = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) { throw "L'archive Trivy $version ne correspond pas à son empreinte publiée." }
    & tar -xzf $archivePath -C $installRoot trivy
    if ($LASTEXITCODE -ne 0) { throw 'Extraction de Trivy impossible.' }
}

Write-Output "##vso[task.prependpath]$installRoot"
& (Join-Path $installRoot 'trivy') --version
if ($LASTEXITCODE -ne 0) { throw "Trivy $version ne démarre pas correctement." }
