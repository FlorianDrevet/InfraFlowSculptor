# Généré par InfraFlowSculptor — projet {{PROJECT_CODE}}. Ne pas modifier : la prochaine publication remplacera ce fichier. Personnalisation : voir README.ifs.md.
#requires -Version 7
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $RepositoryRoot,
    [Parameter(Mandatory)] [ValidatePattern('(?-i)\A[a-z][a-z0-9-]{1,39}\z')] [string] $Application,
    [Parameter(Mandatory)] [ValidatePattern('\A[0-9]+\z')] [string] $BuildId,
    [Parameter(Mandatory)] [ValidateSet('CRITICAL', 'HIGH', 'MEDIUM', 'LOW')] [string] $MinimumSeverity,
    [AllowNull()] [AllowEmptyString()] [string] $Exceptions = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$arguments = @('image', '--scanners', 'vuln')
if ($null -ne $Exceptions -and $Exceptions -cne '') {
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

$severitiesByMinimum = @{
    CRITICAL = 'CRITICAL'
    HIGH = 'CRITICAL,HIGH'
    MEDIUM = 'CRITICAL,HIGH,MEDIUM'
    LOW = 'CRITICAL,HIGH,MEDIUM,LOW'
}
$arguments += @('--image-src', 'docker', '--severity', $severitiesByMinimum[$MinimumSeverity], '--exit-code', '1', '--format', 'table')
$image = 'ifs-app-{0}-{1}' -f $Application, $BuildId
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("ifs-image-scan-{0}" -f [guid]::NewGuid().ToString('N'))
$previousTrivyConfig = [Environment]::GetEnvironmentVariable('TRIVY_CONFIG', 'Process')
$previousTrivyIgnoreFile = [Environment]::GetEnvironmentVariable('TRIVY_IGNOREFILE', 'Process')
[void][IO.Directory]::CreateDirectory($temporaryDirectory)
$scanFailure = $null
try {
    [Environment]::SetEnvironmentVariable('TRIVY_CONFIG', $null, 'Process')
    [Environment]::SetEnvironmentVariable('TRIVY_IGNOREFILE', $null, 'Process')
    Push-Location -LiteralPath $temporaryDirectory
    try {
        $output = @(& trivy @arguments $image 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0) { throw "Trivy a échoué pour $image :`n$($output -join [Environment]::NewLine)" }
    $output | Write-Output
}
catch {
    $scanFailure = $_.Exception
    throw
}
finally {
    try {
        if ([IO.Directory]::Exists($temporaryDirectory)) {
            Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
        }
    }
    catch {
        if ($null -eq $scanFailure) {
            throw
        }

        $scanFailure.Data['TemporaryDirectoryCleanupFailure'] = $_.Exception
    }
    finally {
        [Environment]::SetEnvironmentVariable('TRIVY_CONFIG', $previousTrivyConfig, 'Process')
        [Environment]::SetEnvironmentVariable('TRIVY_IGNOREFILE', $previousTrivyIgnoreFile, 'Process')
    }
}
