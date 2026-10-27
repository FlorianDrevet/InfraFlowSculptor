Describe 'Comparateur hors ligne des aperçus IFS' {
    BeforeAll {
        $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
        $modulePath = Join-Path $repoRoot 'tools/proofs/Compare-IfsPreviews.psm1'
        Import-Module $modulePath -Force
    }

    BeforeEach {
        $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
        Import-Module (Join-Path $repoRoot 'tools/proofs/Compare-IfsPreviews.psm1') -Force
    }

    BeforeAll {
    function New-SyntheticPreview {
        param(
            [string] $Fingerprint = ('a' * 64),
            [string] $GeneratedAt = '2026-10-17T10:00:00Z',
            [object[]] $Modified = @(),
            [object[]] $Created = @()
        )

        return [ordered]@{
            schema = 'ifs-preview/v1'
            project = 'shop42-test'
            component = 'orders'
            target = 'dev'
            unit = 'ifs-shop42-test-orders-dev'
            generatedAt = $GeneratedAt
            fingerprint = $Fingerprint
            pendingOperations = @()
            created = $Created
            modified = $Modified
            recreated = @()
            detachedOrDeleted = @()
            revokedAccess = @()
            dataWrites = @()
            appOwnedState = @()
            limitations = @()
        }
    }

    function Write-SyntheticPreview {
        param([Parameter(Mandatory)][object] $Preview)

        $path = Join-Path $TestDrive ('preview-{0}.json' -f [guid]::NewGuid().ToString('N'))
        $json = ConvertTo-Json -InputObject $Preview -Depth 100
        [IO.File]::WriteAllText($path, $json, [Text.UTF8Encoding]::new($false))
        return $path
    }

    function New-SyntheticChange {
        param(
            [string] $ResourceId = '/subscriptions/test/resourceGroups/rg/providers/Test/widgets/widget-a',
            [string] $ChangeType = 'Modify',
            [object[]] $Delta = @(
                [ordered]@{
                    path = 'properties.setting'
                    propertyChangeType = 'Modify'
                    before = 'old-value'
                    after = 'new-value'
                    children = $null
                }
            )
        )

        return [ordered]@{
            resourceId = $ResourceId
            changeType = $ChangeType
            before = @{ properties = @{ setting = 'old-value' } }
            after = @{ properties = @{ setting = 'new-value' } }
            delta = $Delta
        }
    }
    }

    It 'ignore l ordre des ressources, des deltas et generatedAt quand les empreintes et effets sont identiques' {
        $first = New-SyntheticPreview -Fingerprint ('a' * 64) -GeneratedAt '2026-10-17T10:00:00Z' -Modified @(
            (New-SyntheticChange -ResourceId '/subscriptions/test/resourceGroups/rg/providers/Test/widgets/a'),
            (New-SyntheticChange -ResourceId '/subscriptions/test/resourceGroups/rg/providers/Test/widgets/b')
        )
        $second = New-SyntheticPreview -Fingerprint ('a' * 64) -GeneratedAt '2026-10-17T10:15:00Z' -Modified @(
            (New-SyntheticChange -ResourceId '/subscriptions/test/resourceGroups/rg/providers/Test/widgets/b'),
            (New-SyntheticChange -ResourceId '/subscriptions/test/resourceGroups/rg/providers/Test/widgets/a')
        )
        $extraDelta = [ordered]@{
            path = 'properties.mode'
            propertyChangeType = 'Modify'
            before = 'safe'
            after = 'fast'
            children = $null
        }
        $first.modified[0].delta += $extraDelta
        $second.modified[1].delta += $extraDelta
        $second.modified[1].delta = @($second.modified[1].delta) | Sort-Object path -Descending

        $result = Compare-IfsPreviews -LeftPath (Write-SyntheticPreview $first) -RightPath (Write-SyntheticPreview $second)

        $result.Verdict | Should -Be 'Identique'
        $result.Differences | Should -HaveCount 0
    }

    It 'classe une ressource ajoutee ou retiree comme divergence de ressources sans exposer son identifiant' {
        $left = New-SyntheticPreview -Fingerprint ('a' * 64) -Created @(
            (New-SyntheticChange -ResourceId '/subscriptions/test/resourceGroups/rg/providers/Test/widgets/left' -ChangeType 'Create')
        )
        $right = New-SyntheticPreview -Fingerprint ('b' * 64)

        $result = Compare-IfsPreviews -LeftPath (Write-SyntheticPreview $left) -RightPath (Write-SyntheticPreview $right)

        $result.Verdict | Should -Be 'Divergent'
        $result.Classifications | Should -Contain 'Resources'
        ($result | ConvertTo-Json -Depth 10) | Should -Not -Match '/subscriptions/test'
    }

    It 'classe un type de changement différent' {
        $left = New-SyntheticPreview -Fingerprint ('a' * 64) -Modified @((New-SyntheticChange -ChangeType 'Modify'))
        $right = New-SyntheticPreview -Fingerprint ('b' * 64) -Modified @((New-SyntheticChange -ChangeType 'Delete'))

        $result = Compare-IfsPreviews -LeftPath (Write-SyntheticPreview $left) -RightPath (Write-SyntheticPreview $right)

        $result.Verdict | Should -Be 'Divergent'
        $result.Classifications | Should -Contain 'ChangeType'
    }

    It 'signale les chemins de delta dont la valeur change' {
        $left = New-SyntheticPreview -Fingerprint ('a' * 64) -Modified @((New-SyntheticChange))
        $changedDelta = @(
            [ordered]@{
                path = 'properties.setting'
                propertyChangeType = 'Modify'
                before = 'old-value'
                after = 'different-value'
                children = $null
            }
        )
        $right = New-SyntheticPreview -Fingerprint ('b' * 64) -Modified @((New-SyntheticChange -Delta $changedDelta))

        $result = Compare-IfsPreviews -LeftPath (Write-SyntheticPreview $left) -RightPath (Write-SyntheticPreview $right)

        $result.Verdict | Should -Be 'Divergent'
        $result.Classifications | Should -Contain 'PropertyDelta'
        $result.Differences.path | Should -Contain 'properties.setting'
    }

    It 'compare directement les empreintes de production' {
        $left = New-SyntheticPreview -Fingerprint ('a' * 64)
        $right = New-SyntheticPreview -Fingerprint ('b' * 64)

        $result = Compare-IfsPreviews -LeftPath (Write-SyntheticPreview $left) -RightPath (Write-SyntheticPreview $right)

        $result.Verdict | Should -Be 'Divergent'
        $result.Classifications | Should -Contain 'Fingerprint'
        $result.Classifications | Should -HaveCount 1
    }

    It 'compare tous les effets appOwnedState sans exposer les valeurs' {
        $left = New-SyntheticPreview -Fingerprint ('a' * 64)
        $right = New-SyntheticPreview -Fingerprint ('b' * 64)
        $left.appOwnedState = @([ordered]@{
            resourceId = '/subscriptions/test/resourceGroups/rg/providers/Test/widgets/app'
            parameter = 'app.image'
            image = 'image-tag-a'
        })
        $right.appOwnedState = @([ordered]@{
            resourceId = '/subscriptions/test/resourceGroups/rg/providers/Test/widgets/app'
            parameter = 'app.image'
            image = 'image-tag-b'
        })

        $result = Compare-IfsPreviews -LeftPath (Write-SyntheticPreview $left) -RightPath (Write-SyntheticPreview $right)
        $diagnostic = $result | ConvertTo-Json -Depth 10

        $result.Verdict | Should -Be 'Divergent'
        $result.Classifications | Should -Contain 'EffectDetail'
        $diagnostic | Should -Not -Match '/subscriptions/test|image-tag-a|image-tag-b'
    }

    It 'retourne NonConclusive pour une entrée JSON tronquée' {
        $validPath = Write-SyntheticPreview (New-SyntheticPreview)
        $truncatedPath = Join-Path $TestDrive 'truncated.json'
        [IO.File]::WriteAllText($truncatedPath, ([string][char]123), [Text.UTF8Encoding]::new($false))

        $result = Compare-IfsPreviews -LeftPath $validPath -RightPath $truncatedPath

        $result.Verdict | Should -Be 'NonConcluant'
        $result.Classifications | Should -Contain 'InvalidInput'
    }

    It 'retourne NonConclusive pour une entrée absente' {
        $validPath = Write-SyntheticPreview (New-SyntheticPreview)

        $result = Compare-IfsPreviews -LeftPath $validPath -RightPath (Join-Path $TestDrive 'absent.json')

        $result.Verdict | Should -Be 'NonConcluant'
        $result.Classifications | Should -Contain 'MissingInput'
    }

    It 'retourne NonConclusive quand le contexte ou le schéma ne correspond pas' {
        $left = New-SyntheticPreview
        $right = New-SyntheticPreview
        $right.target = 'prd'
        $right.schema = 'ifs-preview/v2'

        $result = Compare-IfsPreviews -LeftPath (Write-SyntheticPreview $left) -RightPath (Write-SyntheticPreview $right)

        $result.Verdict | Should -Be 'NonConcluant'
        $result.Classifications | Should -Contain 'IncompatiblePreview'
    }
}
