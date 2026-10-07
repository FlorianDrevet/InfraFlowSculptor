Set-StrictMode -Version Latest

function Assert-ProofProjectCode {
    param([Parameter(Mandatory)][string] $ProjectCode)

    if ($ProjectCode -cnotmatch '^[a-z][a-z0-9]{2,11}$') {
        throw 'ProjectCode doit contenir de 3 a 12 caracteres minuscules ou chiffres, et commencer par une lettre.'
    }
}

function Assert-ProofGuid {
    param(
        [Parameter(Mandatory)][string] $Value,
        [Parameter(Mandatory)][string] $Name
    )

    $parsed = [guid]::Empty
    if (-not [guid]::TryParse($Value, [ref]$parsed) -or $parsed -eq [guid]::Empty) {
        throw "$Name doit etre un GUID non nul."
    }
    return $parsed.ToString('D')
}

function Assert-ProofSeparateRepository {
    param(
        [Parameter(Mandatory)][string] $SourceRepository,
        [Parameter(Mandatory)][string] $TargetRepository
    )

    $source = [IO.Path]::GetFullPath($SourceRepository).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
    $target = [IO.Path]::GetFullPath($TargetRepository).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
    $comparison = if ([Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)) {
        [StringComparison]::OrdinalIgnoreCase
    } else {
        [StringComparison]::Ordinal
    }
    $sourcePrefix = $source + [IO.Path]::DirectorySeparatorChar
    $targetPrefix = $target + [IO.Path]::DirectorySeparatorChar
    if ($source.Equals($target, $comparison) -or $source.StartsWith($targetPrefix, $comparison) -or $target.StartsWith($sourcePrefix, $comparison)) {
        throw 'Le clone cible doit etre un depot Git separe, hors du depot InfraFlowSculptor.'
    }
}

function Get-ProofReplacementTable {
    param(
        [Parameter(Mandatory)][string] $ProjectCode,
        [Parameter(Mandatory)][string] $SubscriptionDev,
        [Parameter(Mandatory)][string] $SubscriptionPrd,
        [Parameter(Mandatory)][string] $SubscriptionShared,
        [Parameter(Mandatory)][string] $SqlAdminGroupObjectIdDev,
        [Parameter(Mandatory)][string] $SqlAdminGroupObjectIdPrd
    )

    Assert-ProofProjectCode -ProjectCode $ProjectCode
    $SubscriptionDev = Assert-ProofGuid -Value $SubscriptionDev -Name 'SubscriptionDev'
    $SubscriptionPrd = Assert-ProofGuid -Value $SubscriptionPrd -Name 'SubscriptionPrd'
    $SubscriptionShared = Assert-ProofGuid -Value $SubscriptionShared -Name 'SubscriptionShared'
    $SqlAdminGroupObjectIdDev = Assert-ProofGuid -Value $SqlAdminGroupObjectIdDev -Name 'SqlAdminGroupObjectIdDev'
    $SqlAdminGroupObjectIdPrd = Assert-ProofGuid -Value $SqlAdminGroupObjectIdPrd -Name 'SqlAdminGroupObjectIdPrd'

    $items = [System.Collections.Generic.List[object]]::new()
    $add = {
        param([string] $From, [string] $To)
        $items.Add([pscustomobject]@{ From = $From; To = $To })
    }

    # Noms Azure explicites du tableau reference-pilote.md, section 2.1.
    foreach ($target in @('dev', 'prd')) {
        foreach ($resource in @('core', 'data', 'orders')) {
            & $add "rg-shop-$resource-main-$target" "rg-$ProjectCode-$resource-main-$target"
        }
    }
    foreach ($target in @('dev', 'prd')) {
        & $add "log-shop-main-$target" "log-$ProjectCode-main-$target"
        & $add "appi-shop-main-$target" "appi-$ProjectCode-main-$target"
        & $add "kv-shop-main-$target" "kv-$ProjectCode-main-$target"
        & $add "sql-shop-orders-$target" "sql-$ProjectCode-orders-$target"
        & $add "sqldb-shop-orders-$target" "sqldb-$ProjectCode-orders-$target"
        & $add "id-shop-api-$target" "id-$ProjectCode-api-$target"
        & $add "cae-shop-main-$target" "cae-$ProjectCode-main-$target"
        & $add "ca-shop-api-$target" "ca-$ProjectCode-api-$target"
    }
    & $add 'rg-shop-platform-main-shared' "rg-$ProjectCode-platform-main-shared"
    & $add 'crshopmainshared' "cr${ProjectCode}mainshared"
    & $add 'log-shop-main-<cible>' "log-$ProjectCode-main-<cible>"
    & $add 'appi-shop-main-<cible>' "appi-$ProjectCode-main-<cible>"
    & $add 'kv-shop-main-<cible>' "kv-$ProjectCode-main-<cible>"
    & $add 'sql-shop-orders-<cible>' "sql-$ProjectCode-orders-<cible>"
    & $add 'sqldb-shop-orders-<cible>' "sqldb-$ProjectCode-orders-<cible>"
    & $add 'id-shop-api-<cible>' "id-$ProjectCode-api-<cible>"
    & $add 'cae-shop-main-<cible>' "cae-$ProjectCode-main-<cible>"
    & $add 'ca-shop-api-<cible>' "ca-$ProjectCode-api-<cible>"

    # Noms de projet explicites utilises par le kit et les pipelines du pilote.
    foreach ($component in @('core', 'data', 'orders')) {
        foreach ($target in @('dev', 'prd')) {
            & $add "ifs-shop-$component-$target" "ifs-$ProjectCode-$component-$target"
        }
    }
    & $add 'ifs-shop-platform-shared' "ifs-$ProjectCode-platform-shared"
    & $add 'id-shop-extra-dev' "id-$ProjectCode-extra-dev"
    & $add 'id-shop-extra-prd' "id-$ProjectCode-extra-prd"
    foreach ($target in @('dev', 'prd', 'shared')) {
        & $add "ifs-shop-$target" "ifs-$ProjectCode-$target"
        & $add "ifs-shop-$target-app" "ifs-$ProjectCode-$target-app"
        & $add "id-ifs-deploy-shop-$target" "id-ifs-deploy-$ProjectCode-$target"
        & $add "id-ifs-app-shop-$target" "id-ifs-app-$ProjectCode-$target"
        & $add "rg-ifs-shop-$target" "rg-ifs-$ProjectCode-$target"
        & $add "shop-$target" "$ProjectCode-$target"
    }
    & $add 'ifs-shop-qa' "ifs-$ProjectCode-qa"
    & $add 'id-ifs-deploy-shop-<cible>' "id-ifs-deploy-$ProjectCode-<cible>"
    & $add 'id-ifs-app-shop-<cible>' "id-ifs-app-$ProjectCode-<cible>"
    & $add 'rg-ifs-shop-<cible>' "rg-ifs-$ProjectCode-<cible>"
    & $add 'ifs-shop-<cible>-app' "ifs-$ProjectCode-<cible>-app"
    & $add 'ifs-shop-<cible>' "ifs-$ProjectCode-<cible>"
    & $add 'shop-<cible>' "$ProjectCode-<cible>"
    & $add 'stifsshopdev' "stifs${ProjectCode}dev"
    & $add 'stifsshopprd' "stifs${ProjectCode}prd"
    & $add 'stifsshopshared' "stifs${ProjectCode}shared"
    & $add 'sg-shop-sql-admins' "sg-$ProjectCode-sql-admins"
    & $add 'shop-sql-admins' "$ProjectCode-sql-admins"
    & $add 'Shop Release Approvers' "$($ProjectCode.Substring(0, 1).ToUpperInvariant())$($ProjectCode.Substring(1)) Release Approvers"
    & $add '\shop\' "\$ProjectCode\"

    # Remplacements semantiques de valeurs de projet et des seuls placeholders documentes.
    & $add "'ifs-project': 'shop'" "'ifs-project': '$ProjectCode'"
    & $add '"project": "shop"' "`"project`": `"$ProjectCode`""
    & $add 'project: shop' "project: $ProjectCode"
    & $add 'project=shop' "project=$ProjectCode"
    & $add 'shop · ' "$ProjectCode · "
    & $add 'Architecture IFS — shop' "Architecture IFS — $ProjectCode"
    & $add 'projet shop' "projet $ProjectCode"
    & $add 'projet `shop`' "projet ``$ProjectCode``"
    & $add 'remplacez `shop`' "remplacez ``$ProjectCode``"
    & $add '$model.ProjectCode -ne ''shop''' ('$model.ProjectCode -ne ''' + $ProjectCode + '''')
    & $add '<A>' $SubscriptionDev
    & $add '<B>' $SubscriptionPrd
    & $add '<C>' $SubscriptionShared
    & $add '<SQL_ADMIN_GROUP_OBJECT_ID_DEV>' $SqlAdminGroupObjectIdDev
    & $add '<SQL_ADMIN_GROUP_OBJECT_ID_PRD>' $SqlAdminGroupObjectIdPrd

    return @($items | Sort-Object { $_.From.Length } -Descending)
}

function Invoke-ProofTextReplacement {
    param(
        [Parameter(Mandatory)][string] $Text,
        [Parameter(Mandatory)][object[]] $ReplacementTable
    )

    $lookup = [System.Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $ReplacementTable) {
        $from = [string]$entry.From
        if ($from.Length -gt 0 -and -not $lookup.ContainsKey($from)) {
            $lookup.Add($from, [string]$entry.To)
        }
    }
    if ($lookup.Count -eq 0) { return $Text }

    $alternatives = @($lookup.Keys | Sort-Object { $_.Length } -Descending | ForEach-Object { [regex]::Escape($_) })
    $pattern = '(?:' + [string]::Join('|', $alternatives) + ')'
    $evaluator = [System.Text.RegularExpressions.MatchEvaluator] {
        param([System.Text.RegularExpressions.Match] $match)
        return $lookup[$match.Value]
    }.GetNewClosure()
    return [regex]::Replace($Text, $pattern, $evaluator)
}

function Get-ProofManagedFileList {
    param([Parameter(Mandatory)][string] $SourceRoot)

    $excludedSegments = @('.git', 'bin', 'obj', 'TestResults', '.vs')
    $paths = [System.Collections.Generic.List[string]]::new()
    $sourceFullPath = [IO.Path]::GetFullPath($SourceRoot)
    $gitRoot = (& git -C $sourceFullPath rev-parse --show-toplevel 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitRoot)) {
        throw "La source doit appartenir a un depot Git pour exclure les fichiers non suivis : $SourceRoot"
    }
    $gitRoot = [IO.Path]::GetFullPath($gitRoot)
    $sourceRelativePath = [IO.Path]::GetRelativePath($gitRoot, $sourceFullPath).Replace('\', '/')
    if ($sourceRelativePath -eq '.') { $sourceRelativePath = '' }
    if ([IO.Path]::IsPathRooted($sourceRelativePath) -or $sourceRelativePath -match '^(\.\./|\.\.\\)') {
        throw "La source sort de la racine Git : $SourceRoot"
    }
    $trackedPaths = @(& git -C $gitRoot ls-files --cached -- $sourceRelativePath 2>$null)
    if ($LASTEXITCODE -ne 0) { throw "Impossible de lire les fichiers suivis sous $SourceRoot." }
    $prefix = if ([string]::IsNullOrEmpty($sourceRelativePath)) { '' } else { $sourceRelativePath.TrimEnd('/') + '/' }
    foreach ($trackedPath in $trackedPaths) {
        $repositoryRelativePath = ([string]$trackedPath).Replace('\', '/')
        if ($prefix -and -not $repositoryRelativePath.StartsWith($prefix, [StringComparison]::Ordinal)) { continue }
        $relativePath = if ($prefix) { $repositoryRelativePath.Substring($prefix.Length) } else { $repositoryRelativePath }
        if (@($relativePath.Split('/') | Where-Object { $_ -in $excludedSegments }).Count -gt 0) { continue }
        $filePath = Join-Path $sourceFullPath ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) { throw "Fichier source suivi absent : $relativePath" }
        $paths.Add($relativePath)
    }
    $paths.Sort([StringComparer]::Ordinal)
    return @($paths.ToArray())
}

function Get-ProofLogicalFileHash {
    param([Parameter(Mandatory)][string] $Path)

    try { $content = [IO.File]::ReadAllText($Path, [Text.UTF8Encoding]::new($false, $true)) }
    catch { throw "Fichier gere non UTF-8 ; empreinte logique impossible : $Path" }
    $content = $content.Replace("`r`n", "`n").Replace("`r", "`n")
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($content)
    $hash = [Security.Cryptography.SHA256]::HashData($bytes)
    return [Convert]::ToHexString($hash).ToLowerInvariant()
}

function Write-ProofManifest {
    param(
        [Parameter(Mandatory)][string] $RepositoryPath,
        [Parameter(Mandatory)][object] $Manifest,
        [Parameter(Mandatory)][string[]] $ManagedPaths,
        [Parameter(Mandatory)][int] $Revision,
        [ValidateNotNullOrEmpty()][string] $ProofLineage = 'baseline'
    )

    $files = [System.Collections.Generic.List[object]]::new()
    foreach ($relativePath in $ManagedPaths) {
        $filePath = Join-Path $RepositoryPath ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            throw "Fichier gere absent, manifeste non recalculable : $relativePath"
        }
        $files.Add([ordered]@{
            path = $relativePath
            sha256 = Get-ProofLogicalFileHash -Path $filePath
        })
    }

    $unitFingerprints = [ordered]@{}
    foreach ($component in @('core', 'data', 'platform', 'orders')) {
        $componentFiles = @($files | Where-Object { ([string]$_.path).StartsWith("$component/", [StringComparison]::Ordinal) })
        $componentJson = ConvertTo-Json -InputObject $componentFiles -Depth 100 -Compress
        $componentHash = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($componentJson))
        $unitFingerprints[$component] = [ordered]@{ fingerprint = [Convert]::ToHexString($componentHash).ToLowerInvariant() }
    }

    $Manifest.schema = 'ifs-manifest/v1'
    $Manifest.revision = $Revision
    $Manifest.proofLineage = $ProofLineage
    $Manifest.units = $unitFingerprints
    $Manifest.generatedAtUtc = [DateTime]::UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", [Globalization.CultureInfo]::InvariantCulture)
    $Manifest.files = $files.ToArray()
    $json = (ConvertTo-Json -InputObject $Manifest -Depth 100) -replace "`r`n", "`n"
    if (-not $json.EndsWith("`n", [StringComparison]::Ordinal)) { $json += "`n" }
    $manifestPath = Join-Path $RepositoryPath '.ifs/manifest.json'
    $temporaryPath = Join-Path (Split-Path -Parent $manifestPath) ('.manifest-' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllText($temporaryPath, $json, [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporaryPath, $manifestPath, $true)
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) { [IO.File]::Delete($temporaryPath) }
    }
}

function Assert-ProofManifestWorkingTree {
    param(
        [Parameter(Mandatory)][string] $RepositoryPath,
        [Parameter(Mandatory)][object] $Manifest
    )

    $repositoryRoot = [IO.Path]::GetFullPath($RepositoryPath)
    $gitRoot = (& git -C $repositoryRoot rev-parse --show-toplevel 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($gitRoot).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) -ine
        $repositoryRoot.TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))) {
        throw 'Le depot cible doit viser la racine d''un clone Git.'
    }

    $comparison = if ([Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)) {
        [StringComparer]::OrdinalIgnoreCase
    } else {
        [StringComparer]::Ordinal
    }
    $allowedPaths = [System.Collections.Generic.HashSet[string]]::new($comparison)
    $rootPrefix = $repositoryRoot.TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + [IO.Path]::DirectorySeparatorChar
    $manifestFiles = @($Manifest.files)
    if ($manifestFiles.Count -eq 0) { throw 'Le manifeste ne contient aucun fichier gere.' }

    foreach ($entry in $manifestFiles) {
        $relativePath = ([string]$entry.path).Replace('\', '/')
        if ([string]::IsNullOrWhiteSpace($relativePath) -or [IO.Path]::IsPathRooted($relativePath) -or
            @($relativePath.Split('/') | Where-Object { $_ -in @('', '.', '..') -or $_ -match ':' }).Count -gt 0) {
            throw "Chemin de fichier invalide dans le manifeste : $relativePath"
        }
        $fullPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))))
        if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Un fichier gere du manifeste est absent ou sort du depot : $relativePath"
        }
        $expectedHash = [string]$entry.sha256
        if ($expectedHash -notmatch '^[a-f0-9]{64}$') { throw "Empreinte invalide dans le manifeste : $relativePath" }
        $actualHash = Get-ProofLogicalFileHash -Path $fullPath
        if ($actualHash -cne $expectedHash) { throw "Le fichier gere a change depuis le manifeste : $relativePath" }
        [void]$allowedPaths.Add($relativePath)
    }
    [void]$allowedPaths.Add('.ifs/manifest.json')

    $status = @(& git -C $repositoryRoot status --porcelain=v1 --untracked-files=all 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'Impossible de verifier les changements du clone cible.' }
    foreach ($lineValue in $status) {
        $line = [string]$lineValue
        if ($line.Length -lt 4) { throw 'Git a renvoye un statut de clone illisible.' }
        $relativePath = $line.Substring(3).Replace('\', '/')
        if (-not $allowedPaths.Contains($relativePath)) {
            throw "Le clone contient un changement hors des fichiers verifies par le manifeste : $relativePath"
        }
    }
    return $status.Count
}

function Test-ProofSensitiveKey {
    param([AllowNull()][string] $Key)

    return $Key -match '(?i)(secret|token|password|pwd|authorization|connection.?string|(?:api|account).?key|sharedaccess|sas|signature|(?:^|[._-])(?:sig|pat)(?:$|[._-]))'
}

function Get-ProofRedactionToken {
    param(
        [Parameter(Mandatory)][string] $Value,
        [Parameter(Mandatory)][string] $Prefix,
        [Parameter(Mandatory)][hashtable] $Context
    )

    $mapName = $Prefix + 'Map'
    if (-not $Context.ContainsKey($mapName)) { $Context[$mapName] = [ordered]@{} }
    $map = $Context[$mapName]
    $key = $Value.ToLowerInvariant()
    if (-not $map.Contains($key)) { $map[$key] = '{0}_{1:D3}' -f $Prefix, ($map.Count + 1) }
    return [string]$map[$key]
}

function Get-ProofContextNameVariant {
    param([AllowNull()][string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value)) { return @() }
    $variants = [System.Collections.Generic.List[string]]::new()
    $variants.Add($Value)
    try {
        $encoded = [Uri]::EscapeDataString($Value)
        if (-not $variants.Contains($encoded)) { $variants.Add($encoded) }
        $formEncoded = $encoded.Replace('%20', '+')
        if (-not $variants.Contains($formEncoded)) { $variants.Add($formEncoded) }
        $doubleEncoded = [Uri]::EscapeDataString($encoded)
        if (-not $variants.Contains($doubleEncoded)) { $variants.Add($doubleEncoded) }
    }
    catch { return @($variants.ToArray()) }
    return @($variants.ToArray())
}

function ConvertTo-ProofRedactedMatch {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string] $Text,
        [Parameter(Mandatory)][string] $Pattern,
        [Parameter(Mandatory)][string] $Prefix,
        [Parameter(Mandatory)][hashtable] $Context,
        [int] $ValueGroup = 0,
        [int] $OutputPrefixGroup = -1
    )

    $foundMatches = [regex]::Matches($Text, $Pattern)
    foreach ($match in $foundMatches) {
        $token = Get-ProofRedactionToken -Value $match.Groups[$ValueGroup].Value -Prefix $Prefix -Context $Context
        $replacement = if ($OutputPrefixGroup -ge 0) { $match.Groups[$OutputPrefixGroup].Value + $token } else { $token }
        $Text = $Text.Replace($match.Value, $replacement)
    }
    return $Text
}

function Test-ProofResidualSensitiveValue {
    param(
        [AllowNull()][object] $Value,
        [Parameter(Mandatory)][hashtable] $Context
    )

    if ($null -eq $Value) { return $false }
    if ($Value -is [System.Collections.IDictionary]) {
        foreach ($entry in $Value.GetEnumerator()) {
            if (Test-ProofResidualSensitiveValue -Value $entry.Value -Context $Context) { return $true }
        }
        return $false
    }
    if (($Value -is [System.Collections.IEnumerable]) -and $Value -isnot [string]) {
        foreach ($item in $Value) {
            if (Test-ProofResidualSensitiveValue -Value $item -Context $Context) { return $true }
        }
        return $false
    }
    if ($Value -isnot [string]) { return $false }

    $text = [string]$Value
    $patterns = @(
        '(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b',
        '(?i)\b[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\b',
        '(?i)\b(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\.(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\.(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\.(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\b',
        '(?i)\b(?:aad|msa|vssgp|svc|ent|imp|tfid|vssds|vssu)\.[A-Za-z0-9+/_=-]+',
        '(?i)\bBearer\s+(?!SECRET_REDACTED\b)[A-Za-z0-9._~+/-]+=*',
        '(?i)\bconnection[_-]?string\s*=\s*(?!SECRET_REDACTED\b)(?:"[^"]*"|''[^'']*''|[^\s&]+)',
        '(?i)\bauthorization\s*[:=]\s*(?:Bearer|Basic)\s+(?!SECRET_REDACTED\b)[A-Za-z0-9._~+/-]+=*',
        '(?i)(?:\b|_)(?:azure_devops_ext_pat|ado_recordings_pat|github_token|token|sig|sharedaccesssignature|sharedaccesskey|accountkey|password|pwd|secret|client[_-]?secret|access[_-]?token|refresh[_-]?token|api[_-]?key|personal[_-]?access[_-]?token|pat)\s*[:=]\s*(?!SECRET_REDACTED\b)[^;\s&"''<>]+'
    )
    foreach ($pattern in $patterns) {
        if ([regex]::IsMatch($text, $pattern)) { return $true }
    }
    foreach ($contextValue in @([string]$Context.Organization, [string]$Context.Project)) {
        foreach ($valueToCheck in Get-ProofContextNameVariant -Value $contextValue) {
            if ($valueToCheck.Length -ge 3 -and [regex]::IsMatch($text, '(?i)(?<![A-Za-z0-9])' + [regex]::Escape($valueToCheck) + '(?![A-Za-z0-9])')) {
                return $true
            }
        }
    }
    return $false
}

function ConvertTo-ProofRedactedValue {
    param(
        [AllowNull()][object] $Value,
        [AllowNull()][string] $Key,
        [Parameter(Mandatory)][hashtable] $Context,
        [bool] $IdentityContext = $false,
        [bool] $HostContext = $false
    )

    if ($null -eq $Value) { return $null }
    if (Test-ProofSensitiveKey -Key $Key) { return 'SECRET_REDACTED' }
    $identityProperty = $IdentityContext -or $Key -match '(?i)^(actualApprover|assignedApprover|lastModifiedBy|assignedTo|createdBy|changedBy|completedBy|requestedBy|requestedFor|approvedBy|rejectedBy|approver|approvers|blockedApprovers|identity|identities|owner|user|author)$'
    $hostProperty = $Key -match '(?i)^(agentName|agentPoolName|poolName|workerName|repositoryName|repoName|computerName|hostName|machineName)$'
    $hostContainerProperty = $HostContext -or $Key -match '(?i)^(agent|agents|agentPool|pool|repository|repositories)$'
    if ($Value -is [System.Collections.IDictionary]) {
        $result = [ordered]@{}
        foreach ($entry in $Value.GetEnumerator()) {
            $name = [string]$entry.Key
            if (Test-ProofSensitiveKey -Key $name) { $result[$name] = 'SECRET_REDACTED'; continue }
            if ($name -match '(?i)^organization(Name)?$') { $result[$name] = 'org-redacted'; continue }
            if ($name -match '(?i)^project(Name)?$' -and $entry.Value -is [string]) { $result[$name] = 'project-redacted'; continue }
            if ($name -match '(?i)subscription.*id') {
                $result[$name] = if ($null -eq $entry.Value) { $null } else { Get-ProofRedactionToken -Value ([string]$entry.Value) -Prefix 'SUBSCRIPTION_ID' -Context $Context }
                continue
            }
            if ($name -match '(?i)(object|principal|client|tenant|group|identity).*id|^projectId$|^organizationId$') {
                $result[$name] = if ($null -eq $entry.Value) { $null } else { Get-ProofRedactionToken -Value ([string]$entry.Value) -Prefix 'OBJECT_ID' -Context $Context }
                continue
            }
            if ($name -match '(?i)^descriptor$') {
                $result[$name] = if ($null -eq $entry.Value) { $null } else { Get-ProofRedactionToken -Value ([string]$entry.Value) -Prefix 'IDENTITY_DESCRIPTOR' -Context $Context }
                continue
            }
            if ($name -match '(?i)^(imageUrl|profileUrl)$') { $result[$name] = 'URL_REDACTED'; continue }
            if ($hostContainerProperty -and $name -match '(?i)^(url|remoteUrl|webUrl)$') { $result[$name] = 'URL_REDACTED'; continue }
            if ($name -match '(?i)^(agentName|agentPoolName|poolName|workerName|repositoryName|repoName|computerName|hostName|machineName)$') {
                $result[$name] = if ($null -eq $entry.Value) { $null } else { Get-ProofRedactionToken -Value ([string]$entry.Value) -Prefix 'HOST' -Context $Context }
                continue
            }
            if ($name -match '(?i)^name$' -and $hostContainerProperty) {
                $result[$name] = if ($null -eq $entry.Value) { $null } else { Get-ProofRedactionToken -Value ([string]$entry.Value) -Prefix 'HOST' -Context $Context }
                continue
            }
            if ($name -match '(?i)^displayName$' -and $identityProperty) {
                $result[$name] = if ($null -eq $entry.Value) { $null } else { Get-ProofRedactionToken -Value ([string]$entry.Value) -Prefix 'PERSON' -Context $Context }
                continue
            }
            if ($name -match '(?i)^id$' -and [string]$entry.Value -match '^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$') {
                $idPrefix = if ($identityProperty) { 'OBJECT_ID' } else { 'GUID' }
                $result[$name] = Get-ProofRedactionToken -Value ([string]$entry.Value) -Prefix $idPrefix -Context $Context
                continue
            }
            if ($name -match '(?i)(email|mail|uniqueName|principalName|userName|directoryAlias)$') {
                $result[$name] = if ($null -eq $entry.Value) { $null } else { Get-ProofRedactionToken -Value ([string]$entry.Value) -Prefix 'ADDRESS' -Context $Context }
                continue
            }
            $childHostContext = $name -match '(?i)^(agent|agents|agentPool|pool|repository|repositories)$'
            $result[$name] = ConvertTo-ProofRedactedValue -Value $entry.Value -Key $name -Context $Context -IdentityContext $identityProperty -HostContext $childHostContext
        }
        return $result
    }
    if (($Value -is [System.Collections.IEnumerable]) -and $Value -isnot [string]) {
        $items = [System.Collections.Generic.List[object]]::new()
        foreach ($item in $Value) {
            $items.Add((ConvertTo-ProofRedactedValue -Value $item -Key $Key -Context $Context -IdentityContext $identityProperty -HostContext $hostContainerProperty))
        }
        return ,$items.ToArray()
    }

    if ($Value -is [string]) {
        $text = $Value
        if ($hostProperty) { return Get-ProofRedactionToken -Value $text -Prefix 'HOST' -Context $Context }
        if ($Key -match '(?i)^displayName$' -and $IdentityContext) { return Get-ProofRedactionToken -Value $text -Prefix 'PERSON' -Context $Context }
        if ($Key -match '(?i)subscription.*id') { return Get-ProofRedactionToken -Value $text -Prefix 'SUBSCRIPTION_ID' -Context $Context }
        if ($Key -match '(?i)(object|principal|client|tenant|group|identity).*id|^projectId$|^organizationId$') { return Get-ProofRedactionToken -Value $text -Prefix 'OBJECT_ID' -Context $Context }
        if ($Key -match '(?i)(email|mail|uniqueName|principalName|userName|directoryAlias)$') { return Get-ProofRedactionToken -Value $text -Prefix 'ADDRESS' -Context $Context }
        if ($Key -match '(?i)^descriptor$') { return Get-ProofRedactionToken -Value $text -Prefix 'IDENTITY_DESCRIPTOR' -Context $Context }
        if ($Key -match '(?i)^(imageUrl|profileUrl)$') { return 'URL_REDACTED' }
        foreach ($organizationVariant in Get-ProofContextNameVariant -Value ([string]$Context.Organization)) {
            $text = [regex]::Replace($text, [regex]::Escape($organizationVariant), 'org-redacted', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        }
        foreach ($projectVariant in Get-ProofContextNameVariant -Value ([string]$Context.Project)) {
            $text = [regex]::Replace($text, [regex]::Escape($projectVariant), 'project-redacted', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        }
        $text = ConvertTo-ProofRedactedMatch -Text $text -Pattern '(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b' -Prefix 'ADDRESS' -Context $Context
        $text = ConvertTo-ProofRedactedMatch -Text $text -Pattern '(?i)\b(?:aad|msa|vssgp|svc|ent|imp|tfid|vssds|vssu)\.[A-Za-z0-9+/_=-]+' -Prefix 'IDENTITY_DESCRIPTOR' -Context $Context
        $text = ConvertTo-ProofRedactedMatch -Text $text -Pattern '(?i)(/subscriptions/)([0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12})' -Prefix 'SUBSCRIPTION_ID' -Context $Context -ValueGroup 2 -OutputPrefixGroup 1
        $text = ConvertTo-ProofRedactedMatch -Text $text -Pattern '(?i)\b[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\b' -Prefix 'GUID' -Context $Context
        $text = ConvertTo-ProofRedactedMatch -Text $text -Pattern '(?i)\b(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\.(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\.(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\.(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\b' -Prefix 'IP_ADDRESS' -Context $Context
        $text = [regex]::Replace($text, '(?i)\b(Bearer\s+)[A-Za-z0-9._~+/-]+=*', '$1SECRET_REDACTED')
        $text = [regex]::Replace($text, '(?i)(\bconnection[_-]?string\s*=\s*)("[^"]*"|''[^'']*''|[^ \r\n]+)', '$1SECRET_REDACTED')
        $text = [regex]::Replace($text, '(?i)(\bauthorization\s*[:=]\s*(?:Bearer|Basic)\s+)[A-Za-z0-9._~+/-]+=*', '$1SECRET_REDACTED')
        $text = [regex]::Replace($text, '(?i)((?:\b|_)(?:azure_devops_ext_pat|ado_recordings_pat|github_token|token|sig|sharedaccesssignature|sharedaccesskey|accountkey|password|pwd|secret|client[_-]?secret|access[_-]?token|refresh[_-]?token|api[_-]?key|personal[_-]?access[_-]?token|pat)\s*[:=]\s*)([^;\s&"''<>]+)', '$1SECRET_REDACTED')
        return $text
    }

    return $Value
}

function ConvertTo-ProofRedactedJson {
    param(
        [Parameter(Mandatory)][string] $Json,
        [Parameter(Mandatory)][string] $Organization,
        [Parameter(Mandatory)][string] $Project,
        [hashtable] $Context
    )

    if (-not (Get-Command ConvertFrom-Json).Parameters.ContainsKey('DateKind')) {
        throw 'La capture JSON nécessite PowerShell 7.5 ou plus récent pour préserver les timestamps exacts.'
    }
    $data = ConvertFrom-Json -InputObject $Json -AsHashtable -Depth 100 -DateKind String
    $context = $Context
    if ($null -eq $context) { $context = @{ Organization = $Organization; Project = $Project } }
    else {
        if ($context.ContainsKey('Organization') -and [string]$context.Organization -cne $Organization) { throw 'Le contexte d''anonymisation appartient a une autre organisation.' }
        if ($context.ContainsKey('Project') -and [string]$context.Project -cne $Project) { throw 'Le contexte d''anonymisation appartient a un autre projet.' }
        $context.Organization = $Organization
        $context.Project = $Project
    }
    $safe = ConvertTo-ProofRedactedValue -Value $data -Key $null -Context $context
    if (Test-ProofResidualSensitiveValue -Value $safe -Context $context) {
        throw 'La sortie conserve un identifiant ou une valeur sensible apres anonymisation ; la capture a ete annulee.'
    }
    $text = (ConvertTo-Json -InputObject $safe -Depth 100) -replace "`r`n", "`n"
    if (-not $text.EndsWith("`n", [StringComparison]::Ordinal)) { $text += "`n" }
    return $text
}

function ConvertTo-ProofRedactedText {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string] $Text,
        [Parameter(Mandatory)][string] $Organization,
        [Parameter(Mandatory)][string] $Project,
        [hashtable] $Context
    )

    $context = $Context
    if ($null -eq $context) { $context = @{ Organization = $Organization; Project = $Project } }
    else {
        if ($context.ContainsKey('Organization') -and [string]$context.Organization -cne $Organization) { throw 'Le contexte d''anonymisation appartient a une autre organisation.' }
        if ($context.ContainsKey('Project') -and [string]$context.Project -cne $Project) { throw 'Le contexte d''anonymisation appartient a un autre projet.' }
        $context.Organization = $Organization
        $context.Project = $Project
    }
    $safe = [string](ConvertTo-ProofRedactedValue -Value $Text -Key $null -Context $context)
    if (Test-ProofResidualSensitiveValue -Value $safe -Context $context) {
        throw 'Le texte conserve un identifiant ou une valeur sensible apres anonymisation ; la capture a ete annulee.'
    }
    return $safe
}

Export-ModuleMember -Function @(
    'Assert-ProofProjectCode',
    'Assert-ProofGuid',
    'Assert-ProofSeparateRepository',
    'Get-ProofReplacementTable',
    'Invoke-ProofTextReplacement',
    'Get-ProofManagedFileList',
    'Write-ProofManifest',
    'Assert-ProofManifestWorkingTree',
    'ConvertTo-ProofRedactedJson',
    'ConvertTo-ProofRedactedText'
)
