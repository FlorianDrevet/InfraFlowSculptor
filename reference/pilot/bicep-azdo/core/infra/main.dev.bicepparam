// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
using 'main.bicep'

param target = {
  code: 'dev'
  subscriptionId: '<A>'
  location: 'northeurope'
}

param resourceGroups = {
  main: {
    name: 'rg-shop-core-main-dev'
    location: 'northeurope'
    tags: {
      costCenter: 'ecommerce'
      'ifs-project': 'shop'
      'ifs-component': 'core'
      'ifs-environment': 'dev'
      'managed-by': 'infraflowsculptor'
    }
  }
}

param logMain = {
  deploy: true
  name: 'log-shop-main-dev'
  location: 'northeurope'
  dataRetentionDays: 30
  dailyQuotaGb: '-1'
  skuName: 'PerGB2018'
  publicNetworkAccessForIngestion: 'Enabled'
  publicNetworkAccessForQuery: 'Enabled'
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'core'
    'ifs-environment': 'dev'
    'managed-by': 'infraflowsculptor'
  }
  diagnosticSettings: []
}

param appiMain = {
  deploy: true
  name: 'appi-shop-main-dev'
  location: 'northeurope'
  applicationType: 'web'
  samplingPercentage: 100
  retentionInDays: 365
  disableIpMasking: true
  disableLocalAuth: false
  publicNetworkAccessForIngestion: 'Enabled'
  publicNetworkAccessForQuery: 'Enabled'
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'core'
    'ifs-environment': 'dev'
    'managed-by': 'infraflowsculptor'
  }
  diagnosticSettings: [
    {
      name: 'diag-appi-shop-main-dev'
      logCategoryGroup: 'allLogs'
      metricCategory: 'AllMetrics'
      logAnalyticsDestinationType: 'Dedicated'
    }
  ]
}

param kvMain = {
  deploy: true
  name: 'kv-shop-main-dev'
  location: 'northeurope'
  enablePurgeProtection: false
  enableRbacAuthorization: true
  enableSoftDelete: true
  softDeleteRetentionInDays: 90
  sku: 'standard'
  publicNetworkAccess: 'Enabled'
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'core'
    'ifs-environment': 'dev'
    'managed-by': 'infraflowsculptor'
  }
  diagnosticSettings: [
    {
      name: 'diag-kv-shop-main-dev'
      logCategoryGroup: 'allLogs'
      metricCategory: 'AllMetrics'
      logAnalyticsDestinationType: 'Dedicated'
    }
  ]
}
