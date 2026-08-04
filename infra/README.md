# Deployment Infrastructure

This folder contains Bicep infrastructure for deploying the Azure Functions app with a managed identity-first configuration.

Hosting target for this deployment is Flex Consumption (Linux).

## What gets created

- Azure Storage Account (Data Lake Gen2 enabled)
- Application Insights resource
- Consumption Function App (Windows) with system-assigned managed identity
- RBAC assignment: Storage Blob Data Contributor on the storage account for the Function App identity

## Deployment with Azure Developer CLI

1. Install Azure Developer CLI.
2. Authenticate:

   azd auth login

3. Initialize environment:

   azd env new dev

4. Provision infra:

   azd provision

5. Deploy app:

   azd deploy

## Managed identity notes

- Storage data access is configured to use managed identity through app settings.
- SQL and Content Safety are configured for managed identity-first operation in code.
- You must grant the Function App managed identity the required data-plane permissions in Azure SQL and AI Content Safety.
- If Key Vault references are used for secrets, assign Key Vault Secrets User to the Function App managed identity.

## Parameters

Edit `main.parameters.json` or set values in your azd environment for:

- location
- sqlServer
- sqlDatabase
- contentSafetyEndpoint1/2/3
- openAiEndpoint/openAiDeploymentName/openAiModel
- openAiUseManagedIdentity
- openAiTimeoutSeconds
- keyVaultName
- sqlConnectionSecretUri
- contentSafetyConnectionKey1SecretUri
- contentSafetyConnectionKey2SecretUri
- contentSafetyConnectionKey3SecretUri

For Azure OpenAI/Azure AI Foundry in managed identity mode:
- set openAiUseManagedIdentity to true
- set openAiEndpoint to your project endpoint (for example: https://<project>.services.ai.azure.com)
- set openAiModel to your model name (for example: gpt-5.4)
- tune openAiTimeoutSeconds for larger/longer-latency models

## Key Vault references

Secret-valued settings can be provided as Key Vault references. Example value format:

@Microsoft.KeyVault(SecretUri=https://<vault-name>.vault.azure.net/secrets/<secret-name>/<version>)

## Post-deploy managed identity validation

Run these checks after azd provision/deploy:

1. Get function identity principal id.
2. Verify Storage Blob Data Contributor assignment.
3. Verify Key Vault Secrets User assignment if key vault is used.
4. Verify app settings contain expected endpoint and identity flags.

Example commands:

az functionapp identity show --name <function-app-name> --resource-group <resource-group>
az role assignment list --assignee <principal-id> --resource-group <resource-group> -o table
az functionapp config appsettings list --name <function-app-name> --resource-group <resource-group> -o table
