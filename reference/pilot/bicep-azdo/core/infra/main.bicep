// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.

import {
  appiMainConfig
  kvMainConfig
  logMainConfig
  resourceGroupsConfig
  targetConfig
} from './types.bicep'

targetScope = 'subscription'

param target targetConfig
param resourceGroups resourceGroupsConfig
param logMain logMainConfig
param appiMain appiMainConfig
param kvMain kvMainConfig

resource rg_main 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroups.main.name
  location: resourceGroups.main.location
  tags: resourceGroups.main.tags
}

// Groupe de ressources principal : journalisation.
module logMainModule 'br/public:avm/res/operational-insights/workspace:0.16.0' = if (logMain.deploy) {
  name: 'logMain-${uniqueString(logMain.name)}'
  scope: rg_main
  params: {
    name: logMain.name
    location: logMain.location
    skuName: logMain.skuName
    dataRetention: logMain.dataRetentionDays
    dailyQuotaGb: logMain.dailyQuotaGb
    publicNetworkAccessForIngestion: logMain.publicNetworkAccessForIngestion
    publicNetworkAccessForQuery: logMain.publicNetworkAccessForQuery
    tags: logMain.tags
    diagnosticSettings: []
    enableTelemetry: false
  }
}

// Groupe de ressources principal : télémétrie applicative.
module appiMainModule 'br/public:avm/res/insights/component:0.8.0' = if (appiMain.deploy && logMain.deploy) {
  name: 'appiMain-${uniqueString(appiMain.name)}'
  scope: rg_main
  params: {
    name: appiMain.name
    location: appiMain.location
    applicationType: appiMain.applicationType
    workspaceResourceId: resourceId(
      target.subscriptionId,
      resourceGroups.main.name,
      'Microsoft.OperationalInsights/workspaces',
      logMain.name
    )
    samplingPercentage: appiMain.samplingPercentage
    retentionInDays: appiMain.retentionInDays
    disableIpMasking: appiMain.disableIpMasking
    disableLocalAuth: appiMain.disableLocalAuth
    publicNetworkAccessForIngestion: appiMain.publicNetworkAccessForIngestion
    publicNetworkAccessForQuery: appiMain.publicNetworkAccessForQuery
    tags: appiMain.tags
    diagnosticSettings: [
      for setting in appiMain.diagnosticSettings: {
        name: setting.name
        workspaceResourceId: resourceId(
          target.subscriptionId,
          resourceGroups.main.name,
          'Microsoft.OperationalInsights/workspaces',
          logMain.name
        )
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
    logMainModule
  ]
}

// Groupe de ressources principal : secrets applicatifs.
module kvMainModule 'br/public:avm/res/key-vault/vault:0.14.0' = if (kvMain.deploy && logMain.deploy) {
  name: 'kvMain-${uniqueString(kvMain.name)}'
  scope: rg_main
  params: {
    name: kvMain.name
    location: kvMain.location
    enablePurgeProtection: kvMain.enablePurgeProtection
    enableRbacAuthorization: kvMain.enableRbacAuthorization
    enableSoftDelete: kvMain.enableSoftDelete
    softDeleteRetentionInDays: kvMain.softDeleteRetentionInDays
    sku: kvMain.sku
    publicNetworkAccess: kvMain.publicNetworkAccess
    accessPolicies: []
    secrets: []
    keys: []
    tags: kvMain.tags
    diagnosticSettings: [
      for setting in kvMain.diagnosticSettings: {
        name: setting.name
        workspaceResourceId: resourceId(
          target.subscriptionId,
          resourceGroups.main.name,
          'Microsoft.OperationalInsights/workspaces',
          logMain.name
        )
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
    logMainModule
  ]
}
