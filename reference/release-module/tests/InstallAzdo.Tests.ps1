Describe 'Finalisation du kit Azure DevOps' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
        $script:installScript = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/.ifs/install/scripts/Install-Azdo.ps1'
        $script:installPipeline = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/.ifs/install/install.pipeline.yml'
        $script:environmentNames = @(
            'SYSTEM_COLLECTIONURI', 'SYSTEM_TEAMPROJECT', 'SYSTEM_TEAMPROJECTID', 'BUILD_REPOSITORY_ID',
            'BUILD_REPOSITORY_NAME', 'SYSTEM_ACCESSTOKEN', 'BUILD_SOURCEBRANCH', 'AGENT_TEMPDIRECTORY',
            'BUILD_DEFINITIONID', 'BUILD_BUILDID', 'IFS_READINESS_DEV', 'IFS_APP_AUTH_DEV'
        )
    }

    BeforeEach {
        $script:tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-ado-finalize-' + [guid]::NewGuid().ToString('N'))
        $script:reportDirectory = Join-Path $script:tempRoot 'agent-temp'
        $null = New-Item -ItemType Directory -Path (Join-Path $script:tempRoot '.ifs') -Force
        $null = New-Item -ItemType Directory -Path (Join-Path $script:tempRoot 'core/infra/pipelines') -Force
        $null = New-Item -ItemType Directory -Path $script:reportDirectory -Force
        [IO.File]::WriteAllText((Join-Path $script:tempRoot '.ifs/manifest.json'), '{"revision":1}', [Text.UTF8Encoding]::new($false))
        $release = [ordered]@{
            schema = 'ifs-release/v1'
            project = 'shop'
            target = 'dev'
            subscriptionId = '11111111-1111-1111-1111-111111111111'
            location = 'francecentral'
            protected = $false
            component = 'core'
        }
        [IO.File]::WriteAllText((Join-Path $script:tempRoot 'core/infra/release.dev.json'), (ConvertTo-Json $release -Depth 10), [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $script:tempRoot 'core/infra/pipelines/release.yml'), "extends:`n  template: .ifs/templates/infra-release.yml`n", [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $script:tempRoot 'core/infra/pipelines/ci.yml'), "extends:`n  template: .ifs/templates/infra-ci.yml`n", [Text.UTF8Encoding]::new($false))

        $script:environmentBackup = @{}
        foreach ($name in $script:environmentNames) { $script:environmentBackup[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
        $env:SYSTEM_COLLECTIONURI = 'https://dev.azure.com/example/'
        $env:SYSTEM_TEAMPROJECT = 'shop'
        $env:SYSTEM_TEAMPROJECTID = 'project-id'
        $env:BUILD_REPOSITORY_ID = 'repo-id'
        $env:BUILD_REPOSITORY_NAME = 'InfraFlowSculptor'
        $env:SYSTEM_ACCESSTOKEN = 'pester-token'
        $env:BUILD_SOURCEBRANCH = 'refs/heads/main'
        $env:AGENT_TEMPDIRECTORY = $script:reportDirectory
        $env:BUILD_DEFINITIONID = '999'
        $env:BUILD_BUILDID = '1001'
        [Environment]::SetEnvironmentVariable('IFS_READINESS_DEV', $null, 'Process')
        [Environment]::SetEnvironmentVariable('IFS_APP_AUTH_DEV', $null, 'Process')

        $global:IFS_ADO_FINALIZE_MODE = 'MissingApprover'
        $global:IFS_ADO_CURRENT_BUILD_READS = 0
        $global:IFS_ADO_TIMELINE_READS = 0
        $global:IFS_ADO_PATCHES = [Collections.Generic.List[object]]::new()
        $global:IFS_ADO_CHECK_POSTS = [Collections.Generic.List[object]]::new()
        $global:IFS_ADO_CHECK_GETS = [Collections.Generic.List[string]]::new()
        $global:IFS_ADO_POLICY_GETS = [Collections.Generic.List[string]]::new()
        $global:IFS_ADO_POLICY_UPDATES = [Collections.Generic.List[object]]::new()
        $global:IFS_ADO_TYPE_API_CALLS = [Collections.Generic.List[string]]::new()
        $global:IFS_ADO_THROW_CHECK_POST_AT = 1
        $global:IFS_ADO_DEFINITIONS = @(
            [pscustomobject]@{
                id = 201
                name = 'shop · core · infra · RELEASE'
                path = '\shop\core'
                description = 'managed-by: infraflowsculptor; ifs-kit-revision: 1'
            }
            [pscustomobject]@{
                id = 202
                name = 'shop · core · infra · CI'
                path = '\shop\core'
                description = 'managed-by: infraflowsculptor; ifs-kit-revision: 1'
            }
        )
        $global:IFS_ADO_ENDPOINTS = @(
            [pscustomobject]@{ id = '301'; name = 'ifs-shop-dev'; description = 'managed-by: infraflowsculptor; ifs-kit-revision: 1' }
            [pscustomobject]@{ id = '302'; name = 'ifs-shop-dev-app'; description = 'managed-by: infraflowsculptor; ifs-kit-revision: 1' }
        )

        Mock Invoke-RestMethod {
            param($Method, $Uri, $Headers, $ContentType, $Body)
            $path = ([uri]$Uri).AbsolutePath + ([uri]$Uri).Query
            if ($Method -eq 'PATCH' -and $path -match 'pipelines/pipelinePermissions/(endpoint|environment)/') {
                $global:IFS_ADO_PATCHES.Add([pscustomobject]@{ Uri = [string]$Uri; Body = $Body })
                return [pscustomobject]@{}
            }
            if ($Method -eq 'POST' -and $path -match 'pipelines/checks/configurations') {
                $global:IFS_ADO_CHECK_POSTS.Add([pscustomobject]@{ Uri = [string]$Uri; Body = $Body })
                if ($global:IFS_ADO_CHECK_POSTS.Count -eq $global:IFS_ADO_THROW_CHECK_POST_AT) { throw 'simulated check failure' }
                return [pscustomobject]@{ id = 500 + $global:IFS_ADO_CHECK_POSTS.Count }
            }
            if ($Method -eq 'PATCH' -and $path -match 'pipelines/checks/configurations/') { return [pscustomobject]@{ id = 510 } }
            if ($path -match 'git/repositories/') { return [pscustomobject]@{ defaultBranch = 'refs/heads/main' } }
            if ($path -match 'distributedtask/queues') {
                if ($global:IFS_ADO_FINALIZE_MODE -in @('EmptyQueues', 'MissingBuildQueue')) { return [pscustomobject]@{ value = @() } }
                return [pscustomobject]@{ value = @([pscustomobject]@{ id = 1; name = 'Azure Pipelines' }) }
            }
            if ($path -match 'build/builds/1001/timeline') {
                $global:IFS_ADO_TIMELINE_READS++
                return [ordered]@{ records = @([ordered]@{ type = 'Job'; queueId = 41 }) }
            }
            if ($path -match 'build/builds/1001') {
                $global:IFS_ADO_CURRENT_BUILD_READS++
                if ($global:IFS_ADO_FINALIZE_MODE -eq 'MissingBuildDefinition') { return [ordered]@{ id = 1001; definition = [ordered]@{ id = 999 } } }
                if ($global:IFS_ADO_FINALIZE_MODE -eq 'MissingBuildQueue') { return [ordered]@{ id = 1001 } }
                return [ordered]@{ queue = [ordered]@{ id = 41; name = 'Azure Pipelines' } }
            }
            if ($path -match 'build/definitions') { return [pscustomobject]@{ value = $global:IFS_ADO_DEFINITIONS } }
            if ($Method -eq 'GET' -and $path -match 'policy/configurations') {
                $global:IFS_ADO_POLICY_GETS.Add($path)
                if ($global:IFS_ADO_FINALIZE_MODE -eq 'ExistingBuildValidation') {
                    $settings = [pscustomobject]@{
                        buildDefinitionId = 203
                        displayName = 'IFS: shop · core · infra · PR'
                        filenamePatterns = @('/core/infra/**')
                        manualQueueOnly = $false
                        queueOnSourceUpdateOnly = $true
                        scope = @([pscustomobject]@{ repositoryId = 'repo-id'; refName = 'refs/heads/main'; matchKind = 'Exact' })
                    }
                    $policy = [pscustomobject]@{
                        id = 901
                        isEnabled = $true
                        isBlocking = $true
                        type = [pscustomobject]@{ id = '0609b952-1397-4640-95ec-e00a01b2c241' }
                        settings = $settings
                        revision = 1
                    }
                    return [pscustomobject]@{ value = @($policy) }
                }
                return [pscustomobject]@{ value = @() }
            }
            if ($Method -eq 'PUT' -and $path -match 'policy/configurations/') {
                $global:IFS_ADO_POLICY_UPDATES.Add([pscustomobject]@{ Uri = [string]$Uri; Body = $Body })
                return [pscustomobject]@{ id = 901 }
            }
            if ($path -match 'serviceendpoint/endpoints') { return [pscustomobject]@{ value = $global:IFS_ADO_ENDPOINTS } }
            if ($path -match 'identities\?') {
                if ($global:IFS_ADO_FINALIZE_MODE -in @('MissingApprover', 'EmptyQueues', 'MissingBuildQueue')) { return [pscustomobject]@{ value = @() } }
                if ($global:IFS_ADO_FINALIZE_MODE -eq 'ProjectScopedApprover') {
                    return [pscustomobject]@{ value = @(
                        [pscustomobject]@{
                            id = 'approver-id'
                            providerDisplayName = '[shop]\Shop Release Approvers'
                            properties = [pscustomobject]@{
                                Account = [pscustomobject]@{ '$type' = 'System.String'; '$value' = 'Shop Release Approvers' }
                            }
                        }
                    ) }
                }
                if ($global:IFS_ADO_FINALIZE_MODE -eq 'MissingDisplayName') {
                    return [pscustomobject]@{ value = @(
                        [pscustomobject]@{ id = 'foreign-id'; providerDisplayName = 'Unrelated identity' }
                        [pscustomobject]@{ id = 'approver-id'; providerDisplayName = 'Shop Release Approvers' }
                    ) }
                }
                return [pscustomobject]@{ value = @([pscustomobject]@{ id = 'approver-id'; providerDisplayName = 'Shop Release Approvers'; displayName = 'Shop Release Approvers' }) }
            }
            if ($path -match 'pipelines/checks/types') {
                $global:IFS_ADO_TYPE_API_CALLS.Add($path)
                throw 'unsupported check-types API called'
            }
            if ($path -match 'distributedtask/environments\?') { return [pscustomobject]@{ value = @([pscustomobject]@{ id = 401; name = 'shop-dev'; description = 'managed-by: infraflowsculptor' }) } }
            if ($Method -eq 'GET' -and $path -match 'pipelines/checks/configurations') {
                $global:IFS_ADO_CHECK_GETS.Add($path)
                if ($global:IFS_ADO_FINALIZE_MODE -eq 'ExistingBranchCheck' -and $path -match 'resourceId=401') {
                    return [pscustomobject]@{ value = @([pscustomobject]@{ id = 509; type = [pscustomobject]@{ id = '2ef31ad6-baa0-403a-8b45-2cbc9b4e5563'; name = 'ExclusiveLock' } }) }
                }
                if ($global:IFS_ADO_FINALIZE_MODE -eq 'ExistingBranchCheck' -and $path -match 'resourceId=301') {
                    if ($path.Contains('$expand=settings')) {
                        $settings = [pscustomobject]@{
                            displayName = 'managed-by: infraflowsculptor; ifs-kit-revision: 1'
                            definitionRef = [pscustomobject]@{ id = '86b05a0c-73e6-4f7d-b3cf-e38f3b39a75b'; name = 'evaluatebranchProtection'; version = '0.0.1' }
                            inputs = [pscustomobject]@{ allowedBranches = 'refs/heads/main'; ensureProtectionOfBranch = 'true'; allowUnknownStatusBranch = 'false' }
                        }
                        return [pscustomobject]@{ value = @([pscustomobject]@{ id = 510; resource = [pscustomobject]@{ type = 'endpoint'; id = '301'; name = 'ifs-shop-dev' }; type = [pscustomobject]@{ id = 'fe1de3ee-a436-41b4-bb20-f6eb4cb879a7'; name = 'Task Check' }; settings = $settings }) }
                    }
                    return [pscustomobject]@{ value = @([pscustomobject]@{ id = 510; resource = [pscustomobject]@{ type = 'endpoint'; id = '301'; name = 'ifs-shop-dev' }; type = [pscustomobject]@{ id = 'fe1de3ee-a436-41b4-bb20-f6eb4cb879a7'; name = 'Task Check' } }) }
                }
                return [pscustomobject]@{ value = @() }
            }
            throw "Unexpected mock Azure DevOps request: $Method $path"
        }
    }

    AfterEach {
        foreach ($name in $script:environmentNames) { [Environment]::SetEnvironmentVariable($name, $script:environmentBackup[$name], 'Process') }
        if (Test-Path -LiteralPath $script:tempRoot) { Remove-Item -LiteralPath $script:tempRoot -Recurse -Force }
        Remove-Variable -Name IFS_ADO_FINALIZE_MODE, IFS_ADO_CURRENT_BUILD_READS, IFS_ADO_TIMELINE_READS, IFS_ADO_PATCHES, IFS_ADO_CHECK_POSTS, IFS_ADO_CHECK_GETS, IFS_ADO_POLICY_GETS, IFS_ADO_POLICY_UPDATES, IFS_ADO_TYPE_API_CALLS, IFS_ADO_THROW_CHECK_POST_AT, IFS_ADO_DEFINITIONS, IFS_ADO_ENDPOINTS -Scope Global -ErrorAction SilentlyContinue
    }

    It "retire l'autorisation temporaire si le groupe approbateur est absent" {
        { & $script:installScript -Phase Finalize -RepositoryPath $script:tempRoot } | Should -Throw '*Shop Release Approvers*introuvable*'

        $global:IFS_ADO_PATCHES.Count | Should -BeGreaterThan 0
        @($global:IFS_ADO_PATCHES | Where-Object { $_.Uri -match '/endpoint/301\?' }).Count | Should -BeGreaterThan 0
        @($global:IFS_ADO_PATCHES | Where-Object { $_.Uri -match '/endpoint/302\?' }).Count | Should -BeGreaterThan 0
        foreach ($patch in $global:IFS_ADO_PATCHES) {
            $body = [string]$patch.Body | ConvertFrom-Json
            $installer = @($body.pipelines | Where-Object { [int]$_.id -eq 999 })
            $installer.Count | Should -Be 1
            $installer[0].authorized | Should -BeFalse
        }
    }

    It "retire les autorisations temporaires si Prepare a echoue avant de creer les pipelines" {
        $global:IFS_ADO_DEFINITIONS = @()

        { & $script:installScript -Phase Finalize -RepositoryPath $script:tempRoot } | Should -Throw '*absent apres la phase Prepare*'

        @($global:IFS_ADO_PATCHES | Where-Object { $_.Uri -match '/endpoint/301\?' }).Count | Should -BeGreaterThan 0
        @($global:IFS_ADO_PATCHES | Where-Object { $_.Uri -match '/endpoint/302\?' }).Count | Should -BeGreaterThan 0
    }

    It "retrouve le pipeline courant quand BUILD_DEFINITIONID est absent pendant le nettoyage" {
        $global:IFS_ADO_FINALIZE_MODE = 'MissingBuildDefinition'
        $global:IFS_ADO_DEFINITIONS = @()
        [Environment]::SetEnvironmentVariable('BUILD_DEFINITIONID', $null, 'Process')

        { & $script:installScript -Phase Finalize -RepositoryPath $script:tempRoot } | Should -Throw '*absent apres la phase Prepare*'
        $global:IFS_ADO_CURRENT_BUILD_READS | Should -BeGreaterThan 0
        $global:IFS_ADO_PATCHES.Count | Should -BeGreaterThan 0

        foreach ($patch in $global:IFS_ADO_PATCHES) {
            $body = [string]$patch.Body | ConvertFrom-Json
            @($body.pipelines | Where-Object { [int]$_.id -eq 999 -and -not $_.authorized }).Count | Should -Be 1
        }
    }

    It 'transmet BUILD_DEFINITIONID aux deux phases du pipeline installateur' {
        $pipeline = Get-Content -LiteralPath $script:installPipeline -Raw
        [regex]::Matches($pipeline, '(?m)^\s+BUILD_DEFINITIONID:\s+\$\(System\.DefinitionId\)\s*$').Count | Should -Be 2
    }

    It 'lit les groupes de variables avec la portee du projet Azure DevOps' {
        $installer = Get-Content -LiteralPath $script:installScript -Raw
        [regex]::Matches($installer, 'Invoke-IfsAdoApi -Path "distributedtask/variablegroups\?groupName=').Count | Should -Be 2
    }

    It 'ignore une definition geree qui ne contient pas de description' {
        $global:IFS_ADO_DEFINITIONS += [pscustomobject]@{ id = 203; name = 'shop · test · infra · CI'; path = '\shop\test' }

        { & $script:installScript -Phase Finalize -RepositoryPath $script:tempRoot } | Should -Throw '*Shop Release Approvers*introuvable*'
        $global:IFS_ADO_PATCHES.Count | Should -BeGreaterThan 0
    }

    It 'ignore une identite sans displayName et retrouve le groupe approbateur' {
        $global:IFS_ADO_FINALIZE_MODE = 'MissingDisplayName'
        $releasePath = Join-Path $script:tempRoot 'core/infra/release.dev.json'
        $release = Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json -AsHashtable
        $release['protected'] = $true
        [IO.File]::WriteAllText($releasePath, (ConvertTo-Json $release -Depth 10), [Text.UTF8Encoding]::new($false))

        { & $script:installScript -Phase Finalize -RepositoryPath $script:tempRoot } | Should -Throw '*simulated check failure*'
        $global:IFS_ADO_PATCHES.Count | Should -BeGreaterThan 0
        $global:IFS_ADO_CHECK_POSTS.Count | Should -Be 1
        $approval = [string]$global:IFS_ADO_CHECK_POSTS[0].Body | ConvertFrom-Json
        $approval.type.id | Should -Be '8c6f20a7-a545-4486-9777-f762fafe0d4d'
        $approval.settings.approvers[0].id | Should -Be 'approver-id'
        $approval.settings.approvers[0].displayName | Should -Be 'Shop Release Approvers'
    }

    It 'reconnait le nom exact du groupe dans Account quand providerDisplayName est prefixe au projet' {
        $global:IFS_ADO_FINALIZE_MODE = 'ProjectScopedApprover'
        $releasePath = Join-Path $script:tempRoot 'core/infra/release.dev.json'
        $release = Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json -AsHashtable
        $release['protected'] = $true
        [IO.File]::WriteAllText($releasePath, (ConvertTo-Json $release -Depth 10), [Text.UTF8Encoding]::new($false))

        { & $script:installScript -Phase Finalize -RepositoryPath $script:tempRoot } | Should -Throw '*simulated check failure*'
        $global:IFS_ADO_CHECK_POSTS.Count | Should -Be 1
        $approval = [string]$global:IFS_ADO_CHECK_POSTS[0].Body | ConvertFrom-Json
        $approval.type.id | Should -Be '8c6f20a7-a545-4486-9777-f762fafe0d4d'
        $approval.settings.approvers[0].id | Should -Be 'approver-id'
        $approval.settings.approvers[0].displayName | Should -Be 'Shop Release Approvers'
    }

    It 'utilise les types fixes des controles et evite la route de liste non disponible' {
        $global:IFS_ADO_FINALIZE_MODE = 'PresentApprover'
        $global:IFS_ADO_THROW_CHECK_POST_AT = 3

        { & $script:installScript -Phase Finalize -RepositoryPath $script:tempRoot } | Should -Throw '*simulated check failure*'
        $global:IFS_ADO_TYPE_API_CALLS.Count | Should -Be 0
        $global:IFS_ADO_CHECK_POSTS.Count | Should -Be 3

        $checkTypes = @($global:IFS_ADO_CHECK_POSTS | ForEach-Object { [string]$_.Body | ConvertFrom-Json })
        $checkTypes[0].type.id | Should -Be '2ef31ad6-baa0-403a-8b45-2cbc9b4e5563'
        $checkTypes[1].type.id | Should -Be 'fe1de3ee-a436-41b4-bb20-f6eb4cb879a7'
        $checkTypes[1].settings.definitionRef.id | Should -Be '86b05a0c-73e6-4f7d-b3cf-e38f3b39a75b'
        $checkTypes[1].settings.inputs.allowedBranches | Should -Be 'refs/heads/main'
        $checkTypes[1].settings.inputs.ensureProtectionOfBranch | Should -Be 'true'
        $checkTypes[1].settings.inputs.allowUnknownStatusBranch | Should -Be 'false'
        $checkTypes[2].type.id | Should -Be '4020e66e-b0f3-47e1-bc88-48f3cc59b5f3'
        $checkTypes[2].settings.extendsChecks[0].repositoryType | Should -Be 'git'
        $checkTypes[2].settings.extendsChecks[0].repositoryName | Should -Be 'shop/InfraFlowSculptor'
        $checkTypes[2].settings.extendsChecks[0].repositoryRef | Should -Be 'refs/heads/main'
        $checkTypes[2].settings.extendsChecks[0].templatePath | Should -Be '/.ifs/templates/infra-release.yml'
    }

    It 'finalise le rapport quand les variables de disponibilite Azure sont absentes' {
        $global:IFS_ADO_FINALIZE_MODE = 'PresentApprover'
        $global:IFS_ADO_THROW_CHECK_POST_AT = -1

        { & $script:installScript -Phase Finalize -RepositoryPath $script:tempRoot } | Should -Not -Throw

        $report = Get-Content -LiteralPath (Join-Path $script:reportDirectory 'ifs-install-report.json') -Raw | ConvertFrom-Json
        $report.readiness[0].infraAuthentication | Should -Be 'non verifie'
        $report.readiness[0].appAuthentication | Should -Be 'non verifie'
    }

    It 'demande les reglages des controles existants avant de les realigner' {
        $global:IFS_ADO_FINALIZE_MODE = 'ExistingBranchCheck'
        $global:IFS_ADO_THROW_CHECK_POST_AT = 1

        { & $script:installScript -Phase Finalize -RepositoryPath $script:tempRoot } | Should -Throw '*simulated check failure*'

        @($global:IFS_ADO_CHECK_GETS | Where-Object { $_.Contains('$expand=settings') }).Count | Should -BeGreaterThan 0
        $global:IFS_ADO_PATCHES.Count | Should -BeGreaterThan 0
    }

    It 'met a jour une politique IFS existante sans champ message dans ses reglages' {
        $global:IFS_ADO_FINALIZE_MODE = 'ExistingBuildValidation'
        $global:IFS_ADO_THROW_CHECK_POST_AT = -1
        $global:IFS_ADO_DEFINITIONS += [pscustomobject]@{
            id = 203
            name = 'shop · core · infra · PR'
            path = '\shop\core'
            description = 'managed-by: infraflowsculptor; ifs-kit-revision: 1'
        }
        [IO.File]::WriteAllText((Join-Path $script:tempRoot 'core/infra/pipelines/pr.yml'), "extends:`n  template: .ifs/templates/infra-pr.yml`n", [Text.UTF8Encoding]::new($false))

        { & $script:installScript -Phase Finalize -RepositoryPath $script:tempRoot } | Should -Not -Throw

        $global:IFS_ADO_POLICY_GETS.Count | Should -Be 1
        $global:IFS_ADO_POLICY_UPDATES.Count | Should -Be 1
        $updatedPolicy = [string]$global:IFS_ADO_POLICY_UPDATES[0].Body | ConvertFrom-Json
        $updatedPolicy.settings.displayName | Should -Be 'IFS: shop · core · infra · PR'
        $updatedPolicy.settings.filenamePatterns | Should -Contain '/core/infra/**'
        $updatedPolicy.settings.message | Should -BeNullOrEmpty
    }

    It 'utilise la file du run courant si le projet ne renvoie aucune file' {
        $global:IFS_ADO_FINALIZE_MODE = 'EmptyQueues'

        { & $script:installScript -Phase Finalize -RepositoryPath $script:tempRoot } | Should -Throw '*Shop Release Approvers*introuvable*'
        $global:IFS_ADO_CURRENT_BUILD_READS | Should -Be 1
    }

    It 'derive la file depuis la timeline si le build courant ne fournit pas sa file' {
        $global:IFS_ADO_FINALIZE_MODE = 'MissingBuildQueue'

        { & $script:installScript -Phase Finalize -RepositoryPath $script:tempRoot } | Should -Throw '*Shop Release Approvers*introuvable*'
        $global:IFS_ADO_CURRENT_BUILD_READS | Should -Be 1
        $global:IFS_ADO_TIMELINE_READS | Should -Be 1
    }

    It "retire l'autorisation temporaire si la creation d'un controle echoue" {
        $global:IFS_ADO_FINALIZE_MODE = 'CheckFailure'

        { & $script:installScript -Phase Finalize -RepositoryPath $script:tempRoot } | Should -Throw '*simulated check failure*'

        $global:IFS_ADO_PATCHES.Count | Should -BeGreaterThan 0
        foreach ($patch in $global:IFS_ADO_PATCHES) {
            $body = [string]$patch.Body | ConvertFrom-Json
            $installer = @($body.pipelines | Where-Object { [int]$_.id -eq 999 })
            $installer.Count | Should -Be 1
            $installer[0].authorized | Should -BeFalse
        }
    }
}
