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
- Export results.

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

If  ***InvokeOpenAI* **is set to True, then please popualte:

* "OpenAiDeploymentName",
* "OpenAiKey",
* "OpenAiEndpoint",
* "OpenAiModel"

```
{
  "Azure": {
    "SqlConnectionString": "",
    "ContentSafetyConnectionString": "",
    "ContentSafetyConnectionKey": "",
    "StorageAccountName": "",
    "StorageAccountKey": "",
    "StorageAccountUrl": "",
    "OpenAiDeploymentName": "",
    "OpenAiKey": "",
    "OpenAiEndpoint": "",
    "OpenAiModel": "",
    "AppInsightsConnectionString": "",
    "AnalyzeFrameAzureFunctionUrl": "",
    "InvokeOpenAI": ""
  }
}
```

### Configuration Placeholders

* **Azure SQL Connection String** : `"SqlConnectionString"`
* **Content Safety Connection String** : `"ContentSafetyConnectionString"`
* **Content Safety Connection Key** : `"ContentSafetyConnectionKey"`
* **Storage Account Name** : `"StorageAccountName"`
* **Storage Account Key** : `"StorageAccountKey"`
* **Storage Account URL** : `"StorageAccountUrl"`
* **OpenAI Deployment Name** : `"OpenAiDeploymentName"`
* **OpenAI Key** : `"OpenAiKey"`
* **OpenAI Endpoint** : `"OpenAiEndpoint"`
* **OpenAI Model** : `"OpenAiModel"`
* **App Insights Connection String** : `"AppInsightsConnectionString"`
* **Invoke Open AI**: ""InvokeOpenAI"

## Code Structure

* `Program.cs`: The main entry point of the application.
* `IVideoHelper.cs`: Interface for video-related operations.
* `IStorageHelper.cs`: Interface for storage-related operations.
* `VideoHelper.cs`: Implementation of video-related operations.
* `StorageHelper.cs`: Implementation of storage-related operations.

## Database

This application uses a SQL database.
The database table create scripts can be found under
the "Infrastructure" folder. This code can also be
changed to use any database that we choose. There is
code that writes result data to a Cosmos DB for example under NovelCsam.Helpers/CosmosDBHelper.cs.

* **Create_Tables.sql :** Database tables

## Contributing

Contributions are welcome! Please open an issue or submit a pull request for any improvements or bug fixes.

## License

This project is licensed under the MIT License. See the LICENSE file for details.

## Contact

For any questions or support, please contact [c](vscode-file://vscode-app/c:/Users/cwoodland/AppData/Local/Programs/Microsoft%20VS%20Code/resources/app/out/vs/code/electron-sandbox/workbench/workbench.html)woodland@microsoft.com.
