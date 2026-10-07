Describe 'Finalisation du kit Azure DevOps' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
        $script:installScript = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/.ifs/install/scripts/Install-Azdo.ps1'
        $script:environmentNames = @(
            'SYSTEM_COLLECTIONURI', 'SYSTEM_TEAMPROJECT', 'SYSTEM_TEAMPROJECTID', 'BUILD_REPOSITORY_ID',
            'BUILD_REPOSITORY_NAME', 'SYSTEM_ACCESSTOKEN', 'BUILD_SOURCEBRANCH', 'AGENT_TEMPDIRECTORY',
            'BUILD_DEFINITIONID'
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

        $global:IFS_ADO_FINALIZE_MODE = 'MissingApprover'
        $global:IFS_ADO_PATCHES = [Collections.Generic.List[object]]::new()
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
            if ($Method -eq 'PATCH' -and $path -match 'pipelines/pipelinePermissions/endpoint/') {
                $global:IFS_ADO_PATCHES.Add([pscustomobject]@{ Uri = [string]$Uri; Body = $Body })
                return [pscustomobject]@{}
            }
            if ($Method -eq 'POST' -and $path -match 'pipelines/checks/configurations') { throw 'simulated check failure' }
            if ($path -match 'git/repositories/') { return [pscustomobject]@{ defaultBranch = 'refs/heads/main' } }
            if ($path -match 'distributedtask/queues') { return [pscustomobject]@{ value = @([pscustomobject]@{ id = 1; name = 'Azure Pipelines' }) } }
            if ($path -match 'build/definitions') { return [pscustomobject]@{ value = $global:IFS_ADO_DEFINITIONS } }
            if ($path -match 'serviceendpoint/endpoints') { return [pscustomobject]@{ value = $global:IFS_ADO_ENDPOINTS } }
            if ($path -match 'identities\?') {
                if ($global:IFS_ADO_FINALIZE_MODE -eq 'MissingApprover') { return [pscustomobject]@{ value = @() } }
                return [pscustomobject]@{ value = @([pscustomobject]@{ id = 'approver-id'; providerDisplayName = 'Shop Release Approvers'; displayName = 'Shop Release Approvers' }) }
            }
            if ($path -match 'pipelines/checks/types') {
                return [pscustomobject]@{ value = @(
                    [pscustomobject]@{ id = 'approval'; name = 'Approval'; displayName = 'Approval' }
                    [pscustomobject]@{ id = 'lock'; name = 'Exclusive Lock'; displayName = 'Exclusive Lock' }
                    [pscustomobject]@{ id = 'branch'; name = 'Branch Control'; displayName = 'Branch Control' }
                    [pscustomobject]@{ id = 'template'; name = 'Required Template'; displayName = 'Required Template' }
                ) }
            }
            if ($path -match 'distributedtask/environments\?') { return [pscustomobject]@{ value = @([pscustomobject]@{ id = 401; name = 'shop-dev'; description = 'managed-by: infraflowsculptor' }) } }
            if ($path -match 'pipelines/checks/configurations') { return [pscustomobject]@{ value = @() } }
            throw "Unexpected mock Azure DevOps request: $Method $path"
        }
    }

    AfterEach {
        foreach ($name in $script:environmentNames) { [Environment]::SetEnvironmentVariable($name, $script:environmentBackup[$name], 'Process') }
        if (Test-Path -LiteralPath $script:tempRoot) { Remove-Item -LiteralPath $script:tempRoot -Recurse -Force }
        Remove-Variable -Name IFS_ADO_FINALIZE_MODE, IFS_ADO_PATCHES, IFS_ADO_DEFINITIONS, IFS_ADO_ENDPOINTS -Scope Global -ErrorAction SilentlyContinue
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
