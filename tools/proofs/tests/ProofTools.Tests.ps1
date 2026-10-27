BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '..\ProofTools.psm1') -Force
}

Describe 'ConvertTo-ProofRedactedJson' {
    It 'preserves empty string values in nested JSON' {
        $json = '{"templateParameters":{"systemAssignedSqlClientIdPrd":""}}'

        $redacted = ConvertTo-ProofRedactedJson -Json $json -Organization 'floriandrevet0332' -Project 'shop43'
        $result = ConvertFrom-Json -InputObject $redacted -AsHashtable -Depth 100 -DateKind String

        $result.templateParameters.systemAssignedSqlClientIdPrd | Should -BeExactly ''
    }
}
