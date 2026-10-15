// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
using 'main.bicep'

param target = {
  code: 'dev'
  subscriptionId: '<A>'
  location: 'francecentral'
}

param resourceGroups = {
  main: {
    name: 'rg-shop-data-main-dev'
    location: 'francecentral'
    tags: {
      costCenter: 'ecommerce'
      'ifs-project': 'shop'
      'ifs-component': 'data'
      'ifs-environment': 'dev'
      'managed-by': 'infraflowsculptor'
    }
  }
}

param coreWorkspace = {
  subscriptionId: '<A>'
  resourceGroupName: 'rg-shop-core-main-dev'
  name: 'log-shop-main-dev'
}

param sqlOrders = {
  deploy: true
  name: 'sql-shop-orders-dev'
  location: 'francecentral'
  administratorGroupName: 'sg-shop-sql-admins'
  administratorGroupObjectId: '<SQL_ADMIN_GROUP_OBJECT_ID_DEV>'
  publicNetworkAccess: 'Enabled'
  minimalTlsVersion: '1.2'
  databaseName: 'sqldb-shop-orders-dev'
  databaseSkuName: 'GP_S_Gen5_1'
  databaseAvailabilityZone: -1
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'data'
    'ifs-environment': 'dev'
    'managed-by': 'infraflowsculptor'
  }
  diagnosticSettings: [
    {
      name: 'diag-sql-shop-orders-dev'
      logCategoryGroup: 'allLogs'
      metricCategory: 'AllMetrics'
      logAnalyticsDestinationType: 'Dedicated'
    }
  ]
  databaseDiagnosticSettings: [
    {
      name: 'diag-sqldb-shop-orders-dev'
      logCategoryGroup: 'allLogs'
      metricCategory: 'AllMetrics'
      logAnalyticsDestinationType: 'Dedicated'
    }
  ]
}
