BeforeAll {
    $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
    $script:scanImageScript = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/.ifs/templates/scripts/Invoke-IfsImageScan.ps1'
    $script:installTrivyScript = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/.ifs/templates/scripts/Install-IfsTrivy.ps1'

    function global:trivy {
        param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS = @($Arguments)
        $global:LASTEXITCODE = $global:IFS_IMAGE_SCAN_EXIT_CODE
        return ''
    }
}

Describe 'Invoke-IfsImageScan' {
    It 'rend Trivy disponible immédiatement et pour les tâches suivantes' {
        $installer = Get-Content -LiteralPath $script:installTrivyScript -Raw

        $installer | Should -Match '\$env:PATH\s*='
        $installer | Should -Match '##vso\[task\.prependpath\]'
    }

    BeforeEach {
        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS = @()
        $global:IFS_IMAGE_SCAN_EXIT_CODE = 0
        $script:fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-image-scan-' + [guid]::NewGuid().ToString('N'))
        [void][IO.Directory]::CreateDirectory((Join-Path $script:fixtureRoot 'security policy'))
        [IO.File]::WriteAllText((Join-Path $script:fixtureRoot 'security policy/.trivyignore'), "CVE-2026-12345`n")
    }

    AfterEach {
        if (Test-Path -LiteralPath $script:fixtureRoot -PathType Container) {
            Remove-Item -LiteralPath $script:fixtureRoot -Recurse -Force
        }
    }

    It 'passe le chemin .trivyignore au scanner comme argument distinct' {
        & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions 'security policy/.trivyignore'

        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -Contain '--ignorefile'
        $ignoreFileArgument = [array]::IndexOf([string[]]$global:IFS_IMAGE_SCAN_LAST_ARGUMENTS, '--ignorefile')
        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS[$ignoreFileArgument + 1] |
            Should -Be ([IO.Path]::GetFullPath((Join-Path $script:fixtureRoot 'security policy/.trivyignore')))
        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -Contain '--severity'
        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS[-1] | Should -Be 'ifs-app-api-123'
    }

    It 'omet --ignorefile sans exception configuree' {
        & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'CRITICAL' -Exceptions ''

        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -Not -Contain '--ignorefile'
    }

    It 'refuse un chemin qui remonte hors du dépôt avant de lancer Trivy' {
        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions '../.trivyignore' } |
            Should -Throw '*relatif*'

        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -BeNullOrEmpty
    }

    It 'refuse les chemins absolus' {
        $absolutePath = Join-Path $script:fixtureRoot 'security policy/.trivyignore'

        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions $absolutePath } |
            Should -Throw '*relatif*'
    }

    It 'refuse les chemins absolus avec un lecteur Windows' {
        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions 'C:/.trivyignore' } |
            Should -Throw '*relatif*'
    }

    It 'refuse un fichier absent' {
        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions 'missing/.trivyignore' } |
            Should -Throw '*introuvable*'
    }

    It 'refuse un fichier qui ne porte pas le nom .trivyignore' {
        [IO.File]::WriteAllText((Join-Path $script:fixtureRoot 'security policy/.trivyignore.yaml'), "vulnerabilities: []`n")

        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions 'security policy/.trivyignore.yaml' } |
            Should -Throw '*nom .trivyignore*'
    }

    It 'échoue quand Trivy retourne un code non nul' {
        $global:IFS_IMAGE_SCAN_EXIT_CODE = 2

        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions '' } |
            Should -Throw '*Trivy a échoué*'
    }
}

AfterAll {
    Remove-Item Function:\global:trivy -ErrorAction SilentlyContinue
    Remove-Variable -Name IFS_IMAGE_SCAN_LAST_ARGUMENTS, IFS_IMAGE_SCAN_EXIT_CODE -Scope Global -ErrorAction SilentlyContinue
}
