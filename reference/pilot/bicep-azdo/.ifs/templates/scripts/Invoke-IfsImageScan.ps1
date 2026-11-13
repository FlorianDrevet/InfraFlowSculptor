# Généré par InfraFlowSculptor — projet {{PROJECT_CODE}}. Ne pas modifier : la prochaine publication remplacera ce fichier. Personnalisation : voir README.ifs.md.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $RepositoryRoot,
    [Parameter(Mandatory)] [ValidateNotNullOrEmpty()] [string] $Application,
    [Parameter(Mandatory)] [ValidateNotNullOrEmpty()] [string] $BuildId,
    [Parameter(Mandatory)] [ValidateSet('CRITICAL', 'HIGH', 'MEDIUM', 'LOW')] [string] $MinimumSeverity,
    [AllowNull()] [AllowEmptyString()] [string] $Exceptions = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$arguments = @('image', '--scanners', 'vuln')
if (-not [string]::IsNullOrWhiteSpace($Exceptions)) {
    if ($Exceptions -ne $Exceptions.Trim()) {
        throw 'Le chemin du fichier exceptions ne peut pas commencer ou finir par un espace.'
    }
    if ([IO.Path]::IsPathRooted($Exceptions) -or $Exceptions -match '^[A-Za-z]:' -or $Exceptions.Contains('\') -or $Exceptions.StartsWith('/')) {
        throw 'Le fichier exceptions doit être un chemin relatif au dépôt.'
    }

    $segments = $Exceptions.Split([char[]]@('/'), [StringSplitOptions]::None)
    if (@($segments | Where-Object { [string]::IsNullOrEmpty($_) -or $_ -in @('.', '..') }).Count -gt 0) {
        throw 'Le fichier exceptions doit être un chemin relatif au dépôt sans segments vides, . ou .. .'
    }
    if ($segments[-1] -cne '.trivyignore') {
        throw 'Le fichier exceptions doit porter le nom .trivyignore.'
    }

    if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
        throw "Racine du dépôt introuvable : $RepositoryRoot"
    }
    $repositoryPath = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $candidatePath = $repositoryPath
    for ($index = 0; $index -lt $segments.Count; $index++) {
        $candidatePath = Join-Path $candidatePath $segments[$index]
        if (-not (Test-Path -LiteralPath $candidatePath)) {
            throw "Fichier exceptions introuvable dans le dépôt : $Exceptions"
        }

        $item = Get-Item -LiteralPath $candidatePath
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Le chemin du fichier exceptions ne peut pas traverser de lien symbolique : $Exceptions"
        }
        if ($index -lt $segments.Count - 1 -and -not $item.PSIsContainer) {
            throw "Un dossier du chemin exceptions est introuvable : $Exceptions"
        }
        if ($index -eq $segments.Count - 1 -and $item.PSIsContainer) {
            throw "Le fichier exceptions est un dossier : $Exceptions"
        }
    }

    $ignoreFile = [IO.Path]::GetFullPath($candidatePath)
    $rootPrefix = $repositoryPath + [IO.Path]::DirectorySeparatorChar
    $pathComparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    if (-not $ignoreFile.StartsWith($rootPrefix, $pathComparison)) {
        throw 'Le fichier exceptions sort du dépôt.'
    }
    $arguments += @('--ignorefile', $ignoreFile)
}

$arguments += @('--severity', $MinimumSeverity, '--exit-code', '1', '--format', 'table')
$image = 'ifs-app-{0}-{1}' -f $Application, $BuildId
$output = @(& trivy @arguments $image 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Trivy a échoué pour $image : $($output -join ' ')" }
