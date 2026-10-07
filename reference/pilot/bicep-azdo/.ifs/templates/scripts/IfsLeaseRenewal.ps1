# Généré par InfraFlowSculptor — projet shop. Ne pas modifier : la prochaine publication remplacera ce fichier. Personnalisation : voir README.ifs.md.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Account,
    [Parameter(Mandatory)][string] $Container,
    [Parameter(Mandatory)][string] $Blob,
    [Parameter(Mandatory)][string] $LeaseId,
    [Parameter(Mandatory)][string] $StopFile,
    [Parameter(Mandatory)][string] $FailureFile
)

$ErrorActionPreference = 'Stop'
while ($true) {
    for ($second = 0; $second -lt 30; $second++) {
        if (Test-Path -LiteralPath $StopFile) { exit 0 }
        Start-Sleep -Seconds 1
    }
    if (Test-Path -LiteralPath $StopFile) { exit 0 }
    & az storage blob lease renew --account-name $Account --container-name $Container --blob-name $Blob --lease-id $LeaseId --auth-mode login --output none
    if ($LASTEXITCODE -ne 0) {
        [IO.File]::WriteAllText($FailureFile, ('Renouvellement impossible pour le journal {0}.' -f $Blob))
        exit 1
    }
}
