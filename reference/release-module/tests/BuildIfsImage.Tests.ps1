BeforeAll {
    $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
    $script:buildImageScript = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/.ifs/templates/scripts/Build-IfsImage.ps1'

    function global:az {
        param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
        $call = $Arguments -join ' '
        [void]$global:IFS_BUILD_AZ_CALLS.Add($call)
        $global:LASTEXITCODE = 0
        if ($Arguments.Count -ge 2 -and $Arguments[0] -eq 'cloud' -and $Arguments[1] -eq 'show') {
            return $global:IFS_BUILD_ACR_SUFFIX
        }
        if ($Arguments.Count -ge 3 -and $Arguments[0] -eq 'acr' -and $Arguments[1] -eq 'repository' -and $Arguments[2] -eq 'show') {
            return $global:IFS_BUILD_IMAGE_DIGEST
        }
        if ($Arguments.Count -ge 2 -and $Arguments[0] -eq 'acr' -and $Arguments[1] -eq 'show') {
            $global:LASTEXITCODE = 1
            return 'ERROR: the build identity has no management-plane read permission.'
        }
        return ''
    }

    function global:docker {
        param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
        [void]$global:IFS_BUILD_DOCKER_CALLS.Add($Arguments -join ' ')
        $global:LASTEXITCODE = 0
        return ''
    }

    function global:trivy {
        param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
        [void]$global:IFS_BUILD_TRIVY_CALLS.Add($Arguments -join ' ')
        $global:LASTEXITCODE = 0
        return ''
    }
}

Describe 'Build-IfsImage avec AcrPush seul' {
    BeforeEach {
        $global:IFS_BUILD_AZ_CALLS = [Collections.Generic.List[string]]::new()
        $global:IFS_BUILD_DOCKER_CALLS = [Collections.Generic.List[string]]::new()
        $global:IFS_BUILD_TRIVY_CALLS = [Collections.Generic.List[string]]::new()
        $global:IFS_BUILD_ACR_SUFFIX = '.azurecr.io'
        $global:IFS_BUILD_IMAGE_DIGEST = 'sha256:' + ('a' * 64)
        $global:LASTEXITCODE = 0

        $script:priorBuildId = $env:BUILD_BUILDID
        $script:priorSourceVersion = $env:BUILD_SOURCEVERSION
        $env:BUILD_BUILDID = '123'
        $env:BUILD_SOURCEVERSION = '0123456789abcdef0123456789abcdef01234567'

        $script:fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-build-image-' + [guid]::NewGuid().ToString('N'))
        $script:contextRoot = Join-Path $script:fixtureRoot 'context'
        $script:outputPath = Join-Path $script:fixtureRoot 'artifacts/app/image.json'
        [void][IO.Directory]::CreateDirectory($script:contextRoot)
        [IO.File]::WriteAllText((Join-Path $script:contextRoot 'Dockerfile'), 'FROM scratch', [Text.UTF8Encoding]::new($false))

        $subscriptionId = '00000000-0000-0000-0000-000000000001'
        $release = [ordered]@{
            schema = 'ifs-release/v1'
            project = 'shop42'
            component = 'orders'
            target = 'dev'
            unit = 'ifs-shop42-orders-dev'
            subscriptionId = $subscriptionId
            location = 'francecentral'
            protected = $false
            deploymentIdentityObjectId = $null
            dependencies = @(
                [ordered]@{
                    component = 'platform'
                    resources = @(
                        [ordered]@{ id = "/subscriptions/$subscriptionId/resourceGroups/rg-shop42-platform-main-shared/providers/Microsoft.ContainerRegistry/registries/crtestshared" }
                    )
                }
            )
            secretWrites = @()
            secretReferences = @()
            appOwnedState = @()
            dataAccess = @()
            accessObjectTypes = @()
            restartOnSecretChange = @()
        }
        $script:releasePath = Join-Path $script:fixtureRoot 'release.dev.json'
        [IO.File]::WriteAllText($script:releasePath, (ConvertTo-Json -InputObject $release -Depth 20), [Text.UTF8Encoding]::new($false))
    }

    AfterEach {
        $env:BUILD_BUILDID = $script:priorBuildId
        $env:BUILD_SOURCEVERSION = $script:priorSourceVersion
        if (Test-Path -LiteralPath $script:fixtureRoot -PathType Container) {
            Remove-Item -LiteralPath $script:fixtureRoot -Recurse -Force
        }
    }

    It 'construit le login server depuis le cloud et ne lit pas le registre via ARM' {
        {
            & $script:buildImageScript `
                -RepositoryRoot $script:fixtureRoot `
                -ReleasePath $script:releasePath `
                -BuildContext 'context' `
                -DockerfilePath 'context/Dockerfile' `
                -Application 'api' `
                -ImageManifestPath $script:outputPath
        } | Should -Not -Throw

        @($global:IFS_BUILD_AZ_CALLS | Where-Object { $_ -eq 'cloud show --query suffixes.acrLoginServerEndpoint --output tsv' }).Count | Should -Be 1
        @($global:IFS_BUILD_AZ_CALLS | Where-Object { $_ -match '^acr show(?: |$)' }).Count | Should -Be 0
        $buildCalls = @($global:IFS_BUILD_DOCKER_CALLS | Where-Object { $_ -like 'buildx build *' })
        $buildCalls | Should -HaveCount 1
        $buildCalls[0] | Should -Match ([regex]::Escape('--tag crtestshared.azurecr.io/shop42/orders/api:123-0123456789ab'))
        $imageManifest = Get-Content -LiteralPath $script:outputPath -Raw | ConvertFrom-Json
        $imageManifest.image | Should -Be ('crtestshared.azurecr.io/shop42/orders/api@{0}' -f $global:IFS_BUILD_IMAGE_DIGEST)
    }
}

AfterAll {
    Remove-Item Function:\global:az -ErrorAction SilentlyContinue
    Remove-Item Function:\global:docker -ErrorAction SilentlyContinue
    Remove-Item Function:\global:trivy -ErrorAction SilentlyContinue
    Remove-Variable -Name IFS_BUILD_AZ_CALLS, IFS_BUILD_DOCKER_CALLS, IFS_BUILD_TRIVY_CALLS, IFS_BUILD_ACR_SUFFIX, IFS_BUILD_IMAGE_DIGEST -Scope Global -ErrorAction SilentlyContinue
}
