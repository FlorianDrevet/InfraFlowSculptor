# Recalcule les empreintes de la sortie de référence dans manifest.example.json.
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $ReferenceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch] $Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$referenceRoot = [IO.Path]::GetFullPath($ReferenceRoot)
$outputRoot = Join-Path $referenceRoot 'pilot/bicep-azdo'
$manifestPath = Join-Path $referenceRoot 'pilot/manifest.example.json'
if (-not (Test-Path -LiteralPath $outputRoot -PathType Container)) {
    throw "Sortie de référence introuvable : $outputRoot"
}
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Manifeste d'exemple introuvable : $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable -Depth 100
if ([string]::IsNullOrWhiteSpace([string]$manifest.project) -or [int]$manifest.revision -lt 1) {
    throw 'Le manifeste d''exemple doit déclarer un projet et une révision positive.'
}

$files = [System.Collections.Generic.List[object]]::new()
$relativePaths = [System.Collections.Generic.List[string]]::new()
foreach ($file in Get-ChildItem -LiteralPath $outputRoot -File -Recurse -Force) {
    $relativePath = [IO.Path]::GetRelativePath($outputRoot, $file.FullName).Replace('\', '/')
    $relativePaths.Add($relativePath)
}
$relativePaths.Sort([StringComparer]::Ordinal)
foreach ($relativePath in $relativePaths) {
    $filePath = Join-Path $outputRoot ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
    $files.Add([ordered]@{
        path = $relativePath
        sha256 = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    })
}

if ($files.Count -eq 0) { throw "La sortie de référence est vide : $outputRoot" }
$manifest.files = $files.ToArray()
$json = (ConvertTo-Json -InputObject $manifest -Depth 100) -replace "`r`n", "`n"
if (-not $json.EndsWith("`n", [StringComparison]::Ordinal)) { $json += "`n" }
$expectedBytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
if ($Check) {
    $actualBytes = [IO.File]::ReadAllBytes($manifestPath)
    if ([Convert]::ToBase64String($actualBytes) -cne [Convert]::ToBase64String($expectedBytes)) {
        throw 'manifest.example.json ne correspond pas aux fichiers générés ; exécutez Update-ManifestExample.ps1.'
    }
    Write-Information -MessageData ("OK : manifeste cohérent avec {0} fichiers gérés." -f $files.Count) -InformationAction Continue
    return
}

if ($PSCmdlet.ShouldProcess($manifestPath, 'Recalculer les empreintes SHA-256')) {
    [IO.File]::WriteAllBytes($manifestPath, $expectedBytes)
    Write-Information -MessageData ("Manifeste recalculé : {0} fichiers gérés." -f $files.Count) -InformationAction Continue
}
