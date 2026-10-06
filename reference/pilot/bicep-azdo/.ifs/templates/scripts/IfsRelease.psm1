Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:IfsSchemaDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../../release-module/schemas'))

function Invoke-IfsAz {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string[]] $Arguments, [switch] $AllowFailure)
    $nativeOutput = @(& az @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $text = ($nativeOutput | ForEach-Object { [string] $_ }) -join [Environment]::NewLine
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw ('Azure CLI a échoué (code {0}) pour az {1}. {2}' -f $exitCode, ($Arguments -join ' '), $text)
    }
    return [pscustomobject]@{ ExitCode = $exitCode; Text = $text }
}

function ConvertFrom-IfsJson {
    param([AllowEmptyString()][string] $Json)
    if ([string]::IsNullOrWhiteSpace($Json)) { return $null }
    try { return ConvertFrom-Json -InputObject $Json -Depth 100 }
    catch { throw ('La réponse Azure CLI n''est pas un JSON valide : {0}' -f $_.Exception.Message) }
}

function Get-IfsValue {
    param([AllowNull()][object] $InputObject, [Parameter(Mandatory)][string] $Name, [AllowNull()][object] $Default = $null)
    if ($null -eq $InputObject) { return $Default }
    if ($InputObject -is [System.Collections.IDictionary]) {
        if ($InputObject.Contains($Name)) { return $InputObject[$Name] }
        return $Default
    }
    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -ne $property) { return $property.Value }
    return $Default
}

function Get-IfsSchemaPath {
    param([Parameter(Mandatory)][string] $Name)
    $path = Join-Path $script:IfsSchemaDirectory $Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw ('Schéma absent : {0}' -f $path) }
    return $path
}

function Get-IfsStorageCoordinate {
    param([Parameter(Mandatory)][object] $ReleaseData)
    $project = [string] (Get-IfsValue $ReleaseData 'project')
    $target = [string] (Get-IfsValue $ReleaseData 'target')
    $account = ('stifs{0}{1}' -f $project, $target).ToLowerInvariant() -replace '[^a-z0-9]', ''
    if ($account.Length -lt 3 -or $account.Length -gt 24) {
        throw ('Le compte de journal calculé ({0}) doit contenir de 3 à 24 caractères.' -f $account)
    }
    return [pscustomobject]@{ Account = $account; Container = 'ifs-operations'; Blob = ('{0}.json' -f $ReleaseData.unit) }
}

function Test-IfsBlobExistence {
    param([Parameter(Mandatory)][object] $Coordinates)
    $result = Invoke-IfsAz -Arguments @(
        'storage', 'blob', 'exists', '--account-name', $Coordinates.Account,
        '--container-name', $Coordinates.Container, '--name', $Coordinates.Blob,
        '--auth-mode', 'login', '--query', 'exists', '--output', 'tsv'
    ) -AllowFailure
    if ($result.ExitCode -ne 0) {
        if ($result.Text -match 'ContainerNotFound|ResourceNotFound|StatusCode=404|404') { return $false }
        throw ('Impossible de lire le journal Azure : {0}' -f $result.Text)
    }
    return $result.Text.Trim() -match '^(true|1)$'
}

function Read-IfsJournalBlob {
    param([Parameter(Mandatory)][object] $Coordinates, [AllowNull()][string] $LeaseId)
    $temporaryFile = Join-Path ([IO.Path]::GetTempPath()) ('ifs-journal-read-{0}.json' -f [guid]::NewGuid().ToString('N'))
    try {
        $arguments = @(
            'storage', 'blob', 'download', '--account-name', $Coordinates.Account,
            '--container-name', $Coordinates.Container, '--name', $Coordinates.Blob,
            '--file', $temporaryFile, '--overwrite', 'true', '--auth-mode', 'login', '--output', 'none'
        )
        if ($LeaseId) { $arguments += @('--lease-id', $LeaseId) }
        [void] (Invoke-IfsAz -Arguments $arguments)
        if (-not (Test-Path -LiteralPath $temporaryFile)) { throw 'Azure CLI n''a pas téléchargé le journal.' }
        $journalJson = Get-Content -LiteralPath $temporaryFile -Raw
        try {
            $journalValid = Test-Json -Json $journalJson -SchemaFile (Get-IfsSchemaPath 'operation-journal.schema.json') -ErrorAction Stop
        }
        catch {
            throw ('Le journal d''opérations ne respecte pas operation-journal/v1 : {0}' -f $_.Exception.Message)
        }
        if (-not $journalValid) {
            throw 'Le journal d''opérations ne respecte pas operation-journal/v1.'
        }
        $journal = ConvertFrom-Json -InputObject $journalJson -Depth 100
        if ((Get-IfsValue $journal 'unit') -ne $Coordinates.Blob.Replace('.json', '')) { throw 'Le journal chargé appartient à une autre unité.' }
        return $journal
    }
    finally { Remove-Item -LiteralPath $temporaryFile -Force -ErrorAction SilentlyContinue }
}

function Get-IfsTemporaryFirewallRuleCreatedAt {
    param(
        [Parameter(Mandatory)][object] $Rule,
        [Parameter(Mandatory)][string] $ResourceId
    )
    $createdAt = $null
    if ([string]$Rule.name -match '^ifs-temp-(?<timestamp>\d{14})(?:-|$)') {
        try {
            $createdAt = [DateTimeOffset]::ParseExact(
                $Matches.timestamp,
                'yyyyMMddHHmmss',
                [Globalization.CultureInfo]::InvariantCulture,
                [Globalization.DateTimeStyles]::AssumeUniversal
            )
        }
        catch { $createdAt = $null }
    }
    if (-not $createdAt) { $createdAt = Get-IfsValue (Get-IfsValue $Rule 'systemData') 'createdAt' }
    if (-not $createdAt) {
        $createdAt = (Invoke-IfsAz -Arguments @('resource', 'show', '--ids', $ResourceId, '--query', 'systemData.createdAt', '--output', 'tsv') -AllowFailure).Text.Trim()
    }
    if ($createdAt -is [DateTimeOffset]) { return $createdAt }
    $created = [DateTimeOffset]::MinValue
    if ($createdAt -and [DateTimeOffset]::TryParse([string]$createdAt, [ref]$created)) { return $created }
    return $null
}

function Get-IfsFirewallRunId {
    param([Parameter(Mandatory)][string] $RunId)
    if ($RunId -match '^\d{14}-') { return $RunId }
    return ('{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMddHHmmss'), $RunId)
}

function Save-IfsJournalBlob {
    param([Parameter(Mandatory)][object] $Context)
    if ($Context.RenewalFailureFile -and (Test-Path -LiteralPath $Context.RenewalFailureFile)) {
        throw ('Le renouvellement du bail du journal a échoué : {0}' -f (Get-Content -LiteralPath $Context.RenewalFailureFile -Raw))
    }
    $temporaryFile = Join-Path ([IO.Path]::GetTempPath()) ('ifs-journal-write-{0}.json' -f [guid]::NewGuid().ToString('N'))
    try {
        [IO.File]::WriteAllText($temporaryFile, (ConvertTo-Json -InputObject $Context.Journal -Depth 100), [Text.UTF8Encoding]::new($false))
        $arguments = @(
            'storage', 'blob', 'upload', '--account-name', $Context.Account,
            '--container-name', $Context.Container, '--name', $Context.Blob,
            '--file', $temporaryFile, '--overwrite', 'true', '--auth-mode', 'login',
            '--lease-id', $Context.LeaseId, '--output', 'none'
        )
        [void] (Invoke-IfsAz -Arguments $arguments)
    }
    finally { Remove-Item -LiteralPath $temporaryFile -Force -ErrorAction SilentlyContinue }
}

function ConvertTo-IfsCanonicalValue {
    param([AllowNull()][object] $Value, [switch] $ExcludeAppOwnedState)
    if ($null -eq $Value) { return $null }
    if ($Value -is [System.Collections.IDictionary]) {
        $ordered = [ordered]@{}
        foreach ($key in @($Value.Keys | Sort-Object -Culture ([Globalization.CultureInfo]::InvariantCulture))) {
            if ($ExcludeAppOwnedState -and [string]$key -eq 'appOwnedState') { continue }
            $ordered[[string]$key] = ConvertTo-IfsCanonicalValue -Value $Value[$key] -ExcludeAppOwnedState:$ExcludeAppOwnedState
        }
        return $ordered
    }
    if ($Value -is [pscustomobject]) {
        $ordered = [ordered]@{}
        foreach ($property in @($Value.PSObject.Properties | Sort-Object Name -Culture ([Globalization.CultureInfo]::InvariantCulture))) {
            if ($ExcludeAppOwnedState -and $property.Name -eq 'appOwnedState') { continue }
            $ordered[$property.Name] = ConvertTo-IfsCanonicalValue -Value $property.Value -ExcludeAppOwnedState:$ExcludeAppOwnedState
        }
        return $ordered
    }
    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        $items = [System.Collections.Generic.List[object]]::new()
        foreach ($item in $Value) { $items.Add((ConvertTo-IfsCanonicalValue -Value $item -ExcludeAppOwnedState:$ExcludeAppOwnedState)) }
        return ,$items.ToArray()
    }
    return $Value
}

function Test-IfsNotFoundError {
    param([string] $Message)
    return $Message -match 'ResourceNotFound|NotFound|StatusCode=404|404|was not found'
}

function Get-IfsChangeType {
    param([AllowNull()][object] $Change)
    return [string](Get-IfsValue $Change 'changeType' (Get-IfsValue $Change 'change_type' ''))
}

function Get-IfsWhatIfChange {
    param([AllowNull()][object] $WhatIf)
    $changes = Get-IfsValue $WhatIf 'changes' @()
    return @($changes)
}

function Get-IfsManifestInfo {
    param([Parameter(Mandatory)][string] $ManifestPath)
    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) { throw ('Le manifeste de la révision figée est absent : {0}' -f $ManifestPath) }
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json -Depth 100
    $revision = Get-IfsValue $manifest 'revision'
    $commit = Get-IfsValue $manifest 'commit'
    if ($null -eq $revision -or [string]::IsNullOrWhiteSpace([string]$commit)) { throw 'Le manifeste doit porter une révision et un commit.' }
    return [pscustomobject]@{ Manifest = $manifest; Revision = [int]$revision; Commit = [string]$commit }
}

function Get-IfsAppChangeFingerprintView {
    param([object[]] $Changes, [object] $ReleaseData)
    $appResourceIds = @($ReleaseData.appOwnedState | ForEach-Object { [string]$_.resourceId })
    $filtered = [System.Collections.Generic.List[object]]::new()
    foreach ($change in $Changes) {
        $resourceId = [string](Get-IfsValue $change 'resourceId' (Get-IfsValue $change 'id' ''))
        $appState = @($appResourceIds | Where-Object { $_ -ieq $resourceId }).Count -gt 0
        if (-not $appState) { $filtered.Add($change); continue }
        $propertyChanges = Get-IfsValue $change 'delta' $null
        if ($null -eq $propertyChanges) { $propertyChanges = Get-IfsValue $change 'propertyChanges' $null }
        if ($null -eq $propertyChanges) {
            $filtered.Add([ordered]@{ resourceId = $resourceId; changeType = Get-IfsChangeType $change })
            continue
        }
        $kept = @($propertyChanges | Where-Object { [string](Get-IfsValue $_ 'path' '') -notmatch '(?i)(image|traffic|revision|activeRevisionsMode)' })
        if ($kept.Count -gt 0) { $filtered.Add([ordered]@{ resourceId = $resourceId; changeType = Get-IfsChangeType $change; propertyChanges = $kept }) }
    }
    return $filtered.ToArray()
}

function Test-IfsReleaseData {
    <#
    .SYNOPSIS
    Valide les données non secrètes d'une release IFS.
    .DESCRIPTION
    Vérifie le document de release avec le schéma JSON livré avec le module.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][object] $ReleaseData)
    $json = ConvertTo-Json -InputObject $ReleaseData -Depth 100
    if (-not (Test-Json -Json $json -SchemaFile (Join-Path $script:IfsSchemaDirectory 'release-data.schema.json'))) {
        throw 'Les données de release ne respectent pas le schéma ifs-release/v1.'
    }
    return $true
}

function Read-IfsReleaseData {
    <#
    .SYNOPSIS
    Lit et valide le fichier JSON d'une release.
    .DESCRIPTION
    Le fichier contient uniquement des métadonnées et des références, jamais une valeur secrète.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string] $Path)
    $raw = Get-Content -LiteralPath $Path -Raw
    if (-not (Test-Json -Json $raw -SchemaFile (Join-Path $script:IfsSchemaDirectory 'release-data.schema.json'))) {
        throw ('Le fichier de release ne respecte pas le schéma : {0}' -f $Path)
    }
    return ConvertFrom-Json -InputObject $raw -Depth 100
}

function Read-IfsOperationJournal {
    <#
    .SYNOPSIS
    Lit les opérations en attente sans modifier le journal.
    .DESCRIPTION
    Cette lecture ne prend aucun bail et n'effectue aucun appel d'écriture. Un journal absent est vide.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][object] $ReleaseData)
    $coordinates = Get-IfsStorageCoordinate $ReleaseData
    if (-not (Test-IfsBlobExistence $coordinates)) {
        return [pscustomobject]@{ schema = 'ifs-operation-journal/v1'; unit = $ReleaseData.unit; target = $ReleaseData.target; operations = @() }
    }
    return Read-IfsJournalBlob -Coordinates $coordinates
}

function Open-IfsOperationJournal {
    <#
    .SYNOPSIS
    Ouvre le journal sous un bail exclusif renouvelé pendant la release.
    .DESCRIPTION
    Crée le conteneur et le journal si nécessaire, puis attend au plus dix minutes un bail déjà détenu.
    Fermez le contexte avec Close-IfsOperationJournal dans un bloc finally.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object] $ReleaseData,
        [ValidateRange(0, 600)][int] $WaitTimeoutSeconds = 600,
        [ValidateRange(1, 30)][int] $PollIntervalSeconds = 5
    )
    $coordinates = Get-IfsStorageCoordinate $ReleaseData
    [void](Invoke-IfsAz -Arguments @('storage', 'container', 'create', '--account-name', $coordinates.Account, '--name', $coordinates.Container, '--auth-mode', 'login', '--public-access', 'off', '--output', 'none') -AllowFailure)
    if (-not (Test-IfsBlobExistence $coordinates)) {
        $initial = [pscustomobject]@{ schema = 'ifs-operation-journal/v1'; unit = $ReleaseData.unit; target = $ReleaseData.target; operations = @() }
        $temporaryFile = Join-Path ([IO.Path]::GetTempPath()) ('ifs-journal-init-{0}.json' -f [guid]::NewGuid().ToString('N'))
        try {
            [IO.File]::WriteAllText($temporaryFile, (ConvertTo-Json $initial -Depth 20), [Text.UTF8Encoding]::new($false))
            [void](Invoke-IfsAz -Arguments @('storage', 'blob', 'upload', '--account-name', $coordinates.Account, '--container-name', $coordinates.Container, '--name', $coordinates.Blob, '--file', $temporaryFile, '--overwrite', 'false', '--auth-mode', 'login', '--output', 'none') -AllowFailure)
        }
        finally { Remove-Item -LiteralPath $temporaryFile -Force -ErrorAction SilentlyContinue }
    }
    $deadline = [DateTime]::UtcNow.AddSeconds($WaitTimeoutSeconds)
    $leaseId = $null
    do {
        $attempt = Invoke-IfsAz -Arguments @('storage', 'blob', 'lease', 'acquire', '--account-name', $coordinates.Account, '--container-name', $coordinates.Container, '--blob-name', $coordinates.Blob, '--lease-duration', '60', '--auth-mode', 'login', '--output', 'tsv') -AllowFailure
        if ($attempt.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($attempt.Text)) { $leaseId = $attempt.Text.Trim().Trim('"'); break }
        if ($attempt.Text -notmatch '(?i)LeaseAlreadyPresent|lease.*(held|already|locked)|StatusCode=409|HTTP.?409|condition.?not.?met|LeaseIdMissing') { throw ('Échec de prise du bail du journal : {0}' -f $attempt.Text) }
        if ([DateTime]::UtcNow -ge $deadline) { throw ('Le journal {0} est verrouillé par un autre run depuis plus de {1} secondes.' -f $coordinates.Blob, $WaitTimeoutSeconds) }
        Start-Sleep -Seconds ([Math]::Min($PollIntervalSeconds, [Math]::Max(1, ($deadline - [DateTime]::UtcNow).TotalSeconds)))
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $leaseId) { throw ('Impossible d''obtenir le bail exclusif du journal {0}.' -f $coordinates.Blob) }

    $context = [pscustomobject]@{ Account = $coordinates.Account; Container = $coordinates.Container; Blob = $coordinates.Blob; LeaseId = $leaseId; Journal = $null; RenewalProcess = $null; RenewalStopFile = $null; RenewalFailureFile = $null; ReleaseData = $ReleaseData; Closed = $false }
    try {
        $context.Journal = Read-IfsJournalBlob -Coordinates $coordinates -LeaseId $leaseId
        $context.RenewalStopFile = Join-Path ([IO.Path]::GetTempPath()) ('ifs-lease-stop-{0}' -f [guid]::NewGuid().ToString('N'))
        $context.RenewalFailureFile = Join-Path ([IO.Path]::GetTempPath()) ('ifs-lease-failure-{0}.txt' -f [guid]::NewGuid().ToString('N'))
        $renewalScript = Join-Path $PSScriptRoot 'IfsLeaseRenewal.ps1'
        $pwshPath = (Get-Command pwsh -ErrorAction Stop).Source
        $processArguments = @(
            '-NoProfile', '-NonInteractive', '-File', ('"{0}"' -f $renewalScript),
            '-Account', $coordinates.Account, '-Container', $coordinates.Container, '-Blob', $coordinates.Blob,
            '-LeaseId', $leaseId, '-StopFile', ('"{0}"' -f $context.RenewalStopFile),
            '-FailureFile', ('"{0}"' -f $context.RenewalFailureFile)
        ) -join ' '
        $startParameters = @{ FilePath = $pwshPath; ArgumentList = $processArguments; PassThru = $true }
        if ($IsWindows) { $startParameters.WindowStyle = 'Hidden' }
        $context.RenewalProcess = Start-Process @startParameters
        return $context
    }
    catch {
        [void](Invoke-IfsAz -Arguments @('storage', 'blob', 'lease', 'release', '--account-name', $coordinates.Account, '--container-name', $coordinates.Container, '--blob-name', $coordinates.Blob, '--lease-id', $leaseId, '--auth-mode', 'login', '--output', 'none') -AllowFailure)
        Remove-Item -LiteralPath $context.RenewalStopFile, $context.RenewalFailureFile -Force -ErrorAction SilentlyContinue
        throw
    }
}

function Close-IfsOperationJournal {
    <#
    .SYNOPSIS
    Libère le bail du journal et arrête son renouvellement.
    .DESCRIPTION
    À appeler dans le bloc finally de chaque déploiement.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][object] $Context)
    if ($Context.Closed) { return }
    $Context.Closed = $true
    if ($Context.RenewalProcess) {
        New-Item -ItemType File -Path $Context.RenewalStopFile -Force | Out-Null
        Wait-Process -Id $Context.RenewalProcess.Id -Timeout 5 -ErrorAction SilentlyContinue
        Stop-Process -Id $Context.RenewalProcess.Id -Force -ErrorAction SilentlyContinue
    }
    $renewalError = if ($Context.RenewalFailureFile -and (Test-Path -LiteralPath $Context.RenewalFailureFile)) { Get-Content -LiteralPath $Context.RenewalFailureFile -Raw } else { $null }
    [void](Invoke-IfsAz -Arguments @('storage', 'blob', 'lease', 'release', '--account-name', $Context.Account, '--container-name', $Context.Container, '--blob-name', $Context.Blob, '--lease-id', $Context.LeaseId, '--auth-mode', 'login', '--output', 'none') -AllowFailure)
    Remove-Item -LiteralPath $Context.RenewalStopFile, $Context.RenewalFailureFile -Force -ErrorAction SilentlyContinue
    if ($renewalError) { throw ('Le renouvellement du bail du journal a échoué : {0}' -f $renewalError) }
}

function Get-IfsPendingOperation {
    <#
    .SYNOPSIS
    Retourne les opérations à reprendre avant les nouvelles mutations.
    .DESCRIPTION
    Les opérations ToDo et Started conservent leur révision d'origine dans le rapport.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][object] $Journal)
    return @($Journal.operations | Where-Object { $_.state -in @('ToDo', 'Started') })
}

function Test-IfsDependency {
    <#
    .SYNOPSIS
    Vérifie que les ressources requises par le composant sont déployées.
    .DESCRIPTION
    Interroge Azure Resource Manager par identifiant et nomme le composant manquant dans l'erreur.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][object] $ReleaseData, [string] $Target = [string]$ReleaseData.target)
        foreach ($dependency in $ReleaseData.dependencies) {
        foreach ($resource in $dependency.resources) {
            try { $result = Invoke-IfsAz -Arguments @('resource', 'show', '--ids', $resource.id, '--output', 'json') -AllowFailure }
            catch {
                if (Test-IfsNotFoundError $_.Exception.Message) { throw ('Déployez d''abord {0} en {1}.' -f $dependency.component, $Target) }
                throw
            }
            if ($result.ExitCode -ne 0 -or (Test-IfsNotFoundError $result.Text)) {
                throw ('Déployez d''abord {0} en {1}.' -f $dependency.component, $Target)
            }
        }
    }
    return $true
}

function Test-IfsSecretVariable {
    <#
    .SYNOPSIS
    Vérifie la présence des variables secrètes attendues par la cible.
    .DESCRIPTION
    N'affiche jamais les valeurs. En cas d'absence, indique les noms et le groupe de variables à compléter.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][object] $ReleaseData)
    $missing = [System.Collections.Generic.List[string]]::new()
    foreach ($write in $ReleaseData.secretWrites | Where-Object { $_.source -eq 'pipeline' }) {
        $value = [Environment]::GetEnvironmentVariable([string]$write.variable)
        if ([string]::IsNullOrEmpty($value)) { $missing.Add([string]$write.variable) }
    }
    if ($missing.Count -gt 0) {
        throw ('Variables secrètes vides : {0}. Renseignez-les dans le groupe ifs-{1}-{2}.' -f ($missing -join ', '), $ReleaseData.project, $ReleaseData.target)
    }
    return $true
}

function Test-IfsSecretReference {
    <#
    .SYNOPSIS
    Vérifie que les secrets référencés existent dans leur coffre.
    .DESCRIPTION
    Interroge uniquement les métadonnées du coffre et ne lit jamais les valeurs secrètes.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][object] $ReleaseData)
    foreach ($reference in $ReleaseData.secretReferences) {
        $query = "[?name=='{0}'].id" -f ([string]$reference.secret).Replace("'", "''")
        $result = Invoke-IfsAz -Arguments @('keyvault', 'secret', 'list', '--vault-name', $reference.vault, '--query', $query, '--output', 'json')
        $ids = ConvertFrom-IfsJson $result.Text
        if (@($ids).Count -eq 0) {
            throw ('Déployez d''abord {0} en {1} : le secret {2} du coffre {3} est absent.' -f $reference.writer, $ReleaseData.target, $reference.secret, $reference.vault)
        }
    }
    return $true
}

function Get-IfsEffectFingerprint {
    <#
    .SYNOPSIS
    Calcule l'empreinte stable des effets d'infrastructure.
    .DESCRIPTION
    Sérialise les effets avec les propriétés triées, en excluant appOwnedState selon DEC-102.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][object] $Effects)
    $canonical = ConvertTo-IfsCanonicalValue -Value $Effects -ExcludeAppOwnedState
    $json = ConvertTo-Json -InputObject $canonical -Depth 100 -Compress
    $hash = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($json))
    return ([Convert]::ToHexString($hash)).ToLowerInvariant()
}

function Read-IfsAppOwnedState {
    <#
    .SYNOPSIS
    Relit l'image et le trafic actuellement en service.
    .DESCRIPTION
    À appeler sous le verrou de cible. Retourne l'image de démarrage épinglée si la Container App n'existe pas encore.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][object] $ReleaseData)
    $states = [System.Collections.Generic.List[object]]::new()
    foreach ($owned in $ReleaseData.appOwnedState) {
        $result = Invoke-IfsAz -Arguments @('containerapp', 'show', '--ids', $owned.resourceId, '--output', 'json') -AllowFailure
        if ($result.ExitCode -ne 0) {
            if (-not (Test-IfsNotFoundError $result.Text)) { throw ('Lecture de l''application impossible : {0}' -f $result.Text) }
            $states.Add([pscustomobject]@{ resourceId = $owned.resourceId; parameter = $owned.parameter; image = $owned.bootstrapImage; traffic = @(); bootstrap = $true })
            continue
        }
        $app = ConvertFrom-IfsJson $result.Text
        $properties = Get-IfsValue $app 'properties'
        $containers = Get-IfsValue (Get-IfsValue $properties 'template') 'containers' @()
        $ingress = Get-IfsValue (Get-IfsValue $properties 'configuration') 'ingress'
        $traffic = Get-IfsValue $ingress 'traffic' @()
        foreach ($container in $containers) {
            $states.Add([pscustomobject]@{ resourceId = $owned.resourceId; parameter = $owned.parameter; image = $container.image; traffic = $traffic; bootstrap = $false })
        }
    }
    return $states.ToArray()
}

function Get-IfsAccessRevocation {
    param([object[]] $Changes, [object] $ReleaseData)
    $types = @($ReleaseData.accessObjectTypes)
    return @($Changes | Where-Object {
        $id = [string](Get-IfsValue $_ 'resourceId' '')
        (@($types | Where-Object { $id -match ('(?i)/{0}/' -f [regex]::Escape([string]$_)) }).Count -gt 0) -and
        (Get-IfsChangeType $_) -in @('Delete', 'Remove', 'Detach')
    })
}

function Invoke-IfsPreview {
    <#
    .SYNOPSIS
    Produit un aperçu lisible, sans modifier Azure ni le journal d'opérations.
    .DESCRIPTION
    Vérifie journal, dépendances et secrets, lance What-if et compare les ressources de la pile. L'empreinte
    exclut l'image et le trafic appartenant au pipeline applicatif.
    .PARAMETER ReleaseData
    Données ifs-release/v1 validées.
    .PARAMETER TemplateFile
    Fichier Bicep de l'unité.
    .PARAMETER ParameterFile
    Paramètres figés pour cette release.
    .PARAMETER OutputDirectory
    Dossier recevant ifs-preview.json et ifs-preview.md.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object] $ReleaseData,
        [Parameter(Mandatory)][string] $TemplateFile,
        [Parameter(Mandatory)][string] $ParameterFile,
        [Parameter(Mandatory)][string] $OutputDirectory
    )
    [void](Test-IfsReleaseData $ReleaseData)
    $journal = Read-IfsOperationJournal $ReleaseData
    [void](Test-IfsDependency -ReleaseData $ReleaseData -Target $ReleaseData.target)
    [void](Test-IfsSecretVariable $ReleaseData)
    [void](Test-IfsSecretReference $ReleaseData)
    $whatIfResult = Invoke-IfsAz -Arguments @(
        'deployment', 'sub', 'what-if', '--name', ('ifs-{0}-preview' -f $ReleaseData.unit),
        '--location', $ReleaseData.location, '--template-file', $TemplateFile,
        '--parameters', $ParameterFile, '--result-format', 'FullResourcePayloads', '--output', 'json'
    )
    $whatIf = ConvertFrom-IfsJson $whatIfResult.Text
    $changes = @(Get-IfsWhatIfChange $whatIf)
    $stackResult = Invoke-IfsAz -Arguments @('stack', 'sub', 'show', '--name', $ReleaseData.unit, '--subscription', $ReleaseData.subscriptionId, '--output', 'json') -AllowFailure
    if ($stackResult.ExitCode -eq 0) { $stack = ConvertFrom-IfsJson $stackResult.Text }
    elseif (Test-IfsNotFoundError $stackResult.Text) { $stack = [pscustomobject]@{ resources = @() } }
    else { throw ('Lecture de la pile impossible : {0}' -f $stackResult.Text) }

    $created = @($changes | Where-Object { (Get-IfsChangeType $_) -eq 'Create' })
    $modified = @($changes | Where-Object { (Get-IfsChangeType $_) -eq 'Modify' })
    $recreated = @($changes | Where-Object { (Get-IfsChangeType $_) -match 'Replace|Recreate' })
    $managedResources = @(Get-IfsValue $stack 'resources' @())
    $desiredResourceIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($change in $changes) {
        if ((Get-IfsChangeType $change) -in @('Delete', 'Remove', 'Ignore')) { continue }
        $resourceId = [string](Get-IfsValue $change 'resourceId' '')
        if ($resourceId) { [void]$desiredResourceIds.Add($resourceId) }
    }
    $unmanagedIds = [System.Collections.Generic.List[string]]::new()
    foreach ($resource in $managedResources) {
        $resourceId = [string](Get-IfsValue $resource 'id' (Get-IfsValue $resource 'resourceId' ''))
        if ($resourceId -and -not $desiredResourceIds.Contains($resourceId)) { $unmanagedIds.Add($resourceId) }
    }
    $removedIds = [System.Collections.Generic.List[string]]::new()
    foreach ($change in @($changes | Where-Object { (Get-IfsChangeType $_) -in @('Delete', 'Remove', 'Detach') })) {
        $resourceId = [string](Get-IfsValue $change 'resourceId' '')
        if ($resourceId) { $removedIds.Add($resourceId) }
    }
    foreach ($resourceId in $unmanagedIds) { $removedIds.Add($resourceId) }
    $removedIds = @($removedIds | Sort-Object -Unique)
    $revoked = [System.Collections.Generic.List[object]]::new()
    foreach ($change in @(Get-IfsAccessRevocation -Changes $changes -ReleaseData $ReleaseData)) { $revoked.Add($change) }
    $accessTypes = @($ReleaseData.accessObjectTypes)
    foreach ($resourceId in $unmanagedIds) {
        $isAccessObject = @($accessTypes | Where-Object { $resourceId -match ('(?i)/{0}/' -f [regex]::Escape([string]$_)) }).Count -gt 0
        if ($isAccessObject -and -not @($revoked | Where-Object { $_.resourceId -ieq $resourceId }).Count) {
            $revoked.Add([pscustomobject]@{ resourceId = $resourceId; changeType = 'Detach' })
        }
    }
    $detachedResources = @($removedIds | Where-Object {
        $resourceId = $_
        @($accessTypes | Where-Object { $resourceId -match ('(?i)/{0}/' -f [regex]::Escape([string]$_)) }).Count -eq 0
    } | Sort-Object -Unique)
    $replayed = @(Get-IfsPendingOperation -Journal $journal)
    $dataWriteItems = [System.Collections.Generic.List[object]]::new()
    foreach ($secret in @($ReleaseData.secretWrites)) {
        $dataWriteItems.Add([pscustomobject]@{
            kind = 'secret'
            name = ('{0}/{1}' -f $secret.vault, $secret.secret)
            phase = (Get-IfsSecretWritePhase -SecretWrite $secret -Changes $changes -Stack $stack)
        })
    }
    foreach ($access in @($ReleaseData.dataAccess)) {
        $dataWriteItems.Add([pscustomobject]@{
            kind = 'database-access'
            name = ('{0}/{1}/{2}' -f $access.server, $access.database, $access.principal.resourceId)
        })
    }
    $dataWrites = @($dataWriteItems.ToArray() | Sort-Object kind, name)
    $revokedPreview = @($revoked.ToArray() | Sort-Object { [string](Get-IfsValue $_ 'resourceId' '') })
    $fingerprintChanges = @(Get-IfsAppChangeFingerprintView -Changes $changes -ReleaseData $ReleaseData | Sort-Object { [string](Get-IfsValue $_ 'resourceId' '') })
    $fingerprintStackResources = @($managedResources | Sort-Object { [string](Get-IfsValue $_ 'id' (Get-IfsValue $_ 'resourceId' '')) })
    $effects = [ordered]@{ changes = $fingerprintChanges; stackResources = $fingerprintStackResources; detachedOrDeleted = $detachedResources; revokedAccess = $revokedPreview; dataWrites = $dataWrites; appOwnedState = @($ReleaseData.appOwnedState) }
    $fingerprint = Get-IfsEffectFingerprint -Effects $effects
    $appState = @(Read-IfsAppOwnedState $ReleaseData)
    $preview = [pscustomobject]@{
        schema = 'ifs-preview/v1'
        project = $ReleaseData.project
        component = $ReleaseData.component
        unit = $ReleaseData.unit
        target = $ReleaseData.target
        generatedAt = [DateTime]::UtcNow.ToString('o')
        fingerprint = $fingerprint
        pendingOperations = $replayed
        created = $created
        modified = $modified
        recreated = $recreated
        detachedOrDeleted = $detachedResources
        revokedAccess = $revokedPreview
        dataWrites = $dataWrites
        appOwnedState = $appState
        limitations = @('What-if ne prévoit pas les écritures de plan de données à partir de leur valeur réelle.', 'L''aperçu des piles est une comparaison des ressources ARM gérées et du modèle.')
    }
    $previewJson = ConvertTo-Json -InputObject $preview -Depth 100
    if (-not (Test-Json -Json $previewJson -SchemaFile (Get-IfsSchemaPath 'ifs-preview.schema.json'))) {
        throw 'L''aperçu produit ne respecte pas ifs-preview/v1.'
    }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'ifs-preview.json'), $previewJson, [Text.UTF8Encoding]::new($false))
    $sections = [System.Collections.Generic.List[string]]::new()
    $sections.Add('# Aperçu de release IFS')
    $sections.Add('')
    $sections.Add(('- Composant : {0} / cible : {1}' -f $ReleaseData.component, $ReleaseData.target))
    $sections.Add(('- Empreinte approuvable : {0}' -f $fingerprint))
    foreach ($section in @(
        @{ Title = 'Opérations reprises'; Items = @($replayed | ForEach-Object { '- {0} — {1} — révision d''origine {2}' -f $_.kind, $_.objectId, $_.revision }) },
        @{ Title = 'Ressources créées'; Items = @($created | ForEach-Object { '- {0}' -f (Get-IfsValue $_ 'resourceId') }) },
        @{ Title = 'Ressources modifiées ou recréées'; Items = @(@($modified + $recreated) | ForEach-Object { '- {0} ({1})' -f (Get-IfsValue $_ 'resourceId'), (Get-IfsChangeType $_) }) },
        @{ Title = 'Ressources détachées ou supprimées'; Items = @($detachedResources | ForEach-Object { '- {0}' -f $_ }) },
        @{ Title = 'Accès révoqués'; Items = @($revoked | ForEach-Object { '- {0}' -f (Get-IfsValue $_ 'resourceId') }) },
        @{ Title = 'Écritures de plan de données prévues'; Items = @($dataWrites | ForEach-Object { '- {0} : {1}' -f $_.kind, $_.name }) },
        @{ Title = 'Limites de cet aperçu'; Items = @($preview.limitations | ForEach-Object { '- {0}' -f $_ }) }
    )) {
        $sections.Add('')
        $sections.Add(('## {0}' -f $section.Title))
        if ($section.Items.Count) { $sections.AddRange([string[]]$section.Items) } else { $sections.Add('- Aucune') }
    }
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'ifs-preview.md'), ($sections -join [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
    return $preview
}

function Add-IfsOperation {
    <#
    .SYNOPSIS
    Inscrit une opération stable au journal avant de lancer sa mutation.
    .DESCRIPTION
    L'identifiant est le SHA-256 de l'unité, la cible, la nature et l'objet Azure. Une opération existante conserve sa révision d'origine.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object] $Context,
        [Parameter(Mandatory)][int] $Revision,
        [Parameter(Mandatory)][string] $Commit,
        [Parameter(Mandatory)][string] $Kind,
        [Parameter(Mandatory)][string] $ObjectId,
        [AllowNull()][object] $Details
    )
    $key = '{0}|{1}|{2}|{3}' -f $Context.Journal.unit, $Context.Journal.target, $Kind, $ObjectId
    $id = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($key))).ToLowerInvariant()
    $existing = @($Context.Journal.operations | Where-Object { $_.id -eq $id } | Select-Object -First 1)
    if ($existing.Count) { return $existing[0] }
    $operation = [pscustomobject]@{
        id = $id
        revision = $Revision
        commit = $Commit
        kind = $Kind
        objectId = $ObjectId
        state = 'ToDo'
        createdAt = [DateTime]::UtcNow.ToString('o')
        updatedAt = [DateTime]::UtcNow.ToString('o')
        note = $null
        details = $Details
    }
    $Context.Journal.operations = @($Context.Journal.operations) + @($operation)
    Save-IfsJournalBlob $Context
    return $operation
}

function Set-IfsOperationState {
    <#
    .SYNOPSIS
    Met à jour l'état d'une opération et sauvegarde immédiatement le journal.
    .DESCRIPTION
    Une opération passe de ToDo à Started puis Done. Si l'objet a disparu, Done porte la note « déjà absent ».
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param(
        [Parameter(Mandatory)][object] $Context,
        [Parameter(Mandatory)][string] $OperationId,
        [Parameter(Mandatory)][ValidateSet('ToDo', 'Started', 'Done')][string] $State,
        [object] $ObjectExists
    )
    $matches = @($Context.Journal.operations | Where-Object { $_.id -eq $OperationId } | Select-Object -First 1)
    if (-not $matches.Count) { throw ('Opération absente du journal : {0}' -f $OperationId) }
    $operation = $matches[0]
    $allowed = @{ ToDo = @('ToDo', 'Started'); Started = @('Started', 'Done'); Done = @('Done') }
    if ($State -notin $allowed[$operation.state]) { throw ('Transition de journal interdite : {0} vers {1}.' -f $operation.state, $State) }
    if (-not $PSCmdlet.ShouldProcess($OperationId, ('Définir l''état à {0}' -f $State))) { return $operation }
    $operation.state = $State
    $operation.updatedAt = [DateTime]::UtcNow.ToString('o')
    if ($State -eq 'Done' -and $null -ne $ObjectExists -and -not [bool]$ObjectExists) {
        if ($null -eq $operation.PSObject.Properties['note']) { $operation | Add-Member -NotePropertyName note -NotePropertyValue 'déjà absent' }
        else { $operation.note = 'déjà absent' }
    }
    Save-IfsJournalBlob $Context
    return $operation
}

function Invoke-IfsPendingOperation {
    param(
        [Parameter(Mandatory)][object] $Context,
        [Parameter(Mandatory)][object] $ReleaseData,
        [Parameter(Mandatory)][string] $TemplateFile,
        [Parameter(Mandatory)][string] $ParameterFile,
        [Parameter(Mandatory)][string] $DeploymentIdentityObjectId,
        [Parameter(Mandatory)][string] $SqlScript,
        [Parameter(Mandatory)][int] $Revision,
        [Parameter(Mandatory)][string] $Commit,
        [Parameter(Mandatory)][string] $RunId
    )
    $summary = [pscustomobject]@{
        detachedResources = [System.Collections.Generic.List[string]]::new()
        revokedAccess = [System.Collections.Generic.List[string]]::new()
        temporaryFirewallRules = [System.Collections.Generic.List[string]]::new()
        cleanedFirewallRules = [System.Collections.Generic.List[string]]::new()
    }
    foreach ($operation in @(Get-IfsPendingOperation -Journal $Context.Journal)) {
        if ($operation.kind -eq 'revoke') {
            $live = Invoke-IfsAz -Arguments @('resource', 'show', '--ids', $operation.objectId, '--output', 'json') -AllowFailure
            if ($live.ExitCode -ne 0 -or (Test-IfsNotFoundError $live.Text)) {
                [void](Set-IfsOperationState -Context $Context -OperationId $operation.id -State 'Done' -ObjectExists $false)
                continue
            }
            if ($operation.state -eq 'ToDo') { [void](Set-IfsOperationState -Context $Context -OperationId $operation.id -State 'Started') }
            [void](Invoke-IfsAz -Arguments @('resource', 'delete', '--ids', $operation.objectId, '--output', 'none'))
            [void](Set-IfsOperationState -Context $Context -OperationId $operation.id -State 'Done' -ObjectExists $true)
            $summary.revokedAccess.Add([string]$operation.objectId)
            continue
        }
        if ($operation.kind -eq 'secret-write') {
            $secret = Get-IfsValue $operation.details 'write' $null
            if (-not $secret) { $secret = $operation.details }
            if (-not (Get-IfsValue $secret 'vault')) { $secret = @($ReleaseData.secretWrites | Where-Object { ('{0}/{1}' -f $_.vault, $_.secret) -eq $operation.objectId } | Select-Object -First 1)[0] }
            if (-not $secret) { throw ('Le secret de l''opération {0} n''est plus décrit dans la release courante.' -f $operation.id) }
            if ($operation.state -eq 'ToDo') { [void](Set-IfsOperationState -Context $Context -OperationId $operation.id -State 'Started') }
            [void](Invoke-IfsSecretWrite -ReleaseData ([pscustomobject]@{ secretWrites = @($secret); restartOnSecretChange = $ReleaseData.restartOnSecretChange }))
            [void](Set-IfsOperationState -Context $Context -OperationId $operation.id -State 'Done')
            continue
        }
        if ($operation.kind -eq 'deploy-unit') {
            if ($operation.state -eq 'ToDo') { [void](Set-IfsOperationState -Context $Context -OperationId $operation.id -State 'Started') }
            $stackOutput = Invoke-IfsStackDeployment -ReleaseData $ReleaseData -TemplateFile $TemplateFile -ParameterFile $ParameterFile -DeploymentIdentityObjectId $DeploymentIdentityObjectId
            $summary.detachedResources.AddRange([string[]]$stackOutput.detachedResources)
            if ($env:IFS_TEST_ABORT_AFTER -eq 'deploy-unit') { [Environment]::Exit(137) }
            [void](Set-IfsOperationState -Context $Context -OperationId $operation.id -State 'Done')
            continue
        }
        if ($operation.kind -eq 'data-access') {
            $access = $operation.details
            if (-not $access) { $access = @($ReleaseData.dataAccess | Where-Object { ('{0}/{1}/{2}' -f $_.server, $_.database, $_.principal.resourceId) -eq $operation.objectId } | Select-Object -First 1)[0] }
            if (-not $access) { throw ('L''accès aux données de l''opération {0} n''est plus décrit dans la release courante.' -f $operation.id) }
            if ($operation.state -eq 'ToDo') { [void](Set-IfsOperationState -Context $Context -OperationId $operation.id -State 'Started') }
            $oneAccess = [pscustomobject]@{ dataAccess = @($access); subscriptionId = $ReleaseData.subscriptionId }
            $dataResult = Invoke-IfsDataAccess -ReleaseData $oneAccess -SqlScript $SqlScript -RunId $RunId
            foreach ($rule in $dataResult.temporaryFirewallRules) { $summary.temporaryFirewallRules.Add([string]$rule.Name) }
            $summary.cleanedFirewallRules.AddRange([string[]]$dataResult.cleanedFirewallRules)
            [void](Set-IfsOperationState -Context $Context -OperationId $operation.id -State 'Done')
            continue
        }
        throw ('Aucune stratégie de reprise n''est définie pour l''opération {0} ({1}).' -f $operation.id, $operation.kind)
    }
    return [pscustomobject]@{
        detachedResources = $summary.detachedResources.ToArray()
        revokedAccess = $summary.revokedAccess.ToArray()
        temporaryFirewallRules = $summary.temporaryFirewallRules.ToArray()
        cleanedFirewallRules = $summary.cleanedFirewallRules.ToArray()
    }
}

function Invoke-IfsStackDeployment {
    <#
    .SYNOPSIS
    Déploie l'unité avec une pile ARM et détache les ressources retirées du modèle.
    .DESCRIPTION
    Une cible protégée refuse les suppressions en excluant l'identité de déploiement du deny assignment.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object] $ReleaseData,
        [Parameter(Mandatory)][string] $TemplateFile,
        [Parameter(Mandatory)][string] $ParameterFile,
        [Parameter(Mandatory)][string] $DeploymentIdentityObjectId
    )
    $arguments = @('stack', 'sub', 'create', '--name', $ReleaseData.unit, '--location', $ReleaseData.location, '--template-file', $TemplateFile, '--parameters', $ParameterFile, '--action-on-unmanage', 'detachAll', '--subscription', $ReleaseData.subscriptionId, '--yes', '--output', 'json')
    if ($ReleaseData.protected) {
        if ([string]::IsNullOrWhiteSpace($DeploymentIdentityObjectId)) { throw 'La cible protégée exige l''identité de déploiement pour son deny assignment.' }
        $arguments += @('--deny-settings-mode', 'denyDelete', '--deny-settings-excluded-principals', $DeploymentIdentityObjectId)
    }
    else { $arguments += @('--deny-settings-mode', 'none') }
    $stack = ConvertFrom-IfsJson (Invoke-IfsAz -Arguments $arguments).Text
    return [pscustomobject]@{ result = $stack; detachedResources = @(Get-IfsValue $stack 'detachedResources' @()) }
}

function Invoke-IfsRevocation {
    <#
    .SYNOPSIS
    Supprime les accès retirés de la pile, même en cible protégée.
    .DESCRIPTION
    Chaque révocation est journalisée avant suppression et peut être reprise par la release suivante.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][object] $Context, [Parameter(Mandatory)][object] $ReleaseData, [Parameter(Mandatory)][int] $Revision, [Parameter(Mandatory)][string] $Commit, [Parameter(Mandatory)][string[]] $ObjectIds)
    $deleted = [System.Collections.Generic.List[string]]::new()
    foreach ($objectId in $ObjectIds) {
        $operation = Add-IfsOperation -Context $Context -Revision $Revision -Commit $Commit -Kind 'revoke' -ObjectId $objectId
        if ($operation.state -eq 'Done') { continue }
        $live = Invoke-IfsAz -Arguments @('resource', 'show', '--ids', $objectId, '--output', 'json') -AllowFailure
        if ($live.ExitCode -ne 0 -or (Test-IfsNotFoundError $live.Text)) {
            [void](Set-IfsOperationState -Context $Context -OperationId $operation.id -State 'Done' -ObjectExists $false)
            continue
        }
        if ($operation.state -eq 'ToDo') { [void](Set-IfsOperationState -Context $Context -OperationId $operation.id -State 'Started') }
        [void](Invoke-IfsAz -Arguments @('resource', 'delete', '--ids', $objectId, '--output', 'none'))
        [void](Set-IfsOperationState -Context $Context -OperationId $operation.id -State 'Done' -ObjectExists $true)
        $deleted.Add($objectId)
    }
    return $deleted.ToArray()
}

function Invoke-IfsSecretWrite {
    <#
    .SYNOPSIS
    Écrit les secrets modifiés et redémarre leurs applications consommatrices.
    .DESCRIPTION
    Compare la valeur actuelle sans afficher la valeur. Une valeur identique ne crée pas de nouvelle version.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][object] $ReleaseData, [string[]] $OnlySource = @('pipeline', 'generated'))
    $changedSecrets = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($write in $ReleaseData.secretWrites | Where-Object { $_.source -in $OnlySource }) {
        $value = if ($write.source -eq 'pipeline') { [Environment]::GetEnvironmentVariable([string]$write.variable) } else { $null }
        if ($write.source -eq 'pipeline' -and [string]::IsNullOrEmpty($value)) { throw ('La variable secrète {0} est vide.' -f $write.variable) }
        if ($write.source -eq 'generated') { throw ('Aucune valeur générée n''est fournie pour {0}/{1}.' -f $write.vault, $write.secret) }
        $current = Invoke-IfsAz -Arguments @('keyvault', 'secret', 'show', '--vault-name', $write.vault, '--name', $write.secret, '--query', 'value', '--output', 'tsv') -AllowFailure
        if ($current.ExitCode -eq 0 -and [string]::Equals($current.Text.TrimEnd(), $value, [StringComparison]::Ordinal)) { continue }
        $temporaryFile = Join-Path ([IO.Path]::GetTempPath()) ('ifs-secret-{0}.txt' -f [guid]::NewGuid().ToString('N'))
        try {
            [IO.File]::WriteAllText($temporaryFile, $value, [Text.UTF8Encoding]::new($false))
            [void](Invoke-IfsAz -Arguments @('keyvault', 'secret', 'set', '--vault-name', $write.vault, '--name', $write.secret, '--file', $temporaryFile, '--encoding', 'utf-8', '--output', 'none'))
        }
        finally { Remove-Item -LiteralPath $temporaryFile -Force -ErrorAction SilentlyContinue }
        [void]$changedSecrets.Add([string]$write.secret)
    }
    if ($changedSecrets.Count -gt 0) {
        foreach ($resourceId in $ReleaseData.restartOnSecretChange) {
            $revision = (Invoke-IfsAz -Arguments @('containerapp', 'show', '--ids', $resourceId, '--query', 'properties.latestRevisionName', '--output', 'tsv')).Text.Trim()
            if ($revision) { [void](Invoke-IfsAz -Arguments @('containerapp', 'revision', 'restart', '--ids', $resourceId, '--revision', $revision, '--output', 'none')) }
        }
    }
    return @($changedSecrets)
}

function Get-IfsSqlModuleVersion {
    $pinsPath = Join-Path $PSScriptRoot '../../../../pins.json'
    if (-not (Test-Path -LiteralPath $pinsPath)) { throw ('Le fichier de versions épinglées est absent : {0}' -f $pinsPath) }
    $pins = Get-Content -LiteralPath $pinsPath -Raw | ConvertFrom-Json -Depth 50
    $version = Get-IfsValue (Get-IfsValue $pins 'powerShellModules') 'SqlServer'
    if ([string]::IsNullOrWhiteSpace([string]$version)) { throw 'pins.json ne fixe pas la version du module PowerShell SqlServer.' }
    return [string]$version
}

function Test-IfsSqlPermissionPropagationError {
    param([Parameter(Mandatory)][string] $Message)
    return $Message -match '(?i)permission was denied|permission.*not.*granted|not able to access the database|principal.*not found|does not have permission|error 33134|error 916|error 4060'
}

function Get-IfsSqlPrincipal {
    param([Parameter(Mandatory)][object] $Access, [Parameter(Mandatory)][string] $SubscriptionId)
    if ($Access.principal.kind -eq 'systemAssigned') {
        $identity = ConvertFrom-IfsJson (Invoke-IfsAz -Arguments @('containerapp', 'show', '--ids', $Access.principal.resourceId, '--query', 'identity', '--output', 'json')).Text
        $principalId = [string](Get-IfsValue $identity 'principalId' '')
        $principalName = ([string]$Access.principal.resourceId).Split('/')[-1]
    }
    else {
        $identity = ConvertFrom-IfsJson (Invoke-IfsAz -Arguments @('identity', 'show', '--ids', $Access.principal.resourceId, '--subscription', $SubscriptionId, '--output', 'json')).Text
        $principalId = [string](Get-IfsValue $identity 'principalId' '')
        $principalName = [string](Get-IfsValue $identity 'name' ([string]$Access.principal.resourceId).Split('/')[-1])
    }
    if ($principalId -notmatch '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$') {
        throw ('L''identifiant principal SQL de {0} n''est pas un GUID valide.' -f $Access.principal.resourceId)
    }
    $roles = switch ($Access.level) {
        'Read' { 'db_datareader' }
        'ReadWrite' { 'db_datareader,db_datawriter' }
        'Schema' { 'db_datareader,db_datawriter,db_ddladmin' }
        default { throw ('Niveau d''accès SQL non pris en charge : {0}' -f $Access.level) }
    }
    return [pscustomobject]@{ Name = $principalName; ClientId = $principalId; Roles = $roles }
}

function Invoke-IfsDataAccess {
    <#
    .SYNOPSIS
    Applique les accès SQL des identités sortantes du composant.
    .DESCRIPTION
    Ouvre une règle temporaire si l'exposition est restreinte, utilise un jeton SQL Azure CLI et exécute le script
    data-access.sql avec le module SqlServer épinglé. La règle temporaire est supprimée dans finally.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object] $ReleaseData,
        [Parameter(Mandatory)][string] $SqlScript,
        [Parameter(Mandatory)][string] $RunId
    )
    $scriptPath = (Resolve-Path -LiteralPath $SqlScript).Path
    $version = Get-IfsSqlModuleVersion
    if (-not (Get-Module SqlServer -ListAvailable | Where-Object { $_.Version -eq [version]$version })) {
        Install-Module -Name SqlServer -RequiredVersion $version -Scope CurrentUser -Force -AllowClobber -ErrorAction Stop
    }
    Import-Module SqlServer -RequiredVersion $version -ErrorAction Stop
    $opened = [System.Collections.Generic.List[object]]::new()
    $cleaned = [System.Collections.Generic.List[string]]::new()
    $firewallRunId = Get-IfsFirewallRunId -RunId $RunId
    try {
        foreach ($access in $ReleaseData.dataAccess) {
            $serverName = ([string]$access.server).Split('.')[0]
            $server = ConvertFrom-IfsJson (Invoke-IfsAz -Arguments @('sql', 'server', 'show', '--name', $serverName, '--subscription', $ReleaseData.subscriptionId, '--output', 'json')).Text
            $resourceGroup = [string](Get-IfsValue $server 'resourceGroup')
            if ([string]::IsNullOrWhiteSpace($resourceGroup)) { throw ('Groupe de ressources SQL absent pour {0}.' -f $serverName) }
            $rules = ConvertFrom-IfsJson (Invoke-IfsAz -Arguments @('sql', 'server', 'firewall-rule', 'list', '--name', $serverName, '--resource-group', $resourceGroup, '--subscription', $ReleaseData.subscriptionId, '--output', 'json')).Text
            foreach ($rule in $rules | Where-Object { $_.name -like 'ifs-temp-*' }) {
                $ruleId = '/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Sql/servers/{2}/firewallRules/{3}' -f $ReleaseData.subscriptionId, $resourceGroup, $serverName, $rule.name
                $created = Get-IfsTemporaryFirewallRuleCreatedAt -Rule $rule -ResourceId $ruleId
                if ($created -and ([DateTimeOffset]::UtcNow - $created.ToUniversalTime()).TotalHours -gt 2) {
                    [void](Invoke-IfsAz -Arguments @('sql', 'server', 'firewall-rule', 'delete', '--name', $serverName, '--resource-group', $resourceGroup, '--rule-name', $rule.name, '--subscription', $ReleaseData.subscriptionId, '--yes', '--output', 'none'))
                    $cleaned.Add([string]$rule.name)
                }
            }
            $firewall = $null
            if ($access.exposure -eq 'Restricted') {
                $ip = [string](Invoke-RestMethod -Uri 'https://api.ipify.org' -TimeoutSec 15)
                $suffix = (($firewallRunId -replace '[^a-zA-Z0-9-]', '') + [guid]::NewGuid().ToString('N')).Substring(0, 36)
                $firewall = [pscustomobject]@{ Server = $serverName; ResourceGroup = $resourceGroup; Name = ('ifs-temp-{0}' -f $suffix) }
                [void](Invoke-IfsAz -Arguments @('sql', 'server', 'firewall-rule', 'create', '--name', $serverName, '--resource-group', $resourceGroup, '--rule-name', $firewall.Name, '--start-ip-address', $ip, '--end-ip-address', $ip, '--subscription', $ReleaseData.subscriptionId, '--output', 'none'))
                $opened.Add($firewall)
            }
            $principal = Get-IfsSqlPrincipal -Access $access -SubscriptionId $ReleaseData.subscriptionId
            $tokenResult = ConvertFrom-IfsJson (Invoke-IfsAz -Arguments @('account', 'get-access-token', '--resource', 'https://database.windows.net/', '--output', 'json')).Text
            $token = [string](Get-IfsValue $tokenResult 'accessToken')
            if ([string]::IsNullOrWhiteSpace($token)) { throw 'Azure CLI n''a pas fourni de jeton SQL.' }
            $deadline = [DateTime]::UtcNow.AddMinutes(10)
            do {
                try {
                    Invoke-Sqlcmd -ServerInstance $access.server -Database $access.database -AccessToken $token -InputFile $scriptPath -Variable @(
                        ('PrincipalName={0}' -f $principal.Name), ('ClientId={0}' -f $principal.ClientId), ('Roles={0}' -f $principal.Roles)
                    ) -ErrorAction Stop | Out-Null
                    break
                }
                catch {
                    if (-not (Test-IfsSqlPermissionPropagationError $_.Exception.Message)) { throw }
                    if ([DateTime]::UtcNow -ge $deadline) { throw ('Propagation des droits SQL non terminée après dix minutes : {0}' -f $_.Exception.Message) }
                    Start-Sleep -Seconds 10
                }
            } while ([DateTime]::UtcNow -lt $deadline)
        }
    }
    finally {
        foreach ($rule in $opened) {
            [void](Invoke-IfsAz -Arguments @('sql', 'server', 'firewall-rule', 'delete', '--name', $rule.Server, '--resource-group', $rule.ResourceGroup, '--rule-name', $rule.Name, '--yes', '--output', 'none') -AllowFailure)
        }
    }
    return [pscustomobject]@{ temporaryFirewallRules = @($opened); cleanedFirewallRules = $cleaned.ToArray() }
}

function Write-IfsReleaseReport {
    <#
    .SYNOPSIS
    Écrit le rapport ifs-report/v1 même si le déploiement échoue après une mutation.
    .DESCRIPTION
    Le rapport contient les métadonnées du manifeste, le journal, les ressources détachées, les accès révoqués et le résultat.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object] $ReleaseData,
        [Parameter(Mandatory)][string] $ManifestPath,
        [Parameter(Mandatory)][string] $OutputPath,
        [AllowNull()][object] $Context,
        [string[]] $DetachedResources = @(),
        [string[]] $RevokedAccess = @(),
        [object[]] $Steps = @(),
        [string[]] $TemporaryFirewallRules = @(),
        [string[]] $CleanedFirewallRules = @(),
        [ValidateSet('Succeeded', 'PartiallyApplied', 'FailedBeforeMutation')][string] $Result = 'Succeeded',
        [string] $ErrorMessage
    )
    $metadata = Get-IfsManifestInfo $ManifestPath
    $journal = if ($Context) { $Context.Journal } else { Read-IfsOperationJournal $ReleaseData }
    $report = [pscustomobject]@{
        schema = 'ifs-report/v1'
        project = $ReleaseData.project
        component = $ReleaseData.component
        unit = $ReleaseData.unit
        target = $ReleaseData.target
        revision = $metadata.Revision
        commit = $metadata.Commit
        manifestFingerprint = Get-IfsEffectFingerprint -Effects $metadata.Manifest
        generatedAt = [DateTime]::UtcNow.ToString('o')
        steps = @($Steps)
        journal = $journal
        detachedResources = @($DetachedResources)
        revokedAccess = @($RevokedAccess)
        temporaryFirewallRules = @($TemporaryFirewallRules)
        cleanedFirewallRules = @($CleanedFirewallRules)
        result = $Result
        error = $ErrorMessage
    }
    $json = ConvertTo-Json -InputObject $report -Depth 100
    if (-not (Test-Json -Json $json -SchemaFile (Get-IfsSchemaPath 'ifs-report.schema.json'))) { throw 'Le rapport produit ne respecte pas ifs-report/v1.' }
    $directory = Split-Path -Parent $OutputPath
    if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    [IO.File]::WriteAllText($OutputPath, $json, [Text.UTF8Encoding]::new($false))
    return $report
}

function Invoke-IfsInfraPreview {
    <#
    .SYNOPSIS
    Exécute les contrôles et l'aperçu de release d'infrastructure sans écriture Azure.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $ReleasePath, [Parameter(Mandatory)][string] $TemplateFile, [Parameter(Mandatory)][string] $ParameterFile, [Parameter(Mandatory)][string] $OutputDirectory)
    $releaseData = Read-IfsReleaseData -Path $ReleasePath
    $preview = Invoke-IfsPreview -ReleaseData $releaseData -TemplateFile $TemplateFile -ParameterFile $ParameterFile -OutputDirectory $OutputDirectory
    Write-Output ('##vso[task.uploadfile]{0}' -f (Join-Path $OutputDirectory 'ifs-preview.json'))
    Write-Output ('##vso[task.uploadsummary]{0}' -f (Join-Path $OutputDirectory 'ifs-preview.md'))
    return $preview
}

function Test-IfsNewerManifest {
    param([Parameter(Mandatory)][object] $FrozenManifest, [Parameter(Mandatory)][string] $DefaultBranch, [Parameter(Mandatory)][string] $ManifestPathInRepository, [Parameter(Mandatory)][object] $ReleaseData)
    $fetch = @(& git fetch origin $DefaultBranch 2>&1)
    if ($LASTEXITCODE -ne 0) { throw ('Lecture de la branche par défaut impossible : {0}' -f ($fetch -join ' ')) }
    $show = @(& git show ('origin/{0}:{1}' -f $DefaultBranch, $ManifestPathInRepository) 2>&1)
    if ($LASTEXITCODE -ne 0) { throw ('Manifeste de branche par défaut illisible : {0}' -f ($show -join ' ')) }
    $head = ($show -join [Environment]::NewLine) | ConvertFrom-Json -Depth 100
    if ([int]$head.revision -le [int]$FrozenManifest.revision) { return $false }
    $frozenUnits = Get-IfsValue $FrozenManifest 'units' (Get-IfsValue $FrozenManifest 'components' $null)
    $headUnits = Get-IfsValue $head 'units' (Get-IfsValue $head 'components' $null)
    $frozenUnit = Get-IfsValue $frozenUnits ([string]$ReleaseData.component) $null
    $headUnit = Get-IfsValue $headUnits ([string]$ReleaseData.component) $null
    if ($null -eq $frozenUnit -or $null -eq $headUnit) {
        $frozenUnit = Get-IfsValue $frozenUnits ([string]$ReleaseData.unit) $null
        $headUnit = Get-IfsValue $headUnits ([string]$ReleaseData.unit) $null
    }
    $frozenFingerprint = [string](Get-IfsValue $frozenUnit 'fingerprint' '')
    $headFingerprint = [string](Get-IfsValue $headUnit 'fingerprint' '')
    if (-not $frozenFingerprint -or -not $headFingerprint) {
        if ($null -eq $frozenUnit -or $null -eq $headUnit) {
            throw ('Une révision plus récente ({0}) existe, mais le manifeste ne permet pas de vérifier que {1} est inchangé.' -f $head.revision, $ReleaseData.component)
        }
        $left = Get-IfsEffectFingerprint -Effects $frozenUnit
        $right = Get-IfsEffectFingerprint -Effects $headUnit
        $frozenFingerprint = $left
        $headFingerprint = $right
    }
    if ($frozenFingerprint -ne $headFingerprint) {
        throw ('Une révision plus récente ({0}) modifie ce composant ; la release qu''elle a déclenchée l''appliquera après une nouvelle approbation.' -f $head.revision)
    }
    return $false
}

function Get-IfsSecretWritePhase {
    param([Parameter(Mandatory)][object] $SecretWrite, [object[]] $Changes, [object] $Stack)
    $needle = ('/Microsoft.KeyVault/vaults/{0}' -f [regex]::Escape([string]$SecretWrite.vault))
    foreach ($resource in @((Get-IfsValue $Stack 'resources' @()))) {
        $id = [string](Get-IfsValue $resource 'id' (Get-IfsValue $resource 'resourceId' ''))
        if ($id -match $needle) { return 'postDeploy' }
    }
    foreach ($change in $Changes) {
        $id = [string](Get-IfsValue $change 'resourceId' '')
        if ($id -match $needle) { return 'postDeploy' }
    }
    return 'preDeploy'
}

function Invoke-IfsInfraDeploy {
    <#
    .SYNOPSIS
    Déploie une unité après contrôle de fraîcheur et d'empreinte sous le verrou de cible.
    .DESCRIPTION
    Toutes les opérations prévues sont inscrites au journal avant leur première mutation. Le rapport est écrit dans finally.
    IFS_TEST_ABORT_AFTER=deploy-unit est réservé aux preuves : il termine le processus après l’étape 9 sans rapport.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $ReleasePath,
        [Parameter(Mandatory)][string] $ManifestPath,
        [Parameter(Mandatory)][string] $PreviewPath,
        [Parameter(Mandatory)][string] $TemplateFile,
        [Parameter(Mandatory)][string] $ParameterFile,
        [Parameter(Mandatory)][string] $SqlScript,
        [Parameter(Mandatory)][string] $OutputPath,
        [Parameter(Mandatory)][string] $DefaultBranch,
        [Parameter(Mandatory)][string] $ManifestPathInRepository,
        [Parameter(Mandatory)][string] $DeploymentIdentityObjectId,
        [string] $RunId = [guid]::NewGuid().ToString('N')
    )
    $data = Read-IfsReleaseData -Path $ReleasePath
    $metadata = Get-IfsManifestInfo -ManifestPath $ManifestPath
    $approved = Get-Content -LiteralPath $PreviewPath -Raw | ConvertFrom-Json -Depth 100
    $context = $null
    $steps = [System.Collections.Generic.List[object]]::new()
    $detached = [System.Collections.Generic.List[string]]::new()
    $revoked = [System.Collections.Generic.List[string]]::new()
    $temporaryFirewallRules = [System.Collections.Generic.List[string]]::new()
    $cleanedFirewallRules = [System.Collections.Generic.List[string]]::new()
    $failure = $null
    try {
        [void](Test-IfsNewerManifest -FrozenManifest $metadata.Manifest -DefaultBranch $DefaultBranch -ManifestPathInRepository $ManifestPathInRepository -ReleaseData $data)
        $current = Invoke-IfsPreview -ReleaseData $data -TemplateFile $TemplateFile -ParameterFile $ParameterFile -OutputDirectory (Split-Path -Parent $PreviewPath)
        if ($current.fingerprint -ne $approved.fingerprint) {
            throw 'Les effets ont changé depuis l''aperçu approuvé ; relancez la release pour une nouvelle approbation.'
        }
        $context = Open-IfsOperationJournal -ReleaseData $data
        $appState = @(Read-IfsAppOwnedState $data)

        $previousOperations = @(Get-IfsPendingOperation -Journal $context.Journal)
        $secretOperations = [System.Collections.Generic.List[object]]::new()
        foreach ($secret in $data.secretWrites | Where-Object { $_.source -eq 'pipeline' }) {
            $phaseEntry = @($approved.dataWrites | Where-Object { $_.kind -eq 'secret' -and $_.name -eq ('{0}/{1}' -f $secret.vault, $secret.secret) } | Select-Object -First 1)
            $phase = if ($phaseEntry.Count) { [string]$phaseEntry[0].phase } else { 'preDeploy' }
            $secretOperations.Add([pscustomobject]@{ Secret = $secret; Phase = $phase })
        }
        foreach ($entry in $secretOperations | Where-Object { $_.Phase -eq 'preDeploy' }) {
            [void](Add-IfsOperation -Context $context -Revision $metadata.Revision -Commit $metadata.Commit -Kind 'secret-write' -ObjectId ('{0}/{1}' -f $entry.Secret.vault, $entry.Secret.secret) -Details ([pscustomobject]@{ write = $entry.Secret; phase = $entry.Phase }))
        }
        [void](Add-IfsOperation -Context $context -Revision $metadata.Revision -Commit $metadata.Commit -Kind 'deploy-unit' -ObjectId $data.unit)
        foreach ($access in $approved.revokedAccess) {
            $id = [string](Get-IfsValue $access 'resourceId' '')
            if ($id) { [void](Add-IfsOperation -Context $context -Revision $metadata.Revision -Commit $metadata.Commit -Kind 'revoke' -ObjectId $id) }
        }
        foreach ($entry in $secretOperations | Where-Object { $_.Phase -eq 'postDeploy' }) {
            [void](Add-IfsOperation -Context $context -Revision $metadata.Revision -Commit $metadata.Commit -Kind 'secret-write' -ObjectId ('{0}/{1}' -f $entry.Secret.vault, $entry.Secret.secret) -Details ([pscustomobject]@{ write = $entry.Secret; phase = $entry.Phase }))
        }
        foreach ($item in $data.dataAccess) {
            [void](Add-IfsOperation -Context $context -Revision $metadata.Revision -Commit $metadata.Commit -Kind 'data-access' -ObjectId ('{0}/{1}/{2}' -f $item.server, $item.database, $item.principal.resourceId) -Details $item)
        }
        $steps.Add([pscustomobject]@{ number = 6; name = 'journal'; result = 'Succeeded'; replayed = @($previousOperations | ForEach-Object { $_.id }) })
        try {
            $execution = Invoke-IfsPendingOperation -Context $context -ReleaseData $data -TemplateFile $TemplateFile -ParameterFile $ParameterFile -DeploymentIdentityObjectId $DeploymentIdentityObjectId -SqlScript $SqlScript -Revision $metadata.Revision -Commit $metadata.Commit -RunId $RunId
        }
        catch {
            $createdAfterFailure = [System.Collections.Generic.List[string]]::new()
            foreach ($change in $current.created) {
                $resourceId = [string](Get-IfsValue $change 'resourceId' '')
                if (-not $resourceId) { continue }
                $live = Invoke-IfsAz -Arguments @('resource', 'show', '--ids', $resourceId, '--output', 'json') -AllowFailure
                if ($live.ExitCode -eq 0 -and -not (Test-IfsNotFoundError $live.Text)) { $createdAfterFailure.Add($resourceId) }
            }
            $steps.Add([pscustomobject]@{ number = 9; name = 'deploy-unit'; result = 'PartiallyApplied'; createdResources = $createdAfterFailure.ToArray(); error = $_.Exception.Message })
            throw
        }
        $detached.AddRange([string[]]$execution.detachedResources)
        $revoked.AddRange([string[]]$execution.revokedAccess)
        $temporaryFirewallRules.AddRange([string[]]$execution.temporaryFirewallRules)
        $cleanedFirewallRules.AddRange([string[]]$execution.cleanedFirewallRules)
        $steps.Add([pscustomobject]@{ number = 9; name = 'deploy-unit'; result = 'Succeeded'; appOwnedState = $appState })
        $steps.Add([pscustomobject]@{ number = 11; name = 'data-plane'; result = 'Succeeded' })
    }
    catch {
        $failure = $_.Exception.Message
        throw
    }
    finally {
        $mutationStarted = $false
        if ($context) { $mutationStarted = @($context.Journal.operations | Where-Object { $_.state -in @('Started', 'Done') }).Count -gt 0 }
        try {
            if ($context) { Close-IfsOperationJournal -Context $context }
        }
        finally {
            $result = if ($failure) { if ($mutationStarted) { 'PartiallyApplied' } else { 'FailedBeforeMutation' } } else { 'Succeeded' }
            [void](Write-IfsReleaseReport -ReleaseData $data -ManifestPath $ManifestPath -OutputPath $OutputPath -Context $context -DetachedResources $detached.ToArray() -RevokedAccess $revoked.ToArray() -Steps $steps.ToArray() -TemporaryFirewallRules $temporaryFirewallRules.ToArray() -CleanedFirewallRules $cleanedFirewallRules.ToArray() -Result $result -ErrorMessage $failure)
        }
    }
}

function Invoke-IfsAppDeploy {
    <#
    .SYNOPSIS
    Livre une image immuable d'une Container App, contrôle sa santé et produit le rapport applicatif.
    .DESCRIPTION
    Vérifie l'existence préalable (RG-APP-16), crée une révision et attend un contrôle HTTP 2xx sous cinq minutes.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $Application,
        [Parameter(Mandatory)][string] $Component,
        [Parameter(Mandatory)][string] $Target,
        [Parameter(Mandatory)][string] $ResourceId,
        [Parameter(Mandatory)][ValidatePattern('@sha256:[a-fA-F0-9]{64}$')][string] $Image,
        [Parameter(Mandatory)][uri] $HealthUrl,
        [Parameter(Mandatory)][string] $OutputPath,
        [ValidateRange(1, 300)][int] $HealthTimeoutSeconds = 300
    )
    $result = Invoke-IfsAz -Arguments @('containerapp', 'show', '--ids', $ResourceId, '--output', 'json') -AllowFailure
    if ($result.ExitCode -ne 0 -or (Test-IfsNotFoundError $result.Text)) { throw ('Déployez d''abord l''infrastructure du composant {0} en {1} : application absente ({2}).' -f $Component, $Target, $ResourceId) }
    $revision = 'ifs-{0}' -f ([guid]::NewGuid().ToString('N').Substring(0, 12))
    [void](Invoke-IfsAz -Arguments @('containerapp', 'update', '--ids', $ResourceId, '--image', $Image, '--revision-suffix', $revision, '--output', 'none'))
    $deadline = [DateTime]::UtcNow.AddSeconds($HealthTimeoutSeconds)
    $status = 0
    do {
        try { $status = [int](Invoke-WebRequest -Uri $HealthUrl -Method Get -TimeoutSec 10 -SkipHttpErrorCheck).StatusCode }
        catch { $status = 0 }
        if ($status -ge 200 -and $status -lt 300) { break }
        Start-Sleep -Seconds 5
    } while ([DateTime]::UtcNow -lt $deadline)
    $success = $status -ge 200 -and $status -lt 300
    $report = [pscustomobject]@{
        schema = 'ifs-app-report/v1'
        application = $Application
        target = $Target
        image = $Image
        fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Image))).ToLowerInvariant()
        revision = $revision
        healthStatus = $status
        result = if ($success) { 'Succeeded' } else { 'Failed' }
        generatedAt = [DateTime]::UtcNow.ToString('o')
    }
    $json = ConvertTo-Json -InputObject $report -Depth 50
    if (-not (Test-Json -Json $json -SchemaFile (Get-IfsSchemaPath 'ifs-app-report.schema.json'))) { throw 'Le rapport applicatif ne respecte pas ifs-app-report/v1.' }
    $directory = Split-Path -Parent $OutputPath
    if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    [IO.File]::WriteAllText($OutputPath, $json, [Text.UTF8Encoding]::new($false))
    if (-not $success) { throw ('Le contrôle de santé a échoué après {0} secondes (HTTP {1}).' -f $HealthTimeoutSeconds, $status) }
    return $report
}

Export-ModuleMember -Function @(
    'Read-IfsReleaseData', 'Read-IfsOperationJournal', 'Open-IfsOperationJournal', 'Close-IfsOperationJournal',
    'Get-IfsPendingOperation', 'Test-IfsDependency', 'Test-IfsSecretVariable', 'Test-IfsSecretReference',
    'Invoke-IfsPreview', 'Get-IfsEffectFingerprint', 'Read-IfsAppOwnedState', 'Add-IfsOperation',
    'Set-IfsOperationState', 'Invoke-IfsStackDeployment', 'Invoke-IfsRevocation', 'Invoke-IfsSecretWrite',
    'Invoke-IfsDataAccess', 'Write-IfsReleaseReport', 'Invoke-IfsInfraPreview', 'Invoke-IfsInfraDeploy',
    'Invoke-IfsAppDeploy', 'Test-IfsReleaseData'
)
