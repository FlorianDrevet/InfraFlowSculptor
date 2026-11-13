# Généré par InfraFlowSculptor — projet {{PROJECT_CODE}}. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $RepositoryRoot,
    [Parameter(Mandatory)] [string] $BuildContext,
    [Parameter(Mandatory)] [string] $DockerfilePath,
    [Parameter(Mandatory)] [string] $ImageName,
    [string] $CacheDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepositoryPath {
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string] $RelativePath,
        [Parameter(Mandatory)] [string] $Description
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) {
        throw "$Description doit être un chemin relatif du dépôt."
    }

    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $candidate = [IO.Path]::GetFullPath((Join-Path $rootPath $RelativePath))
    $rootPrefix = $rootPath + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description sort du dépôt."
    }

    return $candidate
}

$contextPath = Resolve-RepositoryPath -Root $RepositoryRoot -RelativePath $BuildContext -Description 'Le contexte Docker'
$dockerfile = Resolve-RepositoryPath -Root $RepositoryRoot -RelativePath $DockerfilePath -Description 'Le Dockerfile'
if (-not (Test-Path -LiteralPath $contextPath -PathType Container)) { throw "Contexte Docker absent : $contextPath" }
if (-not (Test-Path -LiteralPath $dockerfile -PathType Leaf)) { throw "Dockerfile absent : $dockerfile" }
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw "Docker est absent de l’agent." }
if ($ImageName -notmatch '^[a-z0-9][a-z0-9._:/-]{0,254}$') { throw "Le nom de l’image locale est invalide." }

$buildId = [string]$env:BUILD_BUILDID
if ($buildId -notmatch '^[A-Za-z0-9_.-]+$') { throw 'BUILD_BUILDID doit identifier le builder Buildx.' }
$builder = 'ifs-{0}' -f $buildId
$cacheRoot = $null
$nextCache = $null
if (-not [string]::IsNullOrWhiteSpace($CacheDirectory)) {
    $cacheRoot = [IO.Path]::GetFullPath($CacheDirectory)
    if ($cacheRoot -eq [IO.Path]::GetPathRoot($cacheRoot)) { throw 'Le cache Buildx ne peut pas être la racine du disque.' }
    $cacheParent = Split-Path -Parent $cacheRoot
    if ([string]::IsNullOrWhiteSpace($cacheParent)) { throw 'Le cache Buildx doit avoir un dossier parent.' }
    [void][IO.Directory]::CreateDirectory($cacheParent)
    $nextCache = "$cacheRoot.next"
    if (Test-Path -LiteralPath $nextCache -PathType Container) {
        Remove-Item -LiteralPath $nextCache -Recurse -Force
    }
}

try {
    [void](& docker buildx create --name $builder --driver docker-container --use 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'Création du builder Docker Buildx impossible.' }
    [void](& docker buildx inspect --bootstrap 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'Initialisation du builder Docker Buildx impossible.' }

    $buildArguments = @('buildx', 'build', '--platform', 'linux/amd64')
    if ($null -ne $cacheRoot -and (Test-Path -LiteralPath $cacheRoot -PathType Container)) {
        $buildArguments += @('--cache-from', "type=local,src=$cacheRoot")
    }
    if ($null -ne $nextCache) {
        $buildArguments += @('--cache-to', "type=local,dest=$nextCache,mode=max")
    }
    $buildArguments += @('--file', $dockerfile, '--tag', $ImageName, '--load', $contextPath)
    $buildOutput = @(& docker @buildArguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Construction Buildx impossible : $($buildOutput -join ' ')" }

    if ($null -ne $nextCache -and (Test-Path -LiteralPath $nextCache -PathType Container)) {
        $backupCache = "$cacheRoot.previous"
        if (Test-Path -LiteralPath $backupCache -PathType Container) {
            Remove-Item -LiteralPath $backupCache -Recurse -Force
        }
        $hadExistingCache = Test-Path -LiteralPath $cacheRoot -PathType Container
        if ($hadExistingCache) { Move-Item -LiteralPath $cacheRoot -Destination $backupCache }
        try {
            Move-Item -LiteralPath $nextCache -Destination $cacheRoot
        }
        catch {
            if ($hadExistingCache -and (Test-Path -LiteralPath $backupCache -PathType Container)) {
                Move-Item -LiteralPath $backupCache -Destination $cacheRoot
            }
            throw
        }
        if (Test-Path -LiteralPath $backupCache -PathType Container) {
            Remove-Item -LiteralPath $backupCache -Recurse -Force
        }
    }
}
finally {
    [void](& docker buildx rm $builder 2>&1)
}
