[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory)][string] $RepositoryPath,
    [ValidatePattern('^[a-z][a-z0-9]{2,11}$')][string] $ProjectCode = 'shop',
    [Parameter(Mandatory)][string] $SubscriptionDev,
    [Parameter(Mandatory)][string] $SubscriptionPrd,
    [Parameter(Mandatory)][string] $SubscriptionShared,
    [Parameter(Mandatory)][string] $SqlAdminGroupObjectIdDev,
    [Parameter(Mandatory)][string] $SqlAdminGroupObjectIdPrd,
    [switch] $SqlFreeOffer
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$referenceRoot = Join-Path $repoRoot 'reference/pilot/bicep-azdo'
$sampleRoot = Join-Path $repoRoot 'samples/witness-app'
if (-not (Test-Path -LiteralPath $referenceRoot -PathType Container)) { throw "Reference absente : $referenceRoot" }
if (-not (Test-Path -LiteralPath $sampleRoot -PathType Container)) { throw "Application temoin absente : $sampleRoot" }

Import-Module (Join-Path $PSScriptRoot 'ProofTools.psm1') -Force
$replacementTable = Get-ProofReplacementTable `
    -ProjectCode $ProjectCode `
    -SubscriptionDev $SubscriptionDev `
    -SubscriptionPrd $SubscriptionPrd `
    -SubscriptionShared $SubscriptionShared `
    -SqlAdminGroupObjectIdDev $SqlAdminGroupObjectIdDev `
    -SqlAdminGroupObjectIdPrd $SqlAdminGroupObjectIdPrd

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

$managedPaths = @(Get-ProofManagedFileList -SourceRoot $referenceRoot)
$samplePaths = @(Get-ProofManagedFileList -SourceRoot $sampleRoot)
$sourceCommit = (& git -C $repoRoot rev-parse HEAD 2>$null | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'Impossible de lire le commit source de la reference.'
}

function Write-ReferenceFile {
    param([Parameter(Mandatory)][string] $OutputRoot)

    foreach ($sourcePath in @($managedPaths)) {
        $sourceFile = Join-Path $referenceRoot ($sourcePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $targetFile = Join-Path $OutputRoot ($sourcePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $targetDirectory = Split-Path -Parent $targetFile
        [void][IO.Directory]::CreateDirectory($targetDirectory)
        $text = [IO.File]::ReadAllText($sourceFile, [Text.UTF8Encoding]::new($false, $true))
        $text = Invoke-ProofTextReplacement -Text $text -ReplacementTable $replacementTable
        $text = $text.Replace("`r`n", "`n").Replace("`r", "`n")
        [IO.File]::WriteAllText($targetFile, $text, [Text.UTF8Encoding]::new($false))
    }

    foreach ($sourcePath in @($samplePaths)) {
        $sourceFile = Join-Path $sampleRoot ($sourcePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $targetRelativePath = "src/api/$sourcePath"
        $targetFile = Join-Path $OutputRoot ($targetRelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $targetDirectory = Split-Path -Parent $targetFile
        [void][IO.Directory]::CreateDirectory($targetDirectory)
        $text = [IO.File]::ReadAllText($sourceFile, [Text.UTF8Encoding]::new($false, $true))
        $text = Invoke-ProofTextReplacement -Text $text -ReplacementTable $replacementTable
        $text = $text.Replace("`r`n", "`n").Replace("`r", "`n")
        [IO.File]::WriteAllText($targetFile, $text, [Text.UTF8Encoding]::new($false))
    }

    $costProfile = @(Set-ProofCostProfile -BicepRoot $OutputRoot -SqlFreeOffer:$SqlFreeOffer)
    foreach ($setting in $costProfile) {
        $changeState = if ($setting.Changed) { 'mis à jour' } else { 'déjà conforme' }
        Write-Information -MessageData ("Profil de coût : {0} — {1} = {2} ({3})" -f $setting.Path, $setting.Setting, $setting.Value, $changeState) -InformationAction Continue
    }

    $manifest = [ordered]@{
        schema = 'ifs-manifest/v1'
        project = $ProjectCode
        revision = 1
        generatedAtUtc = [DateTime]::UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", [Globalization.CultureInfo]::InvariantCulture)
        commit = $sourceCommit
        files = @()
    }
    $manifestPaths = @($managedPaths) + @($samplePaths | ForEach-Object { "src/api/$_" })
    Write-ProofManifest -RepositoryPath $OutputRoot -Manifest $manifest -ManagedPaths $manifestPaths -Revision 1 -ProofLineage 'baseline'
}

function Assert-ReferencePublication {
    param([Parameter(Mandatory)][string] $OutputRoot)

    $expectedPaths = @($managedPaths) + @($samplePaths | ForEach-Object { "src/api/$_" }) + @('.ifs/manifest.json')
    foreach ($relativePath in $expectedPaths) {
        $filePath = Join-Path $OutputRoot ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) { throw "Fichier attendu absent : $relativePath" }
    }

    $residuals = [System.Collections.Generic.List[string]]::new()
    foreach ($relativePath in $expectedPaths) {
        $filePath = Join-Path $OutputRoot ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        try { $content = [IO.File]::ReadAllText($filePath, [Text.UTF8Encoding]::new($false, $true)) }
        catch { throw "Fichier publie non UTF-8, verification impossible : $relativePath" }
        if ($ProjectCode -cne 'shop' -and $content -match '(?i)-shop-') {
            $residuals.Add($relativePath)
        }
    }
    if ($residuals.Count -gt 0) {
        throw "Le code projet shop subsiste dans : $($residuals -join ', ')"
    }

    $placeholderFiles = [System.Collections.Generic.List[string]]::new()
    foreach ($relativePath in $expectedPaths) {
        $filePath = Join-Path $OutputRoot ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        try { $content = [IO.File]::ReadAllText($filePath, [Text.UTF8Encoding]::new($false, $true)) }
        catch { throw "Fichier publie non UTF-8, verification impossible : $relativePath" }
        if ($content -match '<[ABC]>|<SQL_ADMIN_GROUP_OBJECT_ID_(DEV|PRD)>') { $placeholderFiles.Add($relativePath) }
    }
    if ($placeholderFiles.Count -gt 0) { throw "Un abonnement ou un groupe SQL conserve un placeholder dans : $($placeholderFiles -join ', ')" }

    $savedManifest = Get-Content -LiteralPath (Join-Path $OutputRoot '.ifs/manifest.json') -Raw | ConvertFrom-Json -AsHashtable
    if ($savedManifest.project -cne $ProjectCode -or $savedManifest.revision -ne 1) {
        throw 'Le manifeste publie ne declare pas le projet et la revision attendus.'
    }
    foreach ($component in @('core', 'data', 'platform', 'orders')) {
        $unit = $savedManifest.units[$component]
        if ($null -eq $unit -or [string]$unit.fingerprint -notmatch '^[a-f0-9]{64}$') {
            throw "Le manifeste publie ne contient pas de fingerprint valide pour $component."
        }
    }

    return [pscustomobject]@{
        GeneratedFiles = $managedPaths.Count
        WitnessFiles = $samplePaths.Count
        ExpectedFiles = $expectedPaths.Count
    }
}

if ($WhatIfPreference) {
    $systemTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $stagingRoot = [IO.Path]::GetFullPath((Join-Path $systemTempRoot ("ifs-pilot-whatif-" + [guid]::NewGuid().ToString('N'))))
    if (-not $stagingRoot.StartsWith($systemTempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Le dossier de validation WhatIf sort du dossier temporaire du systeme.'
    }
    try {
        [void][IO.Directory]::CreateDirectory($stagingRoot)
        Write-ReferenceFile -OutputRoot $stagingRoot
        $check = Assert-ReferencePublication -OutputRoot $stagingRoot
        $shopCheck = if ($ProjectCode -ceq 'shop') { 'code projet shop valide' } else { "aucun '-shop-' residuel" }
        Write-Information -MessageData ("WhatIf OK : {0} fichiers de reference, {1} fichiers temoins, {2}. Le clone cible n'a pas ete modifie." -f $check.GeneratedFiles, $check.WitnessFiles, $shopCheck) -InformationAction Continue
    }
    finally {
        if ($stagingRoot.StartsWith($systemTempRoot, [StringComparison]::OrdinalIgnoreCase) -and
            (Test-Path -LiteralPath $stagingRoot -PathType Container)) {
            $priorWhatIf = $WhatIfPreference
            try {
                $WhatIfPreference = $false
                Remove-Item -LiteralPath $stagingRoot -Recurse -Force
            }
            finally { $WhatIfPreference = $priorWhatIf }
        }
    }
    return
}

$sourceStatus = @(& git -C $repoRoot status --porcelain=v1 --untracked-files=all -- 'reference/pilot/bicep-azdo' 'samples/witness-app' 2>&1)
if ($LASTEXITCODE -ne 0) { throw 'Impossible de verifier la proprete des sources de publication.' }
if ($sourceStatus.Count -gt 0) {
    throw "Les sources de publication doivent etre committees avant une vraie publication. Changements detectes : $($sourceStatus -join '; ')"
}
$targetStatus = @(& git -C $destinationRoot status --porcelain=v1 --untracked-files=all 2>&1)
if ($LASTEXITCODE -ne 0) { throw 'Impossible de verifier la proprete du clone cible.' }
if ($targetStatus.Count -gt 0) {
    throw "Le clone cible doit etre propre avant publication. Changements detectes : $($targetStatus -join '; ')"
}

if (-not $PSCmdlet.ShouldProcess($destinationRoot, 'Publier la reference pilote sans commit')) { return }

$expectedPaths = @($managedPaths) + @($samplePaths | ForEach-Object { "src/api/$_" }) + @('.ifs/manifest.json')
$collisions = @(
    foreach ($relativePath in $expectedPaths) {
        $targetPath = Join-Path $destinationRoot ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (Test-Path -LiteralPath $targetPath) { $relativePath }
    }
)
if ($collisions.Count -gt 0) {
    throw "Le clone contient deja des chemins que la publication doit creer : $($collisions -join ', ')"
}

$systemTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$stagingRoot = [IO.Path]::GetFullPath((Join-Path $systemTempRoot ("ifs-pilot-publish-" + [guid]::NewGuid().ToString('N'))))
if (-not $stagingRoot.StartsWith($systemTempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Le dossier de publication sort du dossier temporaire du systeme.'
}
$createdFiles = [System.Collections.Generic.List[string]]::new()
$createdDirectories = [System.Collections.Generic.List[string]]::new()
try {
    [void][IO.Directory]::CreateDirectory($stagingRoot)
    Write-ReferenceFile -OutputRoot $stagingRoot
    $null = Assert-ReferencePublication -OutputRoot $stagingRoot

    $destinationRootPrefix = [IO.Path]::GetFullPath($destinationRoot).TrimEnd($trimChars) + [IO.Path]::DirectorySeparatorChar
    foreach ($relativePath in $expectedPaths) {
        $relativeOsPath = $relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
        $stagedFile = [IO.Path]::GetFullPath((Join-Path $stagingRoot $relativeOsPath))
        $targetFile = [IO.Path]::GetFullPath((Join-Path $destinationRoot $relativeOsPath))
        if (-not $targetFile.StartsWith($destinationRootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Chemin de publication hors du clone cible : $relativePath"
        }
        if (Test-Path -LiteralPath $targetFile) { throw "Le chemin cible est apparu pendant la publication : $relativePath" }
        $targetDirectory = Split-Path -Parent $targetFile
        $directory = $targetDirectory
        while ($directory.StartsWith($destinationRootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            if (Test-Path -LiteralPath $directory -PathType Container) { break }
            if (Test-Path -LiteralPath $directory -PathType Leaf) { break }
            $createdDirectories.Add($directory)
            $directory = Split-Path -Parent $directory
        }
        [void][IO.Directory]::CreateDirectory($targetDirectory)
        $createdFiles.Add($targetFile)
        [IO.File]::Copy($stagedFile, $targetFile, $false)
    }

    $checkResult = Assert-ReferencePublication -OutputRoot $destinationRoot
}
catch {
    foreach ($createdFile in @($createdFiles.ToArray()) | Sort-Object -Descending) {
        try {
            if (Test-Path -LiteralPath $createdFile -PathType Leaf) {
                Remove-Item -LiteralPath $createdFile -Force
            }
        }
        catch { Write-Warning "Echec du rollback du fichier de publication : $createdFile" }
    }
    foreach ($createdDirectory in @($createdDirectories.ToArray() | Sort-Object { $_.Length } -Descending)) {
        try {
            if ((Test-Path -LiteralPath $createdDirectory -PathType Container) -and
                [IO.Directory]::GetFileSystemEntries($createdDirectory).Length -eq 0) {
                [IO.Directory]::Delete($createdDirectory)
            }
        }
        catch { Write-Warning "Echec du rollback du dossier de publication : $createdDirectory" }
    }
    throw
}
finally {
    if ($stagingRoot.StartsWith($systemTempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $stagingRoot).StartsWith('ifs-pilot-publish-', [StringComparison]::Ordinal) -and
        (Test-Path -LiteralPath $stagingRoot -PathType Container)) {
        $priorWhatIf = $WhatIfPreference
        try {
            $WhatIfPreference = $false
            Remove-Item -LiteralPath $stagingRoot -Recurse -Force
        }
        finally { $WhatIfPreference = $priorWhatIf }
    }
}

Write-Information -MessageData ("Reference publiee : {0} fichiers generes, {1} fichiers temoins." -f $checkResult.GeneratedFiles, $checkResult.WitnessFiles) -InformationAction Continue
Write-Information -MessageData 'Etat Git du clone cible (aucun commit ne sera cree) :' -InformationAction Continue
& git -C $destinationRoot status --short --branch
if ($LASTEXITCODE -ne 0) { throw 'Impossible de lire git status du clone cible.' }
