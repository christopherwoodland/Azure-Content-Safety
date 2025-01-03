# NovelCsamDetection

## Azure AI Content Safety
- https://learn.microsoft.com/en-us/azure/ai-services/content-safety/overview
- https://learn.microsoft.com/en-us/azure/ai-services/content-safety/concepts/harm-categories?tabs=warning

## Overview

`NovelCsam.UI.Console` is a console application that provides functionality for extracting frames from video files, uploading them to Azure Blob Storage, and running safety analysis on the extracted frames.

## Features

- Extract frames from video files.
- Upload extracted frames to Azure Blob Storage.
- Run safety analysis on the extracted frames.
- Supports multiple image formats.

## Prerequisites

- .NET 8 SDK
- Azure Storage Account
- Azure Content Safety Service

## Getting Started

### Clone the Repository

```sh
git clone https://github.com/yourusername/NovelCsamDetection.git
cd NovelCsamDetection/NovelCsam.UI.Console
```


## Application Configuration

The application requires configuration for Azure Storage, Content Safety services, Azure SQL Database, Azure Cosmos DB, OpenAI Service, and Application Insights. Update the `appsettings.json` file with your Azure credentials and settings.

```
{
  "Azure": {
    "StorageAccountName": "your_storage_account_name",
    "StorageAccountKey": "your_storage_account_key",
    "ContentSafetyEndpoint": "your_content_safety_endpoint",
    "ContentSafetyKey": "your_content_safety_key",
    "AzureSqlConnectionString": "your_azure_sql_connection_string",
    "CosmosDbConnectionString": "your_cosmos_db_connection_string",
    "CosmosDbDatabaseName": "your_cosmos_db_database_name",
    "CosmosDbContainerName": "your_cosmos_db_container_name",
    "StorageAccountUrl": "your_storage_account_url",
    "OpenAiDeploymentName": "your_open_ai_deployment_name",
    "OpenAiKey": "your_open_ai_key",
    "OpenAiEndpoint": "your_open_ai_endpoint",
    "OpenAiModel": "your_open_ai_model",
    "AppInsightsConnectionString": "your_app_insights_connection_string"
  }
}
```
