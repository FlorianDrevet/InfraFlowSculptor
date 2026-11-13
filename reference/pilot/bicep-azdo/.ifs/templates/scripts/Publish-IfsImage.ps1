# Généré par InfraFlowSculptor — projet {{PROJECT_CODE}}. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ReleasePath,
    [Parameter(Mandatory)] [string] $LocalImageName,
    [Parameter(Mandatory)] [string] $Application,
    [Parameter(Mandatory)] [string] $ImageManifestPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$data = Get-Content -LiteralPath $ReleasePath -Raw | ConvertFrom-Json -AsHashtable -Depth 100
if ($data.schema -ne 'ifs-release/v1') { throw "Le fichier release n’utilise pas le schéma ifs-release/v1." }
$registryResources = @($data.dependencies |
    Where-Object { $_.component -eq 'platform' } |
    ForEach-Object { $_.resources })
$registryResource = @($registryResources |
    Where-Object { $_.id -match '/providers/Microsoft\.ContainerRegistry/registries/[^/]+$' } |
    Select-Object -First 1)
if ($registryResource.Count -eq 0) { throw 'La release ne référence aucun registre Azure Container Registry dans le composant platform.' }
$registryResourceId = [string]$registryResource[0].id
if ($registryResourceId -notmatch '^/subscriptions/([^/]+)/') { throw "L’identifiant de ressource du registre ne contient pas son abonnement." }
$registrySubscription = $Matches[1]
if ($registryResourceId -notmatch '/registries/([^/]+)$') { throw 'Le nom du registre est absent de son identifiant de ressource.' }
$registryName = $Matches[1]

$suffixOutput = @(& az cloud show --query suffixes.acrLoginServerEndpoint --output tsv 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Lecture du suffixe de connexion ACR du cloud Azure impossible : $($suffixOutput -join ' ')" }
$registrySuffix = ($suffixOutput -join [Environment]::NewLine).Trim()
if ($registrySuffix -notmatch '^\.[A-Za-z0-9][A-Za-z0-9.-]*$') { throw "Le suffixe de connexion ACR du cloud Azure est invalide." }
$loginServer = '{0}{1}' -f $registryName, $registrySuffix

$commit = [string]$env:BUILD_SOURCEVERSION
if ($commit -notmatch '^[a-fA-F0-9]{12,}$') { throw 'BUILD_SOURCEVERSION doit contenir un SHA Git.' }
$buildId = [string]$env:BUILD_BUILDID
if ($buildId -notmatch '^[A-Za-z0-9_.-]+$') { throw 'BUILD_BUILDID doit identifier le build.' }
if ($Application -notmatch '^[a-z][a-z0-9-]{1,39}$') { throw "Le nom de l’application est invalide." }
$tag = '{0}-{1}' -f $buildId, $commit.Substring(0, 12).ToLowerInvariant()
$repository = '{0}/{1}/{2}' -f $data.project, $data.component, $Application
$remoteImage = '{0}/{1}:{2}' -f $loginServer, $repository, $tag
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw "Docker est absent de l’agent." }

$imageInspection = @(& docker image inspect $LocalImageName 2>&1)
if ($LASTEXITCODE -ne 0) { throw "L’image locale contrôlée '$LocalImageName' est absente : $($imageInspection -join ' ')" }
[void](& az acr login --name $registryName --subscription $registrySubscription --only-show-errors 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Connexion au registre $registryName impossible." }
[void](& docker tag $LocalImageName $remoteImage 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Étiquetage de l’image pour le registre $registryName impossible." }
$pushOutput = @(& docker push $remoteImage 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Publication de l’image dans ACR impossible : $($pushOutput -join ' ')" }
$digestOutput = @(& az acr repository show --name $registryName --image ('{0}:{1}' -f $repository, $tag) --subscription $registrySubscription --query digest --output tsv 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Lecture de l’empreinte publiée dans ACR impossible : $($digestOutput -join ' ')" }
$digest = ($digestOutput -join [Environment]::NewLine).Trim()
if ($digest -notmatch '^sha256:[a-fA-F0-9]{64}$') { throw "L’empreinte publiée par ACR est invalide." }

$manifest = [ordered]@{
    schema = 'ifs-image/v1'
    project = [string]$data.project
    component = [string]$data.component
    application = $Application
    tag = $tag
    image = '{0}/{1}@{2}' -f $loginServer, $repository, $digest
    commit = $commit
    generatedAt = [DateTime]::UtcNow.ToString('o')
}
$directory = Split-Path -Parent $ImageManifestPath
New-Item -ItemType Directory -Path $directory -Force | Out-Null
[IO.File]::WriteAllText($ImageManifestPath, (ConvertTo-Json -InputObject $manifest -Depth 20), [Text.UTF8Encoding]::new($false))
Write-Output "Image publiée et contrôlée : $($manifest.image)"
