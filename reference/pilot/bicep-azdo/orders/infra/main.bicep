// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.

import {
  apiIdentityConfig
  caeMainConfig
  caApiConfig
  externalResourcesConfig
  resourceGroupsConfig
  targetConfig
} from './types.bicep'

targetScope = 'subscription'

param target targetConfig
param resourceGroups resourceGroupsConfig
param externalResources externalResourcesConfig
param apiIdentity apiIdentityConfig
param caeMain caeMainConfig
param caApi caApiConfig

resource rg_main 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroups.main.name
  location: resourceGroups.main.location
  tags: resourceGroups.main.tags
}

resource coreWorkspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' existing = {
  scope: resourceGroup(
    externalResources.coreWorkspace.subscriptionId,
    externalResources.coreWorkspace.resourceGroupName
  )
  name: externalResources.coreWorkspace.name
}

resource coreAppInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  scope: resourceGroup(
    externalResources.coreAppInsights.subscriptionId,
    externalResources.coreAppInsights.resourceGroupName
  )
  name: externalResources.coreAppInsights.name
}

resource coreKeyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  scope: resourceGroup(externalResources.coreKeyVault.subscriptionId, externalResources.coreKeyVault.resourceGroupName)
  name: externalResources.coreKeyVault.name
}

resource dataSqlServer 'Microsoft.Sql/servers@2025-01-01' existing = {
  scope: resourceGroup(
    externalResources.dataSqlDatabase.server.subscriptionId,
    externalResources.dataSqlDatabase.server.resourceGroupName
  )
  name: externalResources.dataSqlDatabase.server.name
}

resource dataSqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01' existing = {
  parent: dataSqlServer
  name: externalResources.dataSqlDatabase.name
}

resource platformRegistry 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  scope: resourceGroup(
    externalResources.platformRegistry.subscriptionId,
    externalResources.platformRegistry.resourceGroupName
  )
  name: externalResources.platformRegistry.name
}

var apiIdentityResourceId = resourceId(
  target.subscriptionId,
  resourceGroups.main.name,
  'Microsoft.ManagedIdentity/userAssignedIdentities',
  apiIdentity.name
)
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var logAnalyticsReaderRoleId = '73c42c96-874c-492b-b04d-ab87d138a893'
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

// Groupe de ressources principal : identité et environnement Container Apps.
module apiIdentityModule 'br/public:avm/res/managed-identity/user-assigned-identity:0.6.0' = if (apiIdentity.deploy) {
  name: 'apiIdentity-${target.code}-${uniqueString(apiIdentity.name)}'
  scope: resourceGroup(target.subscriptionId, resourceGroups.main.name)
  params: {
    name: apiIdentity.name
    location: apiIdentity.location
    tags: apiIdentity.tags
    enableTelemetry: false
  }
  dependsOn: [
    rg_main
  ]
}

module caeMainModule 'br/public:avm/res/app/managed-environment:0.16.0' = if (caeMain.deploy) {
  name: 'caeMain-${target.code}-${uniqueString(caeMain.name)}'
  scope: resourceGroup(target.subscriptionId, resourceGroups.main.name)
  params: {
    name: caeMain.name
    location: caeMain.location
    internal: caeMain.internal
    zoneRedundant: caeMain.zoneRedundant
    publicNetworkAccess: caeMain.publicNetworkAccess
    workloadProfiles: caeMain.workloadProfiles
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsWorkspaceResourceId: coreWorkspace.id
    }
    tags: caeMain.tags
    diagnosticSettings: [
      for setting in caeMain.diagnosticSettings: {
        name: setting.name
        workspaceResourceId: coreWorkspace.id
        logAnalyticsDestinationType: setting.logAnalyticsDestinationType
        logCategoriesAndGroups: [
          {
            categoryGroup: setting.logCategoryGroup
            enabled: true
          }
        ]
        metricCategories: [
          {
            category: setting.metricCategory
            enabled: true
          }
        ]
      }
    ]
    enableTelemetry: false
  }
  dependsOn: [
    rg_main
  ]
}

// Groupe de ressources principal : droits nécessaires aux dépendances externes.
module keyVaultSecretsUser './key-vault-role-assignment.bicep' = if (caApi.deploy && apiIdentity.deploy) {
  name: 'keyVaultSecretsUser-${target.code}-${uniqueString(externalResources.coreKeyVault.name)}'
  scope: resourceGroup(externalResources.coreKeyVault.subscriptionId, externalResources.coreKeyVault.resourceGroupName)
  params: {
    keyVaultName: externalResources.coreKeyVault.name
    assignmentName: guid(coreKeyVault.id, apiIdentityModule!.outputs.principalId, keyVaultSecretsUserRoleId)
    principalId: apiIdentityModule!.outputs.principalId
    roleDefinitionId: subscriptionResourceId(
      externalResources.coreKeyVault.subscriptionId,
      'Microsoft.Authorization/roleDefinitions',
      keyVaultSecretsUserRoleId
    )
  }
}

module logAnalyticsReader './workspace-role-assignment.bicep' = if (caApi.deploy && apiIdentity.deploy) {
  name: 'logAnalyticsReader-${target.code}-${uniqueString(externalResources.coreWorkspace.name)}'
  scope: resourceGroup(
    externalResources.coreWorkspace.subscriptionId,
    externalResources.coreWorkspace.resourceGroupName
  )
  params: {
    workspaceName: externalResources.coreWorkspace.name
    assignmentName: guid(coreWorkspace.id, apiIdentityModule!.outputs.principalId, logAnalyticsReaderRoleId)
    principalId: apiIdentityModule!.outputs.principalId
    roleDefinitionId: subscriptionResourceId(
      externalResources.coreWorkspace.subscriptionId,
      'Microsoft.Authorization/roleDefinitions',
      logAnalyticsReaderRoleId
    )
  }
}

module acrPull './registry-role-assignment.bicep' = if (caApi.deploy && apiIdentity.deploy) {
  name: 'acrPull-${target.code}-${uniqueString(externalResources.platformRegistry.name)}'
  scope: resourceGroup(
    externalResources.platformRegistry.subscriptionId,
    externalResources.platformRegistry.resourceGroupName
  )
  params: {
    registryName: externalResources.platformRegistry.name
    assignmentName: guid(platformRegistry.id, apiIdentityModule!.outputs.principalId, acrPullRoleId)
    principalId: apiIdentityModule!.outputs.principalId
    roleDefinitionId: subscriptionResourceId(
      externalResources.platformRegistry.subscriptionId,
      'Microsoft.Authorization/roleDefinitions',
      acrPullRoleId
    )
  }
}

// Groupe de ressources principal : application API et ses paramètres applicatifs.
module caApiModule 'br/public:avm/res/app/container-app:0.23.0' = if (caApi.deploy && apiIdentity.deploy && caeMain.deploy) {
  name: 'caApi-${target.code}-${uniqueString(caApi.name)}'
  scope: resourceGroup(target.subscriptionId, resourceGroups.main.name)
  params: {
    name: caApi.name
    location: caApi.location
    environmentResourceId: resourceId(
      target.subscriptionId,
      resourceGroups.main.name,
      'Microsoft.App/managedEnvironments',
      caeMain.name
    )
    containers: [
      {
        name: 'api'
        image: empty(caApi.image)
          ? 'mcr.microsoft.com/k8se/quickstart@sha256:3a4d93c34c6753f24765ab17a36f1754aee9b02082b7d9553d17697b9e8252c4'
          : caApi.image
        resources: {
          cpu: json(caApi.cpu)
          memory: caApi.memory
        }
        env: [
          {
            name: 'Sql__Server'
            value: '${externalResources.dataSqlDatabase.server.name}.${environment().suffixes.sqlServerHostname}'
          }
          {
            name: 'Sql__Database'
            value: dataSqlDatabase.name
          }
          {
            name: 'LogAnalytics__WorkspaceId'
            value: coreWorkspace.properties.customerId
          }
          {
            name: 'Payments__ApiKey'
            secretRef: caApi.paymentsSecretName
          }
          {
            name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
            value: coreAppInsights.properties.ConnectionString
          }
          {
            name: 'AZURE_CLIENT_ID'
            value: apiIdentityModule!.outputs.clientId
          }
        ]
      }
    ]
    secrets: [
      {
        name: caApi.paymentsSecretName
        keyVaultUrl: '${coreKeyVault.properties.vaultUri}secrets/${caApi.paymentsSecretName}'
        identity: apiIdentityResourceId
        value: null
      }
    ]
    registries: [
      {
        server: platformRegistry.properties.loginServer
        identity: apiIdentityResourceId
      }
    ]
    managedIdentities: {
      systemAssigned: true
      userAssignedResourceIds: [
        apiIdentityResourceId
      ]
    }
    ingressExternal: caApi.ingressExternal
    ingressTargetPort: caApi.ingressTargetPort
    ingressAllowInsecure: caApi.ingressAllowInsecure
    ingressTransport: 'auto'
    activeRevisionsMode: 'Single'
    scaleSettings: {
      minReplicas: caApi.minReplicas
      maxReplicas: caApi.maxReplicas
    }
    tags: caApi.tags
    diagnosticSettings: [
      for setting in caApi.diagnosticSettings: {
        name: setting.name
        workspaceResourceId: coreWorkspace.id
        logAnalyticsDestinationType: setting.logAnalyticsDestinationType
        metricCategories: [
          {
            category: setting.metricCategory
            enabled: true
          }
        ]
      }
    ]
    enableTelemetry: false
  }
  dependsOn: [
    caeMainModule
    keyVaultSecretsUser
    logAnalyticsReader
    acrPull
  ]
}
