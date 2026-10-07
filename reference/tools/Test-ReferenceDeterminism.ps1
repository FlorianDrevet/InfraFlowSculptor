# Vérifie les conventions de fichiers de la sortie de référence générée.
[CmdletBinding()]
param(
    [string] $PipelineRoot = (Join-Path $PSScriptRoot '../pilot/bicep-azdo')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$pipelineRoot = [IO.Path]::GetFullPath($PipelineRoot)
if (-not (Test-Path -LiteralPath $pipelineRoot -PathType Container)) {
    throw "Sortie de référence introuvable : $pipelineRoot"
}

$commentPrefixes = @{
    '.bicep' = '// Généré par InfraFlowSculptor'
    '.bicepparam' = '// Généré par InfraFlowSculptor'
    '.md' = '<!-- Généré par InfraFlowSculptor'
    '.ps1' = '# Généré par InfraFlowSculptor'
    '.psm1' = '# Généré par InfraFlowSculptor'
    '.sql' = '-- Généré par InfraFlowSculptor'
    '.yml' = '# Généré par InfraFlowSculptor'
    '.yaml' = '# Généré par InfraFlowSculptor'
}
$utf8Strict = [Text.UTF8Encoding]::new($false, $true)
$files = @(Get-ChildItem -LiteralPath $pipelineRoot -File -Recurse | Sort-Object FullName)
if ($files.Count -eq 0) { throw "La sortie de référence est vide : $pipelineRoot" }

$errors = [System.Collections.Generic.List[string]]::new()
foreach ($file in $files) {
    $relativePath = [IO.Path]::GetRelativePath($pipelineRoot, $file.FullName).Replace('\', '/')
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    if ($bytes.Length -eq 0) {
        $errors.Add("$relativePath : fichier vide")
        continue
    }

    if (($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) -or
        ($bytes.Length -ge 2 -and (($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) -or ($bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF))) -or
        ($bytes.Length -ge 4 -and (($bytes[0] -eq 0x00 -and $bytes[1] -eq 0x00 -and $bytes[2] -eq 0xFE -and $bytes[3] -eq 0xFF) -or ($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE -and $bytes[2] -eq 0x00 -and $bytes[3] -eq 0x00)))) {
        $errors.Add("$relativePath : BOM détecté")
    }

    try { $content = $utf8Strict.GetString($bytes) }
    catch { $errors.Add("$relativePath : contenu non UTF-8"); continue }
    if ($content.Contains("`r")) { $errors.Add("$relativePath : fin de ligne CR détectée (LF attendu)") }
    if ($bytes[$bytes.Length - 1] -ne 0x0A) { $errors.Add("$relativePath : saut de ligne final absent") }

    $prefix = $commentPrefixes[$file.Extension.ToLowerInvariant()]
    if ($prefix -and -not $content.StartsWith($prefix, [StringComparison]::Ordinal)) {
        $errors.Add("$relativePath : en-tête généré absent ou invalide")
    }
}

if ($errors.Count -gt 0) {
    throw ("{0} problème(s) de déterminisme :`n - {1}" -f $errors.Count, ($errors -join "`n - "))
}

Write-Information -MessageData ("OK : {0} fichiers UTF-8 sans BOM, LF, avec saut final et en-tête requis." -f $files.Count) -InformationAction Continue
