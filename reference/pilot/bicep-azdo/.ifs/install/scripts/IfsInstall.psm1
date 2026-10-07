# Généré par InfraFlowSculptor — projet shop. Ne pas modifier : la prochaine publication remplacera ce fichier. Personnalisation : voir README.ifs.md.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:IfsManagedBy = 'infraflowsculptor'
$script:IfsWhatIf = $false
$script:IfsReport = $null

function ConvertTo-IfsTechnicalName {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Value,
        [Parameter(Mandatory)] [ValidateRange(4, 255)] [int] $MaximumLength,
        [switch] $StorageAccount
    )

    $name = $Value.ToLowerInvariant()
    if ($StorageAccount) {
        $name = [regex]::Replace($name, '[^a-z0-9]', '')
    }
    else {
        $name = [regex]::Replace($name, '[^a-z0-9-]', '-')
        $name = [regex]::Replace($name, '-{2,}', '-').Trim('-')
    }
    if ([string]::IsNullOrWhiteSpace($name)) { throw "Le nom technique '$Value' est vide après assainissement." }
    if ($name.Length -le $MaximumLength) { return $name }

    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($name))).Substring(0, 4).ToLowerInvariant()
    $prefixLength = if ($StorageAccount) { $MaximumLength - 4 } else { $MaximumLength - 5 }
    $prefix = $name.Substring(0, $prefixLength)
    if ($StorageAccount) { return "$prefix$hash" }
    $prefix = $prefix.TrimEnd('-')
    return "$prefix-$hash"
}

function Get-IfsStableGuid {
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $Value)

    $bytes = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Value))
    $guidBytes = [byte[]]$bytes[0..15]
    $guidBytes[7] = ($guidBytes[7] -band 0x0f) -bor 0x50
    $guidBytes[8] = ($guidBytes[8] -band 0x3f) -bor 0x80
    return [guid]::new($guidBytes).ToString()
}

function Get-IfsRbacCondition {
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string[]] $RoleDefinitionIds)

    $ids = @($RoleDefinitionIds | ForEach-Object { ([guid]$_).ToString() } | Sort-Object -Unique)
    if ($ids.Count -eq 0) { throw 'Une condition RBAC doit contenir au moins un rôle autorisé.' }
    $set = '{' + ($ids -join ',') + '}'
    return "((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'}) AND !(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals $set)) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals $set))"
}

function Assert-IfsRevisionNotOlder {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [int] $CurrentRevision,
        [Parameter(Mandatory)] [int] $ExistingRevision,
        [Parameter(Mandatory)] [string] $ObjectName
    )

    if ($CurrentRevision -lt $ExistingRevision) {
        throw "Le kit revision $CurrentRevision est plus ancien que la revision $ExistingRevision deja appliquee sur '$ObjectName'. Publiez une revision recente avant de relancer l'installation."
    }
}

function Get-IfsStaleFederatedCredential {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object[]] $Existing,
        [Parameter(Mandatory)] [string[]] $ExpectedNames
    )

    $expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $ExpectedNames) { [void]$expected.Add($name) }
    return @($Existing | Where-Object { ([string]$_.name).StartsWith('ifs-ado-', [StringComparison]::OrdinalIgnoreCase) -and -not $expected.Contains([string]$_.name) })
}

function Get-IfsObjectProperty {
    param([AllowNull()] [object] $Object, [Parameter(Mandatory)] [string] $Name)
    if ($null -eq $Object) { return $null }
    if ($Object -is [Collections.IDictionary]) { return $Object[$Name] }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-IfsTagValue {
    param([AllowNull()] [object] $Tags, [Parameter(Mandatory)] [string] $Name)
    if ($null -eq $Tags) { return $null }
    if ($Tags -is [Collections.IDictionary]) { return [string]$Tags[$Name] }
    $property = $Tags.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return [string]$property.Value
}

function Assert-IfsOwnedResource {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $Resource,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [int] $Revision
    )

    $tags = Get-IfsObjectProperty -Object $Resource -Name 'tags'
    $owner = Get-IfsTagValue -Tags $tags -Name 'managed-by'
    if ($owner -ne $script:IfsManagedBy) {
        throw "L'objet Azure '$Name' existe sans la marque de propriete '$script:IfsManagedBy'; il ne sera pas modifie."
    }
    $marker = Get-IfsTagValue -Tags $tags -Name 'ifs-kit-revision'
    if ($marker -notmatch '^\d+$') { throw "L'objet Azure '$Name' porte une revision du kit absente ou invalide." }
    Assert-IfsRevisionNotOlder -CurrentRevision $Revision -ExistingRevision ([int]$marker) -ObjectName $Name
}

function Invoke-IfsAz {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)] [string[]] $Arguments,
        [switch] $Write,
        [switch] $AllowFailure
    )

    if ($Write -and ($script:IfsWhatIf -or $WhatIfPreference)) {
        Write-Information -MessageData ('WHATIF az ' + ($Arguments -join ' ')) -InformationAction Continue
        return $null
    }
    if ($Write -and -not $PSCmdlet.ShouldProcess(($Arguments -join ' '), 'executer une mutation Azure CLI')) { return $null }
    $azCommand = Get-Command az -ErrorAction SilentlyContinue
    if ($null -eq $azCommand) { throw 'Azure CLI (az) est requis. Installez-le puis relancez le script.' }
    $output = & az @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        $safeCommand = ($Arguments | ForEach-Object { if ($_ -match '(?i)password|secret|token') { '<redacted>' } else { $_ } }) -join ' '
        throw "Azure CLI a echoue (code $exitCode) : az $safeCommand"
    }
    return $output
}

function Invoke-IfsAzJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string[]] $Arguments,
        [switch] $AllowFailure
    )

    $output = Invoke-IfsAz -Arguments ($Arguments + @('--output', 'json')) -AllowFailure:$AllowFailure
    if ($null -eq $output -or [string]::IsNullOrWhiteSpace(($output -join ''))) { return $null }
    try { return (($output -join [Environment]::NewLine) | ConvertFrom-Json -Depth 100) }
    catch { throw "La sortie JSON de 'az $($Arguments -join ' ')' est invalide." }
}

function Get-IfsPilotPlan {
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $RepositoryPath)

    $root = (Resolve-Path -LiteralPath $RepositoryPath).Path
    $manifestPath = Join-Path $root '.ifs/manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Manifeste IFS introuvable : $manifestPath" }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 100
    $revision = [int](Get-IfsObjectProperty -Object $manifest -Name 'revision')
    if ($revision -lt 1) { throw 'La revision .ifs/manifest.json doit etre un entier positif.' }

    $releaseFiles = @(Get-ChildItem -LiteralPath $root -Filter 'release.*.json' -File -Recurse | Where-Object {
        $_.FullName -notmatch '[\\/](\.git|reference|bin|obj)[\\/]'
    })
    if ($releaseFiles.Count -eq 0) { throw 'Aucun fichier release.<cible>.json trouve dans le projet publie.' }

    $releases = foreach ($file in $releaseFiles) {
        $release = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json -Depth 100
        if ([string]$release.schema -ne 'ifs-release/v1') { throw "Schema de release invalide : $($file.FullName)" }
        if ([string]$release.subscriptionId -match '[<>]') { throw "L'abonnement de '$($file.FullName)' contient encore un espace reserve. Completez les parametres Azure avant d'executer l'installation." }
        if ([string]$release.subscriptionId -notmatch '^[0-9a-fA-F-]{36}$') { throw "Identifiant d'abonnement invalide dans '$($file.FullName)'." }
        if ([string]$release.target -notmatch '^[a-z0-9][a-z0-9-]{0,23}$') { throw "Code de cible invalide '$($release.target)' dans '$($file.FullName)'." }
        [pscustomobject]@{
            Project = [string]$release.project
            Target = [string]$release.target
            SubscriptionId = ([guid]$release.subscriptionId).ToString()
            Location = [string]$release.location
            Protected = [bool]$release.protected
            Component = [string]$release.component
            Path = $file.FullName
            Release = $release
        }
    }

    $projects = @($releases.Project | Sort-Object -Unique)
    if ($projects.Count -ne 1 -or [string]::IsNullOrWhiteSpace($projects[0])) { throw 'Tous les fichiers de release doivent declarer le meme code projet.' }
    $targets = foreach ($group in ($releases | Group-Object Target | Sort-Object Name)) {
        $subscriptions = @($group.Group.SubscriptionId | Sort-Object -Unique)
        $locations = @($group.Group.Location | Sort-Object -Unique)
        $protection = @($group.Group.Protected | Sort-Object -Unique)
        if ($subscriptions.Count -ne 1 -or $locations.Count -ne 1 -or $protection.Count -ne 1) {
            throw "Les releases de la cible '$($group.Name)' ne sont pas coherentes en abonnement, region et protection."
        }
        [pscustomobject]@{
            Project = $projects[0]
            Name = $group.Name
            SubscriptionId = $subscriptions[0]
            Location = $locations[0]
            Protected = [bool]$protection[0]
            Components = @($group.Group.Component | Sort-Object -Unique)
            Releases = @($group.Group)
        }
    }
    return [pscustomobject]@{ Revision = $revision; Project = $projects[0]; Targets = @($targets); Releases = @($releases) }
}

function Get-IfsRoleDefinitionId {
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $RoleName)

    $known = @{
        'Contributor' = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
        'Role Based Access Control Administrator' = 'f58310d9-a9f6-439a-9e8d-f62e7b41a168'
        'AcrPull' = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
        'AcrPush' = '8311e382-0749-4cb8-b61a-304f252e45ec'
        'Key Vault Secrets User' = '4633458b-17de-408a-b874-0445c86b69e6'
    'Log Analytics Reader' = '73c42c96-874c-492b-b04d-ab87d138a893'
        'Storage Blob Data Contributor' = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
    }
    if ($known.ContainsKey($RoleName)) { return $known[$RoleName] }
    $definitions = Invoke-IfsAzJson -Arguments @('role', 'definition', 'list', '--name', $RoleName, '--subscription', (Get-Variable IfsCurrentSubscription -Scope Script -ValueOnly -ErrorAction Stop))
    $definition = @($definitions | Where-Object { $_.roleName -eq $RoleName } | Select-Object -First 1)
    if ($definition.Count -eq 0) { throw "Le role Azure '$RoleName' est introuvable dans l'abonnement courant." }
    return ([guid]$definition[0].name).ToString()
}

function Set-IfsRoleAssignment {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)] [string] $SubscriptionId,
        [Parameter(Mandatory)] [string] $Scope,
        [Parameter(Mandatory)] [string] $PrincipalId,
        [Parameter(Mandatory)] [string] $RoleDefinitionId,
        [string] $Condition,
        [string] $Description = ''
    )

    $roleId = ([guid]$RoleDefinitionId).ToString()
    $assignmentId = Get-IfsStableGuid -Value "$Scope|$PrincipalId|$roleId"
    $expectedDescription = "managed-by: $script:IfsManagedBy; ifs-kit-revision: $($script:IfsRevision)"
    if ($Description) { $expectedDescription += "; $Description" }
    $existing = Invoke-IfsAzJson -Arguments @('role', 'assignment', 'list', '--scope', $Scope, '--all', '--subscription', $SubscriptionId)
    $assignment = @($existing | Where-Object { [string]$_.name -eq $assignmentId } | Select-Object -First 1)
    if ($assignment.Count -eq 0) {
        $equivalent = @($existing | Where-Object {
            ([string]$_.principalId -eq $PrincipalId) -and
            ([string]$_.scope -eq $Scope) -and
            ([string]$_.roleDefinitionId).TrimEnd('/').EndsWith($roleId, [StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1)
        if ($equivalent.Count -gt 0) {
            $current = $equivalent[0]
            $sameCondition = ([string]$current.condition -eq [string]$Condition) -and ([string]$current.conditionVersion -eq $(if ($Condition) { '2.0' } else { '' }))
            if (-not $sameCondition) {
                throw "Une attribution preexistante non geree pour '$RoleDefinitionId' ne respecte pas la condition RBAC attendue; aucune attribution ne sera modifiee."
            }
            return 'existe deja, non gere par IFS'
        }
    }
    $arguments = @('role', 'assignment', 'create', '--name', $assignmentId, '--assignee-object-id', $PrincipalId, '--assignee-principal-type', 'ServicePrincipal', '--role', $roleId, '--scope', $Scope, '--description', $expectedDescription, '--subscription', $SubscriptionId)
    if ($Condition) { $arguments += @('--condition', $Condition, '--condition-version', '2.0') }

    if ($assignment.Count -gt 0) {
        $current = $assignment[0]
        if ([string]$current.description -notmatch [regex]::Escape("managed-by: $script:IfsManagedBy")) {
            throw "L'attribution '$assignmentId' existe sans marque de propriete IFS; elle ne sera pas modifiee."
        }
        $conditionMatches = ([string]$current.condition -eq [string]$Condition) -and ([string]$current.conditionVersion -eq $(if ($Condition) { '2.0' } else { '' }))
        $descriptionMatches = [string]$current.description -eq $expectedDescription
        if (([string]$current.roleDefinitionId).TrimEnd('/') -match [regex]::Escape($roleId) -and $conditionMatches -and $descriptionMatches) {
            return 'laisse tel quel'
        }
        $arguments = @('role', 'assignment', 'update', '--ids', [string]$current.id, '--description', $expectedDescription, '--subscription', $SubscriptionId)
        if ($Condition) { $arguments += @('--condition', $Condition, '--condition-version', '2.0') }
        else { $arguments += @('--condition', '', '--condition-version', '') }
        [void](Invoke-IfsAz -Arguments $arguments -Write)
        return 'mis a jour'
    }
    [void](Invoke-IfsAz -Arguments $arguments -Write)
    return 'cree'
}

function Update-IfsResourceTag {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)] [ValidateSet('group', 'identity', 'storage')] [string] $Kind,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $ResourceGroup,
        [Parameter(Mandatory)] [string] $SubscriptionId,
        [Parameter(Mandatory)] [int] $Revision,
        [Parameter(Mandatory)] [object] $Resource
    )

    Assert-IfsOwnedResource -Resource $Resource -Name $Name -Revision $Revision
    $tags = Get-IfsObjectProperty -Object $Resource -Name 'tags'
    if ((Get-IfsTagValue $tags 'ifs-kit-revision') -eq [string]$Revision) { return 'laisse tel quel' }
    $base = @('--subscription', $SubscriptionId)
    switch ($Kind) {
        'group' { $arguments = @('group', 'update', '--name', $Name, '--set', "tags.ifs-kit-revision=$Revision") + $base }
        'identity' { $arguments = @('identity', 'update', '--resource-group', $ResourceGroup, '--name', $Name, '--set', "tags.ifs-kit-revision=$Revision") + $base }
        'storage' { $arguments = @('storage', 'account', 'update', '--resource-group', $ResourceGroup, '--name', $Name, '--set', "tags.ifs-kit-revision=$Revision") + $base }
    }
    [void](Invoke-IfsAz -Arguments $arguments -Write)
    return 'mis a jour'
}

function Set-IfsServiceEndpoint {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)] [string] $Organization,
        [Parameter(Mandatory)] [string] $Project,
        [Parameter(Mandatory)] [string] $ProjectId,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $SubscriptionId,
        [Parameter(Mandatory)] [string] $SubscriptionName,
        [Parameter(Mandatory)] [string] $TenantId,
        [Parameter(Mandatory)] [string] $ClientId,
        [Parameter(Mandatory)] [int] $Revision
    )

    $description = "managed-by: $script:IfsManagedBy; ifs-kit-revision: $Revision"
    $allEndpoints = Invoke-IfsAzJson -Arguments @('devops', 'service-endpoint', 'list', '--org', $Organization, '--project', $Project)
    $endpoint = @($allEndpoints | Where-Object { $_.name -eq $Name } | Select-Object -First 1)
    if ($endpoint.Count -gt 0) {
        $current = $endpoint[0]
        if ([string]$current.description -notmatch [regex]::Escape("managed-by: $script:IfsManagedBy")) {
            throw "La service connection '$Name' existe sans marque de propriete IFS; elle ne sera pas modifiee."
        }
        $existingRevision = [regex]::Match([string]$current.description, 'ifs-kit-revision:\s*(\d+)')
        if (-not $existingRevision.Success) { throw "La service connection '$Name' ne porte pas de revision de kit valide." }
        Assert-IfsRevisionNotOlder -CurrentRevision $Revision -ExistingRevision ([int]$existingRevision.Groups[1].Value) -ObjectName $Name
        if ([int]$existingRevision.Groups[1].Value -lt $Revision -and -not $script:IfsWhatIf) {
            $full = Invoke-IfsAzJson -Arguments @('devops', 'service-endpoint', 'show', '--id', [string]$current.id, '--org', $Organization, '--project', $Project)
            $full.description = $description
            $path = Join-Path ([IO.Path]::GetTempPath()) ('ifs-endpoint-' + [guid]::NewGuid().ToString('N') + '.json')
            try {
                [IO.File]::WriteAllText($path, (ConvertTo-Json -InputObject $full -Depth 100), [Text.UTF8Encoding]::new($false))
                [void](Invoke-IfsAz -Arguments @('devops', 'invoke', '--area', 'serviceendpoint', '--resource', 'endpoints', '--http-method', 'PUT', '--route-parameters', "project=$ProjectId", "endpointId=$($current.id)", '--api-version', '7.1-preview.4', '--in-file', $path, '--org', $Organization, '--output', 'json') -Write)
            }
            finally { Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue }
        }
        return Invoke-IfsAzJson -Arguments @('devops', 'service-endpoint', 'show', '--id', [string]$current.id, '--org', $Organization, '--project', $Project)
    }

    $configuration = [ordered]@{
        authorization = [ordered]@{ parameters = [ordered]@{ tenantid = $TenantId; serviceprincipalid = $ClientId }; scheme = 'WorkloadIdentityFederation' }
        data = [ordered]@{ environment = 'AzureCloud'; scopeLevel = 'Subscription'; subscriptionId = $SubscriptionId; subscriptionName = $SubscriptionName; creationMode = 'Manual' }
        name = $Name
        type = 'AzureRM'
        url = 'https://management.azure.com/'
        isShared = $false
        isReady = $true
        description = $description
        serviceEndpointProjectReferences = @([ordered]@{ projectReference = [ordered]@{ id = $ProjectId; name = $Project }; name = $Name; description = $description })
    }
    $path = Join-Path ([IO.Path]::GetTempPath()) ('ifs-endpoint-' + [guid]::NewGuid().ToString('N') + '.json')
    try {
        [IO.File]::WriteAllText($path, (ConvertTo-Json -InputObject $configuration -Depth 100), [Text.UTF8Encoding]::new($false))
        [void](Invoke-IfsAz -Arguments @('devops', 'service-endpoint', 'create', '--service-endpoint-configuration', $path, '--org', $Organization, '--project', $Project, '--output', 'json') -Write)
    }
    finally { Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue }
    if ($script:IfsWhatIf) { return $null }
    $created = Invoke-IfsAzJson -Arguments @('devops', 'service-endpoint', 'list', '--org', $Organization, '--project', $Project)
    return @($created | Where-Object { $_.name -eq $Name } | Select-Object -First 1)[0]
}

function Set-IfsFederatedCredential {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)] [string] $SubscriptionId,
        [Parameter(Mandatory)] [string] $ResourceGroup,
        [Parameter(Mandatory)] [string] $IdentityName,
        [Parameter(Mandatory)] [object] $Endpoint,
        [Parameter(Mandatory)] [Collections.Generic.HashSet[string]] $ExpectedNames
    )

    if ($null -eq $Endpoint) { return 'a verifier apres la creation de la connexion' }
    $issuer = [string]$Endpoint.authorization.parameters.workloadIdentityFederationIssuer
    $subject = [string]$Endpoint.authorization.parameters.workloadIdentityFederationSubject
    if (-not $issuer -and $Endpoint.PSObject.Properties['workloadIdentityFederationIssuer']) { $issuer = [string]$Endpoint.workloadIdentityFederationIssuer }
    if (-not $subject -and $Endpoint.PSObject.Properties['workloadIdentityFederationSubject']) { $subject = [string]$Endpoint.workloadIdentityFederationSubject }
    if ([string]::IsNullOrWhiteSpace($issuer) -or [string]::IsNullOrWhiteSpace($subject)) {
        throw "Azure DevOps n'a pas retourne l'emetteur et le sujet federes pour '$($Endpoint.name)'."
    }
    if ($issuer -match 'vstoken\.dev\.azure\.com') { throw "La connexion '$($Endpoint.name)' utilise l'ancien emetteur Azure DevOps; recreez-la avec l'emetteur Microsoft Entra." }
    $name = 'ifs-ado-' + ([string]$Endpoint.id).Replace('-', '').Substring(0, 12).ToLowerInvariant()
    [void]$ExpectedNames.Add($name)
    $existing = Invoke-IfsAzJson -Arguments @('identity', 'federated-credential', 'list', '--identity-name', $IdentityName, '--resource-group', $ResourceGroup, '--subscription', $SubscriptionId)
    $credential = @($existing | Where-Object { $_.name -eq $name } | Select-Object -First 1)
    if ($credential.Count -gt 0 -and $credential[0].issuer -eq $issuer -and $credential[0].subject -eq $subject) { return 'laisse tel quel' }
    if ($credential.Count -gt 0) {
        [void](Invoke-IfsAz -Arguments @('identity', 'federated-credential', 'delete', '--name', $name, '--identity-name', $IdentityName, '--resource-group', $ResourceGroup, '--subscription', $SubscriptionId, '--yes') -Write)
    }
    [void](Invoke-IfsAz -Arguments @('identity', 'federated-credential', 'create', '--name', $name, '--identity-name', $IdentityName, '--resource-group', $ResourceGroup, '--issuer', $issuer, '--subject', $subject, '--audiences', 'api://AzureADTokenExchange', '--subscription', $SubscriptionId, '--only-show-errors') -Write)
    return 'cree'
}

function Remove-IfsStaleFederatedCredential {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)] [string] $SubscriptionId,
        [Parameter(Mandatory)] [string] $ResourceGroup,
        [Parameter(Mandatory)] [string] $IdentityName,
        [Parameter(Mandatory)] [object[]] $Existing,
        [Parameter(Mandatory)] [string[]] $ExpectedNames
    )

    foreach ($credential in (Get-IfsStaleFederatedCredential -Existing $Existing -ExpectedNames $ExpectedNames)) {
        if ($PSCmdlet.ShouldProcess("$IdentityName/$($credential.name)", 'supprimer cet identifiant federé Azure DevOps obsolete')) {
            [void](Invoke-IfsAz -Arguments @('identity', 'federated-credential', 'delete', '--name', [string]$credential.name, '--identity-name', $IdentityName, '--resource-group', $ResourceGroup, '--subscription', $SubscriptionId, '--yes') -Write)
            Write-Information -MessageData "Identifiant federé obsolete supprime : $IdentityName/$($credential.name)" -InformationAction Continue
        }
    }
}

function Set-IfsIdentity {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)] [string] $SubscriptionId,
        [Parameter(Mandatory)] [string] $ResourceGroup,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $Location,
        [Parameter(Mandatory)] [int] $Revision
    )

    $identities = Invoke-IfsAzJson -Arguments @('identity', 'list', '--subscription', $SubscriptionId)
    $identity = @($identities | Where-Object { $_.name -eq $Name } | Select-Object -First 1)
    if ($identity.Count -eq 0) {
        [void](Invoke-IfsAz -Arguments @('identity', 'create', '--resource-group', $ResourceGroup, '--name', $Name, '--location', $Location, '--tags', 'managed-by=infraflowsculptor', "ifs-kit-revision=$Revision", '--subscription', $SubscriptionId) -Write)
        if ($script:IfsWhatIf) { return $null }
        $identities = Invoke-IfsAzJson -Arguments @('identity', 'list', '--subscription', $SubscriptionId)
        return @($identities | Where-Object { $_.name -eq $Name } | Select-Object -First 1)[0]
    }
    $identity = $identity[0]
    $actualGroup = [string]$identity.resourceGroup
    if ($actualGroup -ne $ResourceGroup) { throw "L'identite '$Name' existe dans '$actualGroup' plutot que '$ResourceGroup'." }
    $result = Update-IfsResourceTag -Kind identity -Name $Name -ResourceGroup $ResourceGroup -SubscriptionId $SubscriptionId -Revision $Revision -Resource $identity
    if ($result -eq 'mis a jour' -and -not $script:IfsWhatIf) {
        $identities = Invoke-IfsAzJson -Arguments @('identity', 'list', '--subscription', $SubscriptionId)
        return @($identities | Where-Object { $_.name -eq $Name } | Select-Object -First 1)[0]
    }
    return $identity
}

function Set-IfsStorage {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)] [string] $SubscriptionId,
        [Parameter(Mandatory)] [string] $ResourceGroup,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $Location,
        [Parameter(Mandatory)] [int] $Revision
    )

    $accounts = Invoke-IfsAzJson -Arguments @('storage', 'account', 'list', '--subscription', $SubscriptionId)
    $account = @($accounts | Where-Object { $_.name -eq $Name } | Select-Object -First 1)
    $createdNow = $false
    if ($account.Count -eq 0) {
        [void](Invoke-IfsAz -Arguments @('storage', 'account', 'create', '--name', $Name, '--resource-group', $ResourceGroup, '--location', $Location, '--sku', 'Standard_LRS', '--kind', 'StorageV2', '--allow-blob-public-access', 'false', '--allow-shared-key-access', 'false', '--min-tls-version', 'TLS1_2', '--tags', 'managed-by=infraflowsculptor', "ifs-kit-revision=$Revision", '--subscription', $SubscriptionId) -Write)
        if ($script:IfsWhatIf) { return $null }
        $createdNow = $true
        $account = Invoke-IfsAzJson -Arguments @('storage', 'account', 'show', '--name', $Name, '--resource-group', $ResourceGroup, '--subscription', $SubscriptionId)
    }
    else {
        $account = $account[0]
        if ([string]$account.resourceGroup -ne $ResourceGroup) { throw "Le compte de stockage '$Name' existe dans un autre groupe de ressources." }
        [void](Update-IfsResourceTag -Kind storage -Name $Name -ResourceGroup $ResourceGroup -SubscriptionId $SubscriptionId -Revision $Revision -Resource $account)
        $account = Invoke-IfsAzJson -Arguments @('storage', 'account', 'show', '--name', $Name, '--resource-group', $ResourceGroup, '--subscription', $SubscriptionId)
    }
    if ($script:IfsWhatIf) { return $null }
    $accountNeedsUpdate = ([bool]$account.allowBlobPublicAccess) -or ([bool]$account.allowSharedKeyAccess) -or ([string]$account.minimumTlsVersion -ne 'TLS1_2')
    if (-not $createdNow -and $accountNeedsUpdate) {
        [void](Invoke-IfsAz -Arguments @('storage', 'account', 'update', '--name', $Name, '--resource-group', $ResourceGroup, '--allow-blob-public-access', 'false', '--allow-shared-key-access', 'false', '--min-tls-version', 'TLS1_2', '--subscription', $SubscriptionId) -Write)
    }
    $blobProperties = Invoke-IfsAzJson -Arguments @('storage', 'account', 'blob-service-properties', 'show', '--account-name', $Name, '--resource-group', $ResourceGroup, '--subscription', $SubscriptionId)
    $versioningEnabled = [bool]$blobProperties.isVersioningEnabled
    $deleteEnabled = [bool]$blobProperties.deleteRetentionPolicy.enabled
    $deleteDays = [int]$blobProperties.deleteRetentionPolicy.days
    if (-not $versioningEnabled -or -not $deleteEnabled -or $deleteDays -lt 30) {
        [void](Invoke-IfsAz -Arguments @('storage', 'account', 'blob-service-properties', 'update', '--account-name', $Name, '--resource-group', $ResourceGroup, '--enable-versioning', 'true', '--enable-delete-retention', 'true', '--delete-retention-days', '30', '--subscription', $SubscriptionId) -Write)
    }
    $containers = Invoke-IfsAzJson -Arguments @('storage', 'container-rm', 'list', '--resource-group', $ResourceGroup, '--storage-account', $Name, '--subscription', $SubscriptionId)
    $operationsContainer = @($containers | Where-Object { $_.name -eq 'ifs-operations' } | Select-Object -First 1)
    if ($operationsContainer.Count -eq 0) {
        [void](Invoke-IfsAz -Arguments @('storage', 'container-rm', 'create', '--resource-group', $ResourceGroup, '--storage-account', $Name, '--name', 'ifs-operations', '--public-access', 'off', '--subscription', $SubscriptionId) -Write)
    }
    elseif ([string]$operationsContainer[0].publicAccess -notin @('', 'None', 'Off', 'off')) {
        [void](Invoke-IfsAz -Arguments @('storage', 'container-rm', 'update', '--resource-group', $ResourceGroup, '--storage-account', $Name, '--name', 'ifs-operations', '--public-access', 'off', '--subscription', $SubscriptionId) -Write)
    }
    if ($account -is [array]) { return $account[0] }
    return $account
}

function Add-IfsSqlGroupMember {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $PrincipalId,
        [string] $GroupName = 'sg-shop-sql-admins'
    )

    try {
        $group = Invoke-IfsAzJson -Arguments @('ad', 'group', 'show', '--group', $GroupName)
        if ($null -eq $group -or [string]::IsNullOrWhiteSpace([string]$group.id)) { throw "Groupe Entra introuvable: $GroupName" }
        $membership = Invoke-IfsAzJson -Arguments @('ad', 'group', 'member', 'check', '--group', [string]$group.id, '--member-id', $PrincipalId)
        if ($membership.value -eq $true) { return [pscustomobject]@{ Status = 'present'; Command = $null } }
        [void](Invoke-IfsAz -Arguments @('ad', 'group', 'member', 'add', '--group', [string]$group.id, '--member-id', $PrincipalId) -Write)
        return [pscustomobject]@{ Status = $(if ($script:IfsWhatIf) { 'a faire' } else { 'ajoute' }); Command = "az ad group member add --group '$($group.id)' --member-id '$PrincipalId'" }
    }
    catch {
        $groupId = $null
        try { $groupId = [string](Invoke-IfsAz -Arguments @('ad', 'group', 'show', '--group', $GroupName, '--query', 'id', '-o', 'tsv') -AllowFailure) }
        catch { Write-Verbose "Impossible de relire le groupe '$GroupName' apres l'echec: $($_.Exception.Message)" }
        if ($groupId) { $command = "az ad group member add --group '$groupId' --member-id '$PrincipalId'" }
        else { $command = "az ad group create --display-name '$GroupName' --mail-nickname 'shop-sql-admins' ; puis az ad group member add --group '<group-object-id>' --member-id '$PrincipalId'" }
        return [pscustomobject]@{ Status = 'a faire'; Command = $command; Reason = $_.Exception.Message }
    }
}

function Invoke-IfsAzureSetup {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)] [string] $RepositoryPath,
        [Parameter(Mandatory)] [string] $AzureDevOpsOrganization,
        [Parameter(Mandatory)] [string] $AzureDevOpsProject,
        [Parameter(Mandatory)] [string] $TenantId
    )

    $script:IfsWhatIf = [bool]$WhatIfPreference
    $plan = Get-IfsPilotPlan -RepositoryPath $RepositoryPath
    $script:IfsRevision = $plan.Revision
    $tenant = ([guid]$TenantId).ToString()
    $account = Invoke-IfsAzJson -Arguments @('account', 'show')
    if ($null -eq $account) { throw "Aucune session Azure active. Connectez-vous avec 'az login --tenant $tenant'." }
    if ([string]$account.tenantId -ne $tenant) { throw "Le tenant de la session Azure ne correspond pas au TenantId fourni." }
    $adoProject = Invoke-IfsAzJson -Arguments @('devops', 'project', 'show', '--org', $AzureDevOpsOrganization, '--project', $AzureDevOpsProject)
    if ($null -eq $adoProject.id) { throw "Projet Azure DevOps '$AzureDevOpsProject' introuvable dans '$AzureDevOpsOrganization'." }

    $reports = [Collections.Generic.List[object]]::new()
    $script:IfsCurrentSubscription = $plan.Targets[0].SubscriptionId
    $roleContributor = Get-IfsRoleDefinitionId -RoleName 'Contributor'
    $roleStackOwner = Get-IfsRoleDefinitionId -RoleName 'Azure Deployment Stack Owner'
    $roleRbacAdmin = Get-IfsRoleDefinitionId -RoleName 'Role Based Access Control Administrator'
    $roleStorage = Get-IfsRoleDefinitionId -RoleName 'Storage Blob Data Contributor'
    $roleAcrPull = Get-IfsRoleDefinitionId -RoleName 'AcrPull'
    $roleAcrPush = Get-IfsRoleDefinitionId -RoleName 'AcrPush'
    $roleKvSecretsUser = Get-IfsRoleDefinitionId -RoleName 'Key Vault Secrets User'
    $roleLogsReader = Get-IfsRoleDefinitionId -RoleName 'Log Analytics Reader'
    $registryIds = @($plan.Releases | ForEach-Object { $_.Release.dependencies } | ForEach-Object { $_.resources } | ForEach-Object { [string]$_.id } | Where-Object { $_ -match '/providers/Microsoft\.ContainerRegistry/registries/' } | Sort-Object -Unique)
    $registryResourceId = if ($registryIds.Count -gt 0) { $registryIds[0] } else { $null }
    if ($registryIds.Count -gt 1) { throw 'Le kit pilote attend un seul registre partagé.' }
    $registrySubscription = $null
    if ($registryResourceId) {
        $registrySubscription = [regex]::Match($registryResourceId, '^/subscriptions/([^/]+)/').Groups[1].Value
        if ($registrySubscription -notmatch '^[0-9a-fA-F-]{36}$') { throw "Identifiant d'abonnement du registre invalide: $registryResourceId" }
    }

    foreach ($target in $plan.Targets) {
        $steps = [Collections.Generic.List[object]]::new()
        $subscription = $target.SubscriptionId
        $script:IfsCurrentSubscription = $subscription
        try {
            $subscriptionAccount = Invoke-IfsAzJson -Arguments @('account', 'show', '--subscription', $subscription)
            if ($null -eq $subscriptionAccount) { throw "Abonnement $subscription inaccessible." }
            if ([string]$subscriptionAccount.tenantId -ne $tenant) { throw "L'abonnement $subscription appartient a un autre tenant." }
            $resourceGroup = ConvertTo-IfsTechnicalName -Value "rg-ifs-$($plan.Project)-$($target.Name)" -MaximumLength 90
            $rgList = Invoke-IfsAzJson -Arguments @('group', 'list', '--subscription', $subscription)
            $rg = @($rgList | Where-Object { $_.name -eq $resourceGroup } | Select-Object -First 1)
            if ($rg.Count -eq 0) {
                [void](Invoke-IfsAz -Arguments @('group', 'create', '--name', $resourceGroup, '--location', $target.Location, '--tags', 'managed-by=infraflowsculptor', "ifs-kit-revision=$($plan.Revision)", '--subscription', $subscription) -Write)
                $steps.Add([pscustomobject]@{ Name = 'groupe de ressources'; Status = $(if ($script:IfsWhatIf) { 'a faire' } else { 'cree' }) })
            }
            else {
                $rg = $rg[0]
                [void](Update-IfsResourceTag -Kind group -Name $resourceGroup -ResourceGroup $resourceGroup -SubscriptionId $subscription -Revision $plan.Revision -Resource $rg)
                $steps.Add([pscustomobject]@{ Name = 'groupe de ressources'; Status = 'verifie' })
            }

            $deployName = ConvertTo-IfsTechnicalName -Value "id-ifs-deploy-$($plan.Project)-$($target.Name)" -MaximumLength 128
            $appName = ConvertTo-IfsTechnicalName -Value "id-ifs-app-$($plan.Project)-$($target.Name)" -MaximumLength 128
            $deployIdentity = Set-IfsIdentity -SubscriptionId $subscription -ResourceGroup $resourceGroup -Name $deployName -Location $target.Location -Revision $plan.Revision
            $appIdentity = Set-IfsIdentity -SubscriptionId $subscription -ResourceGroup $resourceGroup -Name $appName -Location $target.Location -Revision $plan.Revision
            $steps.Add([pscustomobject]@{ Name = 'identites'; Status = $(if ($null -eq $deployIdentity -or $null -eq $appIdentity) { 'a faire' } else { 'pretes' }) })

            if ($null -eq $deployIdentity -or $null -eq $appIdentity) {
                $steps.Add([pscustomobject]@{ Name = 'federation, roles, SQL, stockage'; Status = 'a verifier apres les creations WhatIf' })
                $reports.Add([pscustomobject]@{ Target = $target.Name; SubscriptionId = $subscription; Status = 'WhatIf'; Steps = @($steps) })
                continue
            }
            if ([string]::IsNullOrWhiteSpace([string]$deployIdentity.clientId) -or [string]::IsNullOrWhiteSpace([string]$deployIdentity.principalId)) { throw "Azure n'a pas retourne les identifiants de '$deployName'." }
            if ([string]::IsNullOrWhiteSpace([string]$appIdentity.clientId) -or [string]::IsNullOrWhiteSpace([string]$appIdentity.principalId)) { throw "Azure n'a pas retourne les identifiants de '$appName'." }

            $storageName = ConvertTo-IfsTechnicalName -Value "stifs$($plan.Project)$($target.Name)" -MaximumLength 24 -StorageAccount
            [void](Set-IfsStorage -SubscriptionId $subscription -ResourceGroup $resourceGroup -Name $storageName -Location $target.Location -Revision $plan.Revision)
            $storageScope = "/subscriptions/$subscription/resourceGroups/$resourceGroup/providers/Microsoft.Storage/storageAccounts/$storageName/blobServices/default/containers/ifs-operations"
            $steps.Add([pscustomobject]@{ Name = 'stockage technique'; Status = 'pret' })

            [void](Set-IfsRoleAssignment -SubscriptionId $subscription -Scope "/subscriptions/$subscription" -PrincipalId ([string]$deployIdentity.principalId) -RoleDefinitionId $roleContributor)
            [void](Set-IfsRoleAssignment -SubscriptionId $subscription -Scope "/subscriptions/$subscription" -PrincipalId ([string]$deployIdentity.principalId) -RoleDefinitionId $roleStackOwner)
            [void](Set-IfsRoleAssignment -SubscriptionId $subscription -Scope $storageScope -PrincipalId ([string]$deployIdentity.principalId) -RoleDefinitionId $roleStorage)
            $steps.Add([pscustomobject]@{ Name = 'roles Contributor, Deployment Stack Owner et stockage'; Status = 'attribues' })

            $targetRoleIds = if ($target.Name -in @('dev', 'prd')) { @($roleKvSecretsUser, $roleLogsReader) } else { @() }
            if ($registryResourceId -and $registrySubscription -eq $subscription -and $target.Name -in @('dev', 'prd')) { $targetRoleIds += $roleAcrPull }
            if ($targetRoleIds.Count -gt 0) {
                $condition = Get-IfsRbacCondition -RoleDefinitionIds $targetRoleIds
                [void](Set-IfsRoleAssignment -SubscriptionId $subscription -Scope "/subscriptions/$subscription" -PrincipalId ([string]$deployIdentity.principalId) -RoleDefinitionId $roleRbacAdmin -Condition $condition)
            }
            if ($registryResourceId -and $registrySubscription -ne $subscription -and $target.Name -in @('dev', 'prd')) {
                $condition = Get-IfsRbacCondition -RoleDefinitionIds @($roleAcrPull)
                [void](Set-IfsRoleAssignment -SubscriptionId $registrySubscription -Scope $registryResourceId -PrincipalId ([string]$deployIdentity.principalId) -RoleDefinitionId $roleRbacAdmin -Condition $condition)
            }
            if ($target.Name -eq 'shared' -and $registryResourceId) {
                [void](Set-IfsRoleAssignment -SubscriptionId $registrySubscription -Scope $registryResourceId -PrincipalId ([string]$appIdentity.principalId) -RoleDefinitionId $roleAcrPush)
            }
            $steps.Add([pscustomobject]@{ Name = 'RBAC conditionne aux roles de la reference'; Status = 'configure' })

            $sqlMembership = Add-IfsSqlGroupMember -PrincipalId ([string]$deployIdentity.principalId)
            $steps.Add([pscustomobject]@{ Name = 'groupe SQL'; Status = $sqlMembership.Status; Command = $sqlMembership.Command; Reason = $sqlMembership.Reason })

            $expectedDeploy = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            $expectedApp = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            $deploymentEndpoint = Set-IfsServiceEndpoint -Organization $AzureDevOpsOrganization -Project $AzureDevOpsProject -ProjectId ([string]$adoProject.id) -Name "ifs-$($plan.Project)-$($target.Name)" -SubscriptionId $subscription -SubscriptionName ([string]$subscriptionAccount.name) -TenantId $tenant -ClientId ([string]$deployIdentity.clientId) -Revision $plan.Revision
            $applicationEndpoint = Set-IfsServiceEndpoint -Organization $AzureDevOpsOrganization -Project $AzureDevOpsProject -ProjectId ([string]$adoProject.id) -Name "ifs-$($plan.Project)-$($target.Name)-app" -SubscriptionId $subscription -SubscriptionName ([string]$subscriptionAccount.name) -TenantId $tenant -ClientId ([string]$appIdentity.clientId) -Revision $plan.Revision
            [void](Set-IfsFederatedCredential -SubscriptionId $subscription -ResourceGroup $resourceGroup -IdentityName $deployName -Endpoint $deploymentEndpoint -ExpectedNames $expectedDeploy)
            [void](Set-IfsFederatedCredential -SubscriptionId $subscription -ResourceGroup $resourceGroup -IdentityName $appName -Endpoint $applicationEndpoint -ExpectedNames $expectedApp)
            $deployFics = Invoke-IfsAzJson -Arguments @('identity', 'federated-credential', 'list', '--identity-name', $deployName, '--resource-group', $resourceGroup, '--subscription', $subscription)
            $appFics = Invoke-IfsAzJson -Arguments @('identity', 'federated-credential', 'list', '--identity-name', $appName, '--resource-group', $resourceGroup, '--subscription', $subscription)
            Remove-IfsStaleFederatedCredential -SubscriptionId $subscription -ResourceGroup $resourceGroup -IdentityName $deployName -Existing @($deployFics) -ExpectedNames @($expectedDeploy) -WhatIf:$script:IfsWhatIf
            Remove-IfsStaleFederatedCredential -SubscriptionId $subscription -ResourceGroup $resourceGroup -IdentityName $appName -Existing @($appFics) -ExpectedNames @($expectedApp) -WhatIf:$script:IfsWhatIf
            $steps.Add([pscustomobject]@{ Name = 'service connections et identifiants federes'; Status = $(if ($script:IfsWhatIf) { 'a faire' } else { 'reconcilies' }) })
            $reports.Add([pscustomobject]@{ Target = $target.Name; SubscriptionId = $subscription; Status = 'reussi'; Steps = @($steps) })
        }
        catch {
            Write-Information -MessageData ("Erreur installation pour la cible '$($target.Name)' : $($_.Exception.Message)") -InformationAction Continue
            $steps.Add([pscustomobject]@{ Name = 'erreur'; Status = 'echec'; Reason = $_.Exception.Message })
            $reports.Add([pscustomobject]@{ Target = $target.Name; SubscriptionId = $subscription; Status = 'echec'; Steps = @($steps) })
        }
    }
    $report = [pscustomobject]@{ project = $plan.Project; revision = $plan.Revision; tenantId = $tenant; targets = @($reports); completedAtUtc = [DateTime]::UtcNow.ToString('O') }
    $report | ConvertTo-Json -Depth 100 | Write-Output
    if (@($reports | Where-Object Status -eq 'echec').Count -gt 0) { throw 'Une ou plusieurs cibles ont echoue. Corrigez les lignes du rapport puis relancez le kit.' }
    return $report
}

Export-ModuleMember -Function ConvertTo-IfsTechnicalName, Get-IfsStableGuid, Get-IfsRbacCondition, Assert-IfsRevisionNotOlder, Get-IfsStaleFederatedCredential, Assert-IfsOwnedResource, Invoke-IfsAz, Invoke-IfsAzJson, Get-IfsPilotPlan, Set-IfsRoleAssignment, Update-IfsResourceTag, Set-IfsFederatedCredential, Remove-IfsStaleFederatedCredential, Add-IfsSqlGroupMember, Invoke-IfsAzureSetup
