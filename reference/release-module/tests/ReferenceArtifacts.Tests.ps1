Describe "Outils de sortie de référence" {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
        $script:updateManifestPath = Join-Path $script:repoRoot "reference/tools/Update-ManifestExample.ps1"
        $script:determinismPath = Join-Path $script:repoRoot "reference/tools/Test-ReferenceDeterminism.ps1"
    }

    It "utilise France Central pour toutes les cibles du pilote" {
        $pilotRoot = Join-Path $script:repoRoot "reference/pilot/bicep-azdo"
        $targetFiles = @(
            Get-ChildItem -LiteralPath $pilotRoot -Filter "*.bicepparam" -File -Recurse
            Get-ChildItem -LiteralPath $pilotRoot -Filter "release.*.json" -File -Recurse
        )

        $targetFiles.Count | Should -BeGreaterThan 0
        foreach ($targetFile in $targetFiles) {
            $content = [IO.File]::ReadAllText($targetFile.FullName)
            $content | Should -Not -Match "(?i)northeurope|westeurope"
            if ($targetFile.Extension -eq '.json') {
                ($content | ConvertFrom-Json).location | Should -Be 'francecentral'
            }
            else {
                $locations = [regex]::Matches($content, "(?im)^\s*location:\s*'(?<region>[^']+)'\s*$")
                $locations.Count | Should -BeGreaterThan 0
                @($locations | Where-Object { $_.Groups['region'].Value -ne 'francecentral' }).Count | Should -Be 0
            }
        }

        $allPilotFiles = @(Get-ChildItem -LiteralPath $pilotRoot -File -Recurse -Force)
        foreach ($pilotFile in $allPilotFiles) {
            [IO.File]::ReadAllText($pilotFile.FullName) | Should -Not -Match "(?i)northeurope|westeurope"
        }

        $installPipeline = [IO.File]::ReadAllText((Join-Path $pilotRoot ".ifs/install/install.pipeline.yml"))
        $installLocations = [regex]::Matches($installPipeline, '(?i)--location\s+francecentral')
        $installLocations.Count | Should -Be 3
        [regex]::Matches($installPipeline, '(?m)^\s+BUILD_BUILDID:\s+\$\(Build\.BuildId\)\s*$').Count | Should -Be 2
    }

    It "configure les diagnostics SQL uniquement au niveau de la base" {
        $dataRoot = Join-Path $script:repoRoot "reference/pilot/bicep-azdo/data/infra"
        $main = [IO.File]::ReadAllText((Join-Path $dataRoot "main.bicep"))
        $types = [IO.File]::ReadAllText((Join-Path $dataRoot "types.bicep"))

        $main | Should -Match "diagnosticSettings: sqlDatabaseDiagnosticSettings"
        $main | Should -Not -Match "sqlOrdersDiagnosticsModule|server-diagnostics\.bicep"
        $types | Should -Match "databaseDiagnosticSettings: diagnosticSettingConfig\[\]"
        $types | Should -Not -Match "(?m)^\s*diagnosticSettings: diagnosticSettingConfig\[\]$"

        foreach ($target in @("dev", "prd")) {
            $parameters = [IO.File]::ReadAllText((Join-Path $dataRoot "main.$target.bicepparam"))
            $parameters | Should -Match "databaseDiagnosticSettings: \["
            $parameters | Should -Match "name: 'diag-sqldb-shop-orders-$target'"
            $parameters | Should -Not -Match "name: 'diag-sql-shop-orders-$target'"
        }
    }

    It "compose le FQDN SQL sans ajouter un point déjà fourni par le suffixe Azure" {
        $ordersMainPath = Join-Path $script:repoRoot "reference/pilot/bicep-azdo/orders/infra/main.bicep"
        $ordersMain = [IO.File]::ReadAllText($ordersMainPath)

        $ordersMain | Should -Match "\$\{externalResources\.dataSqlDatabase\.server\.name\}\$\{environment\(\)\.suffixes\.sqlServerHostname\}"
        $ordersMain | Should -Not -Match "server\.name\}\.\$\{environment\(\)\.suffixes\.sqlServerHostname"
    }

    It "garde les overlays de preuve en France Central" {
        $revisionRoot = Join-Path $script:repoRoot "reference/pilot/revisions"
        $patchFiles = @(Get-ChildItem -LiteralPath $revisionRoot -Filter "changes.patch" -File -Recurse)

        $patchFiles.Count | Should -BeGreaterThan 0
        foreach ($patchFile in $patchFiles) {
            $content = [IO.File]::ReadAllText($patchFile.FullName)
            $content | Should -Not -Match "(?i)northeurope|westeurope"
            foreach ($locationLine in [regex]::Matches($content, "(?im)^[ +-]\s*location:\s*'(?<region>[^']+)'\s*$")) {
                $locationLine.Groups['region'].Value | Should -Be 'francecentral'
            }
        }
    }

    It "recalcule un manifeste stable avec les fichiers et leurs SHA-256 triés" {
        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("ifs-manifest-" + [guid]::NewGuid().ToString("N"))
        $referenceRoot = Join-Path $tempRoot "reference"
        $outputRoot = Join-Path $referenceRoot "pilot/bicep-azdo"
        $manifestPath = Join-Path $referenceRoot "pilot/manifest.example.json"
        $encoding = [Text.UTF8Encoding]::new($false)
        try {
            $null = New-Item -ItemType Directory -Path (Join-Path $outputRoot "z") -Force
            $null = New-Item -ItemType Directory -Path (Join-Path $outputRoot "a") -Force
            $null = New-Item -ItemType Directory -Path (Join-Path $outputRoot ".ifs") -Force
            $firstContent = ConvertTo-Json -InputObject ([ordered]@{ a = 1 }) -Compress
            $lastContent = ConvertTo-Json -InputObject ([ordered]@{ b = 2 }) -Compress
            $hiddenContent = ConvertTo-Json -InputObject ([ordered]@{ hidden = $true }) -Compress
            [IO.File]::WriteAllText((Join-Path $outputRoot "z/last.json"), $lastContent + "`n", $encoding)
            [IO.File]::WriteAllText((Join-Path $outputRoot "a/first.json"), $firstContent + "`n", $encoding)
            [IO.File]::WriteAllText((Join-Path $outputRoot ".ifs/hidden.json"), $hiddenContent + "`n", $encoding)
            $null = New-Item -ItemType Directory -Path (Split-Path -Parent $manifestPath) -Force
            $manifestSeed = [ordered]@{
                schema = "ifs-manifest/v1"
                project = "shop"
                revision = 1
                generatedAtUtc = "2026-10-07T00:00:00Z"
                commit = "sample"
                files = @()
            }
            [IO.File]::WriteAllText($manifestPath, (ConvertTo-Json -InputObject $manifestSeed -Depth 10) + "`n", $encoding)

            & $script:updateManifestPath -ReferenceRoot $referenceRoot
            $firstRun = [IO.File]::ReadAllBytes($manifestPath)
            & $script:updateManifestPath -ReferenceRoot $referenceRoot
            $secondRun = [IO.File]::ReadAllBytes($manifestPath)
            $manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -AsHashtable

            [Convert]::ToHexString($secondRun) | Should -Be ([Convert]::ToHexString($firstRun))
            (@($manifest.files | ForEach-Object { $_.path }) -join ",") | Should -Be ".ifs/hidden.json,a/first.json,z/last.json"
            $manifest.files[0].sha256 | Should -Be (Get-FileHash (Join-Path $outputRoot ".ifs/hidden.json") -Algorithm SHA256).Hash.ToLowerInvariant()
            $manifest.files[1].sha256 | Should -Be (Get-FileHash (Join-Path $outputRoot "a/first.json") -Algorithm SHA256).Hash.ToLowerInvariant()
            $manifest.files[2].sha256 | Should -Be (Get-FileHash (Join-Path $outputRoot "z/last.json") -Algorithm SHA256).Hash.ToLowerInvariant()
            ($secondRun[0..2] -join ",") | Should -Not -Be "239,187,191"
            $secondRun[$secondRun.Length - 1] | Should -Be 10
        }
        finally {
            if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
        }
    }

    It "signale les écarts de BOM, de fins de ligne, de saut final et d'en-tête" {
        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("ifs-determinism-" + [guid]::NewGuid().ToString("N"))
        try {
            $null = New-Item -ItemType Directory -Path $tempRoot -Force
            $badBody = [Text.Encoding]::UTF8.GetBytes("# no generated header")
            [byte[]]$badBytes = [byte[]]@(239, 187, 191) + $badBody + [byte[]]@(13)
            [IO.File]::WriteAllBytes((Join-Path $tempRoot "bad.ps1"), $badBytes)
            { & $script:determinismPath -PipelineRoot $tempRoot } | Should -Throw
        }
        finally {
            if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
        }
    }

    It "vérifie aussi les fichiers dans les répertoires cachés" {
        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("ifs-hidden-output-" + [guid]::NewGuid().ToString("N"))
        $hiddenRoot = Join-Path $tempRoot ".ifs"
        try {
            $null = New-Item -ItemType Directory -Path $hiddenRoot -Force
            [IO.File]::WriteAllText((Join-Path $hiddenRoot "missing-header.ps1"), "Write-Output `"ok`"`n", [Text.UTF8Encoding]::new($false))
            $failure = $null
            try { & $script:determinismPath -PipelineRoot $tempRoot }
            catch { $failure = $_.Exception.Message }
            $failure | Should -Match "en-tête généré absent"
        }
        finally {
            if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
        }
    }
}
