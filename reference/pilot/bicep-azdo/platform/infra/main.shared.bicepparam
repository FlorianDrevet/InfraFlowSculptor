// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.
using 'main.bicep'

param target = {
  code: 'shared'
  subscriptionId: '<C>'
  location: 'northeurope'
}

param resourceGroups = {
  main: {
    name: 'rg-shop-platform-main-shared'
    location: 'northeurope'
    tags: {
      costCenter: 'ecommerce'
      'ifs-project': 'shop'
      'ifs-component': 'platform'
      'ifs-environment': 'shared'
      'managed-by': 'infraflowsculptor'
    }
  }
}

param acrMain = {
  deploy: true
  name: 'crshopmainshared'
  location: 'northeurope'
  acrSku: 'Standard'
  adminUserEnabled: false
  publicNetworkAccess: 'Enabled'
  anonymousPullEnabled: false
  tags: {
    costCenter: 'ecommerce'
    'ifs-project': 'shop'
    'ifs-component': 'platform'
    'ifs-environment': 'shared'
    'managed-by': 'infraflowsculptor'
  }
  diagnosticSettings: []
}
