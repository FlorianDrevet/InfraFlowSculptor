// Module ciblé pour déployer une attribution à l’échelle d’un Key Vault.
targetScope = 'resourceGroup'

@description('Nom du Key Vault qui reçoit le rôle.')
param keyVaultName string

@description('Object ID du principal auquel le rôle est attribué.')
param principalId string

@description('ID de définition du rôle intégré.')
param roleDefinitionId string

var keyVaultSecretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

resource deploymentSecretsOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, principalId, keyVaultSecretsOfficerRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: roleDefinitionId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
