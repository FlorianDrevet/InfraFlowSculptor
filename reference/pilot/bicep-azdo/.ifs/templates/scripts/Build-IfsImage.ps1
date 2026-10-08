# Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $RepositoryRoot,
    [Parameter(Mandatory)] [string] $ReleasePath,
    [Parameter(Mandatory)] [string] $BuildContext,
    [Parameter(Mandatory)] [string] $DockerfilePath,
    [Parameter(Mandatory)] [string] $Application,
    [Parameter(Mandatory)] [string] $ImageManifestPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'IfsRelease.psm1') -Force
$data = Read-IfsReleaseData -Path $ReleasePath
$registryResources = @($data.dependencies | Where-Object { $_.component -eq 'platform' } | ForEach-Object { $_.resources })
$registryResource = @($registryResources | Where-Object { $_.id -match '/providers/Microsoft\.ContainerRegistry/registries/[^/]+$' }) | Select-Object -First 1
if (-not $registryResource) { throw 'La release ne référence aucun registre Azure Container Registry dans le composant platform.' }
$registryResourceId = [string]$registryResource.id
if ($registryResourceId -notmatch '^/subscriptions/([^/]+)/') { throw "L'identifiant de ressource du registre ne contient pas son abonnement." }
$registrySubscription = $Matches[1]
if ($registryResourceId -notmatch '/registries/([^/]+)$') { throw 'Le nom du registre est absent de son identifiant de ressource.' }
$registryName = $Matches[1]
$registryInfoJson = @(& az acr show --name $registryName --subscription $registrySubscription --query '{name:name,loginServer:loginServer}' --output json 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Lecture du registre $registryName impossible : $($registryInfoJson -join ' ')" }
$registry = ($registryInfoJson -join [Environment]::NewLine) | ConvertFrom-Json -Depth 10
if ([string]::IsNullOrWhiteSpace([string]$registry.loginServer)) { throw 'Azure Container Registry ne renvoie aucun loginServer.' }
[void](& az acr login --name $registryName --subscription $registrySubscription --only-show-errors 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Connexion au registre $registryName impossible." }

$contextPath = Join-Path $RepositoryRoot $BuildContext
$dockerfile = Join-Path $RepositoryRoot $DockerfilePath
if (-not (Test-Path -LiteralPath $contextPath -PathType Container)) { throw "Contexte Docker absent : $contextPath" }
if (-not (Test-Path -LiteralPath $dockerfile -PathType Leaf)) { throw "Dockerfile absent : $dockerfile" }
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw "Docker est absent de l'agent." }
if (-not (Get-Command trivy -ErrorAction SilentlyContinue)) { throw "Trivy doit être installé avant la construction de l'image." }

$repository = '{0}/{1}/{2}' -f $data.project, $data.component, $Application
$commit = [string]$env:BUILD_SOURCEVERSION
if ($commit -notmatch '^[a-fA-F0-9]{12,}$') { throw 'BUILD_SOURCEVERSION doit contenir un SHA Git.' }
$tag = '{0}-{1}' -f $env:BUILD_BUILDID, $commit.Substring(0, 12).ToLowerInvariant()
$image = '{0}/{1}:{2}' -f $registry.loginServer, $repository, $tag
$cache = '{0}/{1}:buildcache' -f $registry.loginServer, $repository
$builder = 'ifs-{0}' -f $env:BUILD_BUILDID

try {
    [void](& docker buildx create --name $builder --driver docker-container --use 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'Création du builder Docker Buildx impossible.' }
    [void](& docker buildx inspect --bootstrap 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'Initialisation du builder Docker Buildx impossible.' }
    $buildOutput = @(& docker buildx build --platform linux/amd64 --cache-from "type=registry,ref=$cache" --cache-to "type=registry,ref=$cache,mode=max" --file $dockerfile --tag $image --load $contextPath 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Construction Buildx impossible : $($buildOutput -join ' ')" }

    $scanOutput = @(& trivy image --scanners vuln --severity CRITICAL --exit-code 1 --format table $image 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Trivy a bloqué l'image à cause d'une vulnérabilité critique ou d'un échec de scan : $($scanOutput -join ' ')" }

    $pushOutput = @(& docker push $image 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Publication de l'image dans ACR impossible : $($pushOutput -join ' ')" }
    $digest = [string](& az acr repository show --name $registryName --image "$repository`:$tag" --subscription $registrySubscription --query digest --output tsv 2>&1)
    if ($LASTEXITCODE -ne 0 -or $digest -notmatch '^sha256:[a-fA-F0-9]{64}$') { throw "Impossible de lire l'empreinte SHA-256 publiée par ACR." }

    $manifest = [ordered]@{
        schema = 'ifs-image/v1'
        project = [string]$data.project
        component = [string]$data.component
        application = $Application
        tag = $tag
        image = '{0}/{1}@{2}' -f $registry.loginServer, $repository, $digest
        commit = $commit
        generatedAt = [DateTime]::UtcNow.ToString('o')
    }
    $directory = Split-Path -Parent $ImageManifestPath
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    [IO.File]::WriteAllText($ImageManifestPath, (ConvertTo-Json -InputObject $manifest -Depth 20), [Text.UTF8Encoding]::new($false))
    Write-Output "Image publiée et contrôlée : $($manifest.image)"
}
finally {
    [void](& docker buildx rm $builder 2>&1)
}
