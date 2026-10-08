Describe 'Profil de coût de publication P-08' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
        $script:proofToolsPath = Join-Path $script:repoRoot 'tools/proofs/ProofTools.psm1'
        $script:pilotRoot = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo'
        $script:profilePaths = @(
            'core/infra/main.dev.bicepparam'
            'core/infra/main.prd.bicepparam'
            'data/infra/main.dev.bicepparam'
            'data/infra/main.prd.bicepparam'
            'data/infra/main.bicep'
            'data/infra/types.bicep'
            'orders/infra/main.dev.bicepparam'
            'orders/infra/main.prd.bicepparam'
            'platform/infra/main.shared.bicepparam'
        )
        Import-Module $script:proofToolsPath -Force

        function Copy-PilotCostProfileFiles {
            param([Parameter(Mandatory)][string] $DestinationRoot)

            foreach ($relativePath in $script:profilePaths) {
                $sourcePath = Join-Path $script:pilotRoot $relativePath
                $destinationPath = Join-Path $DestinationRoot $relativePath
                $null = New-Item -ItemType Directory -Path (Split-Path -Parent $destinationPath) -Force
                Copy-Item -LiteralPath $sourcePath -Destination $destinationPath
            }
        }
    }

    It 'applique le profil minimal sur la copie publiée sans modifier la référence' {
        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-p08-cost-profile-' + [guid]::NewGuid().ToString('N'))
        try {
            Copy-PilotCostProfileFiles -DestinationRoot $tempRoot
            $referenceContent = @{}
            foreach ($relativePath in $script:profilePaths) {
                $referenceContent[$relativePath] = [IO.File]::ReadAllText((Join-Path $script:pilotRoot $relativePath))
            }

            $changes = @(Set-ProofCostProfile -BicepRoot $tempRoot)

            $acr = [IO.File]::ReadAllText((Join-Path $tempRoot 'platform/infra/main.shared.bicepparam'))
            $coreDev = [IO.File]::ReadAllText((Join-Path $tempRoot 'core/infra/main.dev.bicepparam'))
            $corePrd = [IO.File]::ReadAllText((Join-Path $tempRoot 'core/infra/main.prd.bicepparam'))
            $dataDev = [IO.File]::ReadAllText((Join-Path $tempRoot 'data/infra/main.dev.bicepparam'))
            $dataPrd = [IO.File]::ReadAllText((Join-Path $tempRoot 'data/infra/main.prd.bicepparam'))
            $dataMain = [IO.File]::ReadAllText((Join-Path $tempRoot 'data/infra/main.bicep'))
            $dataTypes = [IO.File]::ReadAllText((Join-Path $tempRoot 'data/infra/types.bicep'))
            $ordersDev = [IO.File]::ReadAllText((Join-Path $tempRoot 'orders/infra/main.dev.bicepparam'))
            $ordersPrd = [IO.File]::ReadAllText((Join-Path $tempRoot 'orders/infra/main.prd.bicepparam'))

            $acr | Should -Match "acrSku: 'Basic'"
            $acr | Should -Not -Match "acrSku: 'Standard'"
            foreach ($core in @($coreDev, $corePrd)) {
                $core | Should -Match "retentionInDays: 90"
                $core | Should -Match "dailyQuotaGb: '1'"
            }
            $coreDev | Should -Match 'dataRetentionDays: 30'
            $corePrd | Should -Match 'dataRetentionDays: 90'
            foreach ($data in @($dataDev, $dataPrd)) {
                $data | Should -Match "databaseSkuName: 'GP_S_Gen5_1'"
                $data | Should -Match 'autoPauseDelay: 15'
                $data | Should -Match "minCapacity: '0\.5'"
                $data | Should -Match 'zoneRedundant: false'
                $data | Should -Match 'useFreeLimit: false'
                $data | Should -Match "freeLimitExhaustionBehavior: 'AutoPause'"
            }
            foreach ($property in @(
                'autoPauseDelay: sqlOrders.autoPauseDelay'
                'minCapacity: sqlOrders.minCapacity'
                'zoneRedundant: sqlOrders.zoneRedundant'
                'useFreeLimit: sqlOrders.useFreeLimit'
                'freeLimitExhaustionBehavior: sqlOrders.freeLimitExhaustionBehavior'
            )) { $dataMain | Should -Match ([regex]::Escape($property)) }
            foreach ($property in @(
                'autoPauseDelay: int'
                'minCapacity: string'
                'zoneRedundant: bool'
                'useFreeLimit: bool'
                "freeLimitExhaustionBehavior: 'AutoPause' | 'BillOverUsage'"
            )) { $dataTypes | Should -Match ([regex]::Escape($property)) }
            foreach ($orders in @($ordersDev, $ordersPrd)) {
                $orders | Should -Match 'zoneRedundant: false'
                $orders | Should -Match 'minReplicas: 0'
                $orders | Should -Match 'maxReplicas: 1'
            }
            $profileText = @(
                foreach ($file in Get-ChildItem -LiteralPath $tempRoot -Recurse -File | Where-Object { $_.Extension -in @('.bicep', '.bicepparam') }) {
                    [IO.File]::ReadAllText($file.FullName)
                }
            ) -join "`n"
            $profileText | Should -Not -Match 'francecentral'
            $profileText | Should -Not -Match "databaseSkuName: 'GP_Gen5_2'"
            $profileText | Should -Not -Match "acrSku: 'Standard'"
            $changes.Count | Should -BeGreaterThan 0
            @($changes | Where-Object { $_.Changed }).Count | Should -BeGreaterThan 0
            @($changes | Where-Object { $_.Setting -like 'structure.*' }).Count | Should -BeGreaterThan 0
            @($changes | Where-Object { $_.Setting -like 'structure.*' -and $_.Changed }).Count | Should -BeGreaterThan 0

            [IO.File]::ReadAllText((Join-Path $script:pilotRoot 'platform/infra/main.shared.bicepparam')) | Should -Match "acrSku: 'Standard'"
            [IO.File]::ReadAllText((Join-Path $script:pilotRoot 'data/infra/main.prd.bicepparam')) | Should -Match "databaseSkuName: 'GP_Gen5_2'"
            [IO.File]::ReadAllText((Join-Path $script:pilotRoot 'orders/infra/main.prd.bicepparam')) | Should -Match 'zoneRedundant: true'
            foreach ($relativePath in $script:profilePaths) {
                [IO.File]::ReadAllText((Join-Path $script:pilotRoot $relativePath)) | Should -BeExactly $referenceContent[$relativePath]
            }
        }
        finally {
            if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
        }
    }

    It 'active le quota SQL gratuit uniquement en prd sur demande' {
        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-p08-sql-free-' + [guid]::NewGuid().ToString('N'))
        try {
            Copy-PilotCostProfileFiles -DestinationRoot $tempRoot
            Set-ProofCostProfile -BicepRoot $tempRoot | Out-Null
            [IO.File]::ReadAllText((Join-Path $tempRoot 'data/infra/main.prd.bicepparam')) | Should -Match 'useFreeLimit: false'
            Set-ProofCostProfile -BicepRoot $tempRoot -SqlFreeOffer | Out-Null

            $dataDev = [IO.File]::ReadAllText((Join-Path $tempRoot 'data/infra/main.dev.bicepparam'))
            $dataPrd = [IO.File]::ReadAllText((Join-Path $tempRoot 'data/infra/main.prd.bicepparam'))

            $dataDev | Should -Match 'useFreeLimit: false'
            $dataPrd | Should -Match 'useFreeLimit: true'
            $dataPrd | Should -Match "freeLimitExhaustionBehavior: 'AutoPause'"
        }
        finally {
            if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
        }
    }

    It 'ne laisse aucun fichier partiel dans le clone si le profil est invalide' {
        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-p08-publish-fail-' + [guid]::NewGuid().ToString('N'))
        $sourceRoot = Join-Path $tempRoot 'source'
        $targetRoot = Join-Path $tempRoot 'target'
        $publisherPath = Join-Path $sourceRoot 'tools/proofs/Publish-PilotReference.ps1'
        try {
            $null = New-Item -ItemType Directory -Path $sourceRoot, $targetRoot -Force
            $null = New-Item -ItemType Directory -Path (Join-Path $sourceRoot 'tools/proofs') -Force
            $null = New-Item -ItemType Directory -Path (Join-Path $sourceRoot 'reference/pilot') -Force
            $null = New-Item -ItemType Directory -Path (Join-Path $sourceRoot 'samples') -Force
            Copy-Item -LiteralPath (Join-Path $script:repoRoot 'tools/proofs/Publish-PilotReference.ps1') -Destination $publisherPath
            Copy-Item -LiteralPath (Join-Path $script:repoRoot 'tools/proofs/ProofTools.psm1') -Destination (Join-Path $sourceRoot 'tools/proofs/ProofTools.psm1')
            Copy-Item -LiteralPath (Join-Path $script:repoRoot 'reference/pilot/bicep-azdo') -Destination (Join-Path $sourceRoot 'reference/pilot/bicep-azdo') -Recurse
            Copy-Item -LiteralPath (Join-Path $script:repoRoot 'samples/witness-app') -Destination (Join-Path $sourceRoot 'samples/witness-app') -Recurse

            $invalidParamPath = Join-Path $sourceRoot 'reference/pilot/bicep-azdo/platform/infra/main.shared.bicepparam'
            $invalidText = [IO.File]::ReadAllText($invalidParamPath).Replace("acrSku: 'Standard'", "acrSku: 'Premium'")
            $invalidText | Should -Match "acrSku: 'Premium'"
            [IO.File]::WriteAllText($invalidParamPath, $invalidText, [Text.UTF8Encoding]::new($false))

            & git -C $sourceRoot init --quiet
            if ($LASTEXITCODE -ne 0) { throw 'Impossible d initialiser le depot source temporaire.' }
            & git -C $sourceRoot config user.email 'p08-test@example.invalid'
            & git -C $sourceRoot config user.name 'P08 test'
            & git -C $sourceRoot config core.autocrlf false
            & git -C $sourceRoot add --all
            if ($LASTEXITCODE -ne 0) { throw 'Impossible d ajouter le depot source temporaire.' }
            & git -C $sourceRoot commit --quiet -m 'fixture'
            if ($LASTEXITCODE -ne 0) { throw 'Impossible de creer le commit source temporaire.' }

            & git -C $targetRoot init --quiet
            if ($LASTEXITCODE -ne 0) { throw 'Impossible d initialiser le clone cible temporaire.' }
            & git -C $targetRoot config user.email 'p08-test@example.invalid'
            & git -C $targetRoot config user.name 'P08 test'
            & git -C $targetRoot config core.autocrlf false
            [IO.File]::WriteAllText((Join-Path $targetRoot 'README.md'), "clone intact`n", [Text.UTF8Encoding]::new($false))
            & git -C $targetRoot add --all
            if ($LASTEXITCODE -ne 0) { throw 'Impossible d ajouter le clone cible temporaire.' }
            & git -C $targetRoot commit --quiet -m 'fixture'
            if ($LASTEXITCODE -ne 0) { throw 'Impossible de creer le commit cible temporaire.' }

            $publishError = $null
            try {
                & $publisherPath -RepositoryPath $targetRoot -ProjectCode 'shop42' `
                    -SubscriptionDev '11111111-1111-4111-8111-111111111111' `
                    -SubscriptionPrd '22222222-2222-4222-8222-222222222222' `
                    -SubscriptionShared '33333333-3333-4333-8333-333333333333' `
                    -SqlAdminGroupObjectIdDev '44444444-4444-4444-8444-444444444444' `
                    -SqlAdminGroupObjectIdPrd '55555555-5555-4555-8555-555555555555' -Confirm:$false
            }
            catch { $publishError = $_ }

            $publishError | Should -Not -Be $null
            $publishError.Exception.Message | Should -Match 'valeur.*inattendue'
            $targetStatus = (& git -C $targetRoot status --porcelain=v1 --untracked-files=all | Out-String).Trim()
            $targetStatus | Should -Be ''
        }
        finally {
            $tempRootPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + [IO.Path]::DirectorySeparatorChar
            $fullTempRoot = [IO.Path]::GetFullPath($tempRoot)
            if ($fullTempRoot.StartsWith($tempRootPrefix, [StringComparison]::OrdinalIgnoreCase) -and
                (Split-Path -Leaf $fullTempRoot).StartsWith('ifs-p08-publish-fail-', [StringComparison]::Ordinal) -and
                (Test-Path -LiteralPath $fullTempRoot -PathType Container)) {
                Remove-Item -LiteralPath $fullTempRoot -Recurse -Force
            }
        }
    }

    It 'affiche chaque catégorie du profil en WhatIf sans modifier le clone' {
        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-p08-publish-whatif-' + [guid]::NewGuid().ToString('N'))
        $targetRoot = Join-Path $tempRoot 'target'
        $publisherPath = Join-Path $script:repoRoot 'tools/proofs/Publish-PilotReference.ps1'
        try {
            $null = New-Item -ItemType Directory -Path $targetRoot -Force
            & git -C $targetRoot init --quiet
            if ($LASTEXITCODE -ne 0) { throw 'Impossible d initialiser le clone WhatIf temporaire.' }

            $whatIfOutput = (& $publisherPath -RepositoryPath $targetRoot -ProjectCode 'shop42' `
                -SubscriptionDev '11111111-1111-4111-8111-111111111111' `
                -SubscriptionPrd '22222222-2222-4222-8222-222222222222' `
                -SubscriptionShared '33333333-3333-4333-8333-333333333333' `
                -SqlAdminGroupObjectIdDev '44444444-4444-4444-8444-444444444444' `
                -SqlAdminGroupObjectIdPrd '55555555-5555-4555-8555-555555555555' -WhatIf 6>&1 | Out-String)

            $whatIfOutput | Should -Match 'WhatIf OK'
            foreach ($setting in @('appiMain.retentionInDays', 'acrMain.acrSku', 'sqlOrders.databaseSkuName', 'caApi.maxReplicas', 'structure.sqlTypes')) {
                $whatIfOutput | Should -Match ([regex]::Escape($setting))
            }
            $targetStatus = (& git -C $targetRoot status --porcelain=v1 --untracked-files=all | Out-String).Trim()
            $targetStatus | Should -Be ''
            Test-Path -LiteralPath (Join-Path $targetRoot '.ifs/manifest.json') | Should -BeFalse
        }
        finally {
            $tempRootPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + [IO.Path]::DirectorySeparatorChar
            $fullTempRoot = [IO.Path]::GetFullPath($tempRoot)
            if ($fullTempRoot.StartsWith($tempRootPrefix, [StringComparison]::OrdinalIgnoreCase) -and
                (Split-Path -Leaf $fullTempRoot).StartsWith('ifs-p08-publish-whatif-', [StringComparison]::Ordinal) -and
                (Test-Path -LiteralPath $fullTempRoot -PathType Container)) {
                Remove-Item -LiteralPath $fullTempRoot -Recurse -Force
            }
        }
    }

    It 'retire les dossiers crees pendant une publication interrompue' {
        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-p08-publish-rollback-' + [guid]::NewGuid().ToString('N'))
        $targetRoot = Join-Path $tempRoot 'target'
        $publisherPath = Join-Path $script:repoRoot 'tools/proofs/Publish-PilotReference.ps1'
        try {
            $null = New-Item -ItemType Directory -Path $targetRoot -Force
            & git -C $targetRoot init --quiet
            if ($LASTEXITCODE -ne 0) { throw 'Impossible d initialiser le clone de rollback temporaire.' }
            & git -C $targetRoot config user.email 'p08-test@example.invalid'
            & git -C $targetRoot config user.name 'P08 test'
            [IO.File]::WriteAllText((Join-Path $targetRoot 'core'), 'collision fixture', [Text.UTF8Encoding]::new($false))
            & git -C $targetRoot add --all
            if ($LASTEXITCODE -ne 0) { throw 'Impossible d ajouter le fichier de collision.' }
            & git -C $targetRoot commit --quiet -m 'fixture'
            if ($LASTEXITCODE -ne 0) { throw 'Impossible de creer le commit avec collision.' }

            $publishError = $null
            try {
                & $publisherPath -RepositoryPath $targetRoot -ProjectCode 'shop42' `
                    -SubscriptionDev '11111111-1111-4111-8111-111111111111' `
                    -SubscriptionPrd '22222222-2222-4222-8222-222222222222' `
                    -SubscriptionShared '33333333-3333-4333-8333-333333333333' `
                    -SqlAdminGroupObjectIdDev '44444444-4444-4444-8444-444444444444' `
                    -SqlAdminGroupObjectIdPrd '55555555-5555-4555-8555-555555555555' -Confirm:$false
            }
            catch { $publishError = $_ }

            $publishError | Should -Not -Be $null
            $targetStatus = (& git -C $targetRoot status --porcelain=v1 --untracked-files=all | Out-String).Trim()
            $targetStatus | Should -Be ''
            Test-Path -LiteralPath (Join-Path $targetRoot '.ifs') | Should -BeFalse
            Test-Path -LiteralPath (Join-Path $targetRoot 'README.ifs.md') | Should -BeFalse
            Test-Path -LiteralPath (Join-Path $targetRoot 'core') -PathType Leaf | Should -BeTrue
        }
        finally {
            $tempRootPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + [IO.Path]::DirectorySeparatorChar
            $fullTempRoot = [IO.Path]::GetFullPath($tempRoot)
            if ($fullTempRoot.StartsWith($tempRootPrefix, [StringComparison]::OrdinalIgnoreCase) -and
                (Split-Path -Leaf $fullTempRoot).StartsWith('ifs-p08-publish-rollback-', [StringComparison]::Ordinal) -and
                (Test-Path -LiteralPath $fullTempRoot -PathType Container)) {
                Remove-Item -LiteralPath $fullTempRoot -Recurse -Force
            }
        }
    }

    It 'applique les overlays au clone publie et profile dans leur filiation attendue' {
        $publisherPath = Join-Path $script:repoRoot 'tools/proofs/Publish-PilotReference.ps1'
        $revisionPath = Join-Path $script:repoRoot 'tools/proofs/New-ProofRevision.ps1'
        $subscriptionDev = '11111111-1111-4111-8111-111111111111'
        $subscriptionPrd = '22222222-2222-4222-8222-222222222222'
        $subscriptionShared = '33333333-3333-4333-8333-333333333333'
        $adminGroupDev = '44444444-4444-4444-8444-444444444444'
        $adminGroupPrd = '55555555-5555-4555-8555-555555555555'
        $cloneRoots = [System.Collections.Generic.List[string]]::new()

        function New-PublishedCostProfileClone {
            $cloneRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-p08-overlay-' + [guid]::NewGuid().ToString('N'))
            [void][IO.Directory]::CreateDirectory($cloneRoot)
            $cloneRoots.Add($cloneRoot)

            & git -C $cloneRoot init --quiet
            if ($LASTEXITCODE -ne 0) { throw 'Impossible d initialiser le depot temporaire des overlays.' }

            $null = & $publisherPath -RepositoryPath $cloneRoot -ProjectCode 'shop42' `
                -SubscriptionDev $subscriptionDev -SubscriptionPrd $subscriptionPrd -SubscriptionShared $subscriptionShared `
                -SqlAdminGroupObjectIdDev $adminGroupDev -SqlAdminGroupObjectIdPrd $adminGroupPrd -Confirm:$false
            return $cloneRoot
        }

        function Apply-PublishedCostProfileRevision {
            param(
                [Parameter(Mandatory)][string] $CloneRoot,
                [Parameter(Mandatory)][string] $Revision
            )

            $null = & $revisionPath -RepositoryPath $CloneRoot -Revision $Revision -Confirm:$false
        }

        function Assert-PublishedCostProfile {
            param(
                [Parameter(Mandatory)][string] $CloneRoot,
                [int] $ExpectedMaxReplicas = 1
            )

            $acr = [IO.File]::ReadAllText((Join-Path $CloneRoot 'platform/infra/main.shared.bicepparam'))
            $core = [IO.File]::ReadAllText((Join-Path $CloneRoot 'core/infra/main.prd.bicepparam'))
            $data = [IO.File]::ReadAllText((Join-Path $CloneRoot 'data/infra/main.prd.bicepparam'))
            $orders = [IO.File]::ReadAllText((Join-Path $CloneRoot 'orders/infra/main.prd.bicepparam'))

            $acr | Should -Match "acrSku: 'Basic'"
            $core | Should -Match 'retentionInDays: 90'
            $core | Should -Match "dailyQuotaGb: '1'"
            $core | Should -Match 'dataRetentionDays: 90'
            $data | Should -Match "databaseSkuName: 'GP_S_Gen5_1'"
            $data | Should -Match 'useFreeLimit: false'
            $orders | Should -Match 'zoneRedundant: false'
            $orders | Should -Match "maxReplicas: $ExpectedMaxReplicas"
        }

        try {
            $p2Clone = New-PublishedCostProfileClone
            Assert-PublishedCostProfile -CloneRoot $p2Clone
            Apply-PublishedCostProfileRevision -CloneRoot $p2Clone -Revision 'p2'
            Assert-PublishedCostProfile -CloneRoot $p2Clone
            $p2Manifest = Get-Content -LiteralPath (Join-Path $p2Clone '.ifs/manifest.json') -Raw | ConvertFrom-Json
            $p2Manifest.revision | Should -Be 2
            $p2Manifest.proofLineage | Should -Be 'p2'
            $p2Bicep = [IO.File]::ReadAllText((Join-Path $p2Clone 'orders/infra/main.bicep'))
            $p2Bicep | Should -Not -Match 'logAnalyticsReaderRoleId'
            $p2Bicep | Should -Not -Match 'module logAnalyticsReader '

            Apply-PublishedCostProfileRevision -CloneRoot $p2Clone -Revision '3'
            Assert-PublishedCostProfile -CloneRoot $p2Clone
            $rev3Manifest = Get-Content -LiteralPath (Join-Path $p2Clone '.ifs/manifest.json') -Raw | ConvertFrom-Json
            $rev3Manifest.revision | Should -Be 3
            [IO.File]::ReadAllText((Join-Path $p2Clone 'orders/infra/main.prd.bicepparam')) | Should -Match "extraIdentity = \{[\s\S]*?deploy: true[\s\S]*?name: 'id-shop42-extra-prd'"

            Apply-PublishedCostProfileRevision -CloneRoot $p2Clone -Revision '4'
            Assert-PublishedCostProfile -CloneRoot $p2Clone
            $rev4Manifest = Get-Content -LiteralPath (Join-Path $p2Clone '.ifs/manifest.json') -Raw | ConvertFrom-Json
            $rev4Manifest.revision | Should -Be 4
            [IO.File]::ReadAllText((Join-Path $p2Clone 'orders/infra/main.prd.bicepparam')) | Should -Not -Match 'extraIdentity'

            Apply-PublishedCostProfileRevision -CloneRoot $p2Clone -Revision '5'
            Assert-PublishedCostProfile -CloneRoot $p2Clone -ExpectedMaxReplicas 2

            $p2Manifest = Get-Content -LiteralPath (Join-Path $p2Clone '.ifs/manifest.json') -Raw | ConvertFrom-Json
            $p2Manifest.revision | Should -Be 5
            $p2Manifest.proofLineage | Should -Be 'revisioned'
            [IO.File]::ReadAllText((Join-Path $p2Clone 'orders/infra/main.prd.bicepparam')) | Should -Match 'maxReplicas: 2'

            $rev2Clone = New-PublishedCostProfileClone
            Assert-PublishedCostProfile -CloneRoot $rev2Clone
            Apply-PublishedCostProfileRevision -CloneRoot $rev2Clone -Revision '2'

            $rev2Manifest = Get-Content -LiteralPath (Join-Path $rev2Clone '.ifs/manifest.json') -Raw | ConvertFrom-Json
            $rev2Manifest.revision | Should -Be 2
            [IO.File]::ReadAllText((Join-Path $rev2Clone 'core/infra/main.prd.bicepparam')) | Should -Match 'dataRetentionDays: 120'

            $roleClone = New-PublishedCostProfileClone
            Assert-PublishedCostProfile -CloneRoot $roleClone
            Apply-PublishedCostProfileRevision -CloneRoot $roleClone -Revision 'p3-role'
            Assert-PublishedCostProfile -CloneRoot $roleClone

            $roleManifest = Get-Content -LiteralPath (Join-Path $roleClone '.ifs/manifest.json') -Raw | ConvertFrom-Json
            $roleManifest.proofLineage | Should -Be 'p3-role'
            $roleBicep = [IO.File]::ReadAllText((Join-Path $roleClone 'orders/infra/main.bicep'))
            $roleBicep | Should -Match 'logAnalyticsDataReaderRoleId'
            $roleBicep | Should -Not -Match 'logAnalyticsReaderRoleId'
        }
        finally {
            $tempRootPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)) + [IO.Path]::DirectorySeparatorChar
            foreach ($cloneRoot in $cloneRoots) {
                $fullCloneRoot = [IO.Path]::GetFullPath($cloneRoot)
                if ($fullCloneRoot.StartsWith($tempRootPrefix, [StringComparison]::OrdinalIgnoreCase) -and
                    (Split-Path -Leaf $fullCloneRoot).StartsWith('ifs-p08-overlay-', [StringComparison]::Ordinal) -and
                    (Test-Path -LiteralPath $fullCloneRoot -PathType Container)) {
                    Remove-Item -LiteralPath $fullCloneRoot -Recurse -Force
                }
            }
        }
    }

    It 'est idempotent quand le profil est applique plusieurs fois' {
        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-p08-cost-idempotent-' + [guid]::NewGuid().ToString('N'))
        try {
            Copy-PilotCostProfileFiles -DestinationRoot $tempRoot
            $firstPass = @(Set-ProofCostProfile -BicepRoot $tempRoot)
            $firstContent = @{}
            foreach ($relativePath in $script:profilePaths) {
                $firstContent[$relativePath] = [IO.File]::ReadAllText((Join-Path $tempRoot $relativePath))
            }

            $secondPass = @(Set-ProofCostProfile -BicepRoot $tempRoot)

            foreach ($relativePath in $script:profilePaths) {
                [IO.File]::ReadAllText((Join-Path $tempRoot $relativePath)) | Should -BeExactly $firstContent[$relativePath]
            }
            @($firstPass | Where-Object { $_.Changed }).Count | Should -BeGreaterThan 0
            @($secondPass | Where-Object { $_.Changed }).Count | Should -Be 0
        }
        finally {
            if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
        }
    }

    It 'normalise les fichiers publies en UTF-8 sans BOM et en LF' {
        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-p08-cost-encoding-' + [guid]::NewGuid().ToString('N'))
        try {
            Copy-PilotCostProfileFiles -DestinationRoot $tempRoot
            foreach ($relativePath in $script:profilePaths) {
                $path = Join-Path $tempRoot $relativePath
                $text = [IO.File]::ReadAllText($path).Replace("`n", "`r`n")
                [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($true))
            }

            Set-ProofCostProfile -BicepRoot $tempRoot | Out-Null

            foreach ($relativePath in $script:profilePaths) {
                $path = Join-Path $tempRoot $relativePath
                $bytes = [IO.File]::ReadAllBytes($path)
                ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) | Should -BeFalse
                [IO.File]::ReadAllText($path) | Should -Not -Match "`r"
            }
        }
        finally {
            if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
        }
    }

    It 'ne laisse aucune ecriture partielle si une valeur SQL est inattendue' {
        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('ifs-p08-cost-fail-closed-' + [guid]::NewGuid().ToString('N'))
        try {
            Copy-PilotCostProfileFiles -DestinationRoot $tempRoot
            $invalidPath = Join-Path $tempRoot 'data/infra/main.prd.bicepparam'
            $invalidText = [IO.File]::ReadAllText($invalidPath).Replace("databaseSkuName: 'GP_Gen5_2'", "databaseSkuName: 'GP_S_Gen5_2'")
            $invalidText | Should -Match "databaseSkuName: 'GP_S_Gen5_2'"
            [IO.File]::WriteAllText($invalidPath, $invalidText, [Text.UTF8Encoding]::new($false))

            $before = @{}
            foreach ($relativePath in $script:profilePaths) {
                $before[$relativePath] = [IO.File]::ReadAllText((Join-Path $tempRoot $relativePath))
            }

            { Set-ProofCostProfile -BicepRoot $tempRoot } | Should -Throw '*valeur*inattendue*'

            foreach ($relativePath in $script:profilePaths) {
                [IO.File]::ReadAllText((Join-Path $tempRoot $relativePath)) | Should -BeExactly $before[$relativePath]
            }
        }
        finally {
            if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
        }
    }
}
