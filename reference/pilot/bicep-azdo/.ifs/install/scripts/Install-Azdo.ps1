# Généré par InfraFlowSculptor. Ce script configure Azure DevOps depuis System.AccessToken.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('Prepare', 'Finalize')] [string] $Phase,
    [Parameter(Mandatory)] [string] $RepositoryPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'IfsInstall.psm1') -Force

$script:ApiVersion = '7.1'
$script:ChecksApiVersion = '7.1-preview.1'
$script:Organization = $env:SYSTEM_COLLECTIONURI.TrimEnd('/').Split('/')[-1]
$script:Project = $env:SYSTEM_TEAMPROJECT
$script:ProjectId = $env:SYSTEM_TEAMPROJECTID
$script:RepositoryId = $env:BUILD_REPOSITORY_ID
$script:RepositoryName = $env:BUILD_REPOSITORY_NAME
$script:Token = $env:SYSTEM_ACCESSTOKEN
$script:Root = (Resolve-Path -LiteralPath $RepositoryPath).Path
$script:OwnerMarker = 'managed-by: infraflowsculptor'
$script:ReportPath = Join-Path $env:AGENT_TEMPDIRECTORY 'ifs-install-report.json'

if ([string]::IsNullOrWhiteSpace($script:Token)) { throw 'System.AccessToken est absent. Activez son acces dans les options du job Azure Pipelines.' }
if ([string]::IsNullOrWhiteSpace($script:ProjectId) -or [string]::IsNullOrWhiteSpace($script:RepositoryId)) {
    throw 'Azure Pipelines ne fournit pas System.TeamProjectId ou Build.Repository.ID.'
}
if ($env:BUILD_SOURCEBRANCH -ne 'refs/heads/main') { throw 'Le pipeline d''installation doit etre execute depuis la branche par defaut main.' }

function Get-IfsAdoValue {
    param([AllowNull()] [object] $Response)
    if ($null -eq $Response) { return @() }
    if ($Response.PSObject.Properties['value']) { return @($Response.value) }
    if ($Response -is [Collections.IEnumerable] -and $Response -isnot [string]) { return @($Response) }
    return @($Response)
}

function Get-IfsAdoProperty {
    param([AllowNull()] [object] $Object, [Parameter(Mandatory)] [string] $Name)
    if ($null -eq $Object) { return $null }
    if ($Object -is [Collections.IDictionary]) { return $Object[$Name] }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Invoke-IfsAdoApi {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Path,
        [ValidateSet('GET', 'POST', 'PUT', 'PATCH', 'DELETE')] [string] $Method = 'GET',
        [AllowNull()] [object] $Body,
        [switch] $OrganizationScope
    )

    $scope = if ($OrganizationScope) { "https://dev.azure.com/$script:Organization" } else { "https://dev.azure.com/$script:Organization/$([uri]::EscapeDataString($script:Project))" }
    $uri = "$scope/_apis/$($Path.TrimStart('/'))"
    $headers = @{ Authorization = "Bearer $script:Token"; Accept = 'application/json' }
    try {
        if ($null -eq $Body) {
            return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ErrorAction Stop
        }
        $json = ConvertTo-Json -InputObject $Body -Depth 100
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType 'application/json; charset=utf-8' -Body $json -ErrorAction Stop
    }
    catch {
        throw "Azure DevOps REST $Method $Path a echoue : $($_.Exception.Message)"
    }
}

function Add-IfsReportEvent {
    param([string] $Target, [string] $Name, [string] $Status, [string] $Details)
    $script:Report.events += [pscustomobject]@{ target = $Target; name = $Name; status = $Status; details = $Details }
    Write-Information -MessageData ('[{0}] {1}: {2}' -f $Target, $Name, $Status) -InformationAction Continue
}

function Save-IfsReport {
    $script:Report.generatedAtUtc = [DateTime]::UtcNow.ToString('O')
    [IO.File]::WriteAllText($script:ReportPath, (ConvertTo-Json -InputObject $script:Report -Depth 100), [Text.UTF8Encoding]::new($false))
    Write-Information -MessageData "Rapport ecrit dans $script:ReportPath" -InformationAction Continue
}

function Get-IfsReleaseModel {
    $manifestPath = Join-Path $script:Root '.ifs/manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Manifeste introuvable : $manifestPath" }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 100
    $releaseFiles = @(Get-ChildItem -LiteralPath $script:Root -Filter 'release.*.json' -File -Recurse | Where-Object {
        $_.FullName -notmatch '[\\/](\.git|reference|bin|obj)[\\/]'
    })
    if ($releaseFiles.Count -eq 0) { throw 'Aucun fichier release.<cible>.json trouve.' }
    $releases = foreach ($file in $releaseFiles) {
        $value = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json -Depth 100
        [pscustomobject]@{ File = $file; Data = $value; RelativePath = [IO.Path]::GetRelativePath($script:Root, $file.FullName).Replace('\', '/') }
    }
    $projectCodes = @($releases.Data.project | Sort-Object -Unique)
    if ($projectCodes.Count -ne 1) { throw 'Les fichiers release declarent plusieurs projets.' }
    $targets = foreach ($group in ($releases | Group-Object { [string]$_.Data.target } | Sort-Object Name)) {
        $targetReleases = @($group.Group)
        $subscriptions = @($targetReleases.Data.subscriptionId | Sort-Object -Unique)
        $locations = @($targetReleases.Data.location | Sort-Object -Unique)
        $protected = @($targetReleases.Data.protected | Sort-Object -Unique)
        if ($subscriptions.Count -ne 1 -or $locations.Count -ne 1 -or $protected.Count -ne 1) { throw "Releases incoherentes pour la cible $($group.Name)." }
        if ([string]$subscriptions[0] -match '[<>]') { throw "L'abonnement de la cible $($group.Name) n'a pas ete renseigne." }
        [pscustomobject]@{ Name = $group.Name; SubscriptionId = [string]$subscriptions[0]; Location = [string]$locations[0]; Protected = [bool]$protected[0]; Releases = $targetReleases }
    }
    return [pscustomobject]@{ ProjectCode = [string]$projectCodes[0]; Revision = [int]$manifest.revision; Targets = @($targets); Releases = @($releases) }
}

function Get-IfsPipelineDefinition {
    param([Parameter(Mandatory)] [object] $Model)

    $pipelineFiles = @(Get-ChildItem -LiteralPath $script:Root -Filter '*.yml' -File -Recurse | Where-Object {
        $_.FullName -match '[\\/]pipelines[\\/](pr|ci|release)\.yml$' -and $_.FullName -notmatch '[\\/](reference|\.git)[\\/]'
    })
    $definitions = foreach ($file in $pipelineFiles) {
        $relative = [IO.Path]::GetRelativePath($script:Root, $file.FullName).Replace('\', '/')
        if ($relative -match '^(?<component>[^/]+)/infra/pipelines/(?<kind>pr|ci|release)\.yml$') {
            $component = $Matches.component
            $kind = $Matches.kind.ToUpperInvariant()
            $folder = "\$($Model.ProjectCode)\$component"
            $name = "$($Model.ProjectCode) · $component · infra · $kind"
            $isApp = $false
        }
        elseif ($relative -match '^(?<component>[^/]+)/apps/(?<app>[^/]+)/pipelines/(?<kind>pr|ci|release)\.yml$') {
            $component = $Matches.component
            $application = $Matches.app
            $kind = $Matches.kind.ToUpperInvariant()
            $folder = "\$($Model.ProjectCode)\$component\$application"
            $name = "$($Model.ProjectCode) · $component · $application · $kind"
            $isApp = $true
        }
        else { continue }
        [pscustomobject]@{
            Name = $name
            Folder = $folder
            Kind = $kind
            Component = $component
            Application = $(if ($isApp) { $application } else { $null })
            IsApp = $isApp
            RelativePath = $relative
            YamlPath = "/$relative"
            FilePath = $file.FullName
            Description = "$($script:OwnerMarker); ifs-kit-revision: $($Model.Revision)"
            Targets = @($Model.Targets | Where-Object { @($_.Releases | ForEach-Object { $_.Data.component } | Sort-Object -Unique) -contains $component })
        }
    }
    if (@($definitions).Count -eq 0) { throw 'Aucun pipeline PR, CI ou Release pilote trouve.' }
    return @($definitions | Sort-Object Folder, Kind)
}

function Get-IfsCheckType {
    param([Parameter(Mandatory)] [string] $Pattern)
    $definitions = @(
        [pscustomobject]@{ Pattern = '^(Approval)$'; Id = '8c6f20a7-a545-4486-9777-f762fafe0d4d'; Name = 'Approval' }
        [pscustomobject]@{ Pattern = 'Exclusive.?Lock'; Id = '2ef31ad6-baa0-403a-8b45-2cbc9b4e5563'; Name = 'Exclusive Lock' }
        [pscustomobject]@{ Pattern = 'Branch.?Control'; Id = 'fe1de3ee-a436-41b4-bb20-f6eb4cb879a7'; Name = 'Branch Control' }
        [pscustomobject]@{ Pattern = 'Required.?Template'; Id = '4020e66e-b0f3-47e1-bc88-48f3cc59b5f3'; Name = 'Required Template' }
    )
    $type = @($definitions | Where-Object { $_.Pattern -eq $Pattern } | Select-Object -First 1)
    if ($type.Count -eq 0) { throw "Le type de controle Azure DevOps '$Pattern' n'est pas pris en charge par le kit." }
    return [pscustomobject]@{ id = $type[0].Id; name = $type[0].Name }
}

function Set-IfsEnvironment {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([Parameter(Mandatory)] [string] $Name, [Parameter(Mandatory)] [int] $Revision)
    $response = Invoke-IfsAdoApi -Path "distributedtask/environments?name=$([uri]::EscapeDataString($Name))&api-version=$script:ApiVersion"
    $environment = @(Get-IfsAdoValue $response | Where-Object { $_.name -eq $Name } | Select-Object -First 1)
    $description = "$($script:OwnerMarker); ifs-kit-revision: $Revision"
    if ($environment.Count -eq 0) {
        if (-not $PSCmdlet.ShouldProcess($Name, 'creer environnement Azure DevOps')) {
            return [pscustomobject]@{ id = 0; name = $Name; description = $description }
        }
        $environment = Invoke-IfsAdoApi -Method POST -Path "distributedtask/environments?api-version=$script:ApiVersion" -Body @{ name = $Name; description = $description }
        Add-IfsReportEvent -Target $Name -Name 'environment' -Status 'cree' -Details 'Environnement cree.'
        return $environment
    }
    $current = $environment[0]
    $currentDescription = [string](Get-IfsAdoProperty -Object $current -Name 'description')
    if ($currentDescription -notmatch [regex]::Escape($script:OwnerMarker)) { throw "L'environnement '$Name' existe sans marque de propriete IFS." }
    $match = [regex]::Match($currentDescription, 'ifs-kit-revision:\s*(\d+)')
    if (-not $match.Success) { throw "Revision de kit invalide sur l'environnement '$Name'." }
    if ([int]$match.Groups[1].Value -gt $Revision) { throw "Le kit revision $Revision est plus ancien que l'environnement '$Name'." }
    if ([int]$match.Groups[1].Value -lt $Revision) {
        $current.description = $description
        if (-not $PSCmdlet.ShouldProcess($Name, 'actualiser la revision de l environnement Azure DevOps')) { return $current }
        $current = Invoke-IfsAdoApi -Method PATCH -Path "distributedtask/environments/$($current.id)?api-version=$script:ApiVersion" -Body $current
        Add-IfsReportEvent -Target $Name -Name 'environment' -Status 'mis a jour' -Details 'Revision du kit actualisee.'
    }
    else { Add-IfsReportEvent -Target $Name -Name 'environment' -Status 'laisse tel quel' -Details 'La revision est deja courante.' }
    return $current
}

function Set-IfsBuildDefinition {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([Parameter(Mandatory)] [object] $Definition, [Parameter(Mandatory)] [object] $Queue)

    $encodedName = [uri]::EscapeDataString($Definition.Name)
    $encodedPath = [uri]::EscapeDataString($Definition.Folder)
    $response = Invoke-IfsAdoApi -Path "build/definitions?name=$encodedName&path=$encodedPath&api-version=$script:ApiVersion"
    $existing = @(Get-IfsAdoValue $response | Where-Object { $_.name -eq $Definition.Name -and $_.path -eq $Definition.Folder } | Select-Object -First 1)
    $body = [ordered]@{
        name = $Definition.Name
        path = $Definition.Folder
        description = $Definition.Description
        type = 'build'
        queueStatus = 'enabled'
        process = [ordered]@{ type = 2; yamlFilename = $Definition.YamlPath }
        repository = [ordered]@{ id = $script:RepositoryId; name = $script:RepositoryName; type = 'TfsGit'; defaultBranch = $script:DefaultBranch; clean = 'false'; checkoutSubmodules = $false }
        queue = [ordered]@{ id = [int]$Queue.id; name = [string]$Queue.name }
        triggers = @()
    }
    if ($existing.Count -eq 0) {
        if (-not $PSCmdlet.ShouldProcess($Definition.Name, 'creer la definition de pipeline')) { return $Definition }
        $created = Invoke-IfsAdoApi -Method POST -Path "build/definitions?api-version=$script:ApiVersion" -Body $body
        Add-IfsReportEvent -Target $Definition.Component -Name "pipeline $($Definition.Kind)" -Status 'cree' -Details $Definition.Name
        $Definition | Add-Member -NotePropertyName Id -NotePropertyValue ([int]$created.id) -Force
        return $Definition
    }
    $current = Invoke-IfsAdoApi -Path "build/definitions/$($existing[0].id)?api-version=$script:ApiVersion"
    $currentDescription = [string](Get-IfsAdoProperty -Object $current -Name 'description')
    if ($currentDescription -notmatch [regex]::Escape($script:OwnerMarker)) { throw "La definition '$($Definition.Name)' existe sans marque IFS." }
    $revision = [regex]::Match($currentDescription, 'ifs-kit-revision:\s*(\d+)')
    if (-not $revision.Success) { throw "La definition '$($Definition.Name)' ne porte pas de revision de kit." }
    if ([int]$revision.Groups[1].Value -gt [int]($Definition.Description -replace '.*ifs-kit-revision:\s*', '')) {
        throw "Le kit est plus ancien que la definition '$($Definition.Name)'."
    }
    $body.id = [int]$current.id
    $body.revision = [int]$current.revision
    $body.project = $current.project
    $body.createdDate = $current.createdDate
    $body.authoredBy = $current.authoredBy
    $body = [pscustomobject]$body
    if (-not $PSCmdlet.ShouldProcess($Definition.Name, 'mettre a jour la definition de pipeline')) {
        $Definition | Add-Member -NotePropertyName Id -NotePropertyValue ([int]$current.id) -Force
        return $Definition
    }
    $updated = Invoke-IfsAdoApi -Method PUT -Path "build/definitions/$($current.id)?api-version=$script:ApiVersion" -Body $body
    Add-IfsReportEvent -Target $Definition.Component -Name "pipeline $($Definition.Kind)" -Status 'mis a jour' -Details $Definition.Name
    $Definition | Add-Member -NotePropertyName Id -NotePropertyValue ([int]$updated.id) -Force
    return $Definition
}

function Set-IfsVariableGroup {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([Parameter(Mandatory)] [string] $Name, [Parameter(Mandatory)] [string[]] $ExpectedVariables, [Parameter(Mandatory)] [int] $Revision)
    $response = Invoke-IfsAdoApi -Path "distributedtask/variablegroups?groupName=$([uri]::EscapeDataString($Name))&api-version=$script:ApiVersion"
    $groups = @(Get-IfsAdoValue $response | Where-Object { $_.name -eq $Name } | Select-Object -First 1)
    $description = "$($script:OwnerMarker); ifs-kit-revision: $Revision"
    if ($groups.Count -eq 0) {
        $variables = [ordered]@{}
        foreach ($variable in $ExpectedVariables) { $variables[$variable] = [ordered]@{ value = ''; isSecret = $true } }
        $body = [ordered]@{
            name = $Name
            description = $description
            type = 'Vsts'
            variables = $variables
            variableGroupProjectReferences = @([ordered]@{ name = $Name; description = $description; projectReference = [ordered]@{ id = $script:ProjectId; name = $script:Project } })
        }
        if (-not $PSCmdlet.ShouldProcess($Name, 'creer le groupe de variables Azure DevOps')) {
            return [pscustomobject]@{ Id = 0; Name = $Name; Variables = $variables }
        }
        $created = Invoke-IfsAdoApi -OrganizationScope -Method POST -Path "distributedtask/variablegroups?api-version=$script:ApiVersion" -Body $body
        Add-IfsReportEvent -Target $Name -Name 'groupe de variables' -Status 'cree' -Details ($ExpectedVariables -join ', ')
        return [pscustomobject]@{ Id = [int]$created.id; Name = $Name; Variables = $variables }
    }
    $current = $groups[0]
    $currentDescription = [string](Get-IfsAdoProperty -Object $current -Name 'description')
    if ($currentDescription -notmatch [regex]::Escape($script:OwnerMarker)) { throw "Le groupe '$Name' existe sans marque de propriete IFS." }
    $revisionMatch = [regex]::Match($currentDescription, 'ifs-kit-revision:\s*(\d+)')
    if (-not $revisionMatch.Success) { throw "Revision invalide sur le groupe '$Name'." }
    if ([int]$revisionMatch.Groups[1].Value -gt $Revision) { throw "Le kit est plus ancien que le groupe '$Name'." }

    $variables = [ordered]@{}
    $existingNames = @()
    foreach ($property in $current.variables.PSObject.Properties) {
        $existingNames += $property.Name
        $value = $property.Value
        if ($value.isSecret) {
            $variables[$property.Name] = [ordered]@{ isSecret = $true }
            if ($null -ne $value.value -and [string]$value.value -ne '') { $variables[$property.Name].value = [string]$value.value }
        }
        else {
            $variables[$property.Name] = [ordered]@{ value = [string]$value.value; isSecret = $false }
        }
    }
    $missing = @($ExpectedVariables | Where-Object { $_ -notin $existingNames })
    foreach ($variable in $missing) { $variables[$variable] = [ordered]@{ value = ''; isSecret = $true } }
    $orphans = @($existingNames | Where-Object { $_ -notin $ExpectedVariables })
    if ($missing.Count -gt 0 -or [int]$revisionMatch.Groups[1].Value -lt $Revision) {
        if (-not $PSCmdlet.ShouldProcess($Name, 'mettre a jour le groupe de variables Azure DevOps')) {
            return [pscustomobject]@{ Id = [int]$current.id; Name = $Name; Variables = $variables }
        }
        $current.description = $description
        $current.variables = $variables
        $current.variableGroupProjectReferences = @([ordered]@{ name = $Name; description = $description; projectReference = [ordered]@{ id = $script:ProjectId; name = $script:Project } })
        $null = Invoke-IfsAdoApi -OrganizationScope -Method PUT -Path "distributedtask/variablegroups/$($current.id)?api-version=$script:ApiVersion" -Body $current
        Add-IfsReportEvent -Target $Name -Name 'groupe de variables' -Status 'mis a jour' -Details "Ajoute: $($missing -join ', '); valeurs existantes preservees; orphelines conservees: $($orphans -join ', ')"
    }
    else { Add-IfsReportEvent -Target $Name -Name 'groupe de variables' -Status 'laisse tel quel' -Details "Variables attendues presentes; orphelines conservees: $($orphans -join ', ')" }
    return [pscustomobject]@{ Id = [int]$current.id; Name = $Name; Variables = $variables }
}

function Set-IfsPipelinePermission {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([Parameter(Mandatory)] [string] $ResourceType, [Parameter(Mandatory)] [string] $ResourceId, [Parameter(Mandatory)] [AllowEmptyCollection()] [int[]] $AuthorizedPipelineIds, [Parameter(Mandatory)] [int[]] $ManagedPipelineIds)
    $authorized = [Collections.Generic.HashSet[int]]::new()
    foreach ($id in $AuthorizedPipelineIds) { [void]$authorized.Add($id) }
    $body = [ordered]@{
        allPipelines = [ordered]@{ authorized = $false }
        pipelines = @($ManagedPipelineIds | Sort-Object -Unique | ForEach-Object { [ordered]@{ id = $_; authorized = $authorized.Contains($_) } })
    }
    $permissionPath = 'pipelines/pipelinePermissions/{0}/{1}?api-version={2}' -f $ResourceType, $ResourceId, $script:ChecksApiVersion
    if ($PSCmdlet.ShouldProcess($permissionPath, 'realigner les autorisations de pipeline')) {
        $null = Invoke-IfsAdoApi -Method PATCH -Path $permissionPath -Body $body
    }
}

function Get-IfsManagedPipelineId {
    param([Parameter(Mandatory)] [object[]] $CurrentDefinitions, [Parameter(Mandatory)] [string] $ProjectCode)
    $response = Invoke-IfsAdoApi -Path "build/definitions?api-version=$script:ApiVersion"
    $managed = @(Get-IfsAdoValue $response | Where-Object {
        $definitionPath = [string](Get-IfsAdoProperty -Object $_ -Name 'path')
        $description = [string](Get-IfsAdoProperty -Object $_ -Name 'description')
        $definitionPath.StartsWith("\$ProjectCode", [StringComparison]::OrdinalIgnoreCase) -and $description -match [regex]::Escape($script:OwnerMarker)
    } | ForEach-Object { [int](Get-IfsAdoProperty -Object $_ -Name 'id') })
    $managed += @($CurrentDefinitions | Where-Object {
        $null -ne $_.PSObject.Properties['Id'] -and [int]$_.Id -gt 0
    } | ForEach-Object { [int]$_.Id })
    if ($env:BUILD_DEFINITIONID -match '^\d+$') { $managed += [int]$env:BUILD_DEFINITIONID }
    elseif ($env:BUILD_BUILDID -match '^\d+$') {
        $currentBuild = Invoke-IfsAdoApi -Path "build/builds/$($env:BUILD_BUILDID)?api-version=$script:ApiVersion"
        $currentDefinition = Get-IfsAdoProperty -Object $currentBuild -Name 'definition'
        $currentDefinitionId = Get-IfsAdoProperty -Object $currentDefinition -Name 'id'
        if ($null -ne $currentDefinitionId -and [int]$currentDefinitionId -gt 0) {
            $managed += [int]$currentDefinitionId
        }
    }
    return @($managed | Sort-Object -Unique)
}

function Set-IfsFinalEndpointPermission {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)] [string] $ProjectCode,
        [Parameter(Mandatory)] [object[]] $Targets,
        [Parameter(Mandatory)] [object[]] $Definitions,
        [Parameter(Mandatory)] [object[]] $Endpoints,
        [Parameter(Mandatory)] [int[]] $ManagedPipelineIds
    )

    $failures = [Collections.Generic.List[string]]::new()
    foreach ($target in $Targets) {
        $infraName = "ifs-$ProjectCode-$($target.Name)"
        $appName = "$infraName-app"
        $infraIds = @($Definitions | Where-Object {
            $_.Kind -eq 'RELEASE' -and -not $_.IsApp -and $_.Targets.Name -contains $target.Name -and
            $null -ne $_.PSObject.Properties['Id'] -and [int]$_.Id -gt 0
        } | ForEach-Object { [int]$_.Id })
        $appIds = @($Definitions | Where-Object {
            $_.Kind -eq 'RELEASE' -and $_.IsApp -and $_.Targets.Name -contains $target.Name -and
            $null -ne $_.PSObject.Properties['Id'] -and [int]$_.Id -gt 0
        } | ForEach-Object { [int]$_.Id })
        if ($target.Name -eq 'shared') {
            $appIds += @($Definitions | Where-Object {
                $_.Kind -eq 'CI' -and $_.IsApp -and $_.FilePath -match 'orders[\\/]apps[\\/]api' -and
                $null -ne $_.PSObject.Properties['Id'] -and [int]$_.Id -gt 0
            } | ForEach-Object { [int]$_.Id })
        }

        foreach ($endpointPlan in @(
            [pscustomobject]@{ Name = $infraName; AuthorizedPipelineIds = $infraIds }
            [pscustomobject]@{ Name = $appName; AuthorizedPipelineIds = $appIds }
        )) {
            $endpoint = @($Endpoints | Where-Object { $_.name -eq $endpointPlan.Name } | Select-Object -First 1)
            if ($endpoint.Count -eq 0) {
                $failures.Add("Service connection '$($endpointPlan.Name)' introuvable pour retirer l'autorisation de l'installateur.")
                continue
            }
            if ([string](Get-IfsAdoProperty -Object $endpoint[0] -Name 'description') -notmatch [regex]::Escape($script:OwnerMarker)) {
                $failures.Add("Service connection '$($endpointPlan.Name)' sans marque IFS; autorisation non modifiee.")
                continue
            }
            try {
                if ($PSCmdlet.ShouldProcess($endpointPlan.Name, 'retirer les autorisations temporaires de pipeline')) {
                    Set-IfsPipelinePermission -ResourceType 'endpoint' -ResourceId ([string]$endpoint[0].id) -AuthorizedPipelineIds $endpointPlan.AuthorizedPipelineIds -ManagedPipelineIds $ManagedPipelineIds
                }
            }
            catch { $failures.Add("Service connection '$($endpointPlan.Name)' : $($_.Exception.Message)") }
        }
    }

    if ($failures.Count -gt 0) { throw "Retrait des autorisations temporaires incomplet : $($failures -join '; ')" }
}

function Find-IfsIdentity {
    param([Parameter(Mandatory)] [string] $DisplayName)
    $path = "identities?searchFilter=General&filterValue=$([uri]::EscapeDataString($DisplayName))&queryMembership=None&api-version=7.1-preview.1"
    $uri = "https://vssps.dev.azure.com/$script:Organization/_apis/$path"
    $headers = @{ Authorization = "Bearer $script:Token"; Accept = 'application/json' }
    try { $response = Invoke-RestMethod -Method GET -Uri $uri -Headers $headers -ErrorAction Stop }
    catch { throw "Recherche du groupe approbateur '$DisplayName' impossible : $($_.Exception.Message)" }
    $identityMatches = @(Get-IfsAdoValue $response | Where-Object {
        $identityProviderName = [string](Get-IfsAdoProperty -Object $_ -Name 'providerDisplayName')
        $identityDisplayName = [string](Get-IfsAdoProperty -Object $_ -Name 'displayName')
        $identityProperties = Get-IfsAdoProperty -Object $_ -Name 'properties'
        $identityAccount = Get-IfsAdoProperty -Object (Get-IfsAdoProperty -Object $identityProperties -Name 'Account') -Name '$value'
        $identityProviderName -eq $DisplayName -or $identityDisplayName -eq $DisplayName -or [string]$identityAccount -eq $DisplayName
    } | Select-Object -First 1)
    if ($identityMatches.Count -eq 0) { throw "Groupe Azure DevOps '$DisplayName' introuvable. Creez-le ou corrigez son nom avant de relancer." }
    return $identityMatches[0]
}

function Set-IfsCheck {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)] [object] $Type,
        [Parameter(Mandatory)] [string] $ResourceType,
        [Parameter(Mandatory)] [string] $ResourceId,
        [Parameter(Mandatory)] [string] $ResourceName,
        [Parameter(Mandatory)] [object] $Settings,
        [int] $Timeout = 43200,
        [switch] $ImmutableExisting
    )
    $query = "pipelines/checks/configurations?resourceType=$([uri]::EscapeDataString($ResourceType))&resourceId=$([uri]::EscapeDataString($ResourceId))&`$expand=settings&api-version=$script:ChecksApiVersion"
    $response = Invoke-IfsAdoApi -Path $query
    $existing = @(Get-IfsAdoValue $response | Where-Object { $_.type.id -eq $Type.id } | Select-Object -First 1)
    if ($existing.Count -gt 0) {
        $current = $existing[0]
        if ($ImmutableExisting) {
            Add-IfsReportEvent -Target $ResourceName -Name "controle $($Type.name)" -Status 'laisse tel quel' -Details 'Le verrou existe deja; aucune modification.'
            return $current
        }
        $currentSettings = Get-IfsAdoProperty -Object $current -Name 'settings'
        $currentInstructions = [string](Get-IfsAdoProperty -Object $currentSettings -Name 'instructions')
        $currentDisplayName = [string](Get-IfsAdoProperty -Object $currentSettings -Name 'displayName')
        $hasMarker = $currentInstructions.Contains($script:OwnerMarker) -or $currentDisplayName.Contains($script:OwnerMarker)
        if (-not $hasMarker) {
            $jsonCurrent = ConvertTo-Json -InputObject $currentSettings -Depth 50 -Compress
            $jsonExpected = ConvertTo-Json -InputObject $Settings -Depth 50 -Compress
            if ($jsonCurrent -eq $jsonExpected) {
                Add-IfsReportEvent -Target $ResourceName -Name "controle $($Type.name)" -Status 'deja conforme' -Details 'Les reglages correspondent au modele.'
                return $current
            }
            throw "Le controle '$($Type.name)' sur '$ResourceName' existe sans marque IFS et differe du modele."
        }
        $body = [ordered]@{ id = [int]$current.id; resource = $current.resource; type = [ordered]@{ id = [string]$Type.id }; settings = $Settings; timeout = $Timeout; isDisabled = $false }
        if (-not $PSCmdlet.ShouldProcess($ResourceName, "actualiser le controle Azure DevOps '$($Type.name)'")) { return $current }
        $updated = Invoke-IfsAdoApi -Method PATCH -Path "pipelines/checks/configurations/$($current.id)?api-version=$script:ChecksApiVersion" -Body $body
        Add-IfsReportEvent -Target $ResourceName -Name "controle $($Type.name)" -Status 'mis a jour' -Details 'Reglages IFS realignes.'
        return $updated
    }
    $body = [ordered]@{ resource = [ordered]@{ type = $ResourceType; id = [string]$ResourceId; name = $ResourceName }; type = [ordered]@{ id = [string]$Type.id }; settings = $Settings; timeout = $Timeout }
    if (-not $PSCmdlet.ShouldProcess($ResourceName, "creer le controle Azure DevOps '$($Type.name)'")) { return $null }
    $created = Invoke-IfsAdoApi -Method POST -Path "pipelines/checks/configurations?api-version=$script:ChecksApiVersion" -Body $body
    Add-IfsReportEvent -Target $ResourceName -Name "controle $($Type.name)" -Status 'cree' -Details 'Controle ajoute.'
    return $created
}

function Set-IfsBranchPolicy {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([Parameter(Mandatory)] [object] $Definition, [Parameter(Mandatory)] [string[]] $FilenamePatterns)
    $response = Invoke-IfsAdoApi -Path "policy/configurations?api-version=$script:ApiVersion"
    $policies = @(Get-IfsAdoValue $response | Where-Object {
        $_.type.id -eq '0609b952-1397-4640-95ec-e00a01b2c241' -and $_.settings.buildDefinitionId -eq $Definition.Id -and
        @($_.settings.scope | Where-Object { $_.repositoryId -eq $script:RepositoryId -and $_.refName -eq 'refs/heads/main' }).Count -gt 0
    })
    $displayName = "IFS: $($Definition.Name)"
    if ($policies.Count -gt 0) {
        $current = $policies[0]
        $currentSettings = Get-IfsAdoProperty -Object $current -Name 'settings'
        $currentDisplayName = [string](Get-IfsAdoProperty -Object $currentSettings -Name 'displayName')
        if ($currentDisplayName -ne $displayName) { throw "La politique de validation pour '$($Definition.Name)' existe sans marque IFS." }
        $currentSettings.filenamePatterns = $FilenamePatterns
        $body = [ordered]@{ id = [int]$current.id; isEnabled = $true; isBlocking = $true; type = $current.type; settings = $currentSettings; revision = [int]$current.revision }
        if (-not $PSCmdlet.ShouldProcess($Definition.Name, 'mettre a jour la politique Build Validation')) { return }
        $null = Invoke-IfsAdoApi -Method PUT -Path "policy/configurations/$($current.id)?api-version=$script:ApiVersion" -Body $body
        Add-IfsReportEvent -Target $Definition.Component -Name 'Build Validation main' -Status 'mis a jour' -Details $Definition.Name
        return
    }
    $body = [ordered]@{
        isEnabled = $true
        isBlocking = $true
        type = [ordered]@{ id = '0609b952-1397-4640-95ec-e00a01b2c241' }
        settings = [ordered]@{
            buildDefinitionId = [int]$Definition.Id
            displayName = $displayName
            manualQueueOnly = $false
            queueOnSourceUpdateOnly = $true
            validDuration = 0
            filenamePatterns = $FilenamePatterns
            scope = @([ordered]@{ repositoryId = $script:RepositoryId; refName = 'refs/heads/main'; matchKind = 'Exact' })
        }
    }
    if (-not $PSCmdlet.ShouldProcess($Definition.Name, 'creer la politique Build Validation')) { return }
    $null = Invoke-IfsAdoApi -Method POST -Path "policy/configurations?api-version=$script:ApiVersion" -Body $body
    Add-IfsReportEvent -Target $Definition.Component -Name 'Build Validation main' -Status 'cree' -Details $Definition.Name
}

function Initialize-IfsReport {
    if ($Phase -eq 'Finalize' -and (Test-Path -LiteralPath $script:ReportPath -PathType Leaf)) {
        $script:Report = Get-Content -LiteralPath $script:ReportPath -Raw | ConvertFrom-Json -AsHashtable -Depth 100
        if ($null -eq $script:Report.events) { $script:Report.events = @() }
    }
    else {
        $script:Report = [ordered]@{ schema = 'ifs-install-report/v1'; project = $script:Project; events = @(); readiness = @() }
    }
}

$model = Get-IfsReleaseModel
if ($model.ProjectCode -ne 'shop') { throw "Ce kit de reference P-05 attend le projet 'shop'; le manifeste indique '$($model.ProjectCode)'." }
$repository = Invoke-IfsAdoApi -Path "git/repositories/$($script:RepositoryId)?api-version=$script:ApiVersion"
$script:DefaultBranch = [string]$repository.defaultBranch
if ([string]::IsNullOrWhiteSpace($script:DefaultBranch)) { $script:DefaultBranch = 'refs/heads/main' }
if ($script:DefaultBranch -ne 'refs/heads/main') { throw "La branche par defaut '$($script:DefaultBranch)' ne correspond pas au modele pilote qui protege main." }
$queueResponse = Invoke-IfsAdoApi -Path "distributedtask/queues?api-version=$script:ApiVersion"
$queues = @(Get-IfsAdoValue $queueResponse)
$queue = @($queues | Where-Object { $_.name -match 'Azure Pipelines|Hosted' } | Select-Object -First 1)
if ($queue.Count -eq 0) { $queue = @($queues | Select-Object -First 1) }
if ($queue.Count -eq 0 -and $env:BUILD_BUILDID -match '^\d+$') {
    $currentBuild = Invoke-IfsAdoApi -Path "build/builds/$($env:BUILD_BUILDID)?api-version=$script:ApiVersion"
    $currentQueue = Get-IfsAdoProperty -Object $currentBuild -Name 'queue'
    $currentQueueId = Get-IfsAdoProperty -Object $currentQueue -Name 'id'
    if ($null -eq $currentQueueId -or [int]$currentQueueId -le 0) {
        $timeline = Invoke-IfsAdoApi -Path "build/builds/$($env:BUILD_BUILDID)/timeline?api-version=$script:ApiVersion"
        $records = Get-IfsAdoProperty -Object $timeline -Name 'records'
        $currentJob = @($records | Where-Object {
            [string](Get-IfsAdoProperty -Object $_ -Name 'type') -eq 'Job'
        } | Select-Object -First 1)
        $timelineQueueId = if ($currentJob.Count -gt 0) { Get-IfsAdoProperty -Object $currentJob[0] -Name 'queueId' } else { $null }
        if ($null -ne $timelineQueueId -and [int]$timelineQueueId -gt 0) {
            $queueName = Get-IfsAdoProperty -Object $currentQueue -Name 'name'
            if ([string]::IsNullOrWhiteSpace([string]$queueName)) { $queueName = 'Azure Pipelines' }
            $currentQueue = [ordered]@{ id = [int]$timelineQueueId; name = [string]$queueName }
            $currentQueueId = $timelineQueueId
        }
    }
    if ($null -ne $currentQueue -and $null -ne $currentQueueId -and [int]$currentQueueId -gt 0) {
        $queue = @($currentQueue)
        $currentQueueName = Get-IfsAdoProperty -Object $currentQueue -Name 'name'
        Write-Information -MessageData ("File d'agents derivee du run courant : {0}." -f [string]$currentQueueName) -InformationAction Continue
    }
}
if ($queue.Count -eq 0) { throw 'Aucune file d''agents Azure DevOps disponible dans ce projet.' }
$queue = $queue[0]
$definitions = @(Get-IfsPipelineDefinition -Model $model)
Initialize-IfsReport

if ($Phase -eq 'Prepare') {
    $environments = @{}
    foreach ($target in $model.Targets) { $environments[$target.Name] = Set-IfsEnvironment -Name "$($model.ProjectCode)-$($target.Name)" -Revision $model.Revision }

    $managedDefinitions = foreach ($definition in $definitions) { Set-IfsBuildDefinition -Definition $definition -Queue $queue }
    $definitionByName = @{}
    foreach ($definition in $managedDefinitions) { $definitionByName[$definition.Name] = $definition }

    $secretNamesByTarget = @{}
    foreach ($target in $model.Targets) {
        $secretNames = @($target.Releases | ForEach-Object { $_.Data.secretWrites } | Where-Object { $_.source -eq 'pipeline' } | ForEach-Object { [string]$_.variable } | Sort-Object -Unique)
        $secretNamesByTarget[$target.Name] = $secretNames
        if ($secretNames.Count -gt 0) { $null = Set-IfsVariableGroup -Name "ifs-$($model.ProjectCode)-$($target.Name)" -ExpectedVariables $secretNames -Revision $model.Revision }
    }

    $allManagedIds = @(Get-IfsManagedPipelineId -CurrentDefinitions $managedDefinitions -ProjectCode $model.ProjectCode)
    $installerId = if ($env:BUILD_DEFINITIONID -match '^\d+$') { [int]$env:BUILD_DEFINITIONID } else { 0 }
    foreach ($target in $model.Targets) {
        $infraEndpointName = "ifs-$($model.ProjectCode)-$($target.Name)"
        $appEndpointName = "$infraEndpointName-app"
        $endpointsResponse = Invoke-IfsAdoApi -Path "serviceendpoint/endpoints?api-version=7.1-preview.4"
        $endpointList = @(Get-IfsAdoValue $endpointsResponse)
        $infraEndpoint = @($endpointList | Where-Object { $_.name -eq $infraEndpointName } | Select-Object -First 1)
        $appEndpoint = @($endpointList | Where-Object { $_.name -eq $appEndpointName } | Select-Object -First 1)
        if ($infraEndpoint.Count -eq 0 -or $appEndpoint.Count -eq 0) { throw "Les service connections du target '$($target.Name)' sont absentes. Relancez azure-setup.ps1." }
        foreach ($endpoint in @($infraEndpoint[0], $appEndpoint[0])) {
            if ([string](Get-IfsAdoProperty -Object $endpoint -Name 'description') -notmatch [regex]::Escape($script:OwnerMarker)) { throw "La service connection '$($endpoint.name)' n'est pas geree par IFS." }
        }

        $infraReleaseIds = @($managedDefinitions | Where-Object { $_.Kind -eq 'RELEASE' -and -not $_.IsApp -and $_.Targets.Name -contains $target.Name } | ForEach-Object { [int]$_.Id })
        $appReleaseIds = @($managedDefinitions | Where-Object { $_.Kind -eq 'RELEASE' -and $_.IsApp -and $_.Targets.Name -contains $target.Name } | ForEach-Object { [int]$_.Id })
        if ($target.Name -eq 'shared') {
            $appReleaseIds += @($managedDefinitions | Where-Object { $_.Kind -eq 'CI' -and $_.IsApp -and $_.FilePath -match 'orders[\\/]apps[\\/]api' } | ForEach-Object { [int]$_.Id })
        }
        if ($installerId -gt 0) { $infraAuthorized = @($infraReleaseIds + $installerId); $appAuthorized = @($appReleaseIds + $installerId) }
        else { $infraAuthorized = $infraReleaseIds; $appAuthorized = $appReleaseIds }
        Set-IfsPipelinePermission -ResourceType 'endpoint' -ResourceId ([string]$infraEndpoint[0].id) -AuthorizedPipelineIds $infraAuthorized -ManagedPipelineIds $allManagedIds
        Set-IfsPipelinePermission -ResourceType 'endpoint' -ResourceId ([string]$appEndpoint[0].id) -AuthorizedPipelineIds $appAuthorized -ManagedPipelineIds $allManagedIds
        Add-IfsReportEvent -Target $target.Name -Name 'autorisation temporaire' -Status 'preparee' -Details 'Connexions autorisees aux pipelines de livraison; l''installateur est temporairement autorise pour le test de preparation.'

        $environmentId = [int]$environments[$target.Name].id
        Set-IfsPipelinePermission -ResourceType 'environment' -ResourceId ([string]$environmentId) -AuthorizedPipelineIds (@($infraReleaseIds + $appReleaseIds) | Sort-Object -Unique) -ManagedPipelineIds $allManagedIds
        if ($secretNamesByTarget[$target.Name].Count -gt 0) {
            $groupsResponse = Invoke-IfsAdoApi -Path "distributedtask/variablegroups?groupName=$([uri]::EscapeDataString("ifs-$($model.ProjectCode)-$($target.Name)"))&api-version=$script:ApiVersion"
            $groupItem = @(Get-IfsAdoValue $groupsResponse | Where-Object { $_.name -eq "ifs-$($model.ProjectCode)-$($target.Name)" } | Select-Object -First 1)
            $writers = @($managedDefinitions | Where-Object { $_.Component -eq 'core' -and $_.Kind -eq 'RELEASE' -and -not $_.IsApp } | ForEach-Object { [int]$_.Id })
            Set-IfsPipelinePermission -ResourceType 'variablegroup' -ResourceId ([string]$groupItem[0].id) -AuthorizedPipelineIds $writers -ManagedPipelineIds $allManagedIds
        }

        $appCi = @($managedDefinitions | Where-Object { $_.Kind -eq 'CI' -and $_.IsApp -and $_.FilePath -match 'orders[\\/]apps[\\/]api' } | ForEach-Object { [int]$_.Id })
        if ($target.Name -eq 'shared') {
            $sharedAppEndpoint = $appEndpoint[0]
            Set-IfsPipelinePermission -ResourceType 'endpoint' -ResourceId ([string]$sharedAppEndpoint.id) -AuthorizedPipelineIds (@($appReleaseIds + $appCi + $installerId) | Sort-Object -Unique) -ManagedPipelineIds $allManagedIds
        }
    }

    $script:Report.readiness = @($model.Targets | ForEach-Object { [ordered]@{ target = $_.Name; infraAuthentication = 'a executer par les taches AzureCLI'; emptyDeploymentWhatIf = 'a executer par les taches AzureCLI'; appAuthentication = 'a executer par les taches AzureCLI'; sqlGroupMembership = 'a confirmer dans le rapport azure-setup'; secretVariables = 'existence verifiable, contenu masque par Azure DevOps' } })
    Save-IfsReport
    exit 0
}

$finalizationError = $null
$finalizationEndpointList = @()
$finalizationManagedPipelineIds = @()
try {
    $existingDefinitions = Get-IfsAdoValue (Invoke-IfsAdoApi -Path "build/definitions?api-version=$script:ApiVersion")
    foreach ($definition in $definitions) {
        $existing = @($existingDefinitions | Where-Object { $_.name -eq $definition.Name -and $_.path -eq $definition.Folder } | Select-Object -First 1)
        if ($existing.Count -eq 0) { throw "Pipeline '$($definition.Name)' absent apres la phase Prepare." }
        $definition | Add-Member -NotePropertyName Id -NotePropertyValue ([int]$existing[0].id) -Force
    }

$endpointsResponse = Invoke-IfsAdoApi -Path 'serviceendpoint/endpoints?api-version=7.1-preview.4'
$finalizationEndpointList = @(Get-IfsAdoValue $endpointsResponse)
$finalizationManagedPipelineIds = @(Get-IfsManagedPipelineId -CurrentDefinitions $definitions -ProjectCode $model.ProjectCode)

$approver = Find-IfsIdentity -DisplayName 'Shop Release Approvers'
$approverId = [string](Get-IfsAdoProperty -Object $approver -Name 'id')
$approverDisplayName = [string](Get-IfsAdoProperty -Object $approver -Name 'displayName')
if ([string]::IsNullOrWhiteSpace($approverDisplayName)) {
    $approverProperties = Get-IfsAdoProperty -Object $approver -Name 'properties'
    $approverAccount = Get-IfsAdoProperty -Object $approverProperties -Name 'Account'
    $approverDisplayName = [string](Get-IfsAdoProperty -Object $approverAccount -Name '$value')
}
if ([string]::IsNullOrWhiteSpace($approverDisplayName)) {
    $approverDisplayName = [string](Get-IfsAdoProperty -Object $approver -Name 'providerDisplayName')
}
if ([string]::IsNullOrWhiteSpace($approverId) -or [string]::IsNullOrWhiteSpace($approverDisplayName)) {
    throw 'Le groupe approbateur Azure DevOps ne fournit pas son identifiant ou son nom affichable.'
}
$approvalType = Get-IfsCheckType -Pattern '^(Approval)$'
$lockType = Get-IfsCheckType -Pattern 'Exclusive.?Lock'
$branchType = Get-IfsCheckType -Pattern 'Branch.?Control'
$templateType = Get-IfsCheckType -Pattern 'Required.?Template'
$endpointList = $finalizationEndpointList

foreach ($target in $model.Targets) {
    $environmentName = "$($model.ProjectCode)-$($target.Name)"
    $environmentResponse = Invoke-IfsAdoApi -Path "distributedtask/environments?name=$([uri]::EscapeDataString($environmentName))&api-version=$script:ApiVersion"
    $environment = @(Get-IfsAdoValue $environmentResponse | Where-Object { $_.name -eq $environmentName } | Select-Object -First 1)[0]
    $approvalSettings = [ordered]@{ approvers = @([ordered]@{ id = $approverId; displayName = $approverDisplayName }); executionOrder = 'anyOrder'; minRequiredApprovers = 1; instructions = "$($script:OwnerMarker); ifs-kit-revision: $($model.Revision)"; blockedApprovers = @() }
    if ($target.Protected) { $null = Set-IfsCheck -Type $approvalType -ResourceType 'environment' -ResourceId ([string]$environment.id) -ResourceName $environmentName -Settings $approvalSettings }
    $null = Set-IfsCheck -Type $lockType -ResourceType 'environment' -ResourceId ([string]$environment.id) -ResourceName $environmentName -Settings @{} -ImmutableExisting

    foreach ($suffix in @('', '-app')) {
        $endpointName = "ifs-$($model.ProjectCode)-$($target.Name)$suffix"
        $endpoint = @($endpointList | Where-Object { $_.name -eq $endpointName } | Select-Object -First 1)
        if ($endpoint.Count -eq 0) { throw "Service connection '$endpointName' introuvable en finalisation." }
        if ([string](Get-IfsAdoProperty -Object $endpoint[0] -Name 'description') -notmatch [regex]::Escape($script:OwnerMarker)) { throw "Service connection '$endpointName' sans marque IFS." }
        $branchSettings = [ordered]@{
            displayName = "$($script:OwnerMarker); ifs-kit-revision: $($model.Revision)"
            definitionRef = [ordered]@{ id = '86b05a0c-73e6-4f7d-b3cf-e38f3b39a75b'; name = 'evaluatebranchProtection'; version = '0.0.1' }
            inputs = [ordered]@{ allowedBranches = 'refs/heads/main'; ensureProtectionOfBranch = 'true'; allowUnknownStatusBranch = 'false' }
        }
        $isApplicationEndpoint = -not [string]::IsNullOrWhiteSpace($suffix)
        $templatePath = Get-IfsRequiredTemplatePath -Target $target.Name -Application:$isApplicationEndpoint
        $requiredSettings = [ordered]@{
            displayName = "$($script:OwnerMarker); ifs-kit-revision: $($model.Revision)"
            extendsChecks = @([ordered]@{ repositoryType = 'git'; repositoryName = $script:RepositoryName; repositoryRef = $script:DefaultBranch; templatePath = $templatePath })
        }
        $null = Set-IfsCheck -Type $branchType -ResourceType 'endpoint' -ResourceId ([string]$endpoint[0].id) -ResourceName $endpointName -Settings $branchSettings
        $null = Set-IfsCheck -Type $templateType -ResourceType 'endpoint' -ResourceId ([string]$endpoint[0].id) -ResourceName $endpointName -Settings $requiredSettings
        if ($target.Protected -and $suffix) {
            $null = Set-IfsCheck -Type $approvalType -ResourceType 'endpoint' -ResourceId ([string]$endpoint[0].id) -ResourceName $endpointName -Settings $approvalSettings
        }
    }
}

foreach ($definition in $definitions | Where-Object { $_.Kind -eq 'PR' }) {
    $patterns = @("/$($definition.Component)/infra/**", '/.ifs/templates/**', '/.ifs/pins.json')
    if ($definition.IsApp) { $patterns = @("/$($definition.Component)/apps/$($definition.Application)/**", '/src/api/**', '/.ifs/templates/**', '/.ifs/pins.json') }
    $null = Set-IfsBranchPolicy -Definition $definition -FilenamePatterns $patterns
}

foreach ($target in $model.Targets) {
    $infraDefinitionIds = @($definitions | Where-Object { $_.Kind -eq 'RELEASE' -and -not $_.IsApp -and $_.Targets.Name -contains $target.Name } | ForEach-Object { [int]$_.Id })
    $appDefinitionIds = @($definitions | Where-Object { $_.Kind -eq 'RELEASE' -and $_.IsApp -and $_.Targets.Name -contains $target.Name } | ForEach-Object { [int]$_.Id })
    if ($target.Name -eq 'shared') { $appDefinitionIds += @($definitions | Where-Object { $_.Kind -eq 'CI' -and $_.IsApp -and $_.FilePath -match 'orders[\\/]apps[\\/]api' } | ForEach-Object { [int]$_.Id }) }
    $environmentResponse = Invoke-IfsAdoApi -Path "distributedtask/environments?name=$([uri]::EscapeDataString("$($model.ProjectCode)-$($target.Name)"))&api-version=$script:ApiVersion"
    $environment = @(Get-IfsAdoValue $environmentResponse | Where-Object { $_.name -eq "$($model.ProjectCode)-$($target.Name)" } | Select-Object -First 1)[0]
    $authorized = @($infraDefinitionIds + $appDefinitionIds | Sort-Object -Unique)
    Set-IfsPipelinePermission -ResourceType 'environment' -ResourceId ([string]$environment.id) -AuthorizedPipelineIds $authorized -ManagedPipelineIds $finalizationManagedPipelineIds
}

$script:Report.readiness = @($model.Targets | ForEach-Object {
    $targetSuffix = $_.Name.ToUpperInvariant()
    $infraState = [string][Environment]::GetEnvironmentVariable("IFS_READINESS_$targetSuffix", [EnvironmentVariableTarget]::Process)
    $appState = [string][Environment]::GetEnvironmentVariable("IFS_APP_AUTH_$targetSuffix", [EnvironmentVariableTarget]::Process)
    [ordered]@{
        target = $_.Name
        infraAuthentication = $(if ($infraState) { $infraState } else { 'non verifie' })
        emptyDeploymentWhatIf = $(if ($infraState -eq 'Succeeded') { 'reussi' } else { 'echec ou non execute' })
        appAuthentication = $(if ($appState) { $appState } else { 'non verifie' })
        sqlGroupMembership = 'a confirmer dans le rapport azure-setup ou par un administrateur Entra'
        secretVariables = 'noms presents; valeurs masquées par Azure DevOps; verification manuelle requise'
    }
})
}
catch {
    $finalizationError = $_
    throw
}
finally {
    $cleanupError = $null
    try {
        if (@($finalizationEndpointList).Count -eq 0) {
            $endpointsResponse = Invoke-IfsAdoApi -Path 'serviceendpoint/endpoints?api-version=7.1-preview.4'
            $finalizationEndpointList = @(Get-IfsAdoValue $endpointsResponse)
        }
        if (@($finalizationManagedPipelineIds).Count -eq 0) {
            $finalizationManagedPipelineIds = @(Get-IfsManagedPipelineId -CurrentDefinitions $definitions -ProjectCode $model.ProjectCode)
        }
        Set-IfsFinalEndpointPermission -ProjectCode $model.ProjectCode -Targets $model.Targets -Definitions $definitions -Endpoints $finalizationEndpointList -ManagedPipelineIds $finalizationManagedPipelineIds
        foreach ($target in $model.Targets) {
            Add-IfsReportEvent -Target $target.Name -Name 'autorisation temporaire installateur' -Status 'retiree' -Details 'Seuls les pipelines de livraison references sont autorises.'
        }
        Save-IfsReport
    }
    catch { $cleanupError = $_ }

    if ($null -ne $cleanupError) {
        if ($null -ne $finalizationError) {
            throw "La finalisation a echoue : $($finalizationError.Exception.Message). Le retrait des autorisations temporaires a aussi echoue : $($cleanupError.Exception.Message)"
        }
        throw "Le retrait des autorisations temporaires a echoue : $($cleanupError.Exception.Message)"
    }
}
