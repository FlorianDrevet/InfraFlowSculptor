// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.

targetScope = 'resourceGroup'

@description('Nom du registre de conteneurs existant qui reçoit l’attribution de rôle.')
param registryName string

@description('Nom déterministe de l’attribution de rôle.')
param assignmentName string

@description('Identifiant objet du principal auquel le rôle est attribué.')
param principalId string

@description('Identifiant de définition du rôle intégré.')
param roleDefinitionId string

resource registry 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  name: registryName
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: assignmentName
  scope: registry
  properties: {
    roleDefinitionId: roleDefinitionId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
