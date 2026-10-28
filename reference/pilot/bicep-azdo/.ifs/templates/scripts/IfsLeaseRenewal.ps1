# Généré par InfraFlowSculptor — projet shop. Ne pas modifier : la prochaine publication remplacera ce fichier. Personnalisation : voir README.ifs.md.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Account,
    [Parameter(Mandatory)][string] $Container,
    [Parameter(Mandatory)][string] $Blob,
    [Parameter(Mandatory)][string] $LeaseId,
    [Parameter(Mandatory)][string] $StopFile,
    [Parameter(Mandatory)][string] $FailureFile,
    [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int] $OwnerProcessId,
    [Parameter(Mandatory)][long] $OwnerProcessStartTimeUtcTicks
)

$ErrorActionPreference = 'Stop'
$ownerProcess = $null

function Write-LeaseFailure {
    param([Parameter(Mandatory)][string] $Message)
    try { [IO.File]::WriteAllText($FailureFile, $Message) }
    catch { }
}

try {
    try { $ownerProcess = [System.Diagnostics.Process]::GetProcessById($OwnerProcessId) }
    catch [ArgumentException] { exit 0 }

    $actualStartTimeTicks = $ownerProcess.StartTime.ToUniversalTime().Ticks
    $startTimeToleranceTicks = [long]20000000 # 2 s : StartTime n'a pas la meme precision sur Windows et Linux.
    if ([Math]::Abs([double]$actualStartTimeTicks - [double]$OwnerProcessStartTimeUtcTicks) -gt $startTimeToleranceTicks) {
        Write-LeaseFailure 'Le processus associe au bail ne correspond pas au processus proprietaire attendu.'
        exit 1
    }

    while ($true) {
        for ($second = 0; $second -lt 30; $second++) {
            if (Test-Path -LiteralPath $StopFile) { exit 0 }
            if ($ownerProcess.HasExited) { exit 0 }
            Start-Sleep -Seconds 1
        }
        if (Test-Path -LiteralPath $StopFile) { exit 0 }
        if ($ownerProcess.HasExited) { exit 0 }

        & az storage blob lease renew --account-name $Account --container-name $Container --blob-name $Blob --lease-id $LeaseId --auth-mode login --output none
        if ($LASTEXITCODE -ne 0) {
            Write-LeaseFailure ('Renouvellement impossible pour le journal {0}.' -f $Blob)
            exit 1
        }
    }
}
catch {
    Write-LeaseFailure 'La surveillance du bail a echoue avant le prochain renouvellement.'
    exit 1
}
finally {
    if ($ownerProcess) { $ownerProcess.Dispose() }
}
