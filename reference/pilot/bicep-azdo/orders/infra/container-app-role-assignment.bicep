// Module ciblé pour attribuer un rôle à l’échelle d’une Container App.
targetScope = 'resourceGroup'

@description('Nom de la Container App qui reçoit le rôle.')
param containerAppName string

@description('Object ID du principal auquel le rôle est attribué.')
param principalId string

@description('ID de définition du rôle intégré.')
param roleDefinitionId string

var containerAppsContributorRoleId = '358470bc-b998-42bd-ab17-a7e34c199c0f'

resource ordersApi 'Microsoft.App/containerApps@2025-01-01' existing = {
  name: containerAppName
}

resource appDeliveryContainerAppsContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(ordersApi.id, principalId, containerAppsContributorRoleId)
  scope: ordersApi
  properties: {
    roleDefinitionId: roleDefinitionId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
