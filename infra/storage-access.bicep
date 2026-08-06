targetScope = 'resourceGroup'

param identityPrincipalId string
param storageAccountName string
param frontendOrigin string

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2026-04-01' = {
  parent: storage
  name: 'default'
  properties: {
    cors: {
      corsRules: [
        {
          allowedHeaders: ['*']
          allowedMethods: ['GET', 'PUT', 'POST', 'HEAD', 'OPTIONS']
          allowedOrigins: ['http://localhost:5173', 'http://127.0.0.1:5173']
          exposedHeaders: ['*']
          maxAgeInSeconds: 3600
        }
        {
          allowedHeaders: ['*']
          allowedMethods: ['DELETE', 'GET', 'HEAD', 'MERGE', 'POST', 'OPTIONS', 'PUT', 'PATCH']
          allowedOrigins: ['http://localhost:5174', 'http://127.0.0.1:5174']
          exposedHeaders: ['*']
          maxAgeInSeconds: 3600
        }
        {
          allowedHeaders: ['content-type', 'x-ms-blob-type', 'x-ms-version']
          allowedMethods: ['GET', 'HEAD', 'OPTIONS', 'PUT']
          allowedOrigins: [frontendOrigin]
          exposedHeaders: ['etag', 'x-ms-request-id', 'x-ms-version']
          maxAgeInSeconds: 3600
        }
      ]
    }
    deleteRetentionPolicy: {
      allowPermanentDelete: false
      enabled: false
    }
    staticWebsite: {
      enabled: false
    }
  }
}

resource storageBlobOwnerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, identityPrincipalId, 'storage-blob-owner')
  scope: storage
  properties: {
    principalId: identityPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
  }
}

resource storageTableContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, identityPrincipalId, 'storage-table-contributor')
  scope: storage
  properties: {
    principalId: identityPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
  }
}

resource storageQueueContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, identityPrincipalId, 'storage-queue-contributor')
  scope: storage
  properties: {
    principalId: identityPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
  }
}
