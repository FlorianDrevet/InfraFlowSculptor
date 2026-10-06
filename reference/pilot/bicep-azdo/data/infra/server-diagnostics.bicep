// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.

import { diagnosticSettingConfig } from './types.bicep'

targetScope = 'resourceGroup'

@description('Nom du serveur SQL dont les diagnostics sont configurés.')
param sqlServerName string

@description('Identifiant de la destination Log Analytics existante.')
param workspaceResourceId string

@description('Paramètres de diagnostic à appliquer au serveur SQL.')
param diagnosticSettings diagnosticSettingConfig[]

resource sqlOrdersServer 'Microsoft.Sql/servers@2025-01-01' existing = {
  name: sqlServerName
}

resource sqlOrdersDiagnosticSetting 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = [
  for setting in diagnosticSettings: {
    name: setting.name
    scope: sqlOrdersServer
    properties: {
      workspaceId: workspaceResourceId
      logAnalyticsDestinationType: setting.logAnalyticsDestinationType
      logs: [
        {
          categoryGroup: setting.logCategoryGroup
          enabled: true
        }
      ]
      metrics: [
        {
          category: setting.metricCategory
          enabled: true
        }
      ]
    }
  }
]
