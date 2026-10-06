// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.

import {
  acrMainConfig
  resourceGroupsConfig
  targetConfig
} from './types.bicep'

targetScope = 'subscription'

param target targetConfig
param resourceGroups resourceGroupsConfig
param acrMain acrMainConfig

resource rg_main 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroups.main.name
  location: resourceGroups.main.location
  tags: resourceGroups.main.tags
}

// Groupe de ressources principal : registre d’images partagé.
module acrMainModule 'br/public:avm/res/container-registry/registry:0.13.0' = if (acrMain.deploy) {
  name: 'acrMain-${target.code}-${uniqueString(acrMain.name)}'
  scope: rg_main
  params: {
    name: acrMain.name
    location: acrMain.location
    acrSku: acrMain.acrSku
    acrAdminUserEnabled: acrMain.adminUserEnabled
    anonymousPullEnabled: acrMain.anonymousPullEnabled
    publicNetworkAccess: acrMain.publicNetworkAccess
    tags: acrMain.tags
    diagnosticSettings: []
    enableTelemetry: false
  }
}
