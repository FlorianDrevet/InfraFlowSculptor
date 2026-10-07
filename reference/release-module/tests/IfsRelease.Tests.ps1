BeforeAll {
    $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
    $script:modulePath = Join-Path $script:repoRoot "reference/pilot/bicep-azdo/.ifs/templates/scripts/IfsRelease.psm1"
    $script:fixtureRoot = Join-Path $PSScriptRoot "fixtures"
    Import-Module $script:modulePath -Force

    function global:az {
        param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
        $call = @($Arguments)
        [void] $global:IFS_AZ_CALLS.Add($call)
        if ($global:IFS_AZ_HANDLER) {
            return & $global:IFS_AZ_HANDLER $call
        }
        $global:LASTEXITCODE = 0
        return "{}"
    }
}

Describe "Module de release IFS" {
BeforeEach {
    $global:IFS_AZ_CALLS = [System.Collections.Generic.List[object]]::new()
    $global:IFS_JOURNAL = $null
    $global:IFS_FIXTURE_ROOT = $script:fixtureRoot
    $global:IFS_STACK = Get-Content -LiteralPath (Join-Path $script:fixtureRoot "stack-empty.json") -Raw
    $global:IFS_WHATIF = Get-Content -LiteralPath (Join-Path $script:fixtureRoot "what-if-create.json") -Raw
    $global:IFS_AZ_HANDLER = {
        param($Arguments)
        $global:LASTEXITCODE = 0
        $text = $Arguments -join " "
        if ($text -match "storage blob exists") {
            return $(if ($global:IFS_JOURNAL) { "true" } else { "false" })
        }
        if ($text -match "storage blob download") {
            $fileIndex = [Array]::IndexOf($Arguments, "--file") + 1
            Set-Content -LiteralPath $Arguments[$fileIndex] -Value ($global:IFS_JOURNAL | ConvertTo-Json -Depth 50) -NoNewline
            return "{}"
        }
        if ($text -match "storage blob upload") {
            $fileIndex = [Array]::IndexOf($Arguments, "--file") + 1
            $global:IFS_JOURNAL = Get-Content -LiteralPath $Arguments[$fileIndex] -Raw | ConvertFrom-Json -AsHashtable
            return "{}"
        }
        if ($text -match "storage blob lease acquire") { return "ifs-test-lease" }
        if ($text -match "storage blob lease (renew|release)") { return "true" }
        if ($text -match "keyvault secret list") { return [IO.File]::ReadAllText((Join-Path $global:IFS_FIXTURE_ROOT "keyvault-secret-list.json")) }
        if ($text -match "resource show") { return [IO.File]::ReadAllText((Join-Path $global:IFS_FIXTURE_ROOT "resource-show.json")) }
        if ($text -match "deployment sub what-if") {
            return $global:IFS_WHATIF
        }
        if ($text -match "stack sub show") { return $global:IFS_STACK }
        if ($text -match "container create") { return '{"created":true}' }
        if ($text -match "containerapp show") { return [IO.File]::ReadAllText((Join-Path $global:IFS_FIXTURE_ROOT "containerapp-show.json")) }
        return "{}"
    }

    $script:tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("ifs-p03-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $script:tempRoot | Out-Null
    $script:ordersRelease = Join-Path $script:repoRoot "reference/pilot/bicep-azdo/orders/infra/release.dev.json"
    $script:coreRelease = Join-Path $script:repoRoot "reference/pilot/bicep-azdo/core/infra/release.dev.json"
    $script:templatePath = Join-Path $script:repoRoot "reference/pilot/bicep-azdo/orders/infra/main.bicep"
    $script:parameterPath = Join-Path $script:repoRoot "reference/pilot/bicep-azdo/orders/infra/main.dev.bicepparam"
    $script:orderData = Read-IfsReleaseData -Path $script:ordersRelease
}

AfterEach {
    if (Test-Path $script:tempRoot) { Remove-Item $script:tempRoot -Recurse -Force }
}

Describe "Read-IfsReleaseData" {
    It "valide un fichier release du pilote" {
        (Read-IfsReleaseData -Path $script:ordersRelease).component | Should -Be "orders"
    }

    It "refuse une donnée qui ne respecte pas le schéma" {
        $invalid = Join-Path $script:tempRoot "release.json"
        Set-Content -LiteralPath $invalid -Value '{"schema":"ifs-release/v1","component":"oops"}'
        { Read-IfsReleaseData -Path $invalid } | Should -Throw
    }
}

Describe "Aperçu et empreinte" {
    It "reste en lecture seule, même si le journal est absent" {
        $preview = Invoke-IfsPreview -ReleaseData $script:orderData -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -OutputDirectory $script:tempRoot
        $preview.schema | Should -Be "ifs-preview/v1"
        (Get-Content (Join-Path $script:tempRoot "ifs-preview.json") -Raw | ConvertFrom-Json).fingerprint | Should -Not -BeNullOrEmpty
        ($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " } | Where-Object { $_ -match "lease|upload|delete|create|set|update" }) | Should -BeNullOrEmpty
        ($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " } | Where-Object { $_ -match "what-if|storage blob exists|storage blob download" }).Count | Should -BeGreaterThan 0
    }

    It "inclut l'Object ID applicatif dans l'aperçu orders" {
        $applicationId = "22222222-2222-2222-2222-222222222222"
        Invoke-IfsPreview -ReleaseData $script:orderData -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -OutputDirectory $script:tempRoot -ApplicationIdentityObjectId $applicationId | Out-Null
        $call = $global:IFS_AZ_CALLS | Where-Object { ($_ -join " ") -match "deployment sub what-if" } | Select-Object -First 1

        $call | Should -Contain "appDeliveryPrincipalId=$applicationId"
    }

    It "inclut l'Object ID de déploiement dans l'aperçu core" {
        $data = Read-IfsReleaseData -Path $script:coreRelease
        $template = Join-Path $script:repoRoot "reference/pilot/bicep-azdo/core/infra/main.bicep"
        $parameters = Join-Path $script:repoRoot "reference/pilot/bicep-azdo/core/infra/main.dev.bicepparam"
        $deploymentId = "11111111-1111-1111-1111-111111111111"
        $env:MAIN_PAYMENTS_API_KEY = "pester-preview-secret"
        try {
            Invoke-IfsPreview -ReleaseData $data -TemplateFile $template -ParameterFile $parameters -OutputDirectory $script:tempRoot -DeploymentIdentityObjectId $deploymentId | Out-Null
            $call = $global:IFS_AZ_CALLS | Where-Object { ($_ -join " ") -match "deployment sub what-if" } | Select-Object -First 1

            $call | Should -Contain "deploymentPrincipalId=$deploymentId"
        }
        finally { Remove-Item Env:MAIN_PAYMENTS_API_KEY -ErrorAction SilentlyContinue }
    }

    It "exclut l'état appartenant à l'application de l'empreinte" {
        $left = [ordered]@{ changes = @(@{ id = "a"; changeType = "Modify" }); appOwnedState = @{ image = "one" } }
        $right = [ordered]@{ changes = @(@{ id = "a"; changeType = "Modify" }); appOwnedState = @{ image = "two" } }
        (Get-IfsEffectFingerprint -Effects $left) | Should -Be (Get-IfsEffectFingerprint -Effects $right)
    }

    It "inclut les changements de rôle dans l'empreinte" {
        $left = @{ changes = @(@{ id = "role"; role = "Reader" }) }
        $right = @{ changes = @(@{ id = "role"; role = "Owner" }) }
        (Get-IfsEffectFingerprint -Effects $left) | Should -Not -Be (Get-IfsEffectFingerprint -Effects $right)
    }

    It "ignore les changements d'image applicative mais garde les changements de rôle" {
        $module = Get-Module IfsRelease
        $appId = "/subscriptions/test/resourceGroups/test/providers/Microsoft.App/containerApps/api"
        $release = [pscustomobject]@{ appOwnedState = @([pscustomobject]@{ resourceId = $appId }) }
        $imageOne = @([pscustomobject]@{ resourceId = $appId; changeType = "Modify"; delta = @([pscustomobject]@{ path = "properties.template.containers[0].image"; before = "img:1"; after = "img:2" }) })
        $imageTwo = @([pscustomobject]@{ resourceId = $appId; changeType = "Modify"; delta = @([pscustomobject]@{ path = "properties.template.containers[0].image"; before = "img:2"; after = "img:3" }) })
        $roleOne = @([pscustomobject]@{ resourceId = "/subscriptions/test/providers/Microsoft.Authorization/roleAssignments/one"; changeType = "Modify"; delta = @([pscustomobject]@{ path = "properties.roleDefinitionId"; before = "Reader"; after = "Owner" }) })
        $roleTwo = @([pscustomobject]@{ resourceId = "/subscriptions/test/providers/Microsoft.Authorization/roleAssignments/one"; changeType = "Modify"; delta = @([pscustomobject]@{ path = "properties.roleDefinitionId"; before = "Reader"; after = "Contributor" }) })
        $fingerprints = & $module {
            param($data, $firstImage, $secondImage, $firstRole, $secondRole)
            $one = Get-IfsEffectFingerprint -Effects @{ changes = @(Get-IfsAppChangeFingerprintView -Changes $firstImage -ReleaseData $data) }
            $two = Get-IfsEffectFingerprint -Effects @{ changes = @(Get-IfsAppChangeFingerprintView -Changes $secondImage -ReleaseData $data) }
            $roleA = Get-IfsEffectFingerprint -Effects @{ changes = @(Get-IfsAppChangeFingerprintView -Changes $firstRole -ReleaseData $data) }
            $roleB = Get-IfsEffectFingerprint -Effects @{ changes = @(Get-IfsAppChangeFingerprintView -Changes $secondRole -ReleaseData $data) }
            @($one, $two, $roleA, $roleB)
        } $release $imageOne $imageTwo $roleOne $roleTwo
        $fingerprints[0] | Should -Be $fingerprints[1]
        $fingerprints[2] | Should -Not -Be $fingerprints[3]
    }

    It "conserve les opérations d'une révision 3 dans l'aperçu de la révision 4" {
        $global:IFS_JOURNAL = @{
            schema = "ifs-operation-journal/v1"
            unit = "ifs-shop-orders-dev"
            target = "dev"
            operations = @(@{ id = ("a" * 64); revision = 3; commit = "rev3"; kind = "revoke"; objectId = "/role/removed"; state = "ToDo"; createdAt = [DateTime]::UtcNow.ToString("o"); updatedAt = [DateTime]::UtcNow.ToString("o") })
        }
        $journal = Read-IfsOperationJournal -ReleaseData $script:orderData
        $pending = @(Get-IfsPendingOperation -Journal $journal)
        $pending.Count | Should -Be 1
        $pending[0].revision | Should -Be 3
        $pending[0].commit | Should -Be "rev3"
    }

    It "compare les ressources suivies par la pile et prévoit la révocation des rôles sortis du modèle" {
        $roleId = "/subscriptions/test/providers/Microsoft.Authorization/roleAssignments/removed"
        $resourceId = "/subscriptions/test/resourceGroups/test/providers/Microsoft.Storage/storageAccounts/removed"
        $global:IFS_STACK = @{ resources = @(@{ id = $roleId }, @{ id = $resourceId }) } | ConvertTo-Json -Depth 20 -Compress
        $global:IFS_WHATIF = @{ changes = @(@{ resourceId = "/subscriptions/test/providers/Microsoft.Storage/storageAccounts/current"; changeType = "NoChange" }) } | ConvertTo-Json -Depth 20 -Compress
        $preview = Invoke-IfsPreview -ReleaseData $script:orderData -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -OutputDirectory $script:tempRoot
        $preview.detachedOrDeleted | Should -Contain $resourceId
        ($preview.revokedAccess | ForEach-Object resourceId) | Should -Contain $roleId
        ($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " } | Where-Object { $_ -match "lease|upload|resource delete" }) | Should -BeNullOrEmpty
    }
}

Describe "Journal d'opérations" {
    It "retourne un journal vide absent sans créer de blob" {
        $journal = Read-IfsOperationJournal -ReleaseData $script:orderData
        $journal.operations.Count | Should -Be 0
        ($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " } | Where-Object { $_ -match "upload|lease" }) | Should -BeNullOrEmpty
    }

    It "refuse un journal existant qui ne respecte pas son schéma" {
        $global:IFS_JOURNAL = [pscustomobject]@{
            schema = "ifs-operation-journal/v1"; unit = "ifs-shop-orders-dev"; target = "unknown"; operations = @()
        }
        { Read-IfsOperationJournal -ReleaseData $script:orderData } | Should -Throw "*operation-journal/v1*"
    }

    It "ajoute un horodatage UTC aux règles temporaires pour permettre leur nettoyage" {
        $module = Get-Module IfsRelease
        $runId = & $module { param($value) Get-IfsFirewallRunId -RunId $value } "pipeline-run"
        $runId | Should -Match "^\d{14}-pipeline-run$"

        $rule = [pscustomobject]@{ name = "ifs-temp-20200101000000-pipeline-run"; systemData = $null }
        $created = & $module {
            param($value)
            Get-IfsTemporaryFirewallRuleCreatedAt -Rule $value -ResourceId "/subscriptions/test/firewallRules/old"
        } $rule
        ([DateTimeOffset]::UtcNow - $created.ToUniversalTime()).TotalHours | Should -BeGreaterThan 2
        ($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " } | Select-String "resource show") | Should -BeNullOrEmpty
    }

    It "écrit le journal avant la mutation et réutilise l'identifiant lors d'une reprise" {
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            $text = $Arguments -join " "
            if ($text -match "storage blob exists") { return $(if ($global:IFS_JOURNAL) { "true" } else { "false" }) }
            if ($text -match "storage container create") { return "{}" }
            if ($text -match "storage blob upload") {
                $i = [Array]::IndexOf($Arguments, "--file") + 1
                $global:IFS_JOURNAL = Get-Content $Arguments[$i] -Raw | ConvertFrom-Json -AsHashtable
                return "{}"
            }
            if ($text -match "storage blob download") {
                $i = [Array]::IndexOf($Arguments, "--file") + 1
                Set-Content $Arguments[$i] -Value ($global:IFS_JOURNAL | ConvertTo-Json -Depth 50) -NoNewline
                return "{}"
            }
            if ($text -match "lease acquire") { return "ifs-test-lease" }
            if ($text -match "lease (renew|release)") { return "true" }
            return "{}"
        }
        $context = Open-IfsOperationJournal -ReleaseData $script:orderData -WaitTimeoutSeconds 1
        try {
            $first = Add-IfsOperation -Context $context -Revision 3 -Commit "abc123" -Kind "revoke" -ObjectId "/role/old"
            $first.state | Should -Be "ToDo"
            Set-IfsOperationState -Context $context -OperationId $first.id -State "Started"
            $writeIndex = ($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " } | Select-String "blob upload" | Select-Object -First 1).LineNumber
            $writeIndex | Should -BeGreaterThan 0
            $resumed = Get-IfsPendingOperation -Journal $context.Journal
            $resumed.Count | Should -Be 1
            $again = Add-IfsOperation -Context $context -Revision 4 -Commit "def456" -Kind "revoke" -ObjectId "/role/old"
            $again.id | Should -Be $first.id
            $again.revision | Should -Be 3
        }
        finally { Close-IfsOperationJournal -Context $context }
    }

    It "réarme une opération Done pour une nouvelle release et conserve son identifiant stable" {
        Mock Save-IfsJournalBlob -ModuleName IfsRelease {}
        $context = [pscustomobject]@{
            Journal = [pscustomobject]@{ schema = "ifs-operation-journal/v1"; unit = "ifs-shop-orders-dev"; target = "dev"; operations = @() }
            Account = "stifsshopdev"; Container = "ifs-operations"; Blob = "ifs-shop-orders-dev.json"; LeaseId = "test"; RenewalFailureFile = $null
        }
        $previous = Add-IfsOperation -Context $context -Revision 3 -Commit "abc123" -Kind "secret-write" -ObjectId "kv/token" -Details ([pscustomobject]@{ value = "old" })
        Set-IfsOperationState -Context $context -OperationId $previous.id -State "Started" | Out-Null
        Set-IfsOperationState -Context $context -OperationId $previous.id -State "Done" | Out-Null

        $current = Add-IfsOperation -Context $context -Revision 4 -Commit "def456" -Kind "secret-write" -ObjectId "kv/token" -Details ([pscustomobject]@{ value = "new" })

        $current.id | Should -Be $previous.id
        $current.state | Should -Be "ToDo"
        $current.revision | Should -Be 4
        $current.commit | Should -Be "def456"
        $current.details.value | Should -Be "new"
        $sameRelease = Add-IfsOperation -Context $context -Revision 4 -Commit "def456" -Kind "secret-write" -ObjectId "kv/token" -Details ([pscustomobject]@{ value = "ignored" })
        $sameRelease.state | Should -Be "ToDo"
        $sameRelease.details.value | Should -Be "new"
    }

    It "marque un objet absent comme terminé avec la note prévue" {
        $journal = [pscustomobject]@{ schema = "ifs-operation-journal/v1"; unit = "ifs-shop-orders-dev"; target = "dev"; operations = @([pscustomobject]@{ id = "x"; state = "Started"; kind = "revoke"; objectId = "/role/gone"; revision = 3; commit = "abc"; createdAt = [DateTime]::UtcNow.ToString("o"); updatedAt = [DateTime]::UtcNow.ToString("o") }) }
        $context = [pscustomobject]@{ Journal = $journal; Account = "stifsshopdev"; Container = "ifs-operations"; Blob = "ifs-shop-orders-dev.json"; LeaseId = "test"; RenewalFailureFile = $null }
        Set-IfsOperationState -Context $context -OperationId "x" -State "Done" -ObjectExists $false | Out-Null
        $context.Journal.operations[0].note | Should -Be "déjà absent"
    }

    It "enregistre une révocation avant de supprimer l'objet Azure" {
        $context = [pscustomobject]@{
            Journal = [pscustomobject]@{ schema = "ifs-operation-journal/v1"; unit = "ifs-shop-orders-dev"; target = "dev"; operations = @() }
            Account = "stifsshopdev"; Container = "ifs-operations"; Blob = "ifs-shop-orders-dev.json"; LeaseId = "test"; RenewalFailureFile = $null
        }
        Invoke-IfsRevocation -Context $context -ReleaseData $script:orderData -Revision 4 -Commit "rev4" -ObjectIds @("/subscriptions/test/providers/Microsoft.Authorization/roleAssignments/old") | Out-Null
        $calls = @($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " })
        $firstUpload = ($calls | Select-String "storage blob upload" | Select-Object -First 1).LineNumber
        $firstDelete = ($calls | Select-String "resource delete" | Select-Object -First 1).LineNumber
        $firstUpload | Should -BeGreaterThan 0
        $firstUpload | Should -BeLessThan $firstDelete
        $context.Journal.operations[0].state | Should -Be "Done"
    }

    It "rejoue une révocation T02 avant une nouvelle écriture de secret" {
        $env:IFS_RELEASE_TEST_SECRET = "new-value"
        $secret = [pscustomobject]@{ source = "pipeline"; variable = "IFS_RELEASE_TEST_SECRET"; vault = "kv-test"; secret = "token" }
        $now = [DateTime]::UtcNow.ToString("o")
        $journal = [pscustomobject]@{
            schema = "ifs-operation-journal/v1"; unit = "ifs-shop-orders-dev"; target = "dev"
            operations = @(
                [pscustomobject]@{ id = ("a" * 64); revision = 3; commit = "rev3"; kind = "revoke"; objectId = "/role/old"; state = "ToDo"; createdAt = $now; updatedAt = $now; note = $null; details = $null },
                [pscustomobject]@{ id = ("b" * 64); revision = 4; commit = "rev4"; kind = "secret-write"; objectId = "kv-test/token"; state = "ToDo"; createdAt = $now; updatedAt = $now; note = $null; details = [pscustomobject]@{ write = $secret; phase = "preDeploy" } }
            )
        }
        $context = [pscustomobject]@{ Journal = $journal; Account = "stifsshopdev"; Container = "ifs-operations"; Blob = "ifs-shop-orders-dev.json"; LeaseId = "test"; RenewalFailureFile = $null }
        $data = [pscustomobject]@{ secretWrites = @($secret); restartOnSecretChange = @() }
        $module = Get-Module IfsRelease
        try {
            & $module {
                param($ctx, $release)
                Invoke-IfsPendingOperation -Context $ctx -ReleaseData $release -TemplateFile "template.bicep" -ParameterFile "parameters.bicepparam" -DeploymentIdentityObjectId "identity" -SqlScript "data-access.sql" -Revision 4 -Commit "rev4" -RunId "run4"
            } $context $data | Out-Null
        }
        finally { Remove-Item Env:IFS_RELEASE_TEST_SECRET -ErrorAction SilentlyContinue }
        $calls = @($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " })
        $deleteLine = ($calls | Select-String "resource delete" | Select-Object -First 1).LineNumber
        $secretSetLine = ($calls | Select-String "keyvault secret set" | Select-Object -First 1).LineNumber
        $deleteLine | Should -BeGreaterThan 0
        $deleteLine | Should -BeLessThan $secretSetLine
        $context.Journal.operations[0].revision | Should -Be 3
        $context.Journal.operations[0].state | Should -Be "Done"
    }
}

Describe "Secrets et dépendances" {
    It "n'exige aucune variable secrète pour orders, qui ne fait que référencer un secret" {
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            if (($Arguments -join " ") -match "keyvault secret list") { return '["https://example/secrets/payments-api-key"]' }
            return "{}"
        }
        { Test-IfsSecretVariable -ReleaseData $script:orderData } | Should -Not -Throw
        { Test-IfsSecretReference -ReleaseData $script:orderData } | Should -Not -Throw
    }

    It "échoue avant toute mutation quand MAIN_PAYMENTS_API_KEY est vide" {
        $data = Read-IfsReleaseData -Path $script:coreRelease
        Remove-Item Env:MAIN_PAYMENTS_API_KEY -ErrorAction SilentlyContinue
        { Test-IfsSecretVariable -ReleaseData $data } | Should -Throw "*MAIN_PAYMENTS_API_KEY*ifs-shop-dev*"
        ($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " } | Where-Object { $_ -match "upload|lease|what-if|deployment sub create" }) | Should -BeNullOrEmpty
    }

    It "signale le composant et la cible d'une dépendance manquante" {
        $data = [pscustomobject]@{ dependencies = @([pscustomobject]@{component="core";resources=@([pscustomobject]@{id="/missing"})}) }
        $global:IFS_AZ_HANDLER = { param($Arguments) $global:LASTEXITCODE = 1; throw "ResourceNotFound" }
        { Test-IfsDependency -ReleaseData $data -Target "dev" } | Should -Throw "*core*dev*"
    }

    It "vérifie une référence Key Vault sans demander sa valeur" {
        $global:IFS_AZ_HANDLER = { param($Arguments) $global:LASTEXITCODE = 0; return "[]" }
        { Test-IfsSecretReference -ReleaseData $script:orderData } | Should -Throw "*core*dev*"
        ($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " } | Select-String "secret show|secret download") | Should -BeNullOrEmpty
    }

    It "ne réécrit pas un secret inchangé et ne redémarre pas l'application" {
        $env:IFS_RELEASE_TEST_SECRET = "same-value"
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            if (($Arguments -join " ") -match "keyvault secret show") { return $env:IFS_RELEASE_TEST_SECRET }
            return "{}"
        }
        try {
            $data = [pscustomobject]@{
                secretWrites = @([pscustomobject]@{ source = "pipeline"; variable = "IFS_RELEASE_TEST_SECRET"; vault = "kv-test"; secret = "token" })
                restartOnSecretChange = @("/containerapp/api")
            }
            Invoke-IfsSecretWrite -ReleaseData $data | Should -BeNullOrEmpty
            ($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " } | Select-String "secret set|revision restart") | Should -BeNullOrEmpty
        }
        finally { Remove-Item Env:IFS_RELEASE_TEST_SECRET -ErrorAction SilentlyContinue }
    }

    It "écrit un secret modifié sans exposer sa valeur puis redémarre la révision courante" {
        $env:IFS_RELEASE_TEST_SECRET = "new-value-not-for-logs"
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            $text = $Arguments -join " "
            if ($text -match "keyvault secret show") { return "old-value" }
            if ($text -match "containerapp show" -and $text -match "latestRevisionName") { return "api--current" }
            return "{}"
        }
        try {
            $data = [pscustomobject]@{
                secretWrites = @([pscustomobject]@{ source = "pipeline"; variable = "IFS_RELEASE_TEST_SECRET"; vault = "kv-test"; secret = "token" })
                restartOnSecretChange = @("/subscriptions/test/resourceGroups/test/providers/Microsoft.App/containerApps/api")
            }
            Invoke-IfsSecretWrite -ReleaseData $data | Should -Contain "token"
            $calls = @($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " })
            ($calls | Select-String "keyvault secret set").Count | Should -Be 1
            ($calls | Select-String "revision restart.*api--current").Count | Should -Be 1
            ($calls -join [Environment]::NewLine) | Should -Not -Match "new-value-not-for-logs"
        }
        finally { Remove-Item Env:IFS_RELEASE_TEST_SECRET -ErrorAction SilentlyContinue }
    }
}

Describe "Rapports de release" {
    It "écrit et valide un rapport même avant la première mutation" {
        $manifestPath = Join-Path $script:tempRoot "manifest.json"
        $reportPath = Join-Path $script:tempRoot "ifs-report.json"
        Set-Content -LiteralPath $manifestPath -Value '{"revision":4,"commit":"rev4","units":{"orders":{"fingerprint":"abc"}}}'
        $report = Write-IfsReleaseReport -ReleaseData $script:orderData -ManifestPath $manifestPath -OutputPath $reportPath -Steps @() -Result FailedBeforeMutation -ErrorMessage "précontrôle"
        $report.result | Should -Be "FailedBeforeMutation"
        $saved = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        $saved.schema | Should -Be "ifs-report/v1"
        $saved.revision | Should -Be 4
        (Test-Json -Json (Get-Content -LiteralPath $reportPath -Raw) -SchemaFile (Join-Path $script:repoRoot "reference/release-module/schemas/ifs-report.schema.json")) | Should -BeTrue
    }

    It "relit l'image applicative après la prise du bail du journal" {
        $manifestPath = Join-Path $script:tempRoot "manifest-app-state.json"
        $approvedPath = Join-Path $script:tempRoot "approved-app-state.json"
        $reportPath = Join-Path $script:tempRoot "ifs-app-state-report.json"
        $previewDirectory = Join-Path $script:tempRoot "preview-app-state"
        New-Item -ItemType Directory -Path $previewDirectory -Force | Out-Null
        Set-Content -LiteralPath $manifestPath -Value '{"revision":4,"commit":"rev4","units":{"orders":{"fingerprint":"abc"}}}'
        $initialPreview = Invoke-IfsPreview -ReleaseData $script:orderData -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -OutputDirectory $previewDirectory
        Set-Content -LiteralPath $approvedPath -Value (ConvertTo-Json $initialPreview -Depth 50)
        $global:IFS_AZ_CALLS.Clear()
        Mock Test-IfsNewerManifest -ModuleName IfsRelease { return $false }
        Mock Invoke-IfsPendingOperation -ModuleName IfsRelease {
            return [pscustomobject]@{ detachedResources = @(); revokedAccess = @(); temporaryFirewallRules = @(); cleanedFirewallRules = @() }
        }

        {
            Invoke-IfsInfraDeploy -ReleasePath $script:ordersRelease -ManifestPath $manifestPath -PreviewPath $approvedPath -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -SqlScript (Join-Path $script:repoRoot "reference/pilot/bicep-azdo/orders/infra/scripts/data-access.sql") -OutputPath $reportPath -DefaultBranch "main" -ManifestPathInRepository ".ifs/manifest.json" -DeploymentIdentityObjectId "00000000-0000-0000-0000-000000000001" -ApplicationIdentityObjectId "00000000-0000-0000-0000-000000000002"
        } | Should -Not -Throw

        $calls = @($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " })
        $leaseLine = ($calls | Select-String "storage blob lease acquire").LineNumber
        $appStateLines = @($calls | Select-String "containerapp show" | ForEach-Object LineNumber)
        $leaseLine | Should -BeGreaterThan 0
        $appStateLines.Count | Should -Be 2
        $appStateLines[0] | Should -BeLessThan $leaseLine
        $appStateLines[1] | Should -BeGreaterThan $leaseLine
    }

    It "rapporte un déploiement partiel après un échec de pile" {
        $manifestPath = Join-Path $script:tempRoot "manifest-partial.json"
        $approvedPath = Join-Path $script:tempRoot "ifs-approved.json"
        $reportPath = Join-Path $script:tempRoot "ifs-partial-report.json"
        $previewDirectory = Join-Path $script:tempRoot "preview"
        New-Item -ItemType Directory -Path $previewDirectory -Force | Out-Null
        Set-Content -LiteralPath $manifestPath -Value '{"revision":4,"commit":"rev4","units":{"core":{"fingerprint":"abc"}}}'
        $env:MAIN_PAYMENTS_API_KEY = "pester-secret-value"
        try {
            $data = Read-IfsReleaseData -Path $script:coreRelease
            $initialPreview = Invoke-IfsPreview -ReleaseData $data -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -OutputDirectory $previewDirectory
            Set-Content -LiteralPath $approvedPath -Value (ConvertTo-Json $initialPreview -Depth 50)
            Mock Test-IfsNewerManifest -ModuleName IfsRelease { return $false }
            Mock Invoke-IfsStackDeployment -ModuleName IfsRelease { throw "quota failure after two resources" }
            {
                Invoke-IfsInfraDeploy -ReleasePath $script:coreRelease -ManifestPath $manifestPath -PreviewPath $approvedPath -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -SqlScript (Join-Path $script:repoRoot "reference/pilot/bicep-azdo/orders/infra/scripts/data-access.sql") -OutputPath $reportPath -DefaultBranch "main" -ManifestPathInRepository ".ifs/manifest.json" -DeploymentIdentityObjectId "00000000-0000-0000-0000-000000000001"
            } | Should -Throw "*quota failure*"
            $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
            $report.result | Should -Be "PartiallyApplied"
            $failedStack = $report.steps | Where-Object { $_.name -eq "deploy-unit" }
            $failedStack.result | Should -Be "PartiallyApplied"
            $failedStack.createdResources | Should -Contain "/subscriptions/test/resourceGroups/test/providers/Test/widgets/new"
            $calls = @($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " })
            $firstUpload = ($calls | Select-String "storage blob upload" | Select-Object -First 1).LineNumber
            $secretSet = ($calls | Select-String "keyvault secret set" | Select-Object -First 1).LineNumber
            $firstUpload | Should -BeLessThan $secretSet
            ($calls -join [Environment]::NewLine) | Should -Not -Match "pester-secret-value"
        }
        finally { Remove-Item Env:MAIN_PAYMENTS_API_KEY -ErrorAction SilentlyContinue }
    }

    It "arrête l'étape 5 si le manifeste fusionné change les fichiers de l'unité" {
        $manifestPath = Join-Path $script:tempRoot "manifest-frozen.json"
        $previewPath = Join-Path $script:tempRoot "approved-preview.json"
        $reportPath = Join-Path $script:tempRoot "rejected-report.json"
        Set-Content -LiteralPath $manifestPath -Value '{"revision":4,"commit":"rev4","units":{"orders":{"fingerprint":"old"}}}'
        Set-Content -LiteralPath $previewPath -Value '{"fingerprint":"approved"}'
        Mock Test-IfsNewerManifest -ModuleName IfsRelease { throw "Une révision plus récente (5) modifie ce composant." }
        {
            Invoke-IfsInfraDeploy -ReleasePath $script:ordersRelease -ManifestPath $manifestPath -PreviewPath $previewPath -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -SqlScript (Join-Path $script:repoRoot "reference/pilot/bicep-azdo/orders/infra/scripts/data-access.sql") -OutputPath $reportPath -DefaultBranch "main" -ManifestPathInRepository ".ifs/manifest.json" -DeploymentIdentityObjectId "00000000-0000-0000-0000-000000000001" -ApplicationIdentityObjectId "00000000-0000-0000-0000-000000000002"
        } | Should -Throw "*modifie ce composant*"
        (Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json).result | Should -Be "FailedBeforeMutation"
        $calls = @($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " })
        ($calls | Select-String "blob upload|lease acquire|lease renew|stack sub create|resource delete|keyvault secret set|firewall-rule create") | Should -BeNullOrEmpty
    }

    It "compare la même unité entre le manifeste figé et la branche par défaut" {
        function global:git {
            param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
            $global:LASTEXITCODE = 0
            if ($Arguments[0] -eq "show") { return $global:IFS_GIT_MANIFEST }
        }
        $global:IFS_GIT_MANIFEST = '{"revision":5,"units":{"orders":{"fingerprint":"new"}}}'
        $frozen = [pscustomobject]@{ revision = 4; units = [pscustomobject]@{ orders = [pscustomobject]@{ fingerprint = "old" } } }
        $module = Get-Module IfsRelease
        try {
            {
                & $module {
                    param($old, $data)
                    Test-IfsNewerManifest -FrozenManifest $old -DefaultBranch "main" -ManifestPathInRepository ".ifs/manifest.json" -ReleaseData $data
                } $frozen $script:orderData
            } | Should -Throw "*révision plus récente (5)*"

            $global:IFS_GIT_MANIFEST = '{"revision":5,"units":{"orders":{"fingerprint":"old"}}}'
            $unchanged = & $module {
                param($old, $data)
                Test-IfsNewerManifest -FrozenManifest $old -DefaultBranch "main" -ManifestPathInRepository ".ifs/manifest.json" -ReleaseData $data
            } $frozen $script:orderData
            $unchanged | Should -BeFalse
        }
        finally {
            Remove-Item Function:git -ErrorAction SilentlyContinue
            Remove-Variable IFS_GIT_MANIFEST -Scope Global -ErrorAction SilentlyContinue
        }
    }
}

Describe "Déploiement de pile et application" {
    It "confirme la création de pile et conserve les ressources détachées" {
        $data = [pscustomobject]@{
            unit = "ifs-shop-orders-dev"; component = "orders"; location = "francecentral"; subscriptionId = "test"; protected = $false
        }
        $stack = Invoke-IfsStackDeployment -ReleaseData $data -TemplateFile "main.bicep" -ParameterFile "main.bicepparam" -DeploymentIdentityObjectId "identity" -ApplicationIdentityObjectId "22222222-2222-2222-2222-222222222222"
        $call = $global:IFS_AZ_CALLS | Where-Object { ($_ -join " ") -match "stack sub create" } | Select-Object -First 1
        $call | Should -Contain "--yes"
        $call | Should -Contain "--action-on-unmanage"
        $call | Should -Contain "detachAll"
        $call | Should -Contain "--deny-settings-mode"
        $call | Should -Contain "none"
        $stack.detachedResources | Should -BeNullOrEmpty
    }

    It "utilise le principalId Object ID comme SID SQL pour les identités système et utilisateur" {
        $applicationClientId = '11111111-2222-3333-4444-555555555555'
        $principalObjectId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'
        $global:IFS_SQL_IDENTITY = ConvertTo-Json -InputObject @{
            clientId = $applicationClientId
            principalId = $principalObjectId
            name = 'id-ifs-app-shop-dev'
        } -Compress
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            if (($Arguments -join ' ') -match 'containerapp show|identity show') { return $global:IFS_SQL_IDENTITY }
            return '{}'
        }

        foreach ($kind in @('systemAssigned', 'userAssigned')) {
            $access = [pscustomobject]@{
                principal = [pscustomobject]@{
                    kind = $kind
                    resourceId = if ($kind -eq 'systemAssigned') {
                        '/subscriptions/test/resourceGroups/rg/providers/Microsoft.App/containerApps/orders-api'
                    }
                    else {
                        '/subscriptions/test/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-ifs-app-shop-dev'
                    }
                }
                level = 'Read'
            }

            $principal = InModuleScope IfsRelease -Parameters @{ AccessValue = $access } {
                Get-IfsSqlPrincipal -Access $AccessValue -SubscriptionId 'test'
            }

            $principal.ClientId | Should -BeExactly $principalObjectId
            $principal.ClientId | Should -Not -BeExactly $applicationClientId
        }
    }

    It "normalise les ressources détachées objet ou chaîne en identifiants" {
        $resourceObjectId = '/subscriptions/test/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/one'
        $resourceStringId = '/subscriptions/test/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/two'
        $global:IFS_STACK = ConvertTo-Json -InputObject @{ detachedResources = @(@{ id = $resourceObjectId }, $resourceStringId) } -Depth 10 -Compress
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            if (($Arguments -join ' ') -match 'stack sub create') { return $global:IFS_STACK }
            return '{}'
        }
        $data = [pscustomobject]@{
            unit = 'ifs-shop-orders-dev'; component = 'orders'; location = 'francecentral'; subscriptionId = 'test'; protected = $false
        }

        $stack = Invoke-IfsStackDeployment -ReleaseData $data -TemplateFile 'main.bicep' -ParameterFile 'main.bicepparam' -DeploymentIdentityObjectId 'identity' -ApplicationIdentityObjectId '22222222-2222-2222-2222-222222222222'

        $stack.detachedResources.Count | Should -Be 2
        $stack.detachedResources[0] | Should -Be $resourceObjectId
        $stack.detachedResources[1] | Should -Be $resourceStringId
    }

    It "transmet l’Object ID de déploiement au Bicep core pour le rôle Key Vault" {
        $data = [pscustomobject]@{
            unit = "ifs-shop-core-dev"; component = "core"; location = "francecentral"; subscriptionId = "test"; protected = $false
        }
        Invoke-IfsStackDeployment -ReleaseData $data -TemplateFile "main.bicep" -ParameterFile "main.dev.bicepparam" -DeploymentIdentityObjectId "11111111-1111-1111-1111-111111111111" | Out-Null
        $call = $global:IFS_AZ_CALLS | Where-Object { ($_ -join " ") -match "stack sub create" } | Select-Object -First 1

        $call | Should -Contain "deploymentPrincipalId=11111111-1111-1111-1111-111111111111"
    }

    It "transmet l'Object ID applicatif au Bicep platform et orders" {
        $applicationId = "22222222-2222-2222-2222-222222222222"
        foreach ($component in @("platform", "orders")) {
            $global:IFS_AZ_CALLS.Clear()
            $data = [pscustomobject]@{
                unit = "ifs-shop-$component-dev"; component = $component; location = "francecentral"; subscriptionId = "test"; protected = $false
            }
            Invoke-IfsStackDeployment -ReleaseData $data -TemplateFile "main.bicep" -ParameterFile "main.dev.bicepparam" -DeploymentIdentityObjectId "11111111-1111-1111-1111-111111111111" -ApplicationIdentityObjectId $applicationId | Out-Null
            $call = $global:IFS_AZ_CALLS | Where-Object { ($_ -join " ") -match "stack sub create" } | Select-Object -First 1

            $call | Should -Contain "appDeliveryPrincipalId=$applicationId"
        }
    }

    It "livre une image immuable et valide le rapport de santé applicatif" {
        Mock Invoke-WebRequest -ModuleName IfsRelease { [pscustomobject]@{ StatusCode = 204 } }
        Mock Start-Sleep -ModuleName IfsRelease {}
        $image = 'mcr.microsoft.com/ifs/orders-api@sha256:{0}' -f ('a' * 64)
        $outputPath = Join-Path $script:tempRoot "app/ifs-app-report.json"
        $report = Invoke-IfsAppDeploy -Application "orders-api" -Component "orders" -Target "dev" `
            -ResourceId "/subscriptions/test/resourceGroups/test/providers/Microsoft.App/containerApps/orders-api" `
            -Image $image -HealthUrl ([uri]"https://orders-api.example/health") -OutputPath $outputPath -HealthTimeoutSeconds 1

        $report.result | Should -Be "Succeeded"
        $report.healthStatus | Should -Be 204
        $savedReport = Get-Content -LiteralPath $outputPath -Raw
        (Test-Json -Json $savedReport -SchemaFile (Join-Path $script:repoRoot "reference/release-module/schemas/ifs-app-report.schema.json")) | Should -BeTrue
        $updateCall = $global:IFS_AZ_CALLS | Where-Object { ($_ -join " ") -match "containerapp update" } | Select-Object -First 1
        $updateCall | Should -Contain "--image"
        $updateCall | Should -Contain $image
        $updateCall | Should -Contain "--revision-suffix"
    }
}

Describe "Aide du module" {
    It "documente Invoke-IfsPreview en français" {
        $help = Get-Help Invoke-IfsPreview -Full | Out-String
        $help | Should -Match "Aperçu"
        $help | Should -Match "What-if"
    }
}
}
