// Généré par InfraFlowSculptor — Module ciblé pour attribuer un rôle à l’échelle d’un registre ACR.
targetScope = 'resourceGroup'

@description('Nom du registre existant qui reçoit le rôle.')
param registryName string

@description('Object ID du principal auquel le rôle est attribué.')
param principalId string

@description('ID de définition du rôle intégré.')
param roleDefinitionId string

var acrPushRoleId = '8311e382-0749-4cb8-b61a-304f252e45ec'

resource registry 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  name: registryName
}

resource appDeliveryAcrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, principalId, acrPushRoleId)
  scope: registry
  properties: {
    roleDefinitionId: roleDefinitionId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
