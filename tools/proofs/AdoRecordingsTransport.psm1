Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-AdoRecordingsOrganizationUri {
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $Organization)

    if ($Organization -match '^https?://') { return $Organization.TrimEnd('/') }
    return 'https://dev.azure.com/{0}' -f [Uri]::EscapeDataString($Organization)
}

function Invoke-AdoRecordingsCliCommand {
    param(
        [Parameter(Mandatory)][string[]] $Arguments,
        [scriptblock] $AzInvoker
    )

    if ($null -ne $AzInvoker) {
        return @(& $AzInvoker $Arguments)
    }

    if ($null -eq (Get-Command az -ErrorAction SilentlyContinue)) {
        throw 'Azure CLI introuvable ; configurez az ou fournissez un PAT Azure DevOps.'
    }

    $output = @(& az @Arguments 2>$null)
    if ($LASTEXITCODE -ne 0) {
        throw "La commande Azure CLI a echoue (code $LASTEXITCODE)."
    }
    return $output
}

function Invoke-AdoRecordingsCliJsonGet {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $Organization,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $Area,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $Resource,
        [Parameter(Mandatory)][System.Collections.IDictionary] $RouteParameters,
        [Parameter(Mandatory)][System.Collections.IDictionary] $QueryParameters,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $Kind,
        [scriptblock] $AzInvoker
    )

    $arguments = [Collections.Generic.List[string]]::new()
    $organizationUri = ConvertTo-AdoRecordingsOrganizationUri -Organization $Organization
    foreach ($argument in @(
        'devops', 'invoke', '--organization', $organizationUri,
        '--area', $Area, '--resource', $Resource
    )) {
        $arguments.Add([string]$argument)
    }

    if ($RouteParameters.Count -gt 0) {
        $arguments.Add('--route-parameters')
        foreach ($key in @($RouteParameters.Keys | Sort-Object)) {
            $arguments.Add(('{0}={1}' -f $key, $RouteParameters[$key]))
        }
    }

    if ($QueryParameters.Count -gt 0) {
        $arguments.Add('--query-parameters')
        foreach ($key in @($QueryParameters.Keys | Sort-Object)) {
            $arguments.Add(('{0}={1}' -f $key, $QueryParameters[$key]))
        }
    }

    foreach ($argument in @('--api-version', '7.1', '--http-method', 'GET', '--only-show-errors', '--output', 'json')) {
        $arguments.Add($argument)
    }

    try {
        $commandOutput = Invoke-AdoRecordingsCliCommand -Arguments $arguments.ToArray() -AzInvoker $AzInvoker
        $rawJson = (@($commandOutput) -join [Environment]::NewLine).Trim()
        if ([string]::IsNullOrWhiteSpace($rawJson)) {
            if ($Area -eq 'build' -and $Resource -eq 'timeline') {
                $rawJson = '{"count":0,"value":[]}'
            }
            else {
                throw 'empty response'
            }
        }
        $data = ConvertFrom-Json -InputObject $rawJson -AsHashtable -Depth 100 -DateKind String
        return [pscustomobject]@{ RawJson = $rawJson; Data = $data }
    }
    catch {
        Write-Verbose ("Azure CLI GET failure for {0}: {1} ({2})" -f $Kind, $_.Exception.GetType().Name, $_.FullyQualifiedErrorId)
        throw "Lecture Azure DevOps impossible pour '$Kind' avec la session Azure CLI. Verifiez le run et les droits Build (Read)."
    }
}

function Invoke-AdoRecordingsArtifactDownload {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $Organization,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $Project,
        [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int] $RunId,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $ArtifactName,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $Path,
        [scriptblock] $AzInvoker
    )

    $arguments = @(
        'pipelines', 'runs', 'artifact', 'download',
        '--organization', (ConvertTo-AdoRecordingsOrganizationUri -Organization $Organization),
        '--project', $Project,
        '--run-id', [string]$RunId,
        '--artifact-name', $ArtifactName,
        '--path', $Path,
        '--only-show-errors',
        '--output', 'none'
    )

    try {
        $null = Invoke-AdoRecordingsCliCommand -Arguments $arguments -AzInvoker $AzInvoker
    }
    catch {
        Write-Verbose ("Azure CLI artifact download failure for {0}: {1} ({2})" -f $ArtifactName, $_.Exception.GetType().Name, $_.FullyQualifiedErrorId)
        throw "Telechargement de l'artefact '$ArtifactName' impossible avec la session Azure CLI."
    }
}

function Test-AdoRecordingsArtifactName {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $Name)
    return $Name -match '^(ifs-install-report|ifs-report|ifs-preview|ifs-app-report)(-[A-Za-z0-9._-]+)?$'
}

Export-ModuleMember -Function Invoke-AdoRecordingsCliJsonGet, Invoke-AdoRecordingsArtifactDownload, Test-AdoRecordingsArtifactName
