// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
using 'main.bicep'

param target = {
  code: 'prd'
  subscriptionId: '<B>'
  location: 'francecentral'
}

param resourceGroups = {
  main: {
    name: 'rg-shop-data-main-prd'
    location: 'francecentral'
    tags: {
      costCenter: 'ecommerce'
      'ifs-project': 'shop'
      'ifs-component': 'data'
      'ifs-environment': 'prd'
      'managed-by': 'infraflowsculptor'
    }
  }
}

param coreWorkspace = {
  subscriptionId: '<B>'
  resourceGroupName: 'rg-shop-core-main-prd'
  name: 'log-shop-main-prd'
}

param sqlOrders = {
  deploy: true
  name: 'sql-shop-orders-prd'
  location: 'francecentral'
  administratorGroupName: 'sg-shop-sql-admins'
  administratorGroupObjectId: '<SQL_ADMIN_GROUP_OBJECT_ID_PRD>'
  publicNetworkAccess: 'Enabled'
  minimalTlsVersion: '1.2'
  databaseName: 'sqldb-shop-orders-prd'
  databaseSkuName: 'GP_Gen5_2'
  databaseAvailabilityZone: -1
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'data'
    'ifs-environment': 'prd'
    'managed-by': 'infraflowsculptor'
  }
  diagnosticSettings: [
    {
      name: 'diag-sql-shop-orders-prd'
      logCategoryGroup: 'allLogs'
      metricCategory: 'AllMetrics'
      logAnalyticsDestinationType: 'Dedicated'
    }
  ]
  databaseDiagnosticSettings: [
    {
      name: 'diag-sqldb-shop-orders-prd'
      logCategoryGroup: 'allLogs'
      metricCategory: 'AllMetrics'
      logAnalyticsDestinationType: 'Dedicated'
    }
  ]
}
