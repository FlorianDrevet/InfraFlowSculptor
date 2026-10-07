param(
    [string]$VersionsPath = (Join-Path $PSScriptRoot "..\versions.json")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Error "PowerShell 7 est requis. Lancez ce script avec pwsh."
    exit 1
}

if (-not (Test-Path -LiteralPath $VersionsPath -PathType Leaf)) {
    Write-Error "Fichier de versions introuvable : $VersionsPath"
    exit 1
}

try {
    $versions = Get-Content -LiteralPath $VersionsPath -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
}
catch {
    Write-Error "Impossible de lire le JSON des versions : $($_.Exception.Message)"
    exit 1
}

function Invoke-ToolOutput {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter()] [string[]]$Arguments = @()
    )

    $command = Get-Command -Name $Name -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $command) {
        throw "commande introuvable"
    }

    $output = & $command.Source @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "commande terminée avec le code $LASTEXITCODE"
    }

    return (@($output) -join "`n").Trim()
}

function Get-ReportedVersion {
    param(
        [Parameter(Mandatory)] [string]$Tool,
        [Parameter(Mandatory)] [string]$Output
    )

    if ($Tool -eq 'bicep') {
        $match = [regex]::Match($Output, '(?im)Bicep CLI version\s+v?(\d+(?:\.\d+){1,3})')
    }
    else {
        $match = [regex]::Match($Output, '\d+(?:\.\d+){1,3}')
    }

    if (-not $match.Success) {
        throw "version illisible : $Output"
    }

    return [version]::Parse($match.Groups[$match.Groups.Count - 1].Value)
}

function Get-ToolVersion {
    param([Parameter(Mandatory)] [string]$Tool)

    switch ($Tool) {
        'dotnet' { return Get-ReportedVersion -Tool $Tool -Output (Invoke-ToolOutput 'dotnet' @('--version')) }
        'node'   { return Get-ReportedVersion -Tool $Tool -Output (Invoke-ToolOutput 'node' @('--version')) }
        'aspire' { return Get-ReportedVersion -Tool $Tool -Output (Invoke-ToolOutput 'aspire' @('--version')) }
        'python' { return Get-ReportedVersion -Tool $Tool -Output (Invoke-ToolOutput 'python' @('--version')) }
        'pwsh'   { return $PSVersionTable.PSVersion }
        'bicep'  { return Get-ReportedVersion -Tool $Tool -Output (Invoke-ToolOutput 'az' @('bicep', 'version')) }
        'docker' {
            $version = Get-ReportedVersion -Tool $Tool -Output (Invoke-ToolOutput 'docker' @('--version'))
            $null = Invoke-ToolOutput 'docker' @('info', '--format', '{{.ServerVersion}}')
            return $version
        }
        'az' {
            $output = Invoke-ToolOutput 'az' @('version', '--output', 'json')
            $info = $output | ConvertFrom-Json -AsHashtable
            if (-not $info.ContainsKey('azure-cli')) {
                throw "version Azure CLI absente de az version"
            }
            return Get-ReportedVersion -Tool $Tool -Output ([string]$info['azure-cli'])
        }
        'git'    { return Get-ReportedVersion -Tool $Tool -Output (Invoke-ToolOutput 'git' @('--version')) }
        'gh'     { return Get-ReportedVersion -Tool $Tool -Output (Invoke-ToolOutput 'gh' @('--version')) }
        default  { throw "outil non pris en charge : $Tool" }
    }
}

function Get-InstallSuggestion {
    param(
        [Parameter(Mandatory)] [string]$Tool,
        [Parameter(Mandatory)] [bool]$IsInstalled
    )

    switch ($Tool) {
        'dotnet' { if ($IsInstalled) { return 'winget upgrade --id Microsoft.DotNet.SDK.10' }; return 'winget install --id Microsoft.DotNet.SDK.10' }
        'node'   { if ($IsInstalled) { return 'winget upgrade --id OpenJS.NodeJS.LTS' }; return 'winget install --id OpenJS.NodeJS.LTS' }
        'aspire' { if ($IsInstalled) { return "dotnet tool update -g aspire.cli --version $($versions['aspire'])" }; return "dotnet tool install -g aspire.cli --version $($versions['aspire'])" }
        'python' { return 'winget install --id Python.Python.3.11' }
        'pwsh'   { if ($IsInstalled) { return 'winget upgrade --id Microsoft.PowerShell' }; return 'winget install --id Microsoft.PowerShell' }
        'bicep'  { if ($IsInstalled) { return 'az bicep upgrade' }; return 'az bicep install' }
        'docker' { if ($IsInstalled) { return 'Démarrer Docker Desktop, puis relancer le contrôle.' }; return 'winget install --id Docker.DockerDesktop' }
        'az'     { if ($IsInstalled) { return 'winget upgrade --id Microsoft.AzureCLI' }; return 'winget install --id Microsoft.AzureCLI' }
        'git'    { return 'winget install --id Git.Git' }
        'gh'     { return 'winget install --id GitHub.cli' }
        default  { return 'Consulter la documentation officielle de cet outil.' }
    }
}

$toolNames = @('dotnet', 'node', 'aspire', 'python', 'pwsh', 'bicep', 'docker', 'az', 'git', 'gh')
$displayNames = @{
    dotnet = 'SDK .NET'
    node   = 'Node'
    aspire = 'CLI Aspire'
    python = 'Python'
    pwsh   = 'PowerShell 7'
    bicep  = 'Bicep CLI'
    docker = 'Docker'
    az     = 'Azure CLI'
    git    = 'git'
    gh     = 'gh'
}
$rows = [System.Collections.Generic.List[object]]::new()
$hasFailures = $false

foreach ($tool in $toolNames) {
    $minimum = if ($versions.ContainsKey($tool)) { [string]$versions[$tool] } else { $null }
    $expected = if ($minimum) { $minimum } else { 'présence' }
    $found = 'absent'
    $status = 'KO'
    $isInstalled = $false

    try {
        $actual = Get-ToolVersion -Tool $tool
        $found = $actual.ToString()
        $isInstalled = $true
        if (-not $minimum -or $actual -ge [version]::Parse($minimum)) {
            $status = 'OK'
        }
    }
    catch {
        if ($tool -eq 'docker' -and (Get-Command -Name 'docker' -ErrorAction SilentlyContinue)) {
            try {
                $found = (Get-ReportedVersion -Tool 'docker' -Output (Invoke-ToolOutput 'docker' @('--version'))).ToString()
                $isInstalled = $true
            }
            catch {
                $found = 'absent'
            }
        }
    }

    if ($status -ne 'OK') {
        $hasFailures = $true
    }

    $rows.Add([pscustomobject]@{
        Tool = $displayNames[$tool]
        Expected = $expected
        Found = $found
        Status = $status
        Suggestion = if ($status -eq 'OK') { '' } else { Get-InstallSuggestion -Tool $tool -IsInstalled $isInstalled }
    })
}

$toolWidth = [Math]::Max(18, ($rows | ForEach-Object { $_.Tool.Length } | Measure-Object -Maximum).Maximum)
$expectedWidth = [Math]::Max(10, ($rows | ForEach-Object { $_.Expected.Length } | Measure-Object -Maximum).Maximum)
$foundWidth = [Math]::Max(12, ($rows | ForEach-Object { $_.Found.Length } | Measure-Object -Maximum).Maximum)
Write-Output (("{0,-$toolWidth} | {1,-$expectedWidth} | {2,-$foundWidth} | {3}" -f 'Outil', 'Attendu', 'Trouvé', 'État'))
Write-Output ((('-' * $toolWidth) + '-+-' + ('-' * $expectedWidth) + '-+-' + ('-' * $foundWidth) + '-+------'))
foreach ($row in $rows) {
    Write-Output (("{0,-$toolWidth} | {1,-$expectedWidth} | {2,-$foundWidth} | {3}" -f $row.Tool, $row.Expected, $row.Found, $row.Status))
}

foreach ($row in $rows | Where-Object Status -eq 'KO') {
    Write-Output ("Installation suggérée pour {0} : {1}" -f $row.Tool, $row.Suggestion)
}

if ($hasFailures) {
    exit 1
}

exit 0
