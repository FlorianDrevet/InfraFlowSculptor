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
    $global:IFS_WHATIF_PARAMETER_JSON = $null
    $global:IFS_WHATIF_PARAMETER_PATH = $null
    $global:IFS_STACK_PARAMETER_JSON = $null
    $global:IFS_STACK_PARAMETER_PATH = $null
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
        if ($text -match "bicep build-params") {
            $outputIndex = [Array]::IndexOf($Arguments, "--outfile") + 1
            $document = [ordered]@{
                '$schema' = "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#"
                contentVersion = "1.0.0.0"
                parameters = [ordered]@{
                    caApi = [ordered]@{ value = [ordered]@{
                        deploy = $true
                        name = "orders-api"
                        image = "bootstrap-image"
                        cpu = "0.5"
                        preserved = [ordered]@{ marker = "from-parameter-file" }
                    } }
                }
            }
            [IO.File]::WriteAllText($Arguments[$outputIndex], (ConvertTo-Json -InputObject $document -Depth 100), [Text.UTF8Encoding]::new($false))
            return "{}"
        }
        if ($text -match "deployment sub what-if") {
            $parameterIndex = [Array]::IndexOf($Arguments, "--parameters") + 1
            $parameterPath = $Arguments[$parameterIndex]
            if ($parameterPath.EndsWith(".json", [StringComparison]::OrdinalIgnoreCase)) {
                $global:IFS_WHATIF_PARAMETER_PATH = $parameterPath
                $global:IFS_WHATIF_PARAMETER_JSON = Get-Content -LiteralPath $parameterPath -Raw | ConvertFrom-Json -AsHashtable -Depth 100
            }
            return $global:IFS_WHATIF
        }
        if ($text -match "stack sub show") { return $global:IFS_STACK }
        if ($text -match "stack sub create") {
            $parameterIndex = [Array]::IndexOf($Arguments, "--parameters") + 1
            $parameterPath = $Arguments[$parameterIndex]
            if ($parameterPath.EndsWith(".json", [StringComparison]::OrdinalIgnoreCase)) {
                $global:IFS_STACK_PARAMETER_PATH = $parameterPath
                $global:IFS_STACK_PARAMETER_JSON = Get-Content -LiteralPath $parameterPath -Raw | ConvertFrom-Json -AsHashtable -Depth 100
            }
            return $global:IFS_STACK
        }
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
    It "efface le code natif non nul d'une commande Az attendue après l'avoir capturé" {
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 3
            return "ERROR: ResourceNotFound: deployment stack does not exist"
        }
        $module = Get-Module IfsRelease

        $result = & $module { Invoke-IfsAz -Arguments @("stack", "sub", "show") -AllowFailure }

        $result.ExitCode | Should -Be 3
        $global:LASTEXITCODE | Should -Be 0
    }

    It "reste en lecture seule, même si le journal est absent" {
        $preview = Invoke-IfsPreview -ReleaseData $script:orderData -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -OutputDirectory $script:tempRoot
        $preview.schema | Should -Be "ifs-preview/v1"
        (Get-Content (Join-Path $script:tempRoot "ifs-preview.json") -Raw | ConvertFrom-Json).fingerprint | Should -Not -BeNullOrEmpty
        ($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " } | Where-Object { $_ -match "lease|upload|delete|create|set|update" }) | Should -BeNullOrEmpty
        ($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " } | Where-Object { $_ -match "what-if|storage blob exists|storage blob download" }).Count | Should -BeGreaterThan 0
        $whatIfCall = $global:IFS_AZ_CALLS | Where-Object { ($_ -join " ") -match "deployment sub what-if" } | Select-Object -First 1
        $whatIfCall | Should -Contain "--no-pretty-print"
    }

    It "refuse une réponse What-If scalaire au lieu de publier un aperçu vide" {
        $global:IFS_WHATIF = '"pas-un-objet"'

        { Invoke-IfsPreview -ReleaseData $script:orderData -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -OutputDirectory $script:tempRoot } |
            Should -Throw "*objet JSON What-If*"
        Test-Path -LiteralPath (Join-Path $script:tempRoot "ifs-preview.json") | Should -BeFalse
    }

    It "identifie la commande What-If et le jeton initial d'une réponse invalide" {
        $global:IFS_WHATIF = 'invalid response'

        { Invoke-IfsPreview -ReleaseData $script:orderData -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -OutputDirectory $script:tempRoot } |
            Should -Throw "*az deployment sub what-if*invalid*"
        Test-Path -LiteralPath (Join-Path $script:tempRoot "ifs-preview.json") | Should -BeFalse
    }

    It "identifie l'identité gérée si Azure CLI renvoie une sortie invalide" {
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            if (($Arguments -join " ") -match "identity show") { return "invalid identity response" }
            return "{}"
        }
        $release = [pscustomobject]@{ project = "shop42"; target = "dev"; subscriptionId = "test" }

        { Get-IfsManagedIdentity -ReleaseData $release -Kind deploy } |
            Should -Throw "*az identity show pour id-ifs-deploy-shop42-dev*invalid*"
    }

    It "inclut l'Object ID applicatif dans l'aperçu orders" {
        $applicationId = "22222222-2222-2222-2222-222222222222"
        Invoke-IfsPreview -ReleaseData $script:orderData -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -OutputDirectory $script:tempRoot -ApplicationIdentityObjectId $applicationId | Out-Null
        $call = $global:IFS_AZ_CALLS | Where-Object { ($_ -join " ") -match "deployment sub what-if" } | Select-Object -First 1

        $call | Should -Contain "appDeliveryPrincipalId=$applicationId"
    }

    It "transmet l'image applicative actuelle au What-If" {
        $image = "mcr.microsoft.com/example:1"
        $preview = Invoke-IfsPreview -ReleaseData $script:orderData -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -OutputDirectory $script:tempRoot
        $call = $global:IFS_AZ_CALLS | Where-Object { ($_ -join " ") -match "deployment sub what-if" } | Select-Object -First 1

        $parameterIndex = [Array]::IndexOf($call, "--parameters") + 1
        $call[$parameterIndex] | Should -Match "\.json$"
        $call | Should -Not -Contain "caApi.image=$image"
        $global:IFS_WHATIF_PARAMETER_JSON.parameters.caApi.value.image | Should -Be $image
        $global:IFS_WHATIF_PARAMETER_JSON.parameters.caApi.value.preserved.marker | Should -Be "from-parameter-file"
        Test-Path -LiteralPath $global:IFS_WHATIF_PARAMETER_PATH | Should -BeFalse
        $preview.appOwnedState[0].image | Should -Be $image
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

    It "ne prend pas un rôle What-If non pris en charge pour un accès retiré" {
        $roleId = "/subscriptions/test/resourceGroups/core/providers/Microsoft.KeyVault/vaults/kv/providers/Microsoft.Authorization/roleAssignments/current"
        $symbolicRoleId = "[extensionResourceId('/subscriptions/test/resourceGroups/core/providers/Microsoft.KeyVault/vaults/kv', 'Microsoft.Authorization/roleAssignments', guid(...))]"
        $global:IFS_STACK = @{ resources = @(@{ id = $roleId }) } | ConvertTo-Json -Depth 20 -Compress
        $global:IFS_WHATIF = @{ changes = @(@{ resourceId = $symbolicRoleId; changeType = "Unsupported" }) } | ConvertTo-Json -Depth 20 -Compress

        $preview = Invoke-IfsPreview -ReleaseData $script:orderData -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -OutputDirectory $script:tempRoot

        ($preview.revokedAccess | ForEach-Object resourceId) | Should -Not -Contain $roleId
        $preview.limitations | Should -Contain "What-If ne résout pas certains identifiants d’accès ; leur absence ne déclenche pas une révocation implicite."
    }
}

Describe "Journal d'opérations" {
    It "retourne un journal vide absent sans créer de blob" {
        $journal = Read-IfsOperationJournal -ReleaseData $script:orderData
        $journal.operations.Count | Should -Be 0
        ($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " } | Where-Object { $_ -match "upload|lease" }) | Should -BeNullOrEmpty
    }

    It "refuse de créer le conteneur ou le journal en mode reprise existante" {
        { Open-IfsOperationJournal -ReleaseData $script:orderData -RequireExisting -WaitTimeoutSeconds 1 } |
            Should -Throw "*journal attendu pour la reprise est absent*"
        ($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " } | Where-Object { $_ -match "storage container create|storage blob upload|storage blob lease acquire" }) |
            Should -BeNullOrEmpty
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
        Mock Invoke-IfsSecretWrite -ModuleName IfsRelease {}
        $context = [pscustomobject]@{
            Journal = [pscustomobject]@{ schema = "ifs-operation-journal/v1"; unit = "ifs-shop-orders-dev"; target = "dev"; operations = @() }
            Account = "stifsshopdev"; Container = "ifs-operations"; Blob = "ifs-shop-orders-dev.json"; LeaseId = "test"; RenewalFailureFile = $null
        }
        $oldWrite = [pscustomobject]@{ vault = "kv"; secret = "token"; value = "old" }
        $newWrite = [pscustomobject]@{ vault = "kv"; secret = "token"; value = "new" }
        $previous = Add-IfsOperation -Context $context -Revision 3 -Commit "abc123" -Kind "secret-write" -ObjectId "kv/token" -Details ([pscustomobject]@{ write = $oldWrite; phase = "preDeploy" })
        Set-IfsOperationState -Context $context -OperationId $previous.id -State "Started" | Out-Null
        Set-IfsOperationState -Context $context -OperationId $previous.id -State "Done" | Out-Null

        $current = Add-IfsOperation -Context $context -Revision 4 -Commit "def456" -Kind "secret-write" -ObjectId "kv/token" -Details ([pscustomobject]@{ write = $newWrite; phase = "preDeploy" })

        $current.id | Should -Be $previous.id
        $current.state | Should -Be "ToDo"
        $current.revision | Should -Be 4
        $current.commit | Should -Be "def456"
        $current.details.write.value | Should -Be "new"
        $sameRelease = Add-IfsOperation -Context $context -Revision 4 -Commit "def456" -Kind "secret-write" -ObjectId "kv/token" -Details ([pscustomobject]@{ write = [pscustomobject]@{ vault = "kv"; secret = "token"; value = "ignored" }; phase = "preDeploy" })
        $sameRelease.state | Should -Be "ToDo"
        $sameRelease.details.write.value | Should -Be "new"

        $releaseData = [pscustomobject]@{ secretWrites = @($newWrite); restartOnSecretChange = $false }
        InModuleScope IfsRelease -Parameters @{ ContextValue = $context; ReleaseDataValue = $releaseData } {
            Invoke-IfsPendingOperation -Context $ContextValue -ReleaseData $ReleaseDataValue -TemplateFile "main.bicep" -ParameterFile "main.dev.bicepparam" -DeploymentIdentityObjectId "identity" -SqlScript "data-access.sql" -Revision 4 -Commit "def456" -RunId "run-4" | Out-Null
        }
        $current.state | Should -Be "Done"
        Should -Invoke Invoke-IfsSecretWrite -ModuleName IfsRelease -Times 1 -Exactly
    }

    It "reprend une opération Started après interruption et la termine" {
        Mock Save-IfsJournalBlob -ModuleName IfsRelease {}
        Mock Invoke-IfsSecretWrite -ModuleName IfsRelease {}
        $secretWrite = [pscustomobject]@{ vault = "kv"; secret = "token"; value = "resumed" }
        $operation = [pscustomobject]@{
            id = "a" * 64; revision = 4; commit = "def456"; kind = "secret-write"; objectId = "kv/token"; state = "Started"
            createdAt = [DateTime]::UtcNow.ToString("o"); updatedAt = [DateTime]::UtcNow.ToString("o"); note = $null
            details = [pscustomobject]@{ write = $secretWrite; phase = "preDeploy" }
        }
        $context = [pscustomobject]@{
            Journal = [pscustomobject]@{ schema = "ifs-operation-journal/v1"; unit = "ifs-shop-orders-dev"; target = "dev"; operations = @($operation) }
            Account = "stifsshopdev"; Container = "ifs-operations"; Blob = "ifs-shop-orders-dev.json"; LeaseId = "test"; RenewalFailureFile = $null
        }
        $releaseData = [pscustomobject]@{ secretWrites = @($secretWrite); restartOnSecretChange = $false }

        $result = InModuleScope IfsRelease -Parameters @{ ContextValue = $context; ReleaseDataValue = $releaseData } {
            Invoke-IfsPendingOperation -Context $ContextValue -ReleaseData $ReleaseDataValue -TemplateFile "main.bicep" -ParameterFile "main.dev.bicepparam" -DeploymentIdentityObjectId "identity" -SqlScript "data-access.sql" -Revision 4 -Commit "def456" -RunId "run-resume"
        }

        $operation.state | Should -Be "Done"
        Should -Invoke Invoke-IfsSecretWrite -ModuleName IfsRelease -Times 1 -Exactly
        $result.revokedAccess | Should -BeNullOrEmpty
    }

    It "transmet les dépendances du serveur lors de la reprise d'un accès SQL" {
        Mock Save-IfsJournalBlob -ModuleName IfsRelease {}
        Mock Invoke-IfsDataAccess -ModuleName IfsRelease {
            param($ReleaseData, $SqlScript, $RunId)
            $global:IFS_REPLAY_SQL_DEPENDENCIES = @($ReleaseData.dependencies)
            return [pscustomobject]@{ temporaryFirewallRules = @(); cleanedFirewallRules = @() }
        }
        $now = [DateTime]::UtcNow.ToString("o")
        $operation = [pscustomobject]@{
            id = "a" * 64; revision = 4; commit = "def456"; kind = "data-access"; objectId = "server/database/principal"; state = "Started"
            createdAt = $now; updatedAt = $now; note = $null; details = $script:orderData.dataAccess[0]
        }
        $context = [pscustomobject]@{
            Journal = [pscustomobject]@{ schema = "ifs-operation-journal/v1"; unit = "ifs-shop-orders-dev"; target = "dev"; operations = @($operation) }
            Account = "stifsshopdev"; Container = "ifs-operations"; Blob = "ifs-shop-orders-dev.json"; LeaseId = "test"; RenewalFailureFile = $null
        }
        $expectedServer = @($script:orderData.dependencies | ForEach-Object { $_.resources } | Where-Object { $_.id -match '/providers/Microsoft\.Sql/servers/[^/]+$' } | Select-Object -First 1)[0].id
        $global:IFS_REPLAY_SQL_DEPENDENCIES = @()

        InModuleScope IfsRelease -Parameters @{ ContextValue = $context; ReleaseDataValue = $script:orderData } {
            Invoke-IfsPendingOperation -Context $ContextValue -ReleaseData $ReleaseDataValue -TemplateFile "main.bicep" -ParameterFile "main.dev.bicepparam" -DeploymentIdentityObjectId "identity" -SqlScript "data-access.sql" -Revision 4 -Commit "def456" -RunId "run-resume" | Out-Null
        }

        @($global:IFS_REPLAY_SQL_DEPENDENCIES | ForEach-Object { $_.resources } | ForEach-Object { $_.id }) | Should -Contain $expectedServer
        $operation.state | Should -Be "Done"
        Should -Invoke Invoke-IfsDataAccess -ModuleName IfsRelease -Times 1 -Exactly
    }

    Describe "Reprise ciblée d'un accès SQL PRD" {
        BeforeEach {
            $global:IFS_RECOVERY_SQL_CALLS = @()
            $global:IFS_RECOVERY_SQL_FAILURE = $false
            $access = [pscustomobject]@{
                server = "sql-shop42-orders-prd.database.windows.net"
                database = "sqldb-shop42-orders-prd"
                principal = [pscustomobject]@{
                    resourceId = "/subscriptions/test/resourceGroups/rg-shop42-orders-prd/providers/Microsoft.App/containerApps/ca-shop42-api-prd"
                    kind = "systemAssigned"
                }
                level = "ReadWrite"
                exposure = "Public"
            }
            $script:recoveryObjectId = "{0}/{1}/{2}" -f $access.server, $access.database, $access.principal.resourceId
            $operationKey = "ifs-shop42-orders-prd|prd|data-access|$($script:recoveryObjectId)"
            $script:recoveryOperationId = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($operationKey))).ToLowerInvariant()
            $script:recoveryCompletionCommit = "b" * 40
            $operation = [pscustomobject]@{
                id = $script:recoveryOperationId
                revision = 2
                commit = "893f2a3dda4d7c8461be9c93f208a9e84d317962"
                kind = "data-access"
                objectId = $script:recoveryObjectId
                state = "Started"
                createdAt = [DateTime]::UtcNow.ToString("o")
                updatedAt = [DateTime]::UtcNow.ToString("o")
                note = $null
                details = [pscustomobject]@{
                    server = $access.server
                    database = $access.database
                    principal = [pscustomobject]@{ resourceId = $access.principal.resourceId; kind = $access.principal.kind }
                    level = $access.level
                    exposure = $access.exposure
                }
            }
            $global:IFS_RECOVERY_CONTEXT = [pscustomobject]@{
                Journal = [pscustomobject]@{
                    schema = "ifs-operation-journal/v1"
                    unit = "ifs-shop42-orders-prd"
                    target = "prd"
                    operations = @($operation)
                }
                Account = "stifsshop42prd"
                Container = "ifs-operations"
                Blob = "ifs-shop42-orders-prd.json"
                LeaseId = "test"
                RenewalFailureFile = $null
            }
            $script:recoveryRelease = [pscustomobject]@{
                project = "shop42"
                component = "orders"
                target = "prd"
                unit = "ifs-shop42-orders-prd"
                protected = $true
                subscriptionId = "test-subscription"
                dataAccess = @($access)
                dependencies = @()
            }
            Mock Open-IfsOperationJournal -ModuleName IfsRelease {
                param($ReleaseData, $WaitTimeoutSeconds, $PollIntervalSeconds, $RequireExisting)
                $global:IFS_RECOVERY_REQUIRE_EXISTING = [bool]$RequireExisting
                $global:IFS_RECOVERY_CONTEXT
            }
            Mock Close-IfsOperationJournal -ModuleName IfsRelease { }
            Mock Save-IfsJournalBlob -ModuleName IfsRelease { }
            Mock Invoke-IfsDataAccess -ModuleName IfsRelease {
                param($ReleaseData, $SqlScript, $RunId, $SkipStaleFirewallCleanup)
                $global:IFS_RECOVERY_SQL_CALLS += @([pscustomobject]@{ ReleaseData = $ReleaseData; SqlScript = $SqlScript; RunId = $RunId; SkipStaleFirewallCleanup = [bool]$SkipStaleFirewallCleanup })
                if ($global:IFS_RECOVERY_SQL_FAILURE) { throw "SQL indisponible" }
                return [pscustomobject]@{ temporaryFirewallRules = @(); cleanedFirewallRules = @() }
            }
        }

        It "ne reprend que l'opération attendue puis la marque Done" {
            $result = Invoke-IfsDataAccessRecovery -ReleaseData $script:recoveryRelease -OperationId $script:recoveryOperationId -ExpectedRevision 2 -ExpectedCommit "893f2a3dda4d7c8461be9c93f208a9e84d317962" -CompletionCommit $script:recoveryCompletionCommit -SqlScript "data-access.sql" -RunId "recovery-762"

            $global:IFS_RECOVERY_CONTEXT.Journal.operations[0].state | Should -Be "Done"
            $global:IFS_RECOVERY_CONTEXT.Journal.operations[0].commit | Should -Be $script:recoveryCompletionCommit
            $global:IFS_RECOVERY_SQL_CALLS.Count | Should -Be 1
            @($global:IFS_RECOVERY_SQL_CALLS[0].ReleaseData.dataAccess).Count | Should -Be 1
            $global:IFS_RECOVERY_SQL_CALLS[0].RunId | Should -Be "recovery-762"
            $global:IFS_RECOVERY_SQL_CALLS[0].SkipStaleFirewallCleanup | Should -BeTrue
            $global:IFS_RECOVERY_REQUIRE_EXISTING | Should -BeTrue
            $result.operationId | Should -Be $script:recoveryOperationId
            $result.commit | Should -Be $script:recoveryCompletionCommit
            Should -Invoke Close-IfsOperationJournal -ModuleName IfsRelease -Times 1 -Exactly

            InModuleScope IfsRelease -Parameters @{
                ContextValue = $global:IFS_RECOVERY_CONTEXT
                ObjectIdValue = $script:recoveryObjectId
                DetailsValue = $global:IFS_RECOVERY_CONTEXT.Journal.operations[0].details
                CompletionCommitValue = $script:recoveryCompletionCommit
            } {
                $replayed = Add-IfsOperation -Context $ContextValue -Revision 2 -Commit $CompletionCommitValue -Kind "data-access" -ObjectId $ObjectIdValue -Details $DetailsValue
                $replayed.state | Should -Be "Done"
            }
        }

        It "refuse toute autre opération en attente sans exécuter SQL" {
            $other = [pscustomobject]@{ id = "2" * 64; state = "ToDo"; kind = "secret-write"; objectId = "kv/secret" }
            $global:IFS_RECOVERY_CONTEXT.Journal.operations += $other

            { Invoke-IfsDataAccessRecovery -ReleaseData $script:recoveryRelease -OperationId $script:recoveryOperationId -ExpectedRevision 2 -ExpectedCommit "893f2a3dda4d7c8461be9c93f208a9e84d317962" -CompletionCommit $script:recoveryCompletionCommit -SqlScript "data-access.sql" -RunId "recovery-762" } |
                Should -Throw "*une seule opération en attente*"
            $global:IFS_RECOVERY_SQL_CALLS | Should -BeNullOrEmpty
            $global:IFS_RECOVERY_CONTEXT.Journal.operations[0].state | Should -Be "Started"
        }

        It "refuse un identifiant d'opération différent" {
            { Invoke-IfsDataAccessRecovery -ReleaseData $script:recoveryRelease -OperationId ("2" * 64) -ExpectedRevision 2 -ExpectedCommit "893f2a3dda4d7c8461be9c93f208a9e84d317962" -CompletionCommit $script:recoveryCompletionCommit -SqlScript "data-access.sql" -RunId "recovery-762" } |
                Should -Throw "*identifiant, la révision et au commit autorisés*"
            $global:IFS_RECOVERY_SQL_CALLS | Should -BeNullOrEmpty
            $global:IFS_RECOVERY_CONTEXT.Journal.operations[0].state | Should -Be "Started"
        }

        It "refuse un commit source différent de celui de l'opération" {
            { Invoke-IfsDataAccessRecovery -ReleaseData $script:recoveryRelease -OperationId $script:recoveryOperationId -ExpectedRevision 2 -ExpectedCommit "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" -CompletionCommit $script:recoveryCompletionCommit -SqlScript "data-access.sql" -RunId "recovery-762" } |
                Should -Throw "*identifiant, la révision et au commit autorisés*"
            $global:IFS_RECOVERY_SQL_CALLS | Should -BeNullOrEmpty
            $global:IFS_RECOVERY_CONTEXT.Journal.operations[0].state | Should -Be "Started"
        }

        It "refuse un manifeste d'accès qui ne correspond plus au détail journalisé" {
            $script:recoveryRelease.dataAccess[0].level = "ReadOnly"

            { Invoke-IfsDataAccessRecovery -ReleaseData $script:recoveryRelease -OperationId $script:recoveryOperationId -ExpectedRevision 2 -ExpectedCommit "893f2a3dda4d7c8461be9c93f208a9e84d317962" -CompletionCommit $script:recoveryCompletionCommit -SqlScript "data-access.sql" -RunId "recovery-762" } |
                Should -Throw "*ne correspond plus à la release*"
            $global:IFS_RECOVERY_SQL_CALLS | Should -BeNullOrEmpty
            $global:IFS_RECOVERY_CONTEXT.Journal.operations[0].state | Should -Be "Started"
        }

        It "garde l'opération Started si l'exécution SQL échoue" {
            $global:IFS_RECOVERY_SQL_FAILURE = $true

            { Invoke-IfsDataAccessRecovery -ReleaseData $script:recoveryRelease -OperationId $script:recoveryOperationId -ExpectedRevision 2 -ExpectedCommit "893f2a3dda4d7c8461be9c93f208a9e84d317962" -CompletionCommit $script:recoveryCompletionCommit -SqlScript "data-access.sql" -RunId "recovery-762" } |
                Should -Throw "*SQL indisponible*"
            $global:IFS_RECOVERY_CONTEXT.Journal.operations[0].state | Should -Be "Started"
            $global:IFS_RECOVERY_CONTEXT.Journal.operations[0].commit | Should -Be "893f2a3dda4d7c8461be9c93f208a9e84d317962"
            Should -Invoke Close-IfsOperationJournal -ModuleName IfsRelease -Times 1 -Exactly
        }

        It "refuse une exposition restreinte qui demanderait une règle de pare-feu" {
            $script:recoveryRelease.dataAccess[0].exposure = "Restricted"
            $global:IFS_RECOVERY_CONTEXT.Journal.operations[0].details.exposure = "Restricted"

            { Invoke-IfsDataAccessRecovery -ReleaseData $script:recoveryRelease -OperationId $script:recoveryOperationId -ExpectedRevision 2 -ExpectedCommit "893f2a3dda4d7c8461be9c93f208a9e84d317962" -CompletionCommit $script:recoveryCompletionCommit -SqlScript "data-access.sql" -RunId "recovery-762" } |
                Should -Throw "*n'autorise pas de mutation de règle de pare-feu*"
            $global:IFS_RECOVERY_SQL_CALLS | Should -BeNullOrEmpty
            $global:IFS_RECOVERY_CONTEXT.Journal.operations[0].state | Should -Be "Started"
        }

        It "refuse une cible autre que PRD avant de prendre le bail" {
            $script:recoveryRelease.target = "dev"

            { Invoke-IfsDataAccessRecovery -ReleaseData $script:recoveryRelease -OperationId $script:recoveryOperationId -ExpectedRevision 2 -ExpectedCommit "893f2a3dda4d7c8461be9c93f208a9e84d317962" -CompletionCommit $script:recoveryCompletionCommit -SqlScript "data-access.sql" -RunId "recovery-762" } |
                Should -Throw "*cible PRD protégée*"
            $global:IFS_RECOVERY_SQL_CALLS | Should -BeNullOrEmpty
            Should -Not -Invoke Open-IfsOperationJournal -ModuleName IfsRelease
        }
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
    It "refuse une application quand l'empreinte approuvée a changé avant toute mutation" {
        $manifestPath = Join-Path $script:tempRoot "manifest-preview-mismatch.json"
        $approvedPath = Join-Path $script:tempRoot "approved-preview-mismatch.json"
        $reportPath = Join-Path $script:tempRoot "report-preview-mismatch.json"
        $approvedFingerprint = "a" * 64
        Set-Content -LiteralPath $manifestPath -Value '{"revision":4,"commit":"rev4","units":{"core":{"fingerprint":"abc"}}}'
        Set-Content -LiteralPath $approvedPath -Value (ConvertTo-Json -InputObject ([ordered]@{ fingerprint = $approvedFingerprint }) -Compress)

        Mock Test-IfsNewerManifest -ModuleName IfsRelease { return $false }
        Mock Invoke-IfsPreview -ModuleName IfsRelease {
            return [pscustomobject]@{ fingerprint = ("b" * 64) }
        }
        {
            Invoke-IfsInfraDeploy -ReleasePath $script:coreRelease -ManifestPath $manifestPath -PreviewPath $approvedPath -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -SqlScript "" -OutputPath $reportPath -DefaultBranch "main" -ManifestPathInRepository ".ifs/manifest.json" -DeploymentIdentityObjectId "00000000-0000-0000-0000-000000000001"
        } | Should -Throw "*Les effets ont changé depuis l'aperçu approuvé*"

        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        $report.result | Should -Be "FailedBeforeMutation"
        $report.schema | Should -Be "ifs-report/v2"
        $report.approvedPreviewFingerprint | Should -Be $approvedFingerprint
        @($report.journal.operations | Where-Object { $_.state -in @("Started", "Done") }).Count | Should -Be 0
        $calls = @($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " })
        ($calls | Select-String "storage blob lease acquire|storage blob upload|stack sub create|resource delete|keyvault secret set|firewall-rule create") | Should -BeNullOrEmpty
    }

    It "conserve l'aperçu recalculé sans remplacer l'aperçu approuvé en cas de divergence" {
        $approvedDirectory = Join-Path $script:tempRoot "approved-preview-mismatch-capture"
        $reportDirectory = Join-Path $script:tempRoot "reports-preview-mismatch-capture"
        New-Item -ItemType Directory -Path $approvedDirectory, $reportDirectory -Force | Out-Null
        $approvedPath = Join-Path $approvedDirectory "ifs-preview.json"
        $reportPath = Join-Path $reportDirectory "ifs-report.json"
        $manifestPath = Join-Path $script:tempRoot "manifest-preview-mismatch-capture.json"
        Set-Content -LiteralPath $manifestPath -Value '{"revision":4,"commit":"rev4","units":{"core":{"fingerprint":"abc"}}}'

        $preview = [pscustomobject][ordered]@{
            schema = "ifs-preview/v1"
            project = "shop42"
            component = "core"
            unit = "ifs-shop42-core-dev"
            target = "dev"
            generatedAt = "2026-01-01T00:00:00.0000000Z"
            fingerprint = "b" * 64
            pendingOperations = @()
            created = @()
            modified = @()
            recreated = @()
            detachedOrDeleted = @()
            revokedAccess = @()
            dataWrites = @()
            appOwnedState = @()
            limitations = @("aperçu de test")
        }
        $recalculatedBytes = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $preview -Depth 100))
        $approved = [ordered]@{}
        foreach ($property in $preview.PSObject.Properties) { $approved[$property.Name] = $property.Value }
        $approved.fingerprint = "f" * 64
        $approvedBytes = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $approved -Depth 100))
        [IO.File]::WriteAllBytes($approvedPath, $approvedBytes)

        $global:IFS_RECALCULATED_PREVIEW = $preview
        $global:IFS_RECALCULATED_PREVIEW_BYTES = $recalculatedBytes
        Mock Test-IfsNewerManifest -ModuleName IfsRelease { return $false }
        Mock Invoke-IfsPreview -ModuleName IfsRelease {
            param($OutputDirectory)
            New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
            [IO.File]::WriteAllBytes((Join-Path $OutputDirectory "ifs-preview.json"), $global:IFS_RECALCULATED_PREVIEW_BYTES)
            return $global:IFS_RECALCULATED_PREVIEW
        }

        {
            Invoke-IfsInfraDeploy -ReleasePath $script:coreRelease -ManifestPath $manifestPath -PreviewPath $approvedPath -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -SqlScript "" -OutputPath $reportPath -DefaultBranch "main" -ManifestPathInRepository ".ifs/manifest.json" -DeploymentIdentityObjectId "00000000-0000-0000-0000-000000000001"
        } | Should -Throw "*Les effets ont changé depuis l'aperçu approuvé*"

        $recalculatedPath = Join-Path $reportDirectory "ifs-preview-recalculated.json"
        Test-Path -LiteralPath $recalculatedPath -PathType Leaf | Should -BeTrue
        [Convert]::ToBase64String([IO.File]::ReadAllBytes($approvedPath)) | Should -Be ([Convert]::ToBase64String($approvedBytes))
        [Convert]::ToBase64String([IO.File]::ReadAllBytes($recalculatedPath)) | Should -Be ([Convert]::ToBase64String($recalculatedBytes))

        $recalculatedJson = Get-Content -LiteralPath $recalculatedPath -Raw
        $recalculatedPreview = $recalculatedJson | ConvertFrom-Json
        $recalculatedPreview.fingerprint | Should -Be $preview.fingerprint
        $recalculatedPreview.fingerprint | Should -Not -Be ("f" * 64)
        (Test-Json -Json $recalculatedJson -SchemaFile (Join-Path $script:repoRoot "reference/pilot/bicep-azdo/.ifs/templates/schemas/ifs-preview.schema.json")) | Should -BeTrue

        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        $report.result | Should -Be "FailedBeforeMutation"
        @($report.journal.operations | Where-Object { $_.state -in @("Started", "Done") }).Count | Should -Be 0
        $calls = @($global:IFS_AZ_CALLS | ForEach-Object { $_ -join " " })
        ($calls | Select-String "storage blob lease acquire|storage blob upload|stack sub create|resource delete|keyvault secret set|firewall-rule create") | Should -BeNullOrEmpty
    }

    It "ne publie pas d'aperçu recalculé quand les empreintes sont égales" {
        $approvedDirectory = Join-Path $script:tempRoot "approved-preview-equal"
        $reportDirectory = Join-Path $script:tempRoot "reports-preview-equal"
        New-Item -ItemType Directory -Path $approvedDirectory, $reportDirectory -Force | Out-Null
        $approvedPath = Join-Path $approvedDirectory "ifs-preview.json"
        $reportPath = Join-Path $reportDirectory "ifs-report.json"
        $manifestPath = Join-Path $script:tempRoot "manifest-preview-equal.json"
        Set-Content -LiteralPath $manifestPath -Value '{"revision":4,"commit":"rev4","units":{"core":{"fingerprint":"abc"}}}'

        $approvedPreview = [pscustomobject][ordered]@{
            schema = "ifs-preview/v1"
            project = "shop42"
            component = "core"
            unit = "ifs-shop42-core-dev"
            target = "dev"
            generatedAt = "2026-01-01T00:00:00.0000000Z"
            fingerprint = "b" * 64
            pendingOperations = @()
            created = @()
            modified = @()
            recreated = @()
            detachedOrDeleted = @()
            revokedAccess = @()
            dataWrites = @()
            appOwnedState = @()
            limitations = @("aperçu de test")
        }
        $approvedBytes = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $approvedPreview -Depth 100))
        [IO.File]::WriteAllBytes($approvedPath, $approvedBytes)
        $currentPreview = [ordered]@{}
        foreach ($property in $approvedPreview.PSObject.Properties) { $currentPreview[$property.Name] = $property.Value }
        $currentPreview.generatedAt = "2026-02-01T00:00:00.0000000Z"
        $global:IFS_RECALCULATED_PREVIEW = [pscustomobject]$currentPreview
        $global:IFS_RECALCULATED_PREVIEW_BYTES = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $global:IFS_RECALCULATED_PREVIEW -Depth 100))
        Mock Test-IfsNewerManifest -ModuleName IfsRelease { return $false }
        Mock Invoke-IfsPreview -ModuleName IfsRelease {
            param($OutputDirectory)
            New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
            [IO.File]::WriteAllBytes((Join-Path $OutputDirectory "ifs-preview.json"), $global:IFS_RECALCULATED_PREVIEW_BYTES)
            return $global:IFS_RECALCULATED_PREVIEW
        }
        Mock Open-IfsOperationJournal -ModuleName IfsRelease { throw "arrêt de test après comparaison" }

        {
            Invoke-IfsInfraDeploy -ReleasePath $script:coreRelease -ManifestPath $manifestPath -PreviewPath $approvedPath -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -SqlScript "" -OutputPath $reportPath -DefaultBranch "main" -ManifestPathInRepository ".ifs/manifest.json" -DeploymentIdentityObjectId "00000000-0000-0000-0000-000000000001"
        } | Should -Throw "*arrêt de test après comparaison*"

        Test-Path -LiteralPath (Join-Path $reportDirectory "ifs-preview-recalculated.json") -PathType Leaf | Should -BeFalse
        [Convert]::ToBase64String([IO.File]::ReadAllBytes($approvedPath)) | Should -Be ([Convert]::ToBase64String($approvedBytes))
        $approvedPreview.fingerprint | Should -Be $currentPreview.fingerprint
    }

    It "conserve le refus d'origine si l'écriture de l'aperçu recalculé échoue" {
        $approvedDirectory = Join-Path $script:tempRoot "approved-preview-write-failure"
        $reportDirectory = Join-Path $script:tempRoot "reports-preview-write-failure"
        New-Item -ItemType Directory -Path $approvedDirectory, $reportDirectory -Force | Out-Null
        $approvedPath = Join-Path $approvedDirectory "ifs-preview.json"
        $reportPath = Join-Path $reportDirectory "ifs-report.json"
        $manifestPath = Join-Path $script:tempRoot "manifest-preview-write-failure.json"
        Set-Content -LiteralPath $manifestPath -Value '{"revision":4,"commit":"rev4","units":{"core":{"fingerprint":"abc"}}}'

        $preview = [pscustomobject][ordered]@{
            schema = "ifs-preview/v1"
            project = "shop42"
            component = "core"
            unit = "ifs-shop42-core-dev"
            target = "dev"
            generatedAt = "2026-01-01T00:00:00.0000000Z"
            fingerprint = "b" * 64
            pendingOperations = @()
            created = @()
            modified = @()
            recreated = @()
            detachedOrDeleted = @()
            revokedAccess = @()
            dataWrites = @()
            appOwnedState = @()
            limitations = @("aperçu de test")
        }
        $recalculatedBytes = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $preview -Depth 100))
        $approved = [ordered]@{}
        foreach ($property in $preview.PSObject.Properties) { $approved[$property.Name] = $property.Value }
        $approved.fingerprint = "f" * 64
        $approvedBytes = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $approved -Depth 100))
        [IO.File]::WriteAllBytes($approvedPath, $approvedBytes)
        $global:IFS_RECALCULATED_PREVIEW = $preview
        $global:IFS_RECALCULATED_PREVIEW_BYTES = $recalculatedBytes
        Mock Test-IfsNewerManifest -ModuleName IfsRelease { return $false }
        Mock Invoke-IfsPreview -ModuleName IfsRelease {
            param($OutputDirectory)
            New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
            [IO.File]::WriteAllBytes((Join-Path $OutputDirectory "ifs-preview.json"), $global:IFS_RECALCULATED_PREVIEW_BYTES)
            return $global:IFS_RECALCULATED_PREVIEW
        }
        Mock Copy-Item -ModuleName IfsRelease { throw "échec d'écriture simulé" }

        {
            Invoke-IfsInfraDeploy -ReleasePath $script:coreRelease -ManifestPath $manifestPath -PreviewPath $approvedPath -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -SqlScript "" -OutputPath $reportPath -DefaultBranch "main" -ManifestPathInRepository ".ifs/manifest.json" -DeploymentIdentityObjectId "00000000-0000-0000-0000-000000000001"
        } | Should -Throw "*Les effets ont changé depuis l'aperçu approuvé*"

        Assert-MockCalled Copy-Item -ModuleName IfsRelease -Times 1
        [Convert]::ToBase64String([IO.File]::ReadAllBytes($approvedPath)) | Should -Be ([Convert]::ToBase64String($approvedBytes))
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        $report.result | Should -Be "FailedBeforeMutation"
        @($report.journal.operations | Where-Object { $_.state -in @("Started", "Done") }).Count | Should -Be 0
    }

    It "versionne le schéma du rapport en conservant la lecture des rapports v1" {
        $schemaRoot = Join-Path $script:repoRoot "reference/pilot/bicep-azdo/.ifs/templates/schemas"
        $v1SchemaPath = Join-Path $schemaRoot "ifs-report.schema.json"
        $v2SchemaPath = Join-Path $schemaRoot "ifs-report.v2.schema.json"
        Test-Path -LiteralPath $v2SchemaPath -PathType Leaf | Should -BeTrue

        $v1Schema = Get-Content -LiteralPath $v1SchemaPath -Raw | ConvertFrom-Json -AsHashtable
        $v1Schema.properties.schema.const | Should -Be "ifs-report/v1"
        $v2Schema = Get-Content -LiteralPath $v2SchemaPath -Raw | ConvertFrom-Json -AsHashtable
        $v2Schema.properties.schema.const | Should -Be "ifs-report/v2"
        $v2Schema.required | Should -Contain "approvedPreviewFingerprint"

        $v2Report = [ordered]@{
            schema = "ifs-report/v2"
            project = "shop"
            component = "orders"
            unit = "ifs-shop-orders-dev"
            target = "dev"
            revision = 4
            commit = "rev4"
            manifestFingerprint = "b" * 64
            approvedPreviewFingerprint = "a" * 64
            generatedAt = [DateTime]::UtcNow.ToString("o")
            steps = @()
            journal = [ordered]@{ schema = "ifs-operation-journal/v1"; unit = "ifs-shop-orders-dev"; target = "dev"; operations = @() }
            detachedResources = @()
            revokedAccess = @()
            temporaryFirewallRules = @()
            cleanedFirewallRules = @()
            result = "Succeeded"
            error = $null
        }
        $v2Json = ConvertTo-Json -InputObject $v2Report -Depth 100
        (Test-Json -Json $v2Json -SchemaFile $v2SchemaPath) | Should -BeTrue

        $v2ReportWithoutFingerprint = [ordered]@{}
        foreach ($key in $v2Report.Keys) {
            if ($key -ne "approvedPreviewFingerprint") { $v2ReportWithoutFingerprint[$key] = $v2Report[$key] }
        }
        $withoutFingerprintJson = ConvertTo-Json -InputObject $v2ReportWithoutFingerprint -Depth 100
        (Test-Json -Json $withoutFingerprintJson -SchemaFile $v2SchemaPath -ErrorAction SilentlyContinue) | Should -BeFalse

        $v2Report.approvedPreviewFingerprint = "A" * 64
        $invalidFingerprintJson = ConvertTo-Json -InputObject $v2Report -Depth 100
        (Test-Json -Json $invalidFingerprintJson -SchemaFile $v2SchemaPath -ErrorAction SilentlyContinue) | Should -BeFalse

        $v1Report = [ordered]@{}
        foreach ($key in $v2Report.Keys) {
            if ($key -ne "approvedPreviewFingerprint") { $v1Report[$key] = $v2Report[$key] }
        }
        $v1Report.schema = "ifs-report/v1"
        $v1Json = ConvertTo-Json -InputObject $v1Report -Depth 100
        (Test-Json -Json $v1Json -SchemaFile $v1SchemaPath) | Should -BeTrue
        (Test-Json -Json $v1Json -SchemaFile $v2SchemaPath -ErrorAction SilentlyContinue) | Should -BeFalse

        $recordingRoot = Join-Path $script:repoRoot "src/backend/tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings"
        if (Test-Path -LiteralPath $recordingRoot -PathType Container) {
            $capturedReports = @(Get-ChildItem -LiteralPath $recordingRoot -Filter "ifs-report.json" -File -Recurse)
            foreach ($capturedReport in $capturedReports) {
                $capturedJson = Get-Content -LiteralPath $capturedReport.FullName -Raw
                $captured = ConvertFrom-Json -InputObject $capturedJson
                if ($captured.schema -eq "ifs-report/v1") {
                    (Test-Json -Json $capturedJson -SchemaFile $v1SchemaPath) | Should -BeTrue
                }
            }
        }
    }
    It "accepte un script SQL vide pour le déploiement d'une unité sans accès SQL" {
        $manifestPath = Join-Path $script:tempRoot "manifest-empty-sql.json"
        $approvedPath = Join-Path $script:tempRoot "approved-empty-sql.json"
        $reportPath = Join-Path $script:tempRoot "report-empty-sql.json"
        Set-Content -LiteralPath $manifestPath -Value '{"revision":4,"commit":"rev4","units":{"core":{"fingerprint":"approved"}}}'
        $approvedFingerprint = "a" * 64
        Set-Content -LiteralPath $approvedPath -Value (ConvertTo-Json -InputObject ([ordered]@{ fingerprint = $approvedFingerprint; created = @(); dataWrites = @(); revokedAccess = @() }) -Compress)

        Mock Test-IfsNewerManifest -ModuleName IfsRelease { return $false }
        Mock Invoke-IfsPreview -ModuleName IfsRelease {
            return [pscustomobject]@{ fingerprint = $approvedFingerprint; created = @(); dataWrites = @(); revokedAccess = @() }
        }
        Mock Open-IfsOperationJournal -ModuleName IfsRelease {
            return [pscustomobject]@{ Journal = [pscustomobject]@{ unit = "ifs-shop-core-dev"; target = "dev"; operations = @() } }
        }
        Mock Read-IfsAppOwnedState -ModuleName IfsRelease { return @() }
        Mock Get-IfsPendingOperation -ModuleName IfsRelease { return @() }
        Mock Add-IfsOperation -ModuleName IfsRelease { }
        Mock Close-IfsOperationJournal -ModuleName IfsRelease { }
        Mock Write-IfsReleaseReport -ModuleName IfsRelease { }

        {
            Invoke-IfsInfraDeploy -ReleasePath $script:coreRelease -ManifestPath $manifestPath -PreviewPath $approvedPath -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -SqlScript "" -OutputPath $reportPath -DefaultBranch "main" -ManifestPathInRepository ".ifs/manifest.json" -DeploymentIdentityObjectId "00000000-0000-0000-0000-000000000001"
        } | Should -Not -Throw
    }

    It "écrit et valide un rapport même avant la première mutation" {
        $manifestPath = Join-Path $script:tempRoot "manifest.json"
        $reportPath = Join-Path $script:tempRoot "ifs-report.json"
        Set-Content -LiteralPath $manifestPath -Value '{"revision":4,"commit":"rev4","units":{"orders":{"fingerprint":"abc"}}}'
        $approvedFingerprint = "a" * 64
        $report = Write-IfsReleaseReport -ReleaseData $script:orderData -ManifestPath $manifestPath -OutputPath $reportPath -ApprovedPreviewFingerprint $approvedFingerprint -Steps @() -Result FailedBeforeMutation -ErrorMessage "précontrôle"
        $report.result | Should -Be "FailedBeforeMutation"
        $report.schema | Should -Be "ifs-report/v2"
        $report.approvedPreviewFingerprint | Should -Be $approvedFingerprint
        $saved = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        $saved.schema | Should -Be "ifs-report/v2"
        $saved.revision | Should -Be 4
        $saved.approvedPreviewFingerprint | Should -Be $approvedFingerprint
        (Test-Json -Json (Get-Content -LiteralPath $reportPath -Raw) -SchemaFile (Join-Path $script:repoRoot "reference/pilot/bicep-azdo/.ifs/templates/schemas/ifs-report.v2.schema.json")) | Should -BeTrue
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
            param($AppOwnedState)
            [void]$global:IFS_AZ_CALLS.Add([pscustomobject]@{ kind = 'pending-app-owned-state'; state = @($AppOwnedState) })
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
        $pendingState = @($global:IFS_AZ_CALLS | Where-Object { $_.kind -eq 'pending-app-owned-state' })
        $pendingState.Count | Should -Be 1
        $pendingState[0].state[0].image | Should -Be "mcr.microsoft.com/example:1"
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        $report.schema | Should -Be "ifs-report/v2"
        $report.approvedPreviewFingerprint | Should -Be $initialPreview.fingerprint
        $module = Get-Module IfsRelease
        $expectedManifestFingerprint = & $module {
            param($path)
            $metadata = Get-IfsManifestInfo -ManifestPath $path
            Get-IfsEffectFingerprint -Effects $metadata.Manifest
        } $manifestPath
        $report.manifestFingerprint | Should -Be $expectedManifestFingerprint
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
        $approvedFingerprint = "a" * 64
        Set-Content -LiteralPath $previewPath -Value (ConvertTo-Json -InputObject ([ordered]@{ fingerprint = $approvedFingerprint }) -Compress)
        Mock Test-IfsNewerManifest -ModuleName IfsRelease { throw "Une révision plus récente (5) modifie ce composant." }
        {
            Invoke-IfsInfraDeploy -ReleasePath $script:ordersRelease -ManifestPath $manifestPath -PreviewPath $previewPath -TemplateFile $script:templatePath -ParameterFile $script:parameterPath -SqlScript (Join-Path $script:repoRoot "reference/pilot/bicep-azdo/orders/infra/scripts/data-access.sql") -OutputPath $reportPath -DefaultBranch "main" -ManifestPathInRepository ".ifs/manifest.json" -DeploymentIdentityObjectId "00000000-0000-0000-0000-000000000001" -ApplicationIdentityObjectId "00000000-0000-0000-0000-000000000002"
        } | Should -Throw "*modifie ce composant*"
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        $report.result | Should -Be "FailedBeforeMutation"
        $report.schema | Should -Be "ifs-report/v2"
        $report.approvedPreviewFingerprint | Should -Be $approvedFingerprint
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

    It "passe l'image applicative courante comme paramètre de pile" {
        $image = "crshop42.azurecr.io/orders/api@sha256:$('a' * 64)"
        $appState = @([pscustomobject]@{
            resourceId = "/subscriptions/test/resourceGroups/rg/providers/Microsoft.App/containerApps/api"
            parameter = "caApi.image"
            image = $image
            traffic = @()
            bootstrap = $false
        })
        $data = [pscustomobject]@{
            unit = "ifs-shop-orders-dev"; component = "orders"; location = "francecentral"; subscriptionId = "test"; protected = $false
        }

        Invoke-IfsStackDeployment -ReleaseData $data -TemplateFile "main.bicep" -ParameterFile "main.bicepparam" -DeploymentIdentityObjectId "identity" -ApplicationIdentityObjectId "22222222-2222-2222-2222-222222222222" -AppOwnedState $appState | Out-Null
        $call = $global:IFS_AZ_CALLS | Where-Object { ($_ -join " ") -match "stack sub create" } | Select-Object -First 1

        $parameterIndex = [Array]::IndexOf($call, "--parameters") + 1
        $call[$parameterIndex] | Should -Match "\.json$"
        $call | Should -Not -Contain "caApi.image=$image"
        $global:IFS_STACK_PARAMETER_JSON.parameters.caApi.value.image | Should -Be $image
        $global:IFS_STACK_PARAMETER_JSON.parameters.caApi.value.preserved.marker | Should -Be "from-parameter-file"
        Test-Path -LiteralPath $global:IFS_STACK_PARAMETER_PATH | Should -BeFalse
    }

    It "utilise le Client ID Microsoft Entra comme SID SQL pour les identités système et utilisateur" {
        $applicationClientId = '11111111-2222-3333-4444-555555555555'
        $principalObjectId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'
        $global:IFS_SQL_APPLICATION_CLIENT_ID = $applicationClientId
        $global:IFS_SQL_USER_IDENTITY = ConvertTo-Json -InputObject @{
            clientId = $applicationClientId
            principalId = $principalObjectId
            name = 'id-ifs-app-shop-dev'
        } -Compress
        $global:IFS_SQL_SYSTEM_IDENTITY = ConvertTo-Json -InputObject @{
            principalId = $principalObjectId
            type = 'SystemAssigned'
        } -Compress
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            $command = $Arguments -join ' '
            if ($command -match '^containerapp show') { return $global:IFS_SQL_SYSTEM_IDENTITY }
            if ($command -match '^identity show') { return $global:IFS_SQL_USER_IDENTITY }
            if ($command -match '^ad sp show') { return $global:IFS_SQL_APPLICATION_CLIENT_ID }
            return '{}'
        }

        foreach ($kind in @('systemAssigned', 'userAssigned')) {
            $global:IFS_AZ_CALLS.Clear()
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

            $principal.ClientId | Should -BeExactly $applicationClientId
            $servicePrincipalCall = @($global:IFS_AZ_CALLS | Where-Object { ($_ -join ' ') -match '^ad sp show ' })
            if ($kind -eq 'systemAssigned') {
                $servicePrincipalCall.Count | Should -Be 1
                $servicePrincipalCall[0] | Should -Contain $principalObjectId
                $servicePrincipalCall[0] | Should -Contain '--query'
                $servicePrincipalCall[0] | Should -Contain 'appId'
            }
            else {
                $servicePrincipalCall | Should -BeNullOrEmpty
            }
        }
    }

    It "retente un délai transitoire de connexion SQL avec un délai de connexion borné" {
        $global:IFS_SQL_CALLS = [System.Collections.Generic.List[object]]::new()
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            $command = $Arguments -join ' '
            if ($command -match '^sql server show') { return '{"resourceGroup":"rg-shop-orders-main-dev"}' }
            if ($command -match '^sql server firewall-rule list') { return '[]' }
            if ($command -match '^containerapp show') { return '{"principalId":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"}' }
            if ($command -match '^ad sp show') { return '11111111-2222-3333-4444-555555555555' }
            if ($command -match '^account get-access-token') { return '{"accessToken":"test-token"}' }
            return '{}'
        }
        Mock Install-Module -ModuleName IfsRelease { }
        Mock Import-Module -ModuleName IfsRelease { }
        Mock Start-Sleep -ModuleName IfsRelease { }
        function global:Invoke-Sqlcmd {
            [CmdletBinding()]
            param(
                [string] $ServerInstance,
                [string] $Database,
                [string] $AccessToken,
                [string] $InputFile,
                [string[]] $Variable,
                [int] $ConnectionTimeout
            )
            $global:IFS_SQL_CALLS.Add([pscustomobject]@{ ConnectionTimeout = $ConnectionTimeout })
            if ($global:IFS_SQL_CALLS.Count -eq 1) {
                throw [System.TimeoutException]::new('Connection Timeout Expired during the post-login phase.')
            }
        }

        try {
            Invoke-IfsDataAccess -ReleaseData $script:orderData -SqlScript (Join-Path $script:repoRoot "reference/pilot/bicep-azdo/orders/infra/scripts/data-access.sql") -RunId "test-run" | Out-Null

            $global:IFS_SQL_CALLS.Count | Should -Be 2
            @($global:IFS_SQL_CALLS | ForEach-Object ConnectionTimeout | Select-Object -Unique) | Should -Be @(120)
            Should -Invoke Start-Sleep -ModuleName IfsRelease -Times 1 -Exactly
        }
        finally {
            Remove-Item Function:global:Invoke-Sqlcmd -ErrorAction SilentlyContinue
            Remove-Variable IFS_SQL_CALLS -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It "transmet le groupe de ressources à Azure CLI pour accéder au serveur SQL" {
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            $command = $Arguments -join ' '
            if ($command -match '^sql server show') {
                if ($Arguments -notcontains '--resource-group') {
                    $global:LASTEXITCODE = 2
                    return 'ERROR: (--resource-group | --ids) are required'
                }
                return '{"resourceGroup":"rg-shop-orders-main-dev"}'
            }
            if ($command -match '^sql server firewall-rule list') { return '[]' }
            if ($command -match '^containerapp show') { return '{"principalId":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"}' }
            if ($command -match '^ad sp show') { return '11111111-2222-3333-4444-555555555555' }
            if ($command -match '^account get-access-token') { return '{"accessToken":"test-token"}' }
            return '{}'
        }
        Mock Install-Module -ModuleName IfsRelease { }
        Mock Import-Module -ModuleName IfsRelease { }
        function global:Invoke-Sqlcmd {
            [CmdletBinding()]
            param(
                [string] $ServerInstance,
                [string] $Database,
                [string] $AccessToken,
                [string] $InputFile,
                [string[]] $Variable,
                [int] $ConnectionTimeout
            )
        }

        try {
            Invoke-IfsDataAccess -ReleaseData $script:orderData -SqlScript (Join-Path $script:repoRoot "reference/pilot/bicep-azdo/orders/infra/scripts/data-access.sql") -RunId "test-run" | Out-Null

            $serverDependency = @($script:orderData.dependencies | ForEach-Object { $_.resources } | Where-Object { $_.id -match '/providers/Microsoft\.Sql/servers/[^/]+$' }) | Select-Object -First 1
            $expectedResourceGroup = [regex]::Match($serverDependency.id, '/resourceGroups/([^/]+)/').Groups[1].Value
            $expectedServerName = [string]$script:orderData.dataAccess[0].server -split '\.' | Select-Object -First 1
            $serverShowCall = $global:IFS_AZ_CALLS | Where-Object { ($_ -join ' ') -match '^sql server show ' } | Select-Object -First 1
            $resourceGroupIndex = [Array]::IndexOf($serverShowCall, '--resource-group')
            $firewallListCall = $global:IFS_AZ_CALLS | Where-Object { ($_ -join ' ') -match '^sql server firewall-rule list ' } | Select-Object -First 1
            $firewallServerIndex = [Array]::IndexOf($firewallListCall, '--server')

            $resourceGroupIndex | Should -BeGreaterThan -1
            $serverShowCall[$resourceGroupIndex + 1] | Should -BeExactly $expectedResourceGroup
            $firewallServerIndex | Should -BeGreaterThan -1
            $firewallListCall[$firewallServerIndex + 1] | Should -BeExactly $expectedServerName
        }
        finally {
            Remove-Item Function:global:Invoke-Sqlcmd -ErrorAction SilentlyContinue
        }
    }

    It "utilise les options Azure CLI du serveur et de la règle pour le pare-feu SQL temporaire" {
        $restrictedData = ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $script:orderData -Depth 100) -Depth 100
        $restrictedData.dataAccess[0].exposure = 'Restricted'
        $global:IFS_AZ_HANDLER = {
            param($Arguments)
            $global:LASTEXITCODE = 0
            $command = $Arguments -join ' '
            if ($command -match '^sql server show') { return '{"resourceGroup":"rg-shop-orders-main-dev"}' }
            if ($command -match '^sql server firewall-rule list') { return '[]' }
            if ($command -match '^containerapp show') { return '{"principalId":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"}' }
            if ($command -match '^ad sp show') { return '11111111-2222-3333-4444-555555555555' }
            if ($command -match '^account get-access-token') { return '{"accessToken":"test-token"}' }
            return '{}'
        }
        Mock Install-Module -ModuleName IfsRelease { }
        Mock Import-Module -ModuleName IfsRelease { }
        Mock Invoke-RestMethod { return '198.51.100.17' }
        function global:Invoke-Sqlcmd {
            [CmdletBinding()]
            param(
                [string] $ServerInstance,
                [string] $Database,
                [string] $AccessToken,
                [string] $InputFile,
                [string[]] $Variable,
                [int] $ConnectionTimeout
            )
        }

        try {
            Invoke-IfsDataAccess -ReleaseData $restrictedData -SqlScript (Join-Path $script:repoRoot "reference/pilot/bicep-azdo/orders/infra/scripts/data-access.sql") -RunId "test-run" | Out-Null

            $expectedServerName = [string]$restrictedData.dataAccess[0].server -split '\.' | Select-Object -First 1
            $createCall = $global:IFS_AZ_CALLS | Where-Object { ($_ -join ' ') -match '^sql server firewall-rule create ' } | Select-Object -First 1
            $createServerIndex = [Array]::IndexOf($createCall, '--server')
            $createNameIndex = [Array]::IndexOf($createCall, '--name')
            $deleteCall = $global:IFS_AZ_CALLS | Where-Object { ($_ -join ' ') -match '^sql server firewall-rule delete ' } | Select-Object -Last 1
            $deleteServerIndex = [Array]::IndexOf($deleteCall, '--server')
            $deleteNameIndex = [Array]::IndexOf($deleteCall, '--name')

            $createServerIndex | Should -BeGreaterThan -1
            $createCall[$createServerIndex + 1] | Should -BeExactly $expectedServerName
            $createNameIndex | Should -BeGreaterThan -1
            $createCall[$createNameIndex + 1] | Should -Match '^ifs-temp-'
            $deleteServerIndex | Should -BeGreaterThan -1
            $deleteCall[$deleteServerIndex + 1] | Should -BeExactly $expectedServerName
            $deleteNameIndex | Should -BeGreaterThan -1
            $deleteCall[$deleteNameIndex + 1] | Should -Match '^ifs-temp-'
        }
        finally {
            Remove-Item Function:global:Invoke-Sqlcmd -ErrorAction SilentlyContinue
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
            -Image $image -ImageTag "1234-a1b2c3d4e5f6" -HealthUrl ([uri]"https://orders-api.example/health") `
            -OutputPath $outputPath -HealthTimeoutSeconds 1

        $report.result | Should -Be "Succeeded"
        $report.healthStatus | Should -Be 204
        $savedReport = Get-Content -LiteralPath $outputPath -Raw
        (Test-Json -Json $savedReport -SchemaFile (Join-Path $script:repoRoot "reference/release-module/schemas/ifs-app-report.schema.json")) | Should -BeTrue
        $updateCall = $global:IFS_AZ_CALLS | Where-Object { ($_ -join " ") -match "containerapp update" } | Select-Object -First 1
        $updateCall | Should -Contain "--image"
        $updateCall | Should -Contain $image
        $updateCall | Should -Contain "--set-env-vars"
        $updateCall | Should -Contain "IMAGE_TAG=1234-a1b2c3d4e5f6"
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
