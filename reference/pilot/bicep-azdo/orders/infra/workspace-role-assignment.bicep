// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.

targetScope = 'resourceGroup'

@description('Nom du workspace Log Analytics existant qui reçoit l’attribution de rôle.')
param workspaceName string

@description('Nom déterministe de l’attribution de rôle.')
param assignmentName string

@description('Identifiant objet du principal auquel le rôle est attribué.')
param principalId string

@description('Identifiant de définition du rôle intégré.')
param roleDefinitionId string

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' existing = {
  name: workspaceName
}

resource logAnalyticsReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: assignmentName
  scope: workspace
  properties: {
    roleDefinitionId: roleDefinitionId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
