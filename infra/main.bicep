targetScope = 'resourceGroup'

param environmentName string = 'prod'
param location string = resourceGroup().location
param staticWebAppLocation string = 'eastus2'
param storageAccountName string = 'cwacstest001'
param storageAccountResourceGroupName string = 'DefaultResourceGroup-CCAN'
param contentSafetyAccountName string = 'cwcs001'
param contentSafetyResourceGroupName string = 'integration'
param aiServicesAccountName string = 'bhs-development-public-foundry-r'
param contentSafetyEndpoint string = 'https://cwcs001.cognitiveservices.azure.com/'
param openAiEndpoint string = 'https://bhs-development-public-foundry-r.services.ai.azure.com'
param openAiModel string = 'gpt-5.4-1'

var normalizedEnvironment = toLower(replace(environmentName, '-', ''))
var unique = toLower(take(uniqueString(subscription().id, resourceGroup().id, environmentName), 7))
var functionAppName = take('func-content-safety-${normalizedEnvironment}-${unique}', 60)
var staticWebAppName = take('swa-content-safety-${normalizedEnvironment}-${unique}', 60)
var registryName = take('acrcontentsafety${normalizedEnvironment}${unique}', 50)
var identityName = take('id-content-safety-${normalizedEnvironment}-${unique}', 64)
var environmentResourceName = take('cae-content-safety-${normalizedEnvironment}-${unique}', 60)
var schedulerName = take('dts-content-safety-${normalizedEnvironment}-${unique}', 64)
var taskHubName = take('content-safety-${normalizedEnvironment}', 64)
var workspaceName = take('log-content-safety-${normalizedEnvironment}-${unique}', 63)
var appInsightsName = take('appi-content-safety-${normalizedEnvironment}-${unique}', 260)

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  tags: {
    'azd-env-name': environmentName
  }
  sku: {
    name: 'Standard'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  properties: {
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    DisableLocalAuth: true
    IngestionMode: 'LogAnalytics'
  }
}

resource containerEnvironment 'Microsoft.App/managedEnvironments@2025-07-01' = {
  name: environmentResourceName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: workspace.properties.customerId
        sharedKey: workspace.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

resource scheduler 'Microsoft.DurableTask/schedulers@2026-02-01' = {
  name: schedulerName
  location: location
  properties: {
    ipAllowlist: []
    publicNetworkAccess: 'Enabled'
    sku: {
      name: 'Consumption'
    }
  }
}

resource taskHub 'Microsoft.DurableTask/schedulers/taskHubs@2026-02-01' = {
  parent: scheduler
  name: taskHubName
  properties: {}
}

module storageAccess './storage-access.bicep' = {
  name: 'storage-access'
  scope: resourceGroup(storageAccountResourceGroupName)
  params: {
    identityPrincipalId: identity.properties.principalId
    storageAccountName: storageAccountName
  }
}

module aiAccess './ai-access.bicep' = {
  name: 'ai-access'
  scope: resourceGroup(contentSafetyResourceGroupName)
  params: {
    aiServicesAccountName: aiServicesAccountName
    contentSafetyAccountName: contentSafetyAccountName
    identityPrincipalId: identity.properties.principalId
  }
}

resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, identity.id, 'acrpull')
  scope: registry
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}

resource durableTaskContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(taskHub.id, identity.id, 'durable-task-contributor')
  scope: taskHub
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0ad04412-c4d5-4796-b79c-f76d14c8d402')
  }
}

resource monitoringMetricsPublisherRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appInsights.id, identity.id, 'monitoring-metrics-publisher')
  scope: appInsights
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb')
  }
}

resource functionApp 'Microsoft.Web/sites@2025-03-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux,container,azurecontainerapps'
  tags: {
    'azd-env-name': environmentName
    'azd-service-name': 'functions'
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerEnvironment.id
    workloadProfileName: 'Consumption'
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOCKER|mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0'
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: identity.id
      minimumElasticInstanceCount: 1
      functionAppScaleLimit: 10
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      appSettings: [
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'AzureWebJobsStorage__accountName', value: storageAccountName }
        { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
        { name: 'AzureWebJobsStorage__clientId', value: identity.properties.clientId }
        { name: 'AZURE_CLIENT_ID', value: identity.properties.clientId }
        { name: 'DOCKER_REGISTRY_SERVER_URL', value: registry.properties.loginServer }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING', value: 'Authorization=AAD;ClientId=${identity.properties.clientId}' }
        { name: 'AzureFunctionsJobHost__extensions__durableTask__hubName', value: taskHubName }
        { name: 'AzureFunctionsJobHost__extensions__durableTask__storageProvider__type', value: 'azureManaged' }
        { name: 'AzureFunctionsJobHost__extensions__durableTask__storageProvider__connectionStringName', value: 'DURABLE_TASK_SCHEDULER_CONNECTION_STRING' }
        { name: 'DURABLE_TASK_SCHEDULER_CONNECTION_STRING', value: 'Endpoint=${scheduler.properties.endpoint};Authentication=ManagedIdentity;ClientID=${identity.properties.clientId}' }
        { name: 'TASKHUB_NAME', value: taskHubName }
        { name: 'STORAGE_USE_MANAGED_IDENTITY', value: 'true' }
        { name: 'STORAGE_ACCOUNT_NAME', value: storageAccountName }
        { name: 'STORAGE_ACCOUNT_URL', value: 'https://${storageAccountName}.dfs.${environment().suffixes.storage}' }
        { name: 'STORAGE_ACCOUNT_BLOB_URL', value: 'https://${storageAccountName}.blob.${environment().suffixes.storage}' }
        { name: 'UPLOAD_CONTAINER_NAME', value: 'videos' }
        { name: 'UPLOAD_BLOB_PREFIX', value: 'input' }
        { name: 'JSON_EXPORT_CONTAINER_NAME', value: 'results' }
        { name: 'JSON_EXPORT_FOLDER_PATH', value: 'results' }
        { name: 'PROCESSED_SOURCE_DIRECTORY', value: 'processed' }
        { name: 'CONTENT_SAFETY_USE_MANAGED_IDENTITY', value: 'true' }
        { name: 'CONTENT_SAFETY_ENDPOINT1', value: contentSafetyEndpoint }
        { name: 'INVOKE_OPEN_AI', value: 'true' }
        { name: 'OPEN_AI_ENDPOINT', value: openAiEndpoint }
        { name: 'OPEN_AI_MODEL', value: openAiModel }
        { name: 'OPEN_AI_USE_MANAGED_IDENTITY', value: 'true' }
        { name: 'OPEN_AI_TIMEOUT_SECONDS', value: '2147483647' }
        { name: 'RETRY_MAX_ATTEMPTS', value: '3' }
        { name: 'RETRY_BACKOFF_MULTIPLIER', value: '2.0' }
        { name: 'DURABLE_BATCH_SIZE', value: '25' }
        { name: 'DURABLE_FRAME_INTERVAL_SECONDS', value: '1' }
      ]
    }
  }
  dependsOn: [
    acrPullRole
    storageAccess
    aiAccess
    durableTaskContributorRole
  ]
}

resource staticWebApp 'Microsoft.Web/staticSites@2025-03-01' = {
  name: staticWebAppName
  location: staticWebAppLocation
  tags: {
    'azd-env-name': environmentName
    'azd-service-name': 'web'
  }
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    allowConfigFileUpdates: true
    enterpriseGradeCdnStatus: 'Disabled'
  }
}

resource linkedBackend 'Microsoft.Web/staticSites/linkedBackends@2024-11-01' = {
  parent: staticWebApp
  name: 'production'
  kind: 'functionapp'
  properties: {
    backendResourceId: functionApp.id
    region: location
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = registry.properties.loginServer
output AZURE_CONTAINER_REGISTRY_NAME string = registry.name
output AZURE_FUNCTION_APP_NAME string = functionApp.name
output AZURE_STATIC_WEB_APP_NAME string = staticWebApp.name
output SERVICE_FUNCTIONS_ENDPOINT_URL string = 'https://${functionApp.properties.defaultHostName}'
output SERVICE_WEB_ENDPOINT_URL string = 'https://${staticWebApp.properties.defaultHostname}'
output durableTaskSchedulerName string = scheduler.name
output durableTaskHubName string = taskHub.name
output managedIdentityClientId string = identity.properties.clientId
