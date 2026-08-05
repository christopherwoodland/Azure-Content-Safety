targetScope = 'resourceGroup'

@description('Deployment environment name used as a naming suffix.')
param environmentName string = 'dev'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Optional override for the Function App name. Leave empty to auto-generate.')
param functionAppName string = ''

@description('Optional override for the Storage Account name. Leave empty to auto-generate.')
param storageAccountName string = ''

@description('Optional Key Vault name in the same resource group. Required for Key Vault references and secret role assignment.')
param keyVaultName string = ''

@description('Primary Content Safety endpoint URI. Example: https://my-content-safety.cognitiveservices.azure.com')
param contentSafetyEndpoint1 string = ''

@description('Optional Key Vault secret URI for CONTENT_SAFETY_CONNECTION_KEY1 app setting.')
param contentSafetyConnectionKey1SecretUri string = ''

@description('Secondary Content Safety endpoint URI.')
param contentSafetyEndpoint2 string = ''

@description('Optional Key Vault secret URI for CONTENT_SAFETY_CONNECTION_KEY2 app setting.')
param contentSafetyConnectionKey2SecretUri string = ''

@description('Tertiary Content Safety endpoint URI.')
param contentSafetyEndpoint3 string = ''

@description('Optional Key Vault secret URI for CONTENT_SAFETY_CONNECTION_KEY3 app setting.')
param contentSafetyConnectionKey3SecretUri string = ''

@description('Enable OpenAI augmentation for summaries.')
param invokeOpenAi bool = false

@description('Optional Azure OpenAI endpoint URI.')
param openAiEndpoint string = ''

@description('Optional Azure OpenAI deployment name.')
param openAiDeploymentName string = ''

@description('Use managed identity for Azure OpenAI/Azure AI Foundry calls.')
param openAiUseManagedIdentity bool = true

@description('Optional Azure OpenAI model name.')
param openAiModel string = ''

@description('Timeout in seconds for Azure OpenAI requests.')
param openAiTimeoutSeconds int = 240

var normalizedEnv = toLower(replace(environmentName, '-', ''))
var unique = toLower(take(uniqueString(resourceGroup().id, environmentName), 6))
var storageName = empty(storageAccountName) ? take('st${normalizedEnv}${unique}', 24) : toLower(storageAccountName)
var appName = empty(functionAppName) ? take('func-${normalizedEnv}-${unique}', 60) : functionAppName
var contentSafetyKey1SettingValue = empty(contentSafetyConnectionKey1SecretUri) ? '' : '@Microsoft.KeyVault(SecretUri=${contentSafetyConnectionKey1SecretUri})'
var contentSafetyKey2SettingValue = empty(contentSafetyConnectionKey2SecretUri) ? '' : '@Microsoft.KeyVault(SecretUri=${contentSafetyConnectionKey2SecretUri})'
var contentSafetyKey3SettingValue = empty(contentSafetyConnectionKey3SecretUri) ? '' : '@Microsoft.KeyVault(SecretUri=${contentSafetyConnectionKey3SecretUri})'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = if (!empty(keyVaultName)) {
  name: keyVaultName
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    isHnsEnabled: true
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${appName}-ai'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: null
  }
}

resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan'
  location: location
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  kind: 'functionapp'
  properties: {
    reserved: true
  }
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storage.name}/default/deployments'
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'functionapp,linux'
  tags: {
    'azd-service-name': 'functions'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}deployments'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 100
        instanceMemoryMB: 2048
      }
    }
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      use32BitWorkerProcess: false
      appSettings: [
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storage.name
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING'
          value: 'Authorization=AAD'
        }
        {
          name: 'STORAGE_USE_MANAGED_IDENTITY'
          value: 'true'
        }
        {
          name: 'STORAGE_ACCOUNT_NAME'
          value: storage.name
        }
        {
          name: 'STORAGE_ACCOUNT_URL'
          value: 'https://${storage.name}.dfs.${environment().suffixes.storage}'
        }
        {
          name: 'CONTENT_SAFETY_USE_MANAGED_IDENTITY'
          value: 'true'
        }
        {
          name: 'CONTENT_SAFETY_ENDPOINT1'
          value: contentSafetyEndpoint1
        }
        {
          name: 'CONTENT_SAFETY_CONNECTION_KEY1'
          value: contentSafetyKey1SettingValue
        }
        {
          name: 'CONTENT_SAFETY_ENDPOINT2'
          value: contentSafetyEndpoint2
        }
        {
          name: 'CONTENT_SAFETY_CONNECTION_KEY2'
          value: contentSafetyKey2SettingValue
        }
        {
          name: 'CONTENT_SAFETY_ENDPOINT3'
          value: contentSafetyEndpoint3
        }
        {
          name: 'CONTENT_SAFETY_CONNECTION_KEY3'
          value: contentSafetyKey3SettingValue
        }
        {
          name: 'INVOKE_OPEN_AI'
          value: invokeOpenAi ? 'true' : 'false'
        }
        {
          name: 'OPEN_AI_ENDPOINT'
          value: openAiEndpoint
        }
        {
          name: 'OPEN_AI_DEPLOYMENT_NAME'
          value: openAiDeploymentName
        }
        {
          name: 'OPEN_AI_MODEL'
          value: openAiModel
        }
        {
          name: 'OPEN_AI_USE_MANAGED_IDENTITY'
          value: openAiUseManagedIdentity ? 'true' : 'false'
        }
        {
          name: 'OPEN_AI_TIMEOUT_SECONDS'
          value: string(openAiTimeoutSeconds)
        }
        {
          name: 'RETRY_MAX_ATTEMPTS'
          value: '3'
        }
        {
          name: 'RETRY_BACKOFF_MULTIPLIER'
          value: '2.0'
        }
        {
          name: 'DURABLE_BATCH_SIZE'
          value: '25'
        }
      ]
    }
  }
}

resource storageBlobDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, functionApp.id, 'storage-blob-data-contributor')
  scope: storage
  properties: {
    principalId: functionApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalType: 'ServicePrincipal'
  }
}

resource storageBlobDataOwnerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, functionApp.id, 'storage-blob-data-owner')
  scope: storage
  properties: {
    principalId: functionApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
    principalType: 'ServicePrincipal'
  }
}

resource keyVaultSecretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(keyVaultName)) {
  name: guid(keyVault.id, functionApp.id, 'key-vault-secrets-user')
  scope: keyVault
  properties: {
    principalId: functionApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalType: 'ServicePrincipal'
  }
}

output functionAppName string = functionApp.name
output functionAppPrincipalId string = functionApp.identity.principalId
output storageAccountName string = storage.name
output applicationInsightsConnectionString string = appInsights.properties.ConnectionString
