# Content Safety Review

## Modernization Status

- Runtime upgraded to .NET 10.
- Azure Functions uses isolated worker with managed identity-first service access.
- Durable orchestration now passes blob paths instead of binary payloads.
- CI pipeline added under .github/workflows/ci.yml.

## Hosting Target Decision

Infrastructure deployment target is Azure Functions Flex Consumption (Linux).

Important:
- Current image-handling code uses System.Drawing APIs that are Windows-oriented.
- For production Linux execution, replace image operations with a cross-platform library such as SixLabors.ImageSharp or SkiaSharp.

## Azure AI Content Safety

- [https://learn.microsoft.com/en-us/azure/ai-services/content-safety/overview]()
- [https://learn.microsoft.com/en-us/azure/ai-services/content-safety/concepts/harm-categories?tabs=warning]()

## Overview

`NovelCsam.UI.Console` is a console application that provides functionality for extracting frames from video files, uploading them to Azure Blob Storage, and running safety analysis on the extracted frames.

The React web experience is branded as **Content Safety Review**.

## Features

- Extract frames from video files.
- Upload extracted frames to Azure Blob Storage.
- Run safety analysis on the extracted frames.
- Supports multiple image formats.
- Export results.

## Prerequisites

- .NET 10 SDK
  - [Download .NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- Azure Storage Account
- Azure Content Safety Service
  - [https://learn.microsoft.com/en-us/azure/ai-services/content-safety/overview]()
- FFMpeg
  - The `ffmpeg.exe, ffplay.exe, and ffprobe.exe` files must be placed in the `NovelCsam.UI.Console` project directory, as shown in the picture. You can download the executable for various platforms, including Windows, from the link above.
  - [Download FFmpeg](https://ffmpeg.org/download.html)
  - ![1737774750420](image/README/1737774750420.png)

## Getting Started

### Clone the Repository

```sh
git clone https://github.com/yourusername/NovelCsamDetection.git
cd NovelCsamDetection/NovelCsam.UI.Console
```

## Application Configuration

Functions runtime settings are configured with environment variables.

Use NovelCsam.Functions/local.settings.example.json as the baseline for local development.

### Core settings

- FUNCTIONS_WORKER_RUNTIME
- FUNCTIONS_WORKER_RUNTIME_VERSION
- APPLICATIONINSIGHTS_CONNECTION_STRING
- ANALYZE_FRAME_AZURE_FUNCTION_URL

### Storage settings

- STORAGE_USE_MANAGED_IDENTITY
- AZURE_STORAGE_CONNECTION_STRING
- STORAGE_ACCOUNT_NAME=cwacstest001
- STORAGE_ACCOUNT_URL=https://cwacstest001.dfs.core.windows.net
- STORAGE_ACCOUNT_KEY

For local app storage access, the current account name is `cwacstest001`. Leave `AzureWebJobsStorage` on the local development storage value unless you want to run the Functions host against a real Azure Storage account.

### Content Safety settings

- CONTENT_SAFETY_USE_MANAGED_IDENTITY
- CONTENT_SAFETY_ENDPOINT1
- CONTENT_SAFETY_CONNECTION_KEY1
- CONTENT_SAFETY_ENDPOINT2
- CONTENT_SAFETY_CONNECTION_KEY2
- CONTENT_SAFETY_ENDPOINT3
- CONTENT_SAFETY_CONNECTION_KEY3

For managed identity, set `CONTENT_SAFETY_USE_MANAGED_IDENTITY=true` and set `CONTENT_SAFETY_ENDPOINT1=https://cwcs001.cognitiveservices.azure.com/`. Leave the key fields blank.

### OpenAI settings

- INVOKE_OPEN_AI
- OPEN_AI_USE_MANAGED_IDENTITY
- OPEN_AI_PROJECT_ENDPOINT
- OPEN_AI_ENDPOINT
- OPEN_AI_DEPLOYMENT_NAME
- OPEN_AI_MODEL
- OPEN_AI_TIMEOUT_SECONDS

### Runtime tuning settings

- RETRY_MAX_ATTEMPTS
- RETRY_BACKOFF_MULTIPLIER
- DURABLE_BATCH_SIZE
- DETAILED_ANALYSIS_PROMPT
- CHILD_DETECTION_PROMPT

Managed identity is the default production pattern for Storage and Content Safety.

Managed identity is also the default for Azure OpenAI / Azure AI Foundry model invocation.

Result blobs are written to the `results` container and the `results/{runId}/` folder by default.

For your Foundry project endpoint pattern, set:

- OPEN_AI_PROJECT_ENDPOINT=https://<resource>.ai.azure.com/api/projects/<project>
- OPEN_AI_MODEL=gpt-5.4
- OPEN_AI_USE_MANAGED_IDENTITY=true
- OPEN_AI_TIMEOUT_SECONDS=2147483647

Fallback endpoint/deployment variables are also supported:

- OPEN_AI_ENDPOINT=https://<resource>.services.ai.azure.com
- OPEN_AI_DEPLOYMENT_NAME=<deployment-name>

Key-based auth remains available as fallback. Secret-valued app settings should be provided via Key Vault references.

Key Vault reference format:

@Microsoft.KeyVault(SecretUri=https://<vault-name>.vault.azure.net/secrets/<secret-name>/<version>)

## Build and Test

Run locally from repository root:

dotnet build .\NovelCsamDetection.sln --nologo
dotnet test .\NovelCsamDetection.Tests\NovelCsamDetection.Tests.csproj --nologo

## CI Quality Gate

GitHub Actions workflow:
- .github/workflows/ci.yml

CI performs:
- restore
- build with warnings as errors
- test execution

## Managed Identity Validation Checklist

After deployment, validate identity and data-plane access:

1. Confirm function identity is enabled.
2. Confirm role assignment on Storage Account:
   - Storage Blob Data Contributor
3. Confirm Key Vault role assignment if Key Vault references are used:
   - Key Vault Secrets User
4. Confirm Content Safety access for the managed identity.

Example commands:

az functionapp identity show --name <function-app-name> --resource-group <resource-group>
az role assignment list --assignee <principal-id> --resource-group <resource-group> -o table
az functionapp config appsettings list --name <function-app-name> --resource-group <resource-group> -o table

## Code Structure

* `Program.cs`: The main entry point of the application.
* `IVideoHelper.cs`: Interface for video-related operations.
* `IStorageHelper.cs`: Interface for storage-related operations.
* `VideoHelper.cs`: Implementation of video-related operations.
* `StorageHelper.cs`: Implementation of storage-related operations.

## Results Persistence

This application persists frame-level and run-level outputs as JSON blobs in Azure Storage.

- Per-frame output: `results/{runId}/{frameName}.json`
- Run manifest: `results/{runId}/job-result.json`

The console export flow reads the manifest and frame JSON blobs and writes CSV locally.

## Web Wizard Notes

The React wizard in `NovelCsam.Web` includes a per-run archive policy in Step 1:

- `Archive input files after success` is disabled by default.
- Disabled means source files remain in `videos/input` after successful completion so repeated runs can process the same inputs without manual re-upload.
- Enabled moves successfully processed source files to `videos/processed/{runId}/` after a run completes with no frame failures.

In Step 5 (results), each frame card has a pill action button labeled `View details` that opens the frame detail page.

## Contributing

Contributions are welcome! Please open an issue or submit a pull request for any improvements or bug fixes.

## License

This project is licensed under the MIT License. See the LICENSE file for details.

## Contact

For any questions or support, please contact cwoodland@microsoft.com.
