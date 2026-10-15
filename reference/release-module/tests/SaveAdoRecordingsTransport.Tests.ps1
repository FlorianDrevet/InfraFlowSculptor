Describe 'Transport de capture Azure DevOps sans PAT' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
        $script:transportModule = Join-Path $script:repoRoot 'tools/proofs/AdoRecordingsTransport.psm1'
        Import-Module $script:transportModule -Force
    }

    BeforeEach {
        $script:argumentCapture = [pscustomobject]@{ Arguments = @() }
        $capture = $script:argumentCapture
        $script:azInvoker = {
            param([string[]] $Arguments)
            $capture.Arguments = @($Arguments)
            return '{"id":568,"value":[]}'
        }.GetNewClosure()
    }

    It 'lit les endpoints avec la session CLI et transmet les portees explicites' {
        $response = Invoke-AdoRecordingsCliJsonGet -Organization 'example' `
            -Area 'build' -Resource 'timeline' -RouteParameters @{
                project = 'shop42'
                buildId = '568'
            } -QueryParameters @{} -AzInvoker $script:azInvoker -Kind 'timeline'

        $response.Data.id | Should -Be 568
        $script:argumentCapture.Arguments | Should -Contain 'devops'
        $script:argumentCapture.Arguments | Should -Contain 'invoke'
        $script:argumentCapture.Arguments | Should -Contain '--organization'
        $script:argumentCapture.Arguments | Should -Contain 'https://dev.azure.com/example'
        $script:argumentCapture.Arguments | Should -Contain '--area'
        $script:argumentCapture.Arguments | Should -Contain 'build'
        $script:argumentCapture.Arguments | Should -Contain '--resource'
        $script:argumentCapture.Arguments | Should -Contain 'timeline'
        $script:argumentCapture.Arguments | Should -Contain 'project=shop42'
        $script:argumentCapture.Arguments | Should -Contain 'buildId=568'
    }

    It 'telecharge les artefacts par la session CLI avec le projet explicite' {
        Invoke-AdoRecordingsArtifactDownload -Organization 'https://dev.azure.com/example' `
            -Project 'shop42' -RunId 568 -ArtifactName 'ifs-install-report' -Path 'C:\temp\capture' `
            -AzInvoker $script:azInvoker

        $script:argumentCapture.Arguments | Should -Contain 'pipelines'
        $script:argumentCapture.Arguments | Should -Contain 'runs'
        $script:argumentCapture.Arguments | Should -Contain 'artifact'
        $script:argumentCapture.Arguments | Should -Contain 'download'
        $script:argumentCapture.Arguments | Should -Contain '--organization'
        $script:argumentCapture.Arguments | Should -Contain 'https://dev.azure.com/example'
        $script:argumentCapture.Arguments | Should -Contain '--project'
        $script:argumentCapture.Arguments | Should -Contain 'shop42'
        $script:argumentCapture.Arguments | Should -Contain '--run-id'
        $script:argumentCapture.Arguments | Should -Contain '568'
        $script:argumentCapture.Arguments | Should -Contain '--artifact-name'
        $script:argumentCapture.Arguments | Should -Contain 'ifs-install-report'
        $script:argumentCapture.Arguments | Should -Contain '--path'
        $script:argumentCapture.Arguments | Should -Contain 'C:\temp\capture'
    }

    It 'reconnait le rapport de finalisation produit par le kit Azure DevOps' {
        Test-AdoRecordingsArtifactName -Name 'ifs-install-report' | Should -BeTrue
        Test-AdoRecordingsArtifactName -Name 'ifs-report-dev' | Should -BeTrue
        Test-AdoRecordingsArtifactName -Name 'ifs-unrelated-output' | Should -BeFalse
    }
}
