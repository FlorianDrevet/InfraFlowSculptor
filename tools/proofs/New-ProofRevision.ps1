[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory)][string] $RepositoryPath,
    [Parameter(Mandatory)][ValidateSet('2', '3', '4', '5', 'p2', 'p3-role')][string] $Revision
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Import-Module (Join-Path $PSScriptRoot 'ProofTools.psm1') -Force

$destinationRoot = [IO.Path]::GetFullPath($RepositoryPath)
if (-not (Test-Path -LiteralPath $destinationRoot -PathType Container)) {
    throw "Le clone Azure Repos doit deja exister : $destinationRoot"
}
$gitRoot = (& git -C $destinationRoot rev-parse --show-toplevel 2>$null | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitRoot)) {
    throw 'RepositoryPath doit etre un clone Git Azure Repos.'
}
$trimChars = [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
if ([IO.Path]::GetFullPath($gitRoot).TrimEnd($trimChars) -ine $destinationRoot.TrimEnd($trimChars)) {
    throw 'RepositoryPath doit viser la racine du clone Git.'
}
Assert-ProofSeparateRepository -SourceRepository $repoRoot -TargetRepository $destinationRoot

$manifestPath = Join-Path $destinationRoot '.ifs/manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'Le clone ne contient pas .ifs/manifest.json ; executez Publish-PilotReference.ps1 en premier.'
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable -Depth 100
if ($manifest.schema -cne 'ifs-manifest/v1' -or [string]::IsNullOrWhiteSpace([string]$manifest.project)) {
    throw 'Le manifeste du clone n''est pas un ifs-manifest/v1 valide.'
}
$projectCode = [string]$manifest.project
Assert-ProofProjectCode -ProjectCode $projectCode

$revisionDirectoryName = if ($Revision -match '^[0-9]+$') { "rev$Revision" } else { "scenario-$Revision" }
$revisionRoot = Join-Path $repoRoot "reference/pilot/revisions/$revisionDirectoryName"
$patchPath = Join-Path $revisionRoot 'changes.patch'
$metadataPath = Join-Path $revisionRoot 'revision.json'
if (-not (Test-Path -LiteralPath $patchPath -PathType Leaf) -or -not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw "Overlay incomplet : $revisionRoot (changes.patch et revision.json requis)."
}
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
if ([int]$metadata.revision -lt 2 -or [int]$metadata.revision -gt 5) {
    throw "Revision cible invalide dans $metadataPath."
}
$targetLineage = [string]$metadata.lineage
$baseLineages = @($metadata.baseLineages | ForEach-Object { [string]$_ })
if ([string]::IsNullOrWhiteSpace($targetLineage) -or $baseLineages.Count -eq 0) {
    throw "Les metadonnees de $Revision doivent declarer lineage et baseLineages."
}
$expectedSourceRevision = [int]$metadata.revision - 1
if ([int]$manifest.revision -ne $expectedSourceRevision) {
    throw "L'overlay $Revision attend le manifeste revision $expectedSourceRevision ; la cible est a la revision $($manifest.revision)."
}
$sourceLineage = [string]$manifest.proofLineage
if ([string]::IsNullOrWhiteSpace($sourceLineage) -and [int]$manifest.revision -eq 1) { $sourceLineage = 'baseline' }
if ($sourceLineage -notin $baseLineages) {
    throw "L'overlay $Revision attend la filiation '$($baseLineages -join ', ')' ; le clone est '$sourceLineage'."
}
$managedPaths = @($manifest.files | ForEach-Object { [string]$_.path })
if ($managedPaths.Count -eq 0) { throw 'Le manifeste ne contient aucun fichier gere.' }

function Read-ProofValue {
    param([string] $FilePath, [string] $Pattern, [string] $Name)
    $content = [IO.File]::ReadAllText($FilePath, [Text.UTF8Encoding]::new($false))
    $match = [regex]::Match($content, $Pattern)
    if (-not $match.Success) { throw "Valeur $Name absente de $FilePath." }
    return $match.Groups['value'].Value
}

$subscriptionDev = Read-ProofValue -FilePath (Join-Path $destinationRoot 'core/infra/main.dev.bicepparam') -Pattern "subscriptionId:\s*'(?<value>[^']+)'" -Name 'abonnement dev'
$subscriptionPrd = Read-ProofValue -FilePath (Join-Path $destinationRoot 'core/infra/main.prd.bicepparam') -Pattern "subscriptionId:\s*'(?<value>[^']+)'" -Name 'abonnement prd'
$subscriptionShared = Read-ProofValue -FilePath (Join-Path $destinationRoot 'platform/infra/main.shared.bicepparam') -Pattern "subscriptionId:\s*'(?<value>[^']+)'" -Name 'abonnement shared'
$sqlAdminDev = Read-ProofValue -FilePath (Join-Path $destinationRoot 'data/infra/main.dev.bicepparam') -Pattern "administratorGroupObjectId:\s*'(?<value>[^']+)'" -Name 'groupe SQL dev'
$sqlAdminPrd = Read-ProofValue -FilePath (Join-Path $destinationRoot 'data/infra/main.prd.bicepparam') -Pattern "administratorGroupObjectId:\s*'(?<value>[^']+)'" -Name 'groupe SQL prd'
$replacementTable = Get-ProofReplacementTable `
    -ProjectCode $projectCode `
    -SubscriptionDev $subscriptionDev `
    -SubscriptionPrd $subscriptionPrd `
    -SubscriptionShared $subscriptionShared `
    -SqlAdminGroupObjectIdDev $sqlAdminDev `
    -SqlAdminGroupObjectIdPrd $sqlAdminPrd

$patchText = [IO.File]::ReadAllText($patchPath, [Text.UTF8Encoding]::new($false))
$patchText = Invoke-ProofTextReplacement -Text $patchText -ReplacementTable $replacementTable
$tempPatch = Join-Path ([IO.Path]::GetTempPath()) ("ifs-proof-revision-" + [guid]::NewGuid().ToString('N') + '.patch')
[IO.File]::WriteAllText($tempPatch, $patchText, [Text.UTF8Encoding]::new($false))
try {
    & git -C $destinationRoot apply --check $tempPatch
    if ($LASTEXITCODE -ne 0) { throw "L'overlay $Revision ne s'applique pas a l'etat courant du clone." }
    if (-not $PSCmdlet.ShouldProcess($destinationRoot, "Appliquer l'overlay $Revision et recalculer le manifeste")) { return }

    [void](Assert-ProofManifestWorkingTree -RepositoryPath $destinationRoot -Manifest $manifest)

    & git -C $destinationRoot apply $tempPatch
    if ($LASTEXITCODE -ne 0) { throw "Echec d'application de l'overlay $Revision." }

    try {
        Write-ProofManifest -RepositoryPath $destinationRoot -Manifest $manifest -ManagedPaths $managedPaths -Revision ([int]$metadata.revision) -ProofLineage $targetLineage
    }
    catch {
        $manifestError = $_
        & git -C $destinationRoot apply --reverse $tempPatch
        if ($LASTEXITCODE -ne 0) { throw "Echec du manifeste et du retour arriere de l'overlay $Revision ; le clone doit etre examine." }
        throw $manifestError
    }
    Write-Information -MessageData ("Overlay {0} applique, revision du manifeste {1}. Aucun commit n'a ete cree." -f $Revision, $metadata.revision) -InformationAction Continue
    & git -C $destinationRoot status --short --branch
    if ($LASTEXITCODE -ne 0) { throw 'Impossible de lire git status du clone cible.' }
}
finally {
    if (Test-Path -LiteralPath $tempPatch -PathType Leaf) { [IO.File]::Delete($tempPatch) }
}
