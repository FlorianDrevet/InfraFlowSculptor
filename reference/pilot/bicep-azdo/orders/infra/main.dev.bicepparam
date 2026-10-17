// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
using 'main.bicep'

param target = {
  code: 'dev'
  subscriptionId: '<A>'
  location: 'francecentral'
}

param resourceGroups = {
  main: {
    name: 'rg-shop-orders-main-dev'
    location: 'francecentral'
    tags: {
      costCenter: 'ecommerce'
      'ifs-project': 'shop'
      'ifs-component': 'orders'
      'ifs-environment': 'dev'
      'managed-by': 'infraflowsculptor'
    }
  }
}

param externalResources = {
  coreWorkspace: {
    subscriptionId: '<A>'
    resourceGroupName: 'rg-shop-core-main-dev'
    name: 'log-shop-main-dev'
  }
  coreAppInsights: {
    subscriptionId: '<A>'
    resourceGroupName: 'rg-shop-core-main-dev'
    name: 'appi-shop-main-dev'
  }
  coreKeyVault: {
    subscriptionId: '<A>'
    resourceGroupName: 'rg-shop-core-main-dev'
    name: 'kv-shop-main-dev'
  }
  dataSqlDatabase: {
    server: {
      subscriptionId: '<A>'
      resourceGroupName: 'rg-shop-data-main-dev'
      name: 'sql-shop-orders-dev'
    }
    name: 'sqldb-shop-orders-dev'
  }
  platformRegistry: {
    subscriptionId: '<C>'
    resourceGroupName: 'rg-shop-platform-main-shared'
    name: 'crshopmainshared'
  }
}

param apiIdentity = {
  deploy: true
  name: 'id-shop-api-dev'
  location: 'francecentral'
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'orders'
    'ifs-environment': 'dev'
    'managed-by': 'infraflowsculptor'
  }
}

param caeMain = {
  deploy: true
  name: 'cae-shop-main-dev'
  location: 'francecentral'
  internal: false
  zoneRedundant: true
  publicNetworkAccess: 'Enabled'
  workloadProfiles: []
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'orders'
    'ifs-environment': 'dev'
    'managed-by': 'infraflowsculptor'
  }
  diagnosticSettings: [
    {
      name: 'diag-cae-shop-main-dev'
      logCategoryGroup: 'allLogs'
      metricCategory: 'AllMetrics'
      logAnalyticsDestinationType: 'Dedicated'
    }
  ]
}

param caApi = {
  deploy: true
  name: 'ca-shop-api-dev'
  location: 'francecentral'
  image: ''
  cpu: '0.5'
  memory: '1Gi'
  minReplicas: 0
  maxReplicas: 2
  ingressExternal: true
  ingressTargetPort: 8080
  ingressAllowInsecure: false
  paymentsSecretName: 'payments-api-key'
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'orders'
    'ifs-environment': 'dev'
    'managed-by': 'infraflowsculptor'
  }
  diagnosticSettings: [
    {
      name: 'diag-ca-shop-api-dev'
      metricCategory: 'AllMetrics'
      logAnalyticsDestinationType: 'Dedicated'
    }
  ]
}
