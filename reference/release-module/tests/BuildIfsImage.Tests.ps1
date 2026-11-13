BeforeAll {
    $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
    $script:buildImageScript = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/.ifs/templates/scripts/Build-IfsImage.ps1'

    function global:docker {
        param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
        [void]$global:IFS_BUILD_DOCKER_CALLS.Add($Arguments -join ' ')
        if ($Arguments.Count -gt 2 -and $Arguments[0] -eq 'buildx' -and $Arguments[1] -eq 'build') {
            $cacheIndex = [Array]::IndexOf($Arguments, '--cache-to') + 1
            if ($cacheIndex -gt 0 -and $Arguments[$cacheIndex] -match 'dest=([^,]+)') {
                [void][IO.Directory]::CreateDirectory($Matches[1])
            }
        }
        $global:LASTEXITCODE = 0
        return ''
    }

    function global:az {
        param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
        [void]$global:IFS_BUILD_AZ_CALLS.Add($Arguments -join ' ')
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

Describe 'Build-IfsImage sans accès Azure' {
    BeforeEach {
        $global:IFS_BUILD_AZ_CALLS = [Collections.Generic.List[string]]::new()
        $global:IFS_BUILD_DOCKER_CALLS = [Collections.Generic.List[string]]::new()
        $global:IFS_BUILD_TRIVY_CALLS = [Collections.Generic.List[string]]::new()
        $global:LASTEXITCODE = 0
        $script:priorBuildId = $env:BUILD_BUILDID
        $script:priorSourceVersion = $env:BUILD_SOURCEVERSION
        $env:BUILD_BUILDID = '123'
        $env:BUILD_SOURCEVERSION = '0123456789abcdef0123456789abcdef01234567'

        $script:fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-build-image-' + [guid]::NewGuid().ToString('N'))
        $script:contextRoot = Join-Path $script:fixtureRoot 'context'
        $script:cacheRoot = Join-Path $script:fixtureRoot 'cache'
        [void][IO.Directory]::CreateDirectory($script:contextRoot)
        [void][IO.Directory]::CreateDirectory($script:cacheRoot)
        [IO.File]::WriteAllText((Join-Path $script:contextRoot 'Dockerfile'), 'FROM scratch', [Text.UTF8Encoding]::new($false))
    }

    AfterEach {
        $env:BUILD_BUILDID = $script:priorBuildId
        $env:BUILD_SOURCEVERSION = $script:priorSourceVersion
        if (Test-Path -LiteralPath $script:fixtureRoot -PathType Container) {
            Remove-Item -LiteralPath $script:fixtureRoot -Recurse -Force
        }
    }

    It 'construit une image locale avec le cache Buildx sans scanner ni publier' {
        { & $script:buildImageScript -RepositoryRoot $script:fixtureRoot -BuildContext 'context' -DockerfilePath 'context/Dockerfile' -ImageName 'ifs-app:123' -CacheDirectory $script:cacheRoot } |
            Should -Not -Throw

        $global:IFS_BUILD_AZ_CALLS | Should -BeNullOrEmpty
        $global:IFS_BUILD_TRIVY_CALLS | Should -BeNullOrEmpty
        $buildCalls = @($global:IFS_BUILD_DOCKER_CALLS | Where-Object { $_ -like 'buildx build *' })
        $buildCalls | Should -HaveCount 1
        $buildCalls[0] | Should -Match ([regex]::Escape('--tag ifs-app:123'))
        $buildCalls[0] | Should -Match ([regex]::Escape('--cache-to type=local'))
        @($global:IFS_BUILD_DOCKER_CALLS | Where-Object { $_ -like 'push *' }) | Should -BeNullOrEmpty
    }
}

AfterAll {
    Remove-Item Function:\global:az -ErrorAction SilentlyContinue
    Remove-Item Function:\global:docker -ErrorAction SilentlyContinue
    Remove-Item Function:\global:trivy -ErrorAction SilentlyContinue
    Remove-Variable -Name IFS_BUILD_AZ_CALLS, IFS_BUILD_DOCKER_CALLS, IFS_BUILD_TRIVY_CALLS -Scope Global -ErrorAction SilentlyContinue
}
