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
    BeforeEach {
        $global:IFS_AZ_CALLS = [Collections.Generic.List[object]]::new()
        $global:IFS_AZ_HANDLER = $null
        $global:IFS_ROLE_ASSIGNMENTS = [Collections.Generic.List[object]]::new()
    }

    AfterAll {
        Remove-Item Function:\global:az -ErrorAction SilentlyContinue
        Remove-Variable -Name IFS_AZ_CALLS, IFS_AZ_HANDLER, IFS_ROLE_ASSIGNMENTS -Scope Global -ErrorAction SilentlyContinue
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

    It 'supprime les FIC ADO obsoletes et conserve les autres' {
        $global:IFS_AZ_HANDLER = { param($Arguments) $global:LASTEXITCODE = 0; return '{}' }
        $existing = @(
            [pscustomobject]@{ name = 'ifs-ado-current' }
            [pscustomobject]@{ name = 'ifs-ado-old' }
            [pscustomobject]@{ name = 'github-actions' }
        )

        InModuleScope IfsInstall -Parameters @{ ExistingCredentials = $existing } {
            Remove-IfsStaleFederatedCredential -SubscriptionId 'sub' -ResourceGroup 'rg' -IdentityName 'id' -Existing $ExistingCredentials -ExpectedNames @('ifs-ado-current')
        }

        $deletes = @($global:IFS_AZ_CALLS | Where-Object { $_[0] -eq 'identity' -and $_[1] -eq 'federated-credential' -and $_[2] -eq 'delete' })
        $deletes.Count | Should -Be 1
        $deletes[0] | Should -Contain 'ifs-ado-old'
        $deletes[0] | Should -Not -Contain 'github-actions'
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
            $null = New-Item -ItemType Directory -Path (Join-Path $tempRoot '.ifs') -Force
            [IO.File]::WriteAllText((Join-Path $tempRoot '.ifs/manifest.json'), '{"revision":1}', [Text.UTF8Encoding]::new($false))

            $tenantId = '11111111-1111-1111-1111-111111111111'
            $global:IFS_AZ_HANDLER = {
                param($Arguments)
                $global:LASTEXITCODE = 0
                $command = $Arguments -join ' '
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
