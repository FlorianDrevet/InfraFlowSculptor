# Généré par InfraFlowSculptor — tests du kit d'installation.
BeforeAll {
    $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../../../..")).Path
    $script:modulePath = Join-Path $script:repoRoot "reference/pilot/bicep-azdo/.ifs/install/scripts/IfsInstall.psm1"
    Import-Module $script:modulePath -Force
}

Describe "Portées RBAC du kit d'installation" {
    It "autorise Log Analytics Reader et Data Reader sur la dépendance workspace d'orders" {
        $subscriptionId = "11111111-1111-1111-1111-111111111111"
        $workspaceResourceId = "/subscriptions/$subscriptionId/resourceGroups/rg-shop42-core-dev/providers/Microsoft.OperationalInsights/workspaces/log-shop42-main-dev"
        $target = [pscustomobject]@{
            Releases = @(
                [pscustomobject]@{
                    Component = "orders"
                    SubscriptionId = $subscriptionId
                    ResourceGroupName = "rg-shop42-orders-dev"
                    Release = [pscustomobject]@{
                        dependencies = @(
                            [pscustomobject]@{
                                resources = @([pscustomobject]@{ id = $workspaceResourceId })
                            }
                        )
                    }
                }
            )
        }
        $roleIds = @{
            KeyVaultSecretsOfficer = "b86a8fe4-44ce-4948-aee5-eccb2c155cd7"
            AcrPush = "8311e382-0749-4cb8-b61a-304f252e45ec"
            ContainerAppsContributor = "358470bc-b998-42bd-ab17-a7e34c199c0f"
            KeyVaultSecretsUser = "4633458b-17de-408a-b874-0445c86b69e6"
            LogAnalyticsReader = "73c42c96-874c-492b-b04d-ab87d138a893"
            LogAnalyticsDataReader = "3b03c2da-16b3-4a49-8834-0f8130efdd3b"
            AcrPull = "7f951dda-4ed3-4680-a7ca-43fe172d538d"
        }

        $scopePlan = @(Get-IfsRbacScopePlan -Target $target -RoleIds $roleIds) |
            Where-Object Scope -eq "/subscriptions/$subscriptionId/resourceGroups/rg-shop42-core-dev"

        $scopePlan.RoleDefinitionIds | Should -Contain "73c42c96-874c-492b-b04d-ab87d138a893"
        $scopePlan.RoleDefinitionIds | Should -Contain "3b03c2da-16b3-4a49-8834-0f8130efdd3b"
        $scopePlan.RoleDefinitionIds | Should -HaveCount 2
        $condition = Get-IfsRbacCondition -RoleDefinitionIds $scopePlan.RoleDefinitionIds
        $condition | Should -Match "3b03c2da-16b3-4a49-8834-0f8130efdd3b"
    }

    It "résout l'identifiant Azure de Log Analytics Data Reader" {
        $module = Get-Module IfsInstall
        $roleId = & $module { Get-IfsRoleDefinitionId -RoleName "Log Analytics Data Reader" }

        $roleId | Should -Be "3b03c2da-16b3-4a49-8834-0f8130efdd3b"
    }
}
