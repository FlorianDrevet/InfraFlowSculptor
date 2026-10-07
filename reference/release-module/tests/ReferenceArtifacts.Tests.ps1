Describe "Outils de sortie de référence" {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
        $script:updateManifestPath = Join-Path $script:repoRoot "reference/tools/Update-ManifestExample.ps1"
        $script:determinismPath = Join-Path $script:repoRoot "reference/tools/Test-ReferenceDeterminism.ps1"
    }

    It "utilise North Europe pour toutes les cibles du pilote" {
        $pilotRoot = Join-Path $script:repoRoot "reference/pilot/bicep-azdo"
        $targetFiles = @(
            Get-ChildItem -LiteralPath $pilotRoot -Filter "*.bicepparam" -File -Recurse
            Get-ChildItem -LiteralPath $pilotRoot -Filter "release.*.json" -File -Recurse
        )

        $targetFiles.Count | Should -BeGreaterThan 0
        foreach ($targetFile in $targetFiles) {
            $content = [IO.File]::ReadAllText($targetFile.FullName)
            $content | Should -Not -Match "(?i)francecentral"
            $content | Should -Match "(?i)northeurope"
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
