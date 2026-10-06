// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.

import {
  resourceGroupsConfig
  resourceReferenceConfig
  sqlOrdersConfig
  targetConfig
} from './types.bicep'

targetScope = 'subscription'

param target targetConfig
param resourceGroups resourceGroupsConfig
param coreWorkspace resourceReferenceConfig
param sqlOrders sqlOrdersConfig

resource rg_main 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroups.main.name
  location: resourceGroups.main.location
  tags: resourceGroups.main.tags
}

resource coreLog 'Microsoft.OperationalInsights/workspaces@2025-07-01' existing = {
  scope: resourceGroup(coreWorkspace.subscriptionId, coreWorkspace.resourceGroupName)
  name: coreWorkspace.name
}

var sqlDatabaseDiagnosticSettings = [
  for setting in sqlOrders.databaseDiagnosticSettings: {
    name: setting.name
    workspaceResourceId: coreLog.id
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

// Groupe de ressources principal : serveur SQL et base orders.
module sqlOrdersModule 'br/public:avm/res/sql/server:0.22.0' = if (sqlOrders.deploy) {
  name: 'sqlOrders-${target.code}-${uniqueString(sqlOrders.name)}'
  scope: rg_main
  params: {
    name: sqlOrders.name
    location: sqlOrders.location
    administratorLogin: null
    administratorLoginPassword: null
    administrators: {
      login: sqlOrders.administratorGroupName
      sid: sqlOrders.administratorGroupObjectId
      principalType: 'Group'
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: sqlOrders.minimalTlsVersion
    publicNetworkAccess: sqlOrders.publicNetworkAccess
    restrictOutboundNetworkAccess: 'Disabled'
    tags: sqlOrders.tags
    databases: [
      {
        name: sqlOrders.databaseName
        sku: {
          name: sqlOrders.databaseSkuName
        }
        availabilityZone: sqlOrders.databaseAvailabilityZone
        tags: sqlOrders.tags
        diagnosticSettings: sqlDatabaseDiagnosticSettings
        enableTelemetry: false
      }
    ]
    enableTelemetry: false
  }
}

module sqlOrdersDiagnosticsModule './server-diagnostics.bicep' = if (sqlOrders.deploy && !empty(sqlOrders.diagnosticSettings)) {
  name: 'sqlOrdersDiagnostics-${target.code}-${uniqueString(sqlOrders.name)}'
  scope: rg_main
  params: {
    sqlServerName: sqlOrders.name
    workspaceResourceId: coreLog.id
    diagnosticSettings: sqlOrders.diagnosticSettings
  }
  dependsOn: [
    sqlOrdersModule
  ]
}
