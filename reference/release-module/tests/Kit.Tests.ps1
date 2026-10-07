BeforeAll {
    $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
    $script:modulePath = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/.ifs/install/scripts/IfsInstall.psm1'
    Import-Module $script:modulePath -Force

    function global:az {
        param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
        $call = @($Arguments)
        [void]$global:IFS_AZ_CALLS.Add($call)
        if ($global:IFS_AZ_HANDLER) { return & $global:IFS_AZ_HANDLER $call }
        $global:LASTEXITCODE = 0
        return '{}'
    }
}

Describe 'Kit installation P-05' {
    BeforeAll {
        function New-IfsResourceGroupFixture {
            $root = Join-Path ([IO.Path]::GetTempPath()) ('ifs-resource-groups-' + [guid]::NewGuid().ToString('N'))
            $definitions = @(
                [pscustomobject]@{ Component = 'core'; Target = 'dev'; SubscriptionId = 'sub-dev'; Name = 'rg-shop-core-main-dev' }
                [pscustomobject]@{ Component = 'orders'; Target = 'prd'; SubscriptionId = 'sub-prd'; Name = 'rg-shop-orders-main-prd' }
                [pscustomobject]@{ Component = 'data'; Target = 'dev'; SubscriptionId = 'sub-dev'; Name = 'rg-shop-data-main-dev' }
            )
            $releases = [Collections.Generic.List[object]]::new()
            foreach ($definition in $definitions) {
                $infraPath = Join-Path (Join-Path $root $definition.Component) 'infra'
                $null = New-Item -ItemType Directory -Path $infraPath -Force
                $parameterPath = Join-Path $infraPath ('main.{0}.bicepparam' -f $definition.Target)
                [IO.File]::WriteAllText($parameterPath, '// test fixture', [Text.UTF8Encoding]::new($false))
                $tags = [ordered]@{
                    costCenter = 'ecommerce'
                    'ifs-project' = 'shop'
                    'ifs-component' = $definition.Component
                    'ifs-environment' = $definition.Target
                    'managed-by' = 'infraflowsculptor'
                }
                $parameterValues = [pscustomobject]@{
                    parameters = [pscustomobject]@{
                        resourceGroups = [pscustomobject]@{
                            value = [pscustomobject]@{
                                main = [pscustomobject]@{
                                    name = $definition.Name
                                    location = 'northeurope'
                                    tags = $tags
                                }
                            }
                        }
                    }
                }
                $global:IFS_BICEP_PARAMETERS[$parameterPath] = ConvertTo-Json -InputObject $parameterValues -Depth 20 -Compress
                $definition | Add-Member -NotePropertyName Location -NotePropertyValue 'northeurope' -Force
                $definition | Add-Member -NotePropertyName Tags -NotePropertyValue $tags -Force
                $release = [pscustomobject]@{
                    Component = $definition.Component
                    Target = $definition.Target
                    SubscriptionId = $definition.SubscriptionId
                    Path = Join-Path $infraPath 'main.bicep'
                }
                $releases.Add($release)
            }
            return [pscustomobject]@{ Root = $root; Definitions = $definitions; Releases = $releases.ToArray() }
        }

        function Set-IfsResourceGroupAzHandler {
            $global:IFS_AZ_HANDLER = {
                param($Arguments)
                $global:LASTEXITCODE = 0
                if ($Arguments[0] -eq 'bicep' -and $Arguments[1] -eq 'build-params') {
                    $fileIndex = [Array]::IndexOf($Arguments, '--file')
                    $parameterPath = [string]$Arguments[$fileIndex + 1]
                    $parameterJson = [string]$global:IFS_BICEP_PARAMETERS[$parameterPath]
                    return (@{ parametersJson = $parameterJson; templateJson = '{}' } | ConvertTo-Json -Compress)
                }
                if ($Arguments[0] -eq 'group' -and $Arguments[1] -eq 'list') {
                    $subscriptionIndex = [Array]::IndexOf($Arguments, '--subscription')
                    $subscriptionId = [string]$Arguments[$subscriptionIndex + 1]
                    $groups = @()
                    if ($global:IFS_RESOURCE_GROUPS.ContainsKey($subscriptionId)) { $groups = @($global:IFS_RESOURCE_GROUPS[$subscriptionId]) }
                    return ConvertTo-Json -InputObject $groups -Depth 20 -Compress
                }
                if ($Arguments[0] -eq 'group' -and $Arguments[1] -eq 'create') {
                    $valueFor = {
                        param([string] $Name)
                        $index = [Array]::IndexOf($Arguments, $Name)
                        if ($index -ge 0 -and $index + 1 -lt $Arguments.Count) { return [string]$Arguments[$index + 1] }
                        return ''
                    }
                    $tags = [ordered]@{}
                    $tagsIndex = [Array]::IndexOf($Arguments, '--tags')
                    for ($index = $tagsIndex + 1; $index -lt $Arguments.Count; $index++) {
                        $tagMatch = [regex]::Match([string]$Arguments[$index], '^(?<name>[^=]+)=(?<value>.*)$')
                        if (-not $tagMatch.Success) { break }
                        $tags[$tagMatch.Groups['name'].Value] = $tagMatch.Groups['value'].Value
                    }
                    $subscriptionId = & $valueFor '--subscription'
                    $created = [pscustomobject]@{
                        name = & $valueFor '--name'
                        location = & $valueFor '--location'
                        tags = $tags
                    }
                    $existing = @()
                    if ($global:IFS_RESOURCE_GROUPS.ContainsKey($subscriptionId)) { $existing = @($global:IFS_RESOURCE_GROUPS[$subscriptionId]) }
                    $global:IFS_RESOURCE_GROUPS[$subscriptionId] = @($existing) + $created
                    return '{}'
                }
                return '{}'
            }
        }

        function Get-IfsAzArgumentValue {
            param([string[]] $Arguments, [string] $Name)
            $index = [Array]::IndexOf($Arguments, $Name)
            if ($index -ge 0 -and $index + 1 -lt $Arguments.Count) { return [string]$Arguments[$index + 1] }
            return ''
        }
    }

    BeforeEach {
        $global:IFS_AZ_CALLS = [Collections.Generic.List[object]]::new()
        $global:IFS_AZ_HANDLER = $null
        $global:IFS_ROLE_ASSIGNMENTS = [Collections.Generic.List[object]]::new()
        $global:IFS_BICEP_PARAMETERS = @{}
        $global:IFS_RESOURCE_GROUPS = @{}
    }

    AfterAll {
        Remove-Item Function:\global:az -ErrorAction SilentlyContinue
        Remove-Variable -Name IFS_AZ_CALLS, IFS_AZ_HANDLER, IFS_ROLE_ASSIGNMENTS, IFS_BICEP_PARAMETERS, IFS_RESOURCE_GROUPS -Scope Global -ErrorAction SilentlyContinue
    }

    It 'ne lance aucune commande az decriture avec WhatIf' {
        InModuleScope IfsInstall {
            $null = Invoke-IfsAz -Arguments @('group', 'create', '--name', 'rg-test') -Write -WhatIf
        }

        $global:IFS_AZ_CALLS.Count | Should -Be 0
    }

    It 'ne cree pas de nouvelle attribution RBAC a la seconde reconciliation' {
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            if ($Arguments.Count -ge 3 -and $Arguments[0] -eq 'role' -and $Arguments[1] -eq 'assignment' -and $Arguments[2] -eq 'list') {
                return ConvertTo-Json -InputObject @($global:IFS_ROLE_ASSIGNMENTS) -Depth 20 -Compress
            }
            if ($Arguments.Count -ge 3 -and $Arguments[0] -eq 'role' -and $Arguments[1] -eq 'assignment' -and $Arguments[2] -eq 'create') {
                $getValue = {
                    param([string] $Name)
                    $index = [Array]::IndexOf($Arguments, $Name)
                    if ($index -ge 0 -and $index + 1 -lt $Arguments.Count) { return [string]$Arguments[$index + 1] }
                    return ''
                }
                $roleId = & $getValue '--role'
                $assignment = [pscustomobject]@{
                    name = & $getValue '--name'
                    id = "/subscriptions/test/providers/Microsoft.Authorization/roleAssignments/$(& $getValue '--name')"
                    principalId = & $getValue '--assignee-object-id'
                    scope = & $getValue '--scope'
                    roleDefinitionId = "/providers/Microsoft.Authorization/roleDefinitions/$roleId"
                    description = & $getValue '--description'
                    condition = $null
                    conditionVersion = $null
                }
                $global:IFS_ROLE_ASSIGNMENTS.Add($assignment)
                return '{}'
            }
            return '{}'
        }

        InModuleScope IfsInstall {
            $script:IfsRevision = 9
            $arguments = @{
                SubscriptionId = '00000000-0000-0000-0000-000000000001'
                Scope = '/subscriptions/00000000-0000-0000-0000-000000000001'
                PrincipalId = '00000000-0000-0000-0000-000000000002'
                RoleDefinitionId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
            }
            Set-IfsRoleAssignment @arguments | Should -Be 'cree'
            Set-IfsRoleAssignment @arguments | Should -Be 'laisse tel quel'
        }

        @($global:IFS_AZ_CALLS | Where-Object { $_[0] -eq 'role' -and $_[1] -eq 'assignment' -and $_[2] -in @('create', 'update') }).Count | Should -Be 1
    }

    It 'met a jour une attribution RBAC geree avec le document JSON role-assignment' {
        $scope = '/subscriptions/00000000-0000-0000-0000-000000000001'
        $principalId = '00000000-0000-0000-0000-000000000002'
        $roleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
        $condition = "@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] GuidEquals {$roleId}"
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            if ($Arguments[0..2] -join ' ' -eq 'role assignment list') {
                return ConvertTo-Json -InputObject @($global:IFS_ROLE_ASSIGNMENTS) -Depth 20 -Compress
            }
            return '{}'
        }

        InModuleScope IfsInstall -Parameters @{ ScopeValue = $scope; PrincipalValue = $principalId; RoleValue = $roleId; ConditionValue = $condition } {
            $script:IfsRevision = 9
            $assignmentId = Get-IfsStableGuid -Value "$ScopeValue|$PrincipalValue|$RoleValue"
            $global:IFS_ROLE_ASSIGNMENTS.Add([pscustomobject]@{
                name = $assignmentId
                id = "/subscriptions/00000000-0000-0000-0000-000000000001/providers/Microsoft.Authorization/roleAssignments/$assignmentId"
                type = 'Microsoft.Authorization/roleAssignments'
                scope = $ScopeValue
                principalId = $PrincipalValue
                principalType = 'ServicePrincipal'
                roleDefinitionId = "/providers/Microsoft.Authorization/roleDefinitions/$RoleValue"
                description = 'managed-by: infraflowsculptor; ifs-kit-revision: 8'
                condition = 'old-condition'
                conditionVersion = '2.0'
            })

            Set-IfsRoleAssignment -SubscriptionId '00000000-0000-0000-0000-000000000001' -Scope $ScopeValue -PrincipalId $PrincipalValue -RoleDefinitionId $RoleValue -Condition $ConditionValue | Should -Be 'mis a jour'
        }

        $update = @($global:IFS_AZ_CALLS | Where-Object { $_[0] -eq 'role' -and $_[1] -eq 'assignment' -and $_[2] -eq 'update' })
        $update.Count | Should -Be 1
        $update[0] | Should -Contain '--role-assignment'
        $update[0] | Should -Not -Contain '--ids'
        $jsonIndex = [Array]::IndexOf([string[]]$update[0], '--role-assignment') + 1
        $updatedAssignment = [string]$update[0][$jsonIndex] | ConvertFrom-Json
        $updatedAssignment.id | Should -Match '/roleAssignments/'
        $updatedAssignment.description | Should -Be 'managed-by: infraflowsculptor; ifs-kit-revision: 9'
        $updatedAssignment.condition | Should -Be $condition
        $updatedAssignment.conditionVersion | Should -Be '2.0'
        $updatedAssignment.scope | Should -Be $scope
        $updatedAssignment.principalId | Should -Be $principalId
        $updatedAssignment.roleDefinitionId | Should -Match ([regex]::Escape($roleId))
    }

    It 'supprime uniquement les FIC IFS obsoletes et conserve les identifiants etrangers' {
        $global:IFS_AZ_HANDLER = { param($Arguments) $global:LASTEXITCODE = 0; return '{}' }
        $existing = @(
            [pscustomobject]@{ name = 'ifs-ado-current' }
            [pscustomobject]@{ name = 'ifs-ado-old' }
            [pscustomobject]@{ name = 'github-actions'; subject = 'repo:other/project:environment:production' }
        )

        InModuleScope IfsInstall -Parameters @{ ExistingCredentials = $existing } {
            Remove-IfsStaleFederatedCredential -SubscriptionId 'sub' -ResourceGroup 'rg' -IdentityName 'id' -Existing $ExistingCredentials -ExpectedNames @('ifs-ado-current')
        }

        $deletes = @($global:IFS_AZ_CALLS | Where-Object { $_[0] -eq 'identity' -and $_[1] -eq 'federated-credential' -and $_[2] -eq 'delete' })
        $deletes.Count | Should -Be 1
        $deletes[0] | Should -Contain 'ifs-ado-old'
        $deletes[0] | Should -Not -Contain 'github-actions'
    }

    It 'ne remplace pas un FIC IFS deja conforme a la seconde reconciliation' {
        $endpointId = '11111111-2222-3333-4444-555555555555'
        $credentialName = 'ifs-ado-111111112222'
        $issuer = 'https://login.microsoftonline.com/example-tenant/v2.0'
        $subject = 'sc://example/shop/ifs-shop-dev'
        $global:IFS_FEDERATED_CREDENTIALS = @(
            [pscustomobject]@{ name = $credentialName; issuer = $issuer; subject = $subject }
        )
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            if (($Arguments -join ' ') -match 'identity federated-credential list') {
                return ConvertTo-Json -InputObject $global:IFS_FEDERATED_CREDENTIALS -Depth 10 -Compress
            }
            return '{}'
        }
        $endpoint = [pscustomobject]@{
            id = $endpointId
            name = 'ifs-shop-dev'
            authorization = [pscustomobject]@{ parameters = [pscustomobject]@{
                workloadIdentityFederationIssuer = $issuer
                workloadIdentityFederationSubject = $subject
            } }
        }

        InModuleScope IfsInstall -Parameters @{ EndpointValue = $endpoint } {
            $expectedNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            Set-IfsFederatedCredential -SubscriptionId 'sub' -ResourceGroup 'rg' -IdentityName 'id-ifs-deploy-shop-dev' -Endpoint $EndpointValue -ExpectedNames $expectedNames | Should -Be 'laisse tel quel'
            $expectedNames.Contains('ifs-ado-111111112222') | Should -BeTrue
        }

        @($global:IFS_AZ_CALLS | Where-Object { $_[0] -eq 'identity' -and $_[1] -eq 'federated-credential' -and $_[2] -in @('create', 'delete') }).Count | Should -Be 0
    }

    It 'refuse un kit plus ancien que la revision installee' {
        { Assert-IfsRevisionNotOlder -CurrentRevision 1 -ExistingRevision 2 -ObjectName 'rg-test' } | Should -Throw
    }

    It 'limite exactement la condition RBAC aux roles du pilote' {
        $roles = @(
            '7f951dda-4ed3-4680-a7ca-43fe172d538d'
            '4633458b-17de-408a-b874-0445c86b69e6'
            '73c42c96-874c-492b-b04d-ab87d138a893'
        )
        $set = '{4633458b-17de-408a-b874-0445c86b69e6,73c42c96-874c-492b-b04d-ab87d138a893,7f951dda-4ed3-4680-a7ca-43fe172d538d}'
        $expected = "((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'}) AND !(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals $set)) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals $set))"

        (Get-IfsRbacCondition -RoleDefinitionIds $roles) | Should -BeExactly $expected
    }

    It 'limite la délégation RBAC aux groupes de ressources utilisés par chaque composant' {
        $roleIds = @{
            KeyVaultSecretsOfficer = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
            KeyVaultSecretsUser = '4633458b-17de-408a-b874-0445c86b69e6'
            LogAnalyticsReader = '73c42c96-874c-492b-b04d-ab87d138a893'
            AcrPull = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
            AcrPush = '8311e382-0749-4cb8-b61a-304f252e45ec'
            ContainerAppsContributor = '358470bc-b998-42bd-ab17-a7e34c199c0f'
        }
        $target = [pscustomobject]@{
            Name = 'dev'
            Releases = @(
                [pscustomobject]@{ Component = 'core'; SubscriptionId = 'sub-dev'; ResourceGroupName = 'rg-core'; Release = [pscustomobject]@{ dependencies = @() } }
                [pscustomobject]@{
                    Component = 'orders'; SubscriptionId = 'sub-dev'; ResourceGroupName = 'rg-orders'
                    Release = [pscustomobject]@{ dependencies = @(
                        [pscustomobject]@{ resources = @(
                            [pscustomobject]@{ id = '/subscriptions/sub-dev/resourceGroups/rg-core/providers/Microsoft.KeyVault/vaults/kv' }
                            [pscustomobject]@{ id = '/subscriptions/sub-dev/resourceGroups/rg-core/providers/Microsoft.OperationalInsights/workspaces/logs' }
                            [pscustomobject]@{ id = '/subscriptions/sub-shared/resourceGroups/rg-platform/providers/Microsoft.ContainerRegistry/registries/acr' }
                        ) }
                    ) }
                }
                [pscustomobject]@{ Component = 'data'; SubscriptionId = 'sub-dev'; ResourceGroupName = 'rg-data'; Release = [pscustomobject]@{ dependencies = @() } }
            )
        }

        $scopes = Get-IfsRbacScopePlan -Target $target -RoleIds $roleIds

        @($scopes | Where-Object Scope -eq '/subscriptions/sub-dev/resourceGroups/rg-core').RoleDefinitionIds | Should -Contain $roleIds.KeyVaultSecretsOfficer
        @($scopes | Where-Object Scope -eq '/subscriptions/sub-dev/resourceGroups/rg-core').RoleDefinitionIds | Should -Contain $roleIds.KeyVaultSecretsUser
        @($scopes | Where-Object Scope -eq '/subscriptions/sub-dev/resourceGroups/rg-core').RoleDefinitionIds | Should -Contain $roleIds.LogAnalyticsReader
        @($scopes | Where-Object Scope -eq '/subscriptions/sub-dev/resourceGroups/rg-orders').RoleDefinitionIds | Should -Contain $roleIds.ContainerAppsContributor
        @($scopes | Where-Object Scope -eq '/subscriptions/sub-shared/resourceGroups/rg-platform').RoleDefinitionIds | Should -Contain $roleIds.AcrPull
        $scopes.Scope | Should -Not -Contain '/subscriptions/sub-dev'
        $scopes.Scope | Should -Not -Contain '/subscriptions/sub-dev/resourceGroups/rg-data'
    }

    It 'cree seulement les groupes utilises comme portees RBAC avec le nom, la region et les tags Bicep' {
        $fixture = New-IfsResourceGroupFixture
        try {
            Set-IfsResourceGroupAzHandler
            foreach ($release in $fixture.Releases) {
                $componentGroup = Get-IfsBicepResourceGroup -Release $release
                $release | Add-Member -NotePropertyName ResourceGroupName -NotePropertyValue $componentGroup.Name -Force
                $release | Add-Member -NotePropertyName ResourceGroupLocation -NotePropertyValue $componentGroup.Location -Force
                $release | Add-Member -NotePropertyName ResourceGroupTags -NotePropertyValue $componentGroup.Tags -Force
            }
            $requiredScopes = @(
                '/subscriptions/sub-dev/resourceGroups/rg-shop-core-main-dev'
                '/subscriptions/sub-prd/resourceGroups/rg-shop-orders-main-prd'
            )

            InModuleScope IfsInstall -Parameters @{ ReleaseValues = $fixture.Releases; ScopeValues = $requiredScopes } {
                Initialize-IfsComponentResourceGroup -Releases $ReleaseValues -RequiredScopes $ScopeValues
            }

            $creates = @($global:IFS_AZ_CALLS | Where-Object { $_[0] -eq 'group' -and $_[1] -eq 'create' })
            $createdNames = @($creates | ForEach-Object { Get-IfsAzArgumentValue -Arguments $_ -Name '--name' } | Sort-Object)
            ($createdNames -join ',') | Should -Be 'rg-shop-core-main-dev,rg-shop-orders-main-prd'
            $createdNames | Should -Not -Contain 'rg-shop-data-main-dev'

            foreach ($definition in @($fixture.Definitions | Where-Object Component -in @('core', 'orders'))) {
                $create = @($creates | Where-Object { (Get-IfsAzArgumentValue -Arguments $_ -Name '--name') -eq $definition.Name })
                $create.Count | Should -Be 1
                Get-IfsAzArgumentValue -Arguments $create[0] -Name '--location' | Should -Be 'northeurope'
                Get-IfsAzArgumentValue -Arguments $create[0] -Name '--subscription' | Should -Be $definition.SubscriptionId
                $tagIndex = [Array]::IndexOf([string[]]$create[0], '--tags')
                $actualTags = @($create[0][($tagIndex + 1)..($create[0].Count - 1)] | Sort-Object)
                $expectedTags = @($definition.Tags.GetEnumerator() | ForEach-Object { '{0}={1}' -f $_.Key, $_.Value } | Sort-Object)
                ($actualTags -join '|') | Should -Be ($expectedTags -join '|')
            }
        }
        finally {
            if (Test-Path -LiteralPath $fixture.Root) { Remove-Item -LiteralPath $fixture.Root -Recurse -Force }
        }
    }

    It 'ne recree pas les groupes conformes a la seconde initialisation' {
        $fixture = New-IfsResourceGroupFixture
        try {
            Set-IfsResourceGroupAzHandler
            foreach ($release in $fixture.Releases) {
                $componentGroup = Get-IfsBicepResourceGroup -Release $release
                $release | Add-Member -NotePropertyName ResourceGroupName -NotePropertyValue $componentGroup.Name -Force
                $release | Add-Member -NotePropertyName ResourceGroupLocation -NotePropertyValue $componentGroup.Location -Force
                $release | Add-Member -NotePropertyName ResourceGroupTags -NotePropertyValue $componentGroup.Tags -Force
            }
            $requiredScopes = @(
                '/subscriptions/sub-dev/resourceGroups/rg-shop-core-main-dev'
                '/subscriptions/sub-prd/resourceGroups/rg-shop-orders-main-prd'
            )
            $parameters = @{ ReleaseValues = $fixture.Releases; ScopeValues = $requiredScopes }

            InModuleScope IfsInstall -Parameters $parameters {
                Initialize-IfsComponentResourceGroup -Releases $ReleaseValues -RequiredScopes $ScopeValues
            }
            $firstRunCreates = @($global:IFS_AZ_CALLS | Where-Object { $_[0] -eq 'group' -and $_[1] -eq 'create' }).Count
            InModuleScope IfsInstall -Parameters $parameters {
                Initialize-IfsComponentResourceGroup -Releases $ReleaseValues -RequiredScopes $ScopeValues
            }
            $secondRunCreates = @($global:IFS_AZ_CALLS | Where-Object { $_[0] -eq 'group' -and $_[1] -eq 'create' }).Count

            $firstRunCreates | Should -Be 2
            $secondRunCreates | Should -Be $firstRunCreates
        }
        finally {
            if (Test-Path -LiteralPath $fixture.Root) { Remove-Item -LiteralPath $fixture.Root -Recurse -Force }
        }
    }

    It 'refuse un groupe existant dont la region ou les tags de propriete et de contexte sont incorrects sans mutation' {
        $tagCases = @(
            @{ TagName = 'location'; Mode = 'wrong' }
            @{ TagName = 'managed-by'; Mode = 'missing' }
            @{ TagName = 'managed-by'; Mode = 'wrong' }
            @{ TagName = 'ifs-project'; Mode = 'missing' }
            @{ TagName = 'ifs-project'; Mode = 'wrong' }
            @{ TagName = 'ifs-component'; Mode = 'missing' }
            @{ TagName = 'ifs-component'; Mode = 'wrong' }
            @{ TagName = 'ifs-environment'; Mode = 'missing' }
            @{ TagName = 'ifs-environment'; Mode = 'wrong' }
        )
        foreach ($tagCase in $tagCases) {
            $fixture = New-IfsResourceGroupFixture
            try {
                Set-IfsResourceGroupAzHandler
                foreach ($release in $fixture.Releases) {
                    $componentGroup = Get-IfsBicepResourceGroup -Release $release
                    $release | Add-Member -NotePropertyName ResourceGroupName -NotePropertyValue $componentGroup.Name -Force
                    $release | Add-Member -NotePropertyName ResourceGroupLocation -NotePropertyValue $componentGroup.Location -Force
                    $release | Add-Member -NotePropertyName ResourceGroupTags -NotePropertyValue $componentGroup.Tags -Force
                }
                $tags = [ordered]@{
                    'ifs-project' = 'shop'
                    'ifs-component' = 'orders'
                    'ifs-environment' = 'prd'
                    'managed-by' = 'infraflowsculptor'
                }
                if ($tagCase.TagName -ne 'location') {
                    if ($tagCase.Mode -eq 'missing') { $tags.Remove($tagCase.TagName) }
                    else { $tags[$tagCase.TagName] = 'unexpected' }
                }
                $global:IFS_RESOURCE_GROUPS['sub-prd'] = @([pscustomobject]@{
                    name = 'rg-shop-orders-main-prd'
                    location = $(if ($tagCase.TagName -eq 'location') { 'francecentral' } else { 'northeurope' })
                    tags = $tags
                })
                $requiredScopes = @(
                    '/subscriptions/sub-dev/resourceGroups/rg-shop-core-main-dev'
                    '/subscriptions/sub-prd/resourceGroups/rg-shop-orders-main-prd'
                )

                {
                    InModuleScope IfsInstall -Parameters @{ ReleaseValues = $fixture.Releases; ScopeValues = $requiredScopes } {
                        Initialize-IfsComponentResourceGroup -Releases $ReleaseValues -RequiredScopes $ScopeValues
                    }
                } | Should -Throw

                @($global:IFS_AZ_CALLS | Where-Object { $_[0] -eq 'group' -and $_[1] -eq 'create' }).Count | Should -Be 0
            }
            finally {
                if (Test-Path -LiteralPath $fixture.Root) { Remove-Item -LiteralPath $fixture.Root -Recurse -Force }
            }
        }
    }

    It "attribue Key Vault Secrets Officer à l'identité de déploiement dans core" {
        $source = Get-Content -LiteralPath (Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/core/infra/main.bicep') -Raw
        $roleModule = Get-Content -LiteralPath (Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/core/infra/key-vault-role-assignment.bicep') -Raw

        $source | Should -Match 'param deploymentPrincipalId string'
        $source | Should -Match "module deploymentSecretsOfficer './key-vault-role-assignment.bicep'"
        $roleModule | Should -Match 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
        $roleModule | Should -Match 'scope: keyVault'
        $roleModule | Should -Match 'principalId: principalId'
    }

    It 'attribue les rôles de livraison au registre et à la Container App' {
        $platform = Get-Content -LiteralPath (Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/platform/infra/main.bicep') -Raw
        $registryRoleModule = Get-Content -LiteralPath (Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/platform/infra/registry-role-assignment.bicep') -Raw
        $orders = Get-Content -LiteralPath (Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/orders/infra/main.bicep') -Raw
        $containerAppRoleModule = Get-Content -LiteralPath (Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/orders/infra/container-app-role-assignment.bicep') -Raw

        $platform | Should -Match 'param appDeliveryPrincipalId string'
        $platform | Should -Match "module appDeliveryAcrPush './registry-role-assignment.bicep'"
        $registryRoleModule | Should -Match '8311e382-0749-4cb8-b61a-304f252e45ec'
        $registryRoleModule | Should -Match 'scope: registry'
        $registryRoleModule | Should -Match 'principalId: principalId'
        $orders | Should -Match 'param appDeliveryPrincipalId string'
        $orders | Should -Match "module appDeliveryContainerAppsContributor './container-app-role-assignment.bicep'"
        $containerAppRoleModule | Should -Match '358470bc-b998-42bd-ab17-a7e34c199c0f'
        $containerAppRoleModule | Should -Match 'scope: ordersApi'
        $containerAppRoleModule | Should -Match 'principalId: principalId'
    }

    It 'associe chaque service connection au modèle exigé par son pipeline' {
        $ciTemplate = Get-IfsRequiredTemplatePath -Target 'shared' -Application
        $releaseTemplate = Get-IfsRequiredTemplatePath -Target 'dev' -Application
        $ciPipeline = Get-Content -LiteralPath (Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/orders/apps/api/pipelines/ci.yml') -Raw
        $releasePipeline = Get-Content -LiteralPath (Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/orders/apps/api/pipelines/release.yml') -Raw

        $ciTemplate | Should -Be '.ifs/templates/app-ci.yml'
        $releaseTemplate | Should -Be '.ifs/templates/app-release.yml'
        $ciPipeline | Should -Match ("template:\s*" + [regex]::Escape($ciTemplate))
        $releasePipeline | Should -Match ("template:\s*" + [regex]::Escape($releaseTemplate))
        $ciPipeline | Should -Match 'registryServiceConnection: ifs-shop-shared-app'
    }

    It 'active la règle SQL Azure Services seulement si le réseau public est activé' {
        $main = Get-Content -LiteralPath (Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/data/infra/main.bicep') -Raw
        $types = Get-Content -LiteralPath (Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/data/infra/types.bicep') -Raw
        $dev = Get-Content -LiteralPath (Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/data/infra/main.dev.bicepparam') -Raw
        $prd = Get-Content -LiteralPath (Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/data/infra/main.prd.bicepparam') -Raw

        $main | Should -Match "var sqlOrdersFirewallRules = sqlOrders\.publicNetworkAccess == 'Enabled'\s*\?\s*\["
        $main | Should -Match "(?s)name: 'AllowAzureServices'.*startIpAddress: '0\.0\.0\.0'.*endIpAddress: '0\.0\.0\.0'.*\]\s*:\s*\[\]"
        $main | Should -Match 'firewallRules: sqlOrdersFirewallRules'
        $types | Should -Match "publicNetworkAccess: 'Enabled' \| 'Disabled'"
        $dev | Should -Match "publicNetworkAccess: 'Enabled'"
        $prd | Should -Match "publicNetworkAccess: 'Enabled'"
    }

    It 'ne fait aucune mutation Azure pendant le WhatIf du kit complet' {
        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-p05-whatif-' + [guid]::NewGuid().ToString('N'))
        try {
            $sourceRoot = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo'
            $releases = @(Get-ChildItem -LiteralPath $sourceRoot -Filter 'release.*.json' -File -Recurse)
            $subscriptionByTarget = @{
                dev = '00000000-0000-0000-0000-000000000001'
                prd = '00000000-0000-0000-0000-000000000002'
                shared = '00000000-0000-0000-0000-000000000003'
            }
            foreach ($release in $releases) {
                $relativePath = [IO.Path]::GetRelativePath($sourceRoot, $release.FullName)
                $destination = Join-Path $tempRoot $relativePath
                $null = New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force
                $content = Get-Content -LiteralPath $release.FullName -Raw
                $content = $content.Replace('<A>', $subscriptionByTarget.dev).Replace('<B>', $subscriptionByTarget.prd).Replace('<C>', $subscriptionByTarget.shared)
                [IO.File]::WriteAllText($destination, $content, [Text.UTF8Encoding]::new($false))
            }
            $parameterFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Filter 'main.*.bicepparam' -File -Recurse)
            foreach ($parameterFile in $parameterFiles) {
                $relativePath = [IO.Path]::GetRelativePath($sourceRoot, $parameterFile.FullName)
                $destination = Join-Path $tempRoot $relativePath
                $null = New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force
                Copy-Item -LiteralPath $parameterFile.FullName -Destination $destination -Force
            }
            $null = New-Item -ItemType Directory -Path (Join-Path $tempRoot '.ifs') -Force
            [IO.File]::WriteAllText((Join-Path $tempRoot '.ifs/manifest.json'), '{"revision":1}', [Text.UTF8Encoding]::new($false))

            $tenantId = '11111111-1111-1111-1111-111111111111'
            $global:IFS_AZ_HANDLER = {
                param($Arguments)
                $global:LASTEXITCODE = 0
                $command = $Arguments -join ' '
                if ($Arguments[0] -eq 'bicep' -and $Arguments[1] -eq 'build-params') {
                    $parameterPath = [string]$Arguments[[Array]::IndexOf($Arguments, '--file') + 1]
                    $component = Split-Path -Path (Split-Path -Parent (Split-Path -Parent $parameterPath)) -Leaf
                    $parameterStem = [IO.Path]::GetFileNameWithoutExtension($parameterPath)
                    $target = $parameterStem.Split('.')[1]
                    $resourceGroup = [pscustomobject]@{
                        name = "rg-shop-$component-main-$target"
                        location = 'northeurope'
                        tags = [ordered]@{
                            costCenter = 'ecommerce'
                            'ifs-project' = 'shop'
                            'ifs-component' = $component
                            'ifs-environment' = $target
                            'managed-by' = 'infraflowsculptor'
                        }
                    }
                    $parameterJson = ConvertTo-Json -InputObject ([pscustomobject]@{ parameters = [pscustomobject]@{ resourceGroups = [pscustomobject]@{ value = [pscustomobject]@{ main = $resourceGroup } } } }) -Depth 20 -Compress
                    return (@{ parametersJson = $parameterJson; templateJson = '{}' } | ConvertTo-Json -Compress)
                }
                if ($command -eq 'account show') { return '{"tenantId":"11111111-1111-1111-1111-111111111111"}' }
                if ($Arguments[0] -eq 'account' -and $Arguments[1] -eq 'show') { return ('{"tenantId":"11111111-1111-1111-1111-111111111111","name":"test","id":"' + $Arguments[[Array]::IndexOf($Arguments, '--subscription') + 1] + '"}') }
                if ($Arguments[0] -eq 'devops' -and $Arguments[1] -eq 'project') { return '{"id":"22222222-2222-2222-2222-222222222222"}' }
                if ($Arguments[0] -eq 'role' -and $Arguments[1] -eq 'definition') { return '[{"roleName":"Azure Deployment Stack Owner","name":"33333333-3333-3333-3333-333333333333"}]' }
                if ($Arguments[0] -eq 'group' -and $Arguments[1] -eq 'list') { return '[]' }
                if ($Arguments[0] -eq 'identity' -and $Arguments[1] -eq 'list') { return '[]' }
                return '{}'
            }

            $null = Invoke-IfsAzureSetup -RepositoryPath $tempRoot -AzureDevOpsOrganization 'https://dev.azure.com/example' -AzureDevOpsProject 'shop' -TenantId $tenantId -WhatIf
            $mutatingVerbs = @('create', 'update', 'delete', 'add', 'remove', 'set', 'assign')
            $writes = @($global:IFS_AZ_CALLS | Where-Object { @($_ | Where-Object { $_ -in $mutatingVerbs }).Count -gt 0 })
            $writes.Count | Should -Be 0
        }
        finally {
            if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
        }
    }
}
