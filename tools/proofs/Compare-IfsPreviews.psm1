Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:PreviewCollections = @(
    'pendingOperations',
    'created',
    'modified',
    'recreated',
    'detachedOrDeleted',
    'revokedAccess',
    'dataWrites',
    'appOwnedState'
)
$script:ResourceCollections = @(
    'created',
    'modified',
    'recreated',
    'detachedOrDeleted',
    'revokedAccess',
    'dataWrites',
    'appOwnedState'
)

function Test-IfsDictionary {
    param([AllowNull()][object] $Value)

    return $null -ne $Value -and $Value -is [System.Collections.IDictionary]
}

function Test-IfsArray {
    param([AllowNull()][object] $Value)

    return $null -ne $Value -and $Value -is [System.Collections.IEnumerable] -and
        $Value -isnot [string] -and -not (Test-IfsDictionary $Value)
}

function Get-IfsValue {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Object,
        [Parameter(Mandatory)][string] $Name
    )

    if ($Object.Contains($Name)) {
        return $Object[$Name]
    }

    return $null
}

function Get-CanonicalJson {
    param(
        [AllowNull()][object] $Value,
        [switch] $SortDeltaChildren
    )

    if ($null -eq $Value) {
        return 'null'
    }

    if (Test-IfsDictionary $Value) {
        $keys = [string[]]@($Value.Keys | ForEach-Object { [string]$_ })
        [Array]::Sort($keys, [StringComparer]::Ordinal)
        $parts = [System.Collections.Generic.List[string]]::new()
        foreach ($key in $keys) {
            $keyJson = ConvertTo-Json -InputObject $key -Compress -Depth 100
            $child = $Value[$key]
            if ($SortDeltaChildren -and $key -ceq 'children' -and (Test-IfsArray $child)) {
                $childJson = Get-UnorderedArrayCanonicalJson -Values $child -SortDeltaChildren
            }
            else {
                $childJson = Get-CanonicalJson -Value $child -SortDeltaChildren:$SortDeltaChildren
            }
            $parts.Add("${keyJson}:$childJson")
        }
        return '{' + ($parts -join ',') + '}'
    }

    if (Test-IfsArray $Value) {
        $parts = [System.Collections.Generic.List[string]]::new()
        foreach ($item in $Value) {
            $parts.Add((Get-CanonicalJson -Value $item -SortDeltaChildren:$SortDeltaChildren))
        }
        return '[' + ($parts -join ',') + ']'
    }

    return ConvertTo-Json -InputObject $Value -Compress -Depth 100
}

function Get-UnorderedArrayCanonicalJson {
    param(
        [Parameter(Mandatory)][object] $Values,
        [switch] $SortDeltaChildren
    )

    $canonicalItems = [System.Collections.Generic.List[string]]::new()
    foreach ($item in $Values) {
        $canonicalItems.Add((Get-CanonicalJson -Value $item -SortDeltaChildren:$SortDeltaChildren))
    }

    [string[]]$sortedItems = $canonicalItems.ToArray()
    [Array]::Sort($sortedItems, [StringComparer]::Ordinal)
    return '[' + ($sortedItems -join ',') + ']'
}

function Get-RootValueCanonicalJson {
    param([AllowNull()][object] $Value)

    if (Test-IfsArray $Value) {
        return Get-UnorderedArrayCanonicalJson -Values $Value
    }

    return Get-CanonicalJson -Value $Value
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)][string] $Text)

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return [Convert]::ToHexString($algorithm.ComputeHash($bytes)).ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-SafePath {
    param([AllowNull()][object] $Path)

    $text = [string]$Path
    if ([string]::IsNullOrWhiteSpace($text) -or $text.Length -gt 160 -or
        $text -notmatch '^[A-Za-z0-9_.\[\]-]+$' -or
        $text -match '(?i)subscriptions/|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}') {
        return '<redacted>'
    }

    return $text
}

function New-IfsDifference {
    param(
        [Parameter(Mandatory)][string] $Classification,
        [Parameter(Mandatory)][string] $Collection,
        [AllowNull()][string] $ResourceKey,
        [AllowNull()][string] $Path
    )

    return [pscustomobject]@{
        classification = $Classification
        collection = $Collection
        resourceKey = $ResourceKey
        path = $Path
    }
}

function New-IfsComparisonResult {
    param(
        [Parameter(Mandatory)][string] $Verdict,
        [string[]] $Classifications = @(),
        [AllowEmptyCollection()][object[]] $Differences = @(),
        [AllowNull()][object] $Metadata = $null
    )

    return [pscustomobject]@{
        Verdict = $Verdict
        Classifications = @($Classifications | Sort-Object -Unique)
        Differences = @($Differences)
        Metadata = $Metadata
    }
}

function Read-IfsPreview {
    param([Parameter(Mandatory)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [pscustomobject]@{ Ok = $false; Failure = 'MissingInput'; Preview = $null }
    }

    try {
        $json = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path).Path)
        $preview = ConvertFrom-Json -InputObject $json -AsHashtable -Depth 100 -ErrorAction Stop
    }
    catch {
        return [pscustomobject]@{ Ok = $false; Failure = 'InvalidInput'; Preview = $null }
    }

    if (-not (Test-IfsDictionary $preview)) {
        return [pscustomobject]@{ Ok = $false; Failure = 'InvalidInput'; Preview = $null }
    }

    return [pscustomobject]@{ Ok = $true; Failure = $null; Preview = $preview }
}

function Test-IfsPreviewShape {
    param([Parameter(Mandatory)][System.Collections.IDictionary] $Preview)

    foreach ($name in @('schema', 'project', 'component', 'target', 'unit', 'fingerprint')) {
        $value = Get-IfsValue -Object $Preview -Name $name
        if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) {
            return $false
        }
    }

    if ($Preview.generatedAt -isnot [string] -and $Preview.generatedAt -isnot [datetime]) {
        return $false
    }

    if ([string]$Preview.schema -cne 'ifs-preview/v1' -or
        [string]$Preview.fingerprint -notmatch '^[0-9a-fA-F]{64}$') {
        return $false
    }

    foreach ($name in $script:PreviewCollections + @('limitations')) {
        if (-not $Preview.Contains($name) -or -not (Test-IfsArray $Preview[$name])) {
            return $false
        }
    }

    foreach ($name in $script:ResourceCollections) {
        foreach ($entry in $Preview[$name]) {
            if (-not (Test-IfsDictionary $entry)) {
                return $false
            }
        }
    }

    foreach ($entry in $Preview.modified) {
        if (-not $entry.Contains('resourceId') -or -not $entry.Contains('changeType') -or
            -not $entry.Contains('delta') -or -not (Test-IfsArray $entry.delta)) {
            return $false
        }
        foreach ($delta in $entry.delta) {
            if (-not (Test-IfsDictionary $delta) -or -not $delta.Contains('path') -or
                -not $delta.Contains('propertyChangeType')) {
                return $false
            }
        }
    }

    return $true
}

function ConvertTo-IfsTimestamp {
    param([AllowNull()][object] $Value)

    if ($Value -is [datetime]) {
        return $Value.ToUniversalTime().ToString('o', [System.Globalization.CultureInfo]::InvariantCulture)
    }

    return [string]$Value
}

function Get-EntryIdentity {
    param(
        [Parameter(Mandatory)][string] $Collection,
        [Parameter(Mandatory)][object] $Entry
    )

    if (Test-IfsDictionary $Entry) {
        if ($Entry.Contains('resourceId') -and -not [string]::IsNullOrWhiteSpace([string]$Entry.resourceId)) {
            return 'resource:' + [string]$Entry.resourceId
        }
        if ($Entry.Contains('name') -and -not [string]::IsNullOrWhiteSpace([string]$Entry.name)) {
            $kind = if ($Entry.Contains('kind')) { [string]$Entry.kind } else { '' }
            return 'named:' + $kind + ':' + [string]$Entry.name
        }
        if ($Entry.Contains('symbolicName') -and -not [string]::IsNullOrWhiteSpace([string]$Entry.symbolicName)) {
            return 'symbolic:' + [string]$Entry.symbolicName
        }
    }

    return 'entry:' + (Get-CanonicalJson -Value $Entry -SortDeltaChildren)
}

function New-EntryMap {
    param(
        [Parameter(Mandatory)][string] $Collection,
        [Parameter(Mandatory)][object] $Entries
    )

    $map = [System.Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach ($entry in $Entries) {
        $identity = Get-EntryIdentity -Collection $Collection -Entry $entry
        $token = Get-Sha256Hex -Text ($Collection + [char]0 + $identity)
        if (-not $map.ContainsKey($token)) {
            $map.Add($token, [System.Collections.Generic.List[object]]::new())
        }
        $map[$token].Add($entry)
    }

    return ,$map
}

function Add-IfsDifference {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[object]] $Differences,
        [Parameter(Mandatory)][string] $Classification,
        [Parameter(Mandatory)][string] $Collection,
        [AllowNull()][string] $ResourceKey,
        [AllowNull()][string] $Path
    )

    $Differences.Add((New-IfsDifference -Classification $Classification -Collection $Collection `
        -ResourceKey $ResourceKey -Path $Path))
}

function Compare-IfsDeltas {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[object]] $Differences,
        [Parameter(Mandatory)][string] $ResourceKey,
        [Parameter(Mandatory)][object] $LeftDeltas,
        [Parameter(Mandatory)][object] $RightDeltas
    )

    $leftMap = [System.Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    $rightMap = [System.Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach ($side in @(@{ Items = $LeftDeltas; Map = $leftMap }, @{ Items = $RightDeltas; Map = $rightMap })) {
        foreach ($delta in $side.Items) {
            $identity = [string]$delta.path + [char]0 + [string]$delta.propertyChangeType
            $key = Get-Sha256Hex -Text $identity
            if (-not $side.Map.ContainsKey($key)) {
                $side.Map.Add($key, [System.Collections.Generic.List[object]]::new())
            }
            $side.Map[$key].Add($delta)
        }
    }

    $keys = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($key in $leftMap.Keys) { [void]$keys.Add($key) }
    foreach ($key in $rightMap.Keys) { [void]$keys.Add($key) }

    foreach ($key in $keys) {
        $hasLeft = $leftMap.ContainsKey($key)
        $hasRight = $rightMap.ContainsKey($key)
        $different = -not ($hasLeft -and $hasRight)
        if (-not $different) {
            $leftJson = Get-UnorderedArrayCanonicalJson -Values $leftMap[$key] -SortDeltaChildren
            $rightJson = Get-UnorderedArrayCanonicalJson -Values $rightMap[$key] -SortDeltaChildren
            $different = $leftJson -cne $rightJson
        }

        if ($different) {
            $delta = if ($hasLeft) { $leftMap[$key][0] } else { $rightMap[$key][0] }
            $safePath = Get-SafePath -Path $delta.path
            Add-IfsDifference -Differences $Differences -Classification 'PropertyDelta' `
                -Collection 'modified' -ResourceKey $ResourceKey -Path $safePath
        }
    }
}

function Compare-IfsModifiedEntry {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[object]] $Differences,
        [Parameter(Mandatory)][string] $ResourceKey,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Left,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Right
    )

    if ([string]$Left.changeType -cne [string]$Right.changeType) {
        Add-IfsDifference -Differences $Differences -Classification 'ChangeType' `
            -Collection 'modified' -ResourceKey $ResourceKey -Path 'changeType'
    }

    Compare-IfsDeltas -Differences $Differences -ResourceKey $ResourceKey `
        -LeftDeltas $Left.delta -RightDeltas $Right.delta

    $leftFields = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    $rightFields = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($field in $Left.Keys) { if ($field -cnotin @('resourceId', 'changeType', 'delta')) { [void]$leftFields.Add([string]$field) } }
    foreach ($field in $Right.Keys) { if ($field -cnotin @('resourceId', 'changeType', 'delta')) { [void]$rightFields.Add([string]$field) } }
    $fields = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($field in $leftFields) { [void]$fields.Add($field) }
    foreach ($field in $rightFields) { [void]$fields.Add($field) }

    foreach ($field in $fields) {
        $hasLeft = $Left.Contains($field)
        $hasRight = $Right.Contains($field)
        $different = -not ($hasLeft -and $hasRight)
        if (-not $different) {
            $different = (Get-CanonicalJson -Value $Left[$field]) -cne (Get-CanonicalJson -Value $Right[$field])
        }
        if ($different) {
            $safeField = Get-SafePath -Path $field
            Add-IfsDifference -Differences $Differences -Classification 'EffectDetail' `
                -Collection 'modified' -ResourceKey $ResourceKey -Path ('modified.' + $safeField)
        }
    }
}

function Compare-IfsCollection {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[object]] $Differences,
        [Parameter(Mandatory)][string] $Collection,
        [Parameter(Mandatory)][object] $LeftEntries,
        [Parameter(Mandatory)][object] $RightEntries,
        [switch] $CompareModified
    )

    $leftMap = New-EntryMap -Collection $Collection -Entries $LeftEntries
    $rightMap = New-EntryMap -Collection $Collection -Entries $RightEntries
    $keys = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($key in $leftMap.Keys) { [void]$keys.Add($key) }
    foreach ($key in $rightMap.Keys) { [void]$keys.Add($key) }

    foreach ($key in $keys) {
        $hasLeft = $leftMap.ContainsKey($key)
        $hasRight = $rightMap.ContainsKey($key)
        $resourceKey = $key.Substring(0, 12)
        if (-not ($hasLeft -and $hasRight)) {
            Add-IfsDifference -Differences $Differences -Classification 'Resources' `
                -Collection $Collection -ResourceKey $resourceKey -Path $null
            continue
        }

        $leftItems = $leftMap[$key]
        $rightItems = $rightMap[$key]
        $leftJson = Get-UnorderedArrayCanonicalJson -Values $leftItems -SortDeltaChildren
        $rightJson = Get-UnorderedArrayCanonicalJson -Values $rightItems -SortDeltaChildren
        if ($leftJson -ceq $rightJson) {
            continue
        }

        if ($CompareModified -and $leftItems.Count -eq 1 -and $rightItems.Count -eq 1) {
            Compare-IfsModifiedEntry -Differences $Differences -ResourceKey $resourceKey `
                -Left $leftItems[0] -Right $rightItems[0]
        }
        else {
            Add-IfsDifference -Differences $Differences -Classification 'EffectDetail' `
                -Collection $Collection -ResourceKey $resourceKey -Path $Collection
        }
    }
}

function Compare-IfsRootEffects {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[object]] $Differences,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Left,
        [Parameter(Mandatory)][System.Collections.IDictionary] $Right
    )

    $excluded = @('schema', 'project', 'component', 'target', 'unit', 'generatedAt', 'fingerprint') +
        $script:PreviewCollections + @('limitations')
    $leftFields = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    $rightFields = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($field in $Left.Keys) { if ($field -cnotin $excluded) { [void]$leftFields.Add([string]$field) } }
    foreach ($field in $Right.Keys) { if ($field -cnotin $excluded) { [void]$rightFields.Add([string]$field) } }
    $fields = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($field in $leftFields) { [void]$fields.Add($field) }
    foreach ($field in $rightFields) { [void]$fields.Add($field) }

    foreach ($field in $fields) {
        $hasLeft = $Left.Contains($field)
        $hasRight = $Right.Contains($field)
        $different = -not ($hasLeft -and $hasRight)
        if (-not $different) {
            $different = (Get-RootValueCanonicalJson -Value $Left[$field]) -cne
                (Get-RootValueCanonicalJson -Value $Right[$field])
        }
        if ($different) {
            $safeField = Get-SafePath -Path $field
            Add-IfsDifference -Differences $Differences -Classification 'EffectDetail' `
                -Collection 'root' -ResourceKey $null -Path $safeField
        }
    }

    $leftLimitations = Get-UnorderedArrayCanonicalJson -Values $Left.limitations
    $rightLimitations = Get-UnorderedArrayCanonicalJson -Values $Right.limitations
    if ($leftLimitations -cne $rightLimitations) {
        Add-IfsDifference -Differences $Differences -Classification 'EffectDetail' `
            -Collection 'limitations' -ResourceKey $null -Path 'limitations'
    }
}

function Compare-IfsPreviews {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $LeftPath,
        [Parameter(Mandatory)][string] $RightPath
    )

    $leftRead = Read-IfsPreview -Path $LeftPath
    $rightRead = Read-IfsPreview -Path $RightPath
    $failures = [System.Collections.Generic.List[string]]::new()
    foreach ($read in @($leftRead, $rightRead)) {
        if (-not $read.Ok) {
            $failures.Add([string]$read.Failure)
        }
    }
    $failures = @($failures | Sort-Object -Unique)
    if ($failures.Count -gt 0) {
        return New-IfsComparisonResult -Verdict 'NonConcluant' -Classifications $failures
    }

    $left = $leftRead.Preview
    $right = $rightRead.Preview
    if (-not (Test-IfsPreviewShape $left) -or -not (Test-IfsPreviewShape $right)) {
        return New-IfsComparisonResult -Verdict 'NonConcluant' -Classifications @('IncompatiblePreview')
    }

    foreach ($field in @('schema', 'project', 'component', 'target', 'unit')) {
        if ([string]$left[$field] -cne [string]$right[$field]) {
            return New-IfsComparisonResult -Verdict 'NonConcluant' -Classifications @('IncompatiblePreview')
        }
    }

    $metadata = [pscustomobject]@{
        Left = [pscustomobject]@{
            GeneratedAt = ConvertTo-IfsTimestamp $left.generatedAt
            Fingerprint = [string]$left.fingerprint
        }
        Right = [pscustomobject]@{
            GeneratedAt = ConvertTo-IfsTimestamp $right.generatedAt
            Fingerprint = [string]$right.fingerprint
        }
    }

    $differences = [System.Collections.Generic.List[object]]::new()
    foreach ($collection in $script:ResourceCollections) {
        Compare-IfsCollection -Differences $differences -Collection $collection `
            -LeftEntries $left[$collection] -RightEntries $right[$collection] `
            -CompareModified:($collection -ceq 'modified')
    }
    Compare-IfsCollection -Differences $differences -Collection 'pendingOperations' `
        -LeftEntries $left.pendingOperations -RightEntries $right.pendingOperations
    Compare-IfsRootEffects -Differences $differences -Left $left -Right $right

    if ([string]$left.fingerprint -cne [string]$right.fingerprint) {
        Add-IfsDifference -Differences $differences -Classification 'Fingerprint' `
            -Collection 'root' -ResourceKey $null -Path 'fingerprint'
    }

    $classifications = @($differences | ForEach-Object { $_.classification } | Sort-Object -Unique)
    $verdict = if ($differences.Count -eq 0) { 'Identique' } else { 'Divergent' }
    $orderedDifferences = @($differences | Sort-Object collection, resourceKey, path, classification)
    return New-IfsComparisonResult -Verdict $verdict -Classifications $classifications `
        -Differences $orderedDifferences -Metadata $metadata
}

Export-ModuleMember -Function Compare-IfsPreviews
