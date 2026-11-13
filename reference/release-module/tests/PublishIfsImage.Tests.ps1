BeforeAll {
    $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
    $script:publishImageScript = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/.ifs/templates/scripts/Publish-IfsImage.ps1'

    function global:az {
        param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
        [void]$global:IFS_PUBLISH_AZ_CALLS.Add($Arguments -join ' ')
        $global:LASTEXITCODE = 0
        if ($Arguments.Count -ge 2 -and $Arguments[0] -eq 'cloud' -and $Arguments[1] -eq 'show') {
            return '.azurecr.io'
        }
        if ($Arguments.Count -ge 3 -and $Arguments[0] -eq 'acr' -and $Arguments[1] -eq 'repository' -and $Arguments[2] -eq 'show') {
            return 'sha256:' + ('a' * 64)
        }
        return ''
    }

    function global:docker {
        param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
        [void]$global:IFS_PUBLISH_DOCKER_CALLS.Add($Arguments -join ' ')
        $global:LASTEXITCODE = 0
        return ''
    }
}

Describe 'Publish-IfsImage après le scan' {
    BeforeEach {
        $global:IFS_PUBLISH_AZ_CALLS = [Collections.Generic.List[string]]::new()
        $global:IFS_PUBLISH_DOCKER_CALLS = [Collections.Generic.List[string]]::new()
        $global:LASTEXITCODE = 0
        $script:priorBuildId = $env:BUILD_BUILDID
        $script:priorSourceVersion = $env:BUILD_SOURCEVERSION
        $env:BUILD_BUILDID = '123'
        $env:BUILD_SOURCEVERSION = '0123456789abcdef0123456789abcdef01234567'
        $script:fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-publish-image-' + [guid]::NewGuid().ToString('N'))
        [void][IO.Directory]::CreateDirectory($script:fixtureRoot)

        $script:subscriptionId = '00000000-0000-0000-0000-000000000001'
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
                        [ordered]@{ id = "/subscriptions/$script:subscriptionId/resourceGroups/rg-shop42-platform-main-shared/providers/Microsoft.ContainerRegistry/registries/crtestshared" }
                    )
                }
            )
        }
        $script:releasePath = Join-Path $script:fixtureRoot 'release.dev.json'
        $script:manifestPath = Join-Path $script:fixtureRoot 'artifacts/app/image.json'
        [IO.File]::WriteAllText($script:releasePath, (ConvertTo-Json -InputObject $release -Depth 20), [Text.UTF8Encoding]::new($false))
    }

    AfterEach {
        $env:BUILD_BUILDID = $script:priorBuildId
        $env:BUILD_SOURCEVERSION = $script:priorSourceVersion
        if (Test-Path -LiteralPath $script:fixtureRoot -PathType Container) {
            Remove-Item -LiteralPath $script:fixtureRoot -Recurse -Force
        }
    }

    It 'authentifie, étiquette, publie après contrôle et écrit le manifeste immuable' {
        { & $script:publishImageScript -ReleasePath $script:releasePath -LocalImageName 'ifs-app:123' -Application 'api' -ImageManifestPath $script:manifestPath } |
            Should -Not -Throw

        $tag = '123-0123456789ab'
        $global:IFS_PUBLISH_DOCKER_CALLS | Should -Contain "tag ifs-app:123 crtestshared.azurecr.io/shop42/orders/api:$tag"
        $global:IFS_PUBLISH_DOCKER_CALLS | Should -Contain "push crtestshared.azurecr.io/shop42/orders/api:$tag"
        $global:IFS_PUBLISH_AZ_CALLS | Should -Contain "acr login --name crtestshared --subscription $script:subscriptionId --only-show-errors"
        $manifest = Get-Content -LiteralPath $script:manifestPath -Raw | ConvertFrom-Json
        $manifest.image | Should -Be ('crtestshared.azurecr.io/shop42/orders/api@{0}' -f ('sha256:' + ('a' * 64)))
    }
}

AfterAll {
    Remove-Item Function:\global:az -ErrorAction SilentlyContinue
    Remove-Item Function:\global:docker -ErrorAction SilentlyContinue
    Remove-Variable -Name IFS_PUBLISH_AZ_CALLS, IFS_PUBLISH_DOCKER_CALLS -Scope Global -ErrorAction SilentlyContinue
}
