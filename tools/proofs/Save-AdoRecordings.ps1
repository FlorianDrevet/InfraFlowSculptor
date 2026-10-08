[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $Organization,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $Project,
    [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int] $RunId,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')][string] $Scenario,
    [string] $RepositoryPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ProofTools.psm1') -Force
if (-not (Get-Command ConvertFrom-Json).Parameters.ContainsKey('DateKind')) {
    throw 'Save-AdoRecordings.ps1 nécessite PowerShell 7.5 ou plus récent.'
}

$token = if (-not [string]::IsNullOrWhiteSpace($env:AZURE_DEVOPS_EXT_PAT)) {
    $env:AZURE_DEVOPS_EXT_PAT
} elseif (-not [string]::IsNullOrWhiteSpace($env:ADO_RECORDINGS_PAT)) {
    $env:ADO_RECORDINGS_PAT
} else {
    $secureToken = Read-Host -Prompt 'PAT Azure DevOps avec le droit Build (Read)' -AsSecureString
    $tokenPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($tokenPointer) }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($tokenPointer)
        $secureToken.Dispose()
    }
}
if ([string]::IsNullOrWhiteSpace($token)) { throw 'Le PAT Azure DevOps est vide.' }

$repositoryRoot = [IO.Path]::GetFullPath($RepositoryPath)
$gitRoot = (& git -C $repositoryRoot rev-parse --show-toplevel 2>$null | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitRoot)) {
    throw 'RepositoryPath doit viser la racine du clone InfraFlowSculptor.'
}
if ([IO.Path]::GetFullPath($gitRoot).TrimEnd('\') -ine $repositoryRoot.TrimEnd('\')) {
    throw 'RepositoryPath doit viser la racine du depot InfraFlowSculptor.'
}

$recordingsRoot = Join-Path $repositoryRoot 'src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings'
$finalOutputRoot = Join-Path $recordingsRoot $Scenario
if (Test-Path -LiteralPath $finalOutputRoot) {
    throw "Le scenario de capture existe deja ; choisissez un autre Scenario : $finalOutputRoot"
}
[void][IO.Directory]::CreateDirectory($recordingsRoot)
$outputRoot = Join-Path $recordingsRoot ('.' + $Scenario + '.capture-' + [guid]::NewGuid().ToString('N'))
$outputRootFullPath = [IO.Path]::GetFullPath($outputRoot)
$recordingsRootFullPath = [IO.Path]::GetFullPath($recordingsRoot).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + [IO.Path]::DirectorySeparatorChar
if (-not $outputRootFullPath.StartsWith($recordingsRootFullPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Le dossier temporaire de capture sort du depot.'
}
[void][IO.Directory]::CreateDirectory($outputRoot)
$stagingRoot = $outputRoot
try {

$organizationSegment = [Uri]::EscapeDataString($Organization)
$projectSegment = [Uri]::EscapeDataString($Project)
$baseUri = "https://dev.azure.com/$organizationSegment/$projectSegment/_apis"
$authorization = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$token"))
$headers = @{ Authorization = "Basic $authorization"; Accept = 'application/json' }
$apiVersion = '7.1'
$redactionContext = @{ Organization = $Organization; Project = $Project }

function Invoke-AdoJsonGet {
    param([Parameter(Mandatory)][string] $Uri, [Parameter(Mandatory)][string] $Kind)
    try {
        $response = Invoke-WebRequest -Uri $Uri -Method Get -Headers $headers -ErrorAction Stop
        $rawJson = [string]$response.Content
        $data = ConvertFrom-Json -InputObject $rawJson -AsHashtable -Depth 100 -DateKind String
        return [pscustomobject]@{ RawJson = $rawJson; Data = $data }
    }
    catch {
        throw "Lecture Azure DevOps impossible pour '$Kind'. Verifiez le run, les droits Build (Read) et le PAT."
    }
}

function Get-AdoPropertyValue {
    param([Parameter(Mandatory)][object] $Data, [Parameter(Mandatory)][string] $Name)
    if ($Data -is [System.Collections.IDictionary]) { return $Data[$Name] }
    $property = $Data.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

$pathComparison = if ([Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)) {
    [StringComparison]::OrdinalIgnoreCase
} else {
    [StringComparison]::Ordinal
}

function Get-ProofDirectoryPrefix {
    param([Parameter(Mandatory)][string] $Path)
    $fullPath = [IO.Path]::GetFullPath($Path)
    $trimmedPath = $fullPath.TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
    return $trimmedPath + [IO.Path]::DirectorySeparatorChar
}

function Save-SanitizedJson {
    param([Parameter(Mandatory)][string] $RawJson, [Parameter(Mandatory)][string] $Path)
    $safeJson = ConvertTo-ProofRedactedJson -Json $RawJson -Organization $Organization -Project $Project -Context $redactionContext
    [IO.File]::WriteAllText($Path, $safeJson, [Text.UTF8Encoding]::new($false))
}

function Get-SafeArtifactPath {
    param([Parameter(Mandatory)][string] $ArtifactName, [Parameter(Mandatory)][string] $RelativePath)

    if ($ArtifactName -notmatch '^(ifs-report|ifs-preview|ifs-app-report)(-[A-Za-z0-9._-]+)?$') {
        throw 'Le nom de l''artefact ne correspond pas au format attendu.'
    }
    $name = ConvertTo-ProofRedactedText -Text $RelativePath -Organization $Organization -Project $Project -Context $redactionContext
    $name = $name.Replace('\', '/')
    $segments = @($name.Split('/'))
    $invalidSegment = @($segments | Where-Object {
        $_ -in @('', '.', '..') -or $_ -match '[<>:"|?*\x00-\x1f]' -or $_.EndsWith(' ', [StringComparison]::Ordinal) -or $_.EndsWith('.', [StringComparison]::Ordinal) -or $_ -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?$'
    })
    if ([IO.Path]::IsPathRooted($name) -or $invalidSegment.Count -gt 0) {
        throw 'Le chemin fourni par un artefact ne peut pas etre extrait de facon sure.'
    }
    $root = Join-Path $outputRoot 'artifacts'
    $artifactsRoot = Get-ProofDirectoryPrefix -Path $root
    $path = Join-Path $root $ArtifactName
    foreach ($segment in $name.Split('/')) { $path = Join-Path $path $segment }
    $fullPath = [IO.Path]::GetFullPath($path)
    $artifactRoot = Get-ProofDirectoryPrefix -Path (Join-Path $root $ArtifactName)
    if (-not $artifactRoot.StartsWith($artifactsRoot, $pathComparison) -or
        -not $fullPath.StartsWith($artifactRoot, $pathComparison)) {
        throw 'Un chemin d''artefact sort du dossier de capture.'
    }
    return $fullPath
}

function Save-SanitizedArtifactFile {
    param([Parameter(Mandatory)][string] $SourcePath, [Parameter(Mandatory)][string] $TargetPath)

    $bytes = [IO.File]::ReadAllBytes($SourcePath)
    if ($bytes.Length -gt 25MB) { throw "Fichier d'artefact trop volumineux pour une capture lisible : $SourcePath" }
    try { $content = [Text.UTF8Encoding]::new($false, $true).GetString($bytes) }
    catch { throw "Un artefact non textuel a ete refuse pour eviter d'enregistrer des donnees non anonymisees : $SourcePath" }

    if ([IO.Path]::GetExtension($TargetPath) -ieq '.json') {
        $content = ConvertTo-ProofRedactedJson -Json $content -Organization $Organization -Project $Project -Context $redactionContext
    }
    else {
        $content = ConvertTo-ProofRedactedText -Text $content -Organization $Organization -Project $Project -Context $redactionContext
        if (-not $content.EndsWith("`n", [StringComparison]::Ordinal)) { $content += "`n" }
    }
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $TargetPath))
    [IO.File]::WriteAllText($TargetPath, $content, [Text.UTF8Encoding]::new($false))
}

$buildUri = "$baseUri/build/builds/$RunId`?api-version=$apiVersion"
$timelineUri = "$baseUri/build/builds/$RunId/timeline?api-version=$apiVersion"
$artifactListUri = "$baseUri/build/builds/$RunId/artifacts?api-version=$apiVersion"
$approvalUri = "$baseUri/pipelines/approvals?%24expand=steps&state=all&top=1000&api-version=$apiVersion"

$build = Invoke-AdoJsonGet -Uri $buildUri -Kind 'build'
$timeline = Invoke-AdoJsonGet -Uri $timelineUri -Kind 'timeline'
$artifactList = Invoke-AdoJsonGet -Uri $artifactListUri -Kind 'liste des artefacts'
$approvalList = Invoke-AdoJsonGet -Uri $approvalUri -Kind 'approbations'

Save-SanitizedJson -RawJson $build.RawJson -Path (Join-Path $outputRoot 'build.json')
Save-SanitizedJson -RawJson $timeline.RawJson -Path (Join-Path $outputRoot 'timeline.json')
Save-SanitizedJson -RawJson $artifactList.RawJson -Path (Join-Path $outputRoot 'artifacts.json')

$queueTimeText = [string](Get-AdoPropertyValue -Data $build.Data -Name 'queueTime')
if ([string]::IsNullOrWhiteSpace($queueTimeText)) { throw 'Le run ne contient pas queueTime ; la capture ne peut pas etre rattachee a un contexte de run.' }
try { $queueTime = [DateTimeOffset]::Parse($queueTimeText, [Globalization.CultureInfo]::InvariantCulture).ToUniversalTime() }
catch { throw 'Le champ queueTime du run n''est pas une date valide.' }
$finishTimeText = [string](Get-AdoPropertyValue -Data $build.Data -Name 'finishTime')
$finishTimeUtc = $null
if (-not [string]::IsNullOrWhiteSpace($finishTimeText)) {
    try { $finishTimeUtc = [DateTimeOffset]::Parse($finishTimeText, [Globalization.CultureInfo]::InvariantCulture).ToUniversalTime().ToString('o') }
    catch { throw 'Le champ finishTime du run n''est pas une date valide.' }
}
$approvalItems = @(Get-AdoPropertyValue -Data $approvalList.Data -Name 'value')
$approvalCapture = [ordered]@{
    captureScope = 'Project approvals snapshot at capture time; the API response does not identify a build or pipeline run.'
    runIdContext = $RunId
    queueTimeUtc = $queueTime.ToString('o')
    finishTimeUtc = $finishTimeUtc
    captureTimeUtc = [DateTime]::UtcNow.ToString('o')
    count = $approvalItems.Count
    possiblyTruncated = $approvalItems.Count -ge 1000
    value = $approvalItems
}
Save-SanitizedJson -RawJson (ConvertTo-Json -InputObject $approvalCapture -Depth 100) -Path (Join-Path $outputRoot 'approvals.json')

$artifactValues = @(Get-AdoPropertyValue -Data $artifactList.Data -Name 'value')
$wantedArtifacts = @($artifactValues | Where-Object {
    [string](Get-AdoPropertyValue -Data $_ -Name 'name') -match '^(ifs-report|ifs-preview|ifs-app-report)(-[A-Za-z0-9._-]+)?$'
})
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("ifs-ado-recordings-" + [guid]::NewGuid().ToString('N'))
$absoluteTempRoot = [IO.Path]::GetFullPath($tempRoot)
$absoluteSystemTemp = Get-ProofDirectoryPrefix -Path ([IO.Path]::GetTempPath())
if (-not $absoluteTempRoot.StartsWith($absoluteSystemTemp, $pathComparison)) {
    throw 'Le dossier temporaire de capture sort du dossier temporaire du systeme.'
}
[void][IO.Directory]::CreateDirectory($absoluteTempRoot)
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($artifact in $wantedArtifacts) {
        $artifactName = [string]$artifact.name
        $artifactNameForDisk = ConvertTo-ProofRedactedText -Text $artifactName -Organization $Organization -Project $Project -Context $redactionContext
        $zipPath = Join-Path $absoluteTempRoot (([guid]::NewGuid().ToString('N')) + '.zip')
        $escapedName = [Uri]::EscapeDataString($artifactName)
        $downloadUri = "$baseUri/build/builds/$RunId/artifacts?artifactName=$escapedName&api-version=$apiVersion"
        try {
            Invoke-WebRequest -Uri $downloadUri -Method Get -Headers (@{ Authorization = "Basic $authorization"; Accept = 'application/zip' }) -OutFile $zipPath -ErrorAction Stop | Out-Null
        }
        catch {
            throw "Telechargement de l'artefact '$artifactNameForDisk' impossible ; aucun contenu brut n'a ete conserve."
        }

        $zipBytes = [IO.File]::ReadAllBytes($zipPath)
        if ($zipBytes.Length -lt 4 -or $zipBytes[0] -ne 0x50 -or $zipBytes[1] -ne 0x4B) {
            throw "La reponse de l'artefact '$artifactNameForDisk' n'est pas une archive ZIP."
        }

        $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
        $extractionRoot = Join-Path $absoluteTempRoot ("extract-" + [guid]::NewGuid().ToString('N'))
        [void][IO.Directory]::CreateDirectory($extractionRoot)
        try {
            foreach ($entry in $archive.Entries) {
                if ([string]::IsNullOrWhiteSpace($entry.Name)) { continue }
                $entryPath = $entry.FullName.Replace('\', '/')
                $targetPath = Get-SafeArtifactPath -ArtifactName $artifactNameForDisk -RelativePath $entryPath
                $sourceFileName = [guid]::NewGuid().ToString('N') + [IO.Path]::GetExtension($entry.Name)
                $sourceFilePath = Join-Path $extractionRoot $sourceFileName
                $extractionPrefix = Get-ProofDirectoryPrefix -Path $extractionRoot
                if (-not [IO.Path]::GetFullPath($sourceFilePath).StartsWith($extractionPrefix, $pathComparison)) {
                    throw 'Un chemin temporaire d''artefact sort du dossier de capture.'
                }
                $sourceStream = $entry.Open()
                $targetStream = [IO.File]::Create($sourceFilePath)
                try { $sourceStream.CopyTo($targetStream) }
                finally { $targetStream.Dispose(); $sourceStream.Dispose() }
                Save-SanitizedArtifactFile -SourcePath $sourceFilePath -TargetPath $targetPath
            }
        }
        finally {
            $archive.Dispose()
            $extractionFullPath = [IO.Path]::GetFullPath($extractionRoot)
            if ($extractionFullPath.StartsWith((Get-ProofDirectoryPrefix -Path $absoluteTempRoot), $pathComparison) -and
                (Test-Path -LiteralPath $extractionFullPath -PathType Container)) {
                Remove-Item -LiteralPath $extractionRoot -Recurse -Force
            }
        }
    }
}
finally {
    if (Test-Path -LiteralPath $absoluteTempRoot -PathType Container) {
        Remove-Item -LiteralPath $absoluteTempRoot -Recurse -Force
    }
}

$finalOutputFullPath = [IO.Path]::GetFullPath($finalOutputRoot)
if (-not $finalOutputFullPath.StartsWith($recordingsRootFullPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Le dossier final de capture sort du depot.'
}
[IO.Directory]::Move($stagingRoot, $finalOutputRoot)
$outputRoot = $finalOutputRoot
}
finally {
    if (Test-Path -LiteralPath $stagingRoot -PathType Container) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}

$artifactSummary = @($wantedArtifacts | ForEach-Object { [string]$_.name })
Write-Information -MessageData ("Capture anonymisee enregistree : {0}. Artefacts captures : {1}. Approbations dans le snapshot projet : {2}." -f $outputRoot, $(if ($artifactSummary.Count -eq 0) { 'aucun' } else { $artifactSummary -join ', ' }), $approvalItems.Count) -InformationAction Continue
