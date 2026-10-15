Describe 'Chemins des templates Azure Pipelines dans la reference' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
        $script:pilotRoot = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo'
        $script:templateReferences = @(
            Get-ChildItem -LiteralPath $script:pilotRoot -Recurse -File -Include '*.yml', '*.yaml' |
                ForEach-Object {
                    $file = $_
                    $content = [IO.File]::ReadAllText($file.FullName)
                    foreach ($match in [regex]::Matches($content, '(?m)^\s*template:\s*(?<path>[^\s#]+)')) {
                        [pscustomobject]@{
                            File = $file.FullName
                            Path = [string]$match.Groups['path'].Value
                        }
                    }
                }
        )
    }

    It 'utilise un chemin absolu du depot qui existe pour chaque template local' {
        $script:templateReferences.Count | Should -BeGreaterThan 0

        foreach ($reference in $script:templateReferences) {
            $reference.Path | Should -Match '^/' -Because $reference.File
            $resolvedPath = Join-Path $script:pilotRoot $reference.Path.TrimStart('/')
            Test-Path -LiteralPath $resolvedPath -PathType Leaf | Should -BeTrue -Because $reference.File
        }
    }
}

Describe "Exposition du principal de service aux tâches AzureCLI infra" {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
        $script:pilotRoot = Join-Path $script:repoRoot 'reference/pilot/bicep-azdo'
        $script:infraStagesPath = Join-Path $script:pilotRoot '.ifs/templates/infra-target-stages.yml'
        $script:infraStages = [IO.File]::ReadAllText($script:infraStagesPath)
    }

    It "Expose servicePrincipalId à la tâche d’aperçu" {
        $taskStart = $script:infraStages.IndexOf('    - task: AzureCLI@2', [StringComparison]::Ordinal)
        $taskEnd = $script:infraStages.IndexOf('    - publish:', $taskStart, [StringComparison]::Ordinal)
        $taskStart | Should -BeGreaterThan -1
        $taskEnd | Should -BeGreaterThan $taskStart
        $previewTask = $script:infraStages.Substring($taskStart, $taskEnd - $taskStart)
        $previewTask | Should -Match '(?m)^\s*addSpnToEnvironment:\s*true\b'
    }
}
