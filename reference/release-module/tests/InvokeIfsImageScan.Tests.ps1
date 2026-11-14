BeforeAll {
    $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
    $script:scanImageScript = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/.ifs/templates/scripts/Invoke-IfsImageScan.ps1'
    $script:installTrivyScript = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo/.ifs/templates/scripts/Install-IfsTrivy.ps1'

    function global:trivy {
        param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS = @($Arguments)
        $global:LASTEXITCODE = $global:IFS_IMAGE_SCAN_EXIT_CODE
        $global:IFS_IMAGE_SCAN_LAST_CWD = (Get-Location).Path
        $global:IFS_IMAGE_SCAN_CWD_HAS_CONFIG = Test-Path -LiteralPath (Join-Path $global:IFS_IMAGE_SCAN_LAST_CWD 'trivy.yaml')
        $global:IFS_IMAGE_SCAN_CWD_HAS_IGNORE_FILE = Test-Path -LiteralPath (Join-Path $global:IFS_IMAGE_SCAN_LAST_CWD '.trivyignore')
        $global:IFS_IMAGE_SCAN_TRIVY_CONFIG = $env:TRIVY_CONFIG
        $global:IFS_IMAGE_SCAN_TRIVY_IGNOREFILE = $env:TRIVY_IGNOREFILE
        return $global:IFS_IMAGE_SCAN_OUTPUT
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
        $global:IFS_IMAGE_SCAN_OUTPUT = @()
        $global:IFS_IMAGE_SCAN_LAST_CWD = ''
        $global:IFS_IMAGE_SCAN_CWD_HAS_CONFIG = $false
        $global:IFS_IMAGE_SCAN_CWD_HAS_IGNORE_FILE = $false
        $global:IFS_IMAGE_SCAN_TRIVY_CONFIG = $null
        $global:IFS_IMAGE_SCAN_TRIVY_IGNOREFILE = $null
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
        $imageSourceArgument = [array]::IndexOf([string[]]$global:IFS_IMAGE_SCAN_LAST_ARGUMENTS, '--image-src')
        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS[$ignoreFileArgument + 1] |
            Should -Be ([IO.Path]::GetFullPath((Join-Path $script:fixtureRoot 'security policy/.trivyignore')))
        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -Contain '--severity'
        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -Contain '--image-src'
        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS[$imageSourceArgument + 1] | Should -Be 'docker'
        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS[-1] | Should -Be 'ifs-app-api-123'
    }

    It 'refuse un nom application invalide pour image' {
        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api/other' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions '' } |
            Should -Throw

        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -BeNullOrEmpty
    }

    It 'refuse les majuscules dans le nom application' {
        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'Api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions '' } |
            Should -Throw

        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -BeNullOrEmpty
    }

    It 'refuse un saut de ligne final dans le nom application' {
        $application = "api`n"

        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application $application -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions '' } |
            Should -Throw

        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -BeNullOrEmpty
    }

    It 'refuse un saut de ligne final dans le Build ID' {
        $buildId = "123`n"

        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId $buildId -MinimumSeverity 'HIGH' -Exceptions '' } |
            Should -Throw

        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -BeNullOrEmpty
    }

    It 'refuse un identifiant de build avec un caractère interdit' {
        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123:latest' -MinimumSeverity 'HIGH' -Exceptions '' } |
            Should -Throw

        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -BeNullOrEmpty
    }

    It 'refuse les majuscules dans le nom de dépôt image du build' {
        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '1A' -MinimumSeverity 'HIGH' -Exceptions '' } |
            Should -Throw

        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -BeNullOrEmpty
    }

    It 'isole Trivy des configurations et exceptions présentes dans le dépôt' {
        [IO.File]::WriteAllText((Join-Path $script:fixtureRoot 'trivy.yaml'), "exit-code: 0`n")
        [IO.File]::WriteAllText((Join-Path $script:fixtureRoot '.trivyignore'), "CVE-2026-12345`n")
        Push-Location -LiteralPath $script:fixtureRoot
        try {
            & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'CRITICAL' -Exceptions ''
        }
        finally {
            Pop-Location
        }

        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -Not -Contain '--ignorefile'
        $global:IFS_IMAGE_SCAN_LAST_CWD | Should -Not -Be $script:fixtureRoot
        $global:IFS_IMAGE_SCAN_CWD_HAS_CONFIG | Should -BeFalse
        $global:IFS_IMAGE_SCAN_CWD_HAS_IGNORE_FILE | Should -BeFalse
        Test-Path -LiteralPath $global:IFS_IMAGE_SCAN_LAST_CWD | Should -BeFalse
    }

    It 'ignore les variables Trivy héritées pendant le scan puis les restaure' {
        $env:TRIVY_CONFIG = Join-Path $script:fixtureRoot 'trivy.yaml'
        $env:TRIVY_IGNOREFILE = Join-Path $script:fixtureRoot '.trivyignore'

        try {
            & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions ''

            $global:IFS_IMAGE_SCAN_TRIVY_CONFIG | Should -BeNullOrEmpty
            $global:IFS_IMAGE_SCAN_TRIVY_IGNOREFILE | Should -BeNullOrEmpty
            $env:TRIVY_CONFIG | Should -Be (Join-Path $script:fixtureRoot 'trivy.yaml')
            $env:TRIVY_IGNOREFILE | Should -Be (Join-Path $script:fixtureRoot '.trivyignore')
        }
        finally {
            Remove-Item Env:TRIVY_CONFIG, Env:TRIVY_IGNOREFILE -ErrorAction SilentlyContinue
        }
    }

    It 'transmet le seuil de sévérité comme une liste cumulative à Trivy' {
        $expectedSeverities = @(
            @{ Minimum = 'CRITICAL'; Expected = 'CRITICAL' },
            @{ Minimum = 'HIGH'; Expected = 'CRITICAL,HIGH' },
            @{ Minimum = 'MEDIUM'; Expected = 'CRITICAL,HIGH,MEDIUM' },
            @{ Minimum = 'LOW'; Expected = 'CRITICAL,HIGH,MEDIUM,LOW' }
        )

        foreach ($severity in $expectedSeverities) {
            & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity $severity.Minimum -Exceptions ''
            $severityArgument = [array]::IndexOf([string[]]$global:IFS_IMAGE_SCAN_LAST_ARGUMENTS, '--severity')
            $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS[$severityArgument + 1] | Should -Be $severity.Expected
        }
    }

    It 'affiche le rapport Trivy apres un scan réussi' {
        $global:IFS_IMAGE_SCAN_OUTPUT = @('Scan report line one', 'Scan report line two')

        $result = & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions ''

        $result | Should -Contain 'Scan report line one'
        $result | Should -Contain 'Scan report line two'
    }

    It 'refuse exceptions avec espaces seuls' {
        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions ' ' } |
            Should -Throw '*espace*'

        $global:IFS_IMAGE_SCAN_LAST_ARGUMENTS | Should -BeNullOrEmpty
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

    It 'refuse le nom .TRIVYIGNORE avec une casse différente' {
        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions 'security policy/.TRIVYIGNORE' } |
            Should -Throw '*nom .trivyignore*'
    }

    It 'refuse les séparateurs antislash dans le chemin exceptions' {
        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions 'security\ policy/.trivyignore' } |
            Should -Throw '*relatif*'
    }

    It 'refuse les segments vides dans le chemin exceptions' {
        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions 'security//.trivyignore' } |
            Should -Throw '*segments vides*'
    }

    It 'refuse les segments point dans le chemin exceptions' {
        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions 'security/./.trivyignore' } |
            Should -Throw '*segments vides*'
    }

    It 'refuse un dossier utilisé comme fichier exceptions' {
        [void][IO.Directory]::CreateDirectory((Join-Path $script:fixtureRoot 'folder/.trivyignore'))

        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions 'folder/.trivyignore' } |
            Should -Throw '*dossier*'
    }

    It 'refuse un fichier exceptions symbolique' {
        $targetPath = Join-Path $script:fixtureRoot 'security policy/.trivyignore'
        $linkPath = Join-Path $script:fixtureRoot 'linked-ignore/.trivyignore'
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $linkPath))
        try {
            [void][IO.File]::CreateSymbolicLink($linkPath, $targetPath)
        }
        catch {
            Set-ItResult -Skipped -Because 'la création de liens symboliques est indisponible pour cet utilisateur'
            return
        }

        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions 'linked-ignore/.trivyignore' } |
            Should -Throw '*symbolique*'
    }

    It 'refuse un dossier intermédiaire symbolique' {
        $linkPath = Join-Path $script:fixtureRoot 'linked-security'
        try {
            [void][IO.Directory]::CreateSymbolicLink($linkPath, (Join-Path $script:fixtureRoot 'security policy'))
        }
        catch {
            Set-ItResult -Skipped -Because 'la création de liens symboliques est indisponible pour cet utilisateur'
            return
        }

        { & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions 'linked-security/.trivyignore' } |
            Should -Throw '*symbolique*'
    }

    It 'échoue quand Trivy retourne un code non nul' {
        $global:IFS_IMAGE_SCAN_EXIT_CODE = 2
        $global:IFS_IMAGE_SCAN_OUTPUT = @('Trivy first line', 'Trivy second line')

        $exception = $null
        try {
            & $script:scanImageScript -RepositoryRoot $script:fixtureRoot -Application 'api' -BuildId '123' -MinimumSeverity 'HIGH' -Exceptions ''
        }
        catch {
            $exception = $_.Exception
        }

        $exception | Should -Not -BeNullOrEmpty
        $exception.Message | Should -Match 'Trivy first line\r?\nTrivy second line'
    }
}

AfterAll {
    Remove-Item Function:\global:trivy -ErrorAction SilentlyContinue
    Remove-Variable -Name IFS_IMAGE_SCAN_LAST_ARGUMENTS, IFS_IMAGE_SCAN_EXIT_CODE, IFS_IMAGE_SCAN_OUTPUT, IFS_IMAGE_SCAN_LAST_CWD, IFS_IMAGE_SCAN_CWD_HAS_CONFIG, IFS_IMAGE_SCAN_CWD_HAS_IGNORE_FILE, IFS_IMAGE_SCAN_TRIVY_CONFIG, IFS_IMAGE_SCAN_TRIVY_IGNOREFILE -Scope Global -ErrorAction SilentlyContinue
}
