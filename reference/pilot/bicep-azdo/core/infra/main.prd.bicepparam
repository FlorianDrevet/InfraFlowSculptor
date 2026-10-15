// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
using 'main.bicep'

param target = {
  code: 'prd'
  subscriptionId: '<B>'
  location: 'francecentral'
}

param resourceGroups = {
  main: {
    name: 'rg-shop-core-main-prd'
    location: 'francecentral'
    tags: {
      costCenter: 'ecommerce'
      'ifs-project': 'shop'
      'ifs-component': 'core'
      'ifs-environment': 'prd'
      'managed-by': 'infraflowsculptor'
    }
  }
}

param logMain = {
  deploy: true
  name: 'log-shop-main-prd'
  location: 'francecentral'
  dataRetentionDays: 90
  dailyQuotaGb: '-1'
  skuName: 'PerGB2018'
  publicNetworkAccessForIngestion: 'Enabled'
  publicNetworkAccessForQuery: 'Enabled'
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'core'
    'ifs-environment': 'prd'
    'managed-by': 'infraflowsculptor'
  }
  diagnosticSettings: []
}

param appiMain = {
  deploy: true
  name: 'appi-shop-main-prd'
  location: 'francecentral'
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
    'ifs-environment': 'prd'
    'managed-by': 'infraflowsculptor'
  }
  diagnosticSettings: [
    {
      name: 'diag-appi-shop-main-prd'
      logCategoryGroup: 'allLogs'
      metricCategory: 'AllMetrics'
      logAnalyticsDestinationType: 'Dedicated'
    }
  ]
}

param kvMain = {
  deploy: true
  name: 'kv-shop-main-prd'
  location: 'francecentral'
  enablePurgeProtection: true
  enableRbacAuthorization: true
  enableSoftDelete: true
  softDeleteRetentionInDays: 90
  sku: 'standard'
  publicNetworkAccess: 'Enabled'
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'core'
    'ifs-environment': 'prd'
    'managed-by': 'infraflowsculptor'
  }
  diagnosticSettings: [
    {
      name: 'diag-kv-shop-main-prd'
      logCategoryGroup: 'allLogs'
      metricCategory: 'AllMetrics'
      logAnalyticsDestinationType: 'Dedicated'
    }
  ]
}
