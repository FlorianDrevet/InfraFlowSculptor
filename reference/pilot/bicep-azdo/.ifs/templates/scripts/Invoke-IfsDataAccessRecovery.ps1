# Généré par InfraFlowSculptor — projet shop. Ne pas modifier : la prochaine publication remplacera ce fichier. Personnalisation : voir README.ifs.md.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ReleasePath,
    [Parameter(Mandatory)][string] $OperationId,
    [Parameter(Mandatory)][int] $ExpectedRevision,
    [Parameter(Mandatory)][string] $ExpectedCommit,
    [Parameter(Mandatory)][string] $SqlScript,
    [Parameter(Mandatory)][string] $RunId,
    [Parameter(Mandatory)][string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'IfsRelease.psm1') -Force

$report = [ordered]@{
    schema = 'ifs-data-access-recovery/v1'
    generatedAt = [DateTime]::UtcNow.ToString('o')
    result = 'Failed'
    operationId = $OperationId
    expectedRevision = $ExpectedRevision
    expectedCommit = $ExpectedCommit
    completionCommit = $null
    completedCommit = $null
    project = $null
    component = $null
    unit = $null
    target = $null
    error = $null
}

try {
    $releaseData = Read-IfsReleaseData -Path $ReleasePath
    $manifestPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../manifest.json'))
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw 'Le manifeste de la publication est absent ; le commit de clôture est invérifiable.'
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 100
    if ([string]$manifest.schema -cne 'ifs-manifest/v1' -or [string]$manifest.project -cne 'shop42' -or
        [int]$manifest.revision -ne $ExpectedRevision -or [string]$manifest.proofLineage -cne 'p3-role' -or
        [string]$manifest.commit -notmatch '^[0-9a-f]{40}$') {
        throw 'Le manifeste ne correspond pas à la révision PRD autorisée ; le commit de clôture est invérifiable.'
    }
    $completionCommit = [string]$manifest.commit
    $report.completionCommit = $completionCommit
    $report.project = [string]$releaseData.project
    $report.component = [string]$releaseData.component
    $report.unit = [string]$releaseData.unit
    $report.target = [string]$releaseData.target
    if ($releaseData.project -cne 'shop42' -or $releaseData.component -cne 'orders' -or
        $releaseData.unit -cne 'ifs-shop42-orders-prd' -or $releaseData.target -cne 'prd' -or
        -not [bool]$releaseData.protected) {
        throw 'La reprise est réservée à orders/infra PRD protégé de Shop42.'
    }

    $activeSubscription = [string](& az account show --only-show-errors --query id --output tsv 2>$null)
    $azExitCode = $LASTEXITCODE
    $global:LASTEXITCODE = 0
    if ($azExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($activeSubscription)) {
        throw 'Azure CLI ne permet pas de vérifier la souscription active.'
    }
    if (-not [string]::Equals($activeSubscription.Trim(), [string]$releaseData.subscriptionId, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'La souscription Azure active ne correspond pas à la release PRD.'
    }

    $recovery = Invoke-IfsDataAccessRecovery `
        -ReleaseData $releaseData `
        -OperationId $OperationId `
        -ExpectedRevision $ExpectedRevision `
        -ExpectedCommit $ExpectedCommit `
        -CompletionCommit $completionCommit `
        -SqlScript $SqlScript `
        -RunId $RunId
    $report.result = $recovery.result
    $report.completedCommit = $recovery.commit
    $report.temporaryFirewallRules = @($recovery.temporaryFirewallRules)
    $report.cleanedFirewallRules = @($recovery.cleanedFirewallRules)
}
catch {
    $report.error = $_.Exception.Message
    throw
}
finally {
    $report.generatedAt = [DateTime]::UtcNow.ToString('o')
    $outputDirectory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) { [void][IO.Directory]::CreateDirectory($outputDirectory) }
    [IO.File]::WriteAllText($OutputPath, (ConvertTo-Json -InputObject $report -Depth 20), [Text.UTF8Encoding]::new($false))
}
