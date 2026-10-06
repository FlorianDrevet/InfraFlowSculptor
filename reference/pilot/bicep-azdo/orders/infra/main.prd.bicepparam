// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
using 'main.bicep'

param target = {
  code: 'prd'
  subscriptionId: '<B>'
  location: 'francecentral'
}

param resourceGroups = {
  main: {
    name: 'rg-shop-orders-main-prd'
    location: 'francecentral'
    tags: {
      costCenter: 'ecommerce'
      'ifs-project': 'shop'
      'ifs-component': 'orders'
      'ifs-environment': 'prd'
      'managed-by': 'infraflowsculptor'
    }
  }
}

param externalResources = {
  coreWorkspace: {
    subscriptionId: '<B>'
    resourceGroupName: 'rg-shop-core-main-prd'
    name: 'log-shop-main-prd'
  }
  coreAppInsights: {
    subscriptionId: '<B>'
    resourceGroupName: 'rg-shop-core-main-prd'
    name: 'appi-shop-main-prd'
  }
  coreKeyVault: {
    subscriptionId: '<B>'
    resourceGroupName: 'rg-shop-core-main-prd'
    name: 'kv-shop-main-prd'
  }
  dataSqlDatabase: {
    server: {
      subscriptionId: '<B>'
      resourceGroupName: 'rg-shop-data-main-prd'
      name: 'sql-shop-orders-prd'
    }
    name: 'sqldb-shop-orders-prd'
  }
  platformRegistry: {
    subscriptionId: '<C>'
    resourceGroupName: 'rg-shop-platform-main-shared'
    name: 'crshopmainshared'
  }
}

param apiIdentity = {
  deploy: true
  name: 'id-shop-api-prd'
  location: 'francecentral'
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'orders'
    'ifs-environment': 'prd'
    'managed-by': 'infraflowsculptor'
  }
}

param caeMain = {
  deploy: true
  name: 'cae-shop-main-prd'
  location: 'francecentral'
  internal: false
  zoneRedundant: true
  publicNetworkAccess: 'Enabled'
  workloadProfiles: []
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'orders'
    'ifs-environment': 'prd'
    'managed-by': 'infraflowsculptor'
  }
  diagnosticSettings: [
    {
      name: 'diag-cae-shop-main-prd'
      logCategoryGroup: 'allLogs'
      metricCategory: 'AllMetrics'
      logAnalyticsDestinationType: 'Dedicated'
    }
  ]
}

param caApi = {
  deploy: true
  name: 'ca-shop-api-prd'
  location: 'francecentral'
  image: ''
  cpu: '1'
  memory: '2Gi'
  minReplicas: 1
  maxReplicas: 3
  ingressExternal: true
  ingressTargetPort: 8080
  ingressAllowInsecure: false
  paymentsSecretName: 'payments-api-key'
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'orders'
    'ifs-environment': 'prd'
    'managed-by': 'infraflowsculptor'
  }
  diagnosticSettings: [
    {
      name: 'diag-ca-shop-api-prd'
      metricCategory: 'AllMetrics'
      logAnalyticsDestinationType: 'Dedicated'
    }
  ]
}
