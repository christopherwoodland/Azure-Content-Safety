# Configuration System Implementation

## Overview
Successfully implemented a centralized configuration system for Azure Functions using the `FunctionSettings` class. All hardcoded values have been moved to environment variables with sensible defaults.

## Files Created/Modified

### 1. **FunctionSettings.cs** (NEW)
- **Location:** `NovelCsam.Functions/Configuration/FunctionSettings.cs`
- **Purpose:** Centralized configuration settings with environment variable support
- **Key Features:**
  - XML-documented properties for all settings
  - Factory method `FromEnvironment()` for loading from env vars
  - Default values with fallback behavior
  - Type-safe configuration access

### 2. **Program.cs** (MODIFIED)
- **Change:** Added FunctionSettings registration
  ```csharp
  builder.Services.AddSingleton(_ => FunctionSettings.FromEnvironment());
  ```
- **Effect:** FunctionSettings available via dependency injection

### 3. **ListBlobs.cs** (MODIFIED)
- **Changes:**
  - Added `FunctionSettings` parameter to constructor
  - Replaced hardcoded `3` with `_settings.BlobListingMaxDepth`
- **Configurable:** `BLOB_LISTING_MAX_DEPTH` environment variable

### 4. **AnalyzeFrame.cs** (MODIFIED)
- **Changes:**
  - Added `FunctionSettings` parameter to constructor
  - Updated retry policy to use `_settings.RetryMaxAttempts` and `_settings.RetryBackoffMultiplier`
  - Replaced hardcoded prompts with `_settings.DetailedAnalysisPrompt` and `_settings.ChildDetectionPrompt`
  - Replaced hardcoded "429" with `_settings.RateLimitErrorCode`
- **Configurable:**
  - `RETRY_MAX_ATTEMPTS`
  - `RETRY_BACKOFF_MULTIPLIER`
  - `DETAILED_ANALYSIS_PROMPT`
  - `CHILD_DETECTION_PROMPT`
  - `RATE_LIMIT_ERROR_CODE`

### 5. **GlobalUsings.cs** (MODIFIED)
- **Change:** Added `global using NovelCsam.Functions.Configuration;`
- **Effect:** FunctionSettings available in all function files without explicit using

### 6. **local.settings.json** (CREATED)
- **Location:** `NovelCsam.Functions/local.settings.json`
- **Purpose:** Template for local development configuration
- **Contains:**
  - Function runtime settings
  - All new configuration values with defaults
  - Azure service connection strings
  - OpenAI settings
  - AI prompt templates

## Configuration Properties

| Property | Env Variable | Default | Purpose |
|----------|--------------|---------|---------|
| `BlobListingMaxDepth` | `BLOB_LISTING_MAX_DEPTH` | `3` | Max recursion depth for folder listing |
| `RetryMaxAttempts` | `RETRY_MAX_ATTEMPTS` | `3` | Retry attempts for 429 errors |
| `RetryBackoffMultiplier` | `RETRY_BACKOFF_MULTIPLIER` | `2.0` | Exponential backoff base (2s, 4s, 8s...) |
| `RateLimitErrorCode` | `RATE_LIMIT_ERROR_CODE` | `"429"` | HTTP status code for rate limiting |
| `DetailedAnalysisPrompt` | `DETAILED_ANALYSIS_PROMPT` | 450-word analysis prompt | AI prompt for image analysis |
| `ChildDetectionPrompt` | `CHILD_DETECTION_PROMPT` | Yes/No detection prompt | AI prompt for child detection |
| `EnableJsonExport` | `ENABLE_JSON_EXPORT` | `false` | Enable JSON export to blob storage |
| `JsonExportContainerName` | `JSON_EXPORT_CONTAINER_NAME` | `"results"` | Container name for JSON exports |
| `JsonExportFolderPath` | `JSON_EXPORT_FOLDER_PATH` | `"json-results"` | Folder path for JSON result files |
| `EnableSqlPersistence` | `ENABLE_SQL_PERSISTENCE` | `true` | Persist results to SQL database |

## How to Use

### Local Development
1. Copy `local.settings.json` to your local environment
2. Update values as needed for your local setup
3. Functions will automatically load settings from environment variables

### Azure Deployment
1. Set environment variables in Azure Function App settings
2. Function settings are loaded at startup from `Environment.GetEnvironmentVariable()`
3. No code changes required between environments

### Example: Changing Retry Behavior

```json
{
  "Values": {
    "RETRY_MAX_ATTEMPTS": "5",
    "RETRY_BACKOFF_MULTIPLIER": "3.0"
  }
}
```

### Example: Enabling JSON Export

```json
{
  "Values": {
    "ENABLE_JSON_EXPORT": "true",
    "JSON_EXPORT_CONTAINER_NAME": "analysis-results",
    "JSON_EXPORT_FOLDER_PATH": "json-exports/2025"
  }
}
```

## Results Export Feature

### Feature Description

The system can optionally export analysis results to Azure Blob Storage in JSON format alongside database persistence. This provides:

- **Flexibility:** Choose between database storage, JSON export, or both
- **Audit Trail:** JSON files serve as immutable records of analysis
- **Integration:** Easy to parse and integrate with downstream systems
- **Scalability:** Blob storage handles large result sets efficiently

### How It Works

1. **Per-Result Export:** After each frame is analyzed and stored in the database
2. **Individual Files:** Each result is a separate JSON file with format: `{RunId}_{FrameName}_{ResultId}.json`
3. **Non-Blocking:** Export failures don't prevent analysis continuation
4. **Timestamped:** Filenames include timestamps for versioning

### Enable/Disable

Set `ENABLE_JSON_EXPORT` to:

- `false` (default) - Results only stored in database
- `true` - Results stored in both database AND JSON

## Storage Options

### Database Persistence
Use `ENABLE_SQL_PERSISTENCE` to control whether results are stored in the SQL database:

- `true` (default) - Results persisted to SQL database
- `false` - Results skipped for database (useful if only using JSON export)

### Storage Combinations

| SQL Enabled | JSON Enabled | Behavior |
|------------|-------------|----------|
| `true` | `false` | Results → Database only (traditional mode) |
| `true` | `true` | Results → Database AND JSON blob storage |
| `false` | `true` | Results → JSON blob storage only |
| `false` | `false` | Results discarded (not recommended) |

### JSON File Format

```json
{
  "id": "unique-result-id",
  "runId": "run-123",
  "frame": "frame_001.jpg",
  "md5Hash": "abc123def456",
  "hate": 0,
  "violence": 2,
  "sexual": 0,
  "selfHarm": 0,
  "summary": "Analysis summary text",
  "childYesNo": "No",
  "imageBase64": "base64-encoded-image-if-enabled"
}
```

### Storage Configuration

- **Container:** Specify via `JSON_EXPORT_CONTAINER_NAME`
- **Folder:** Organize results via `JSON_EXPORT_FOLDER_PATH`
- **Filename Pattern:** `{RunId}_{SanitizedFrameName}_{ResultId}.json`

## Benefits

✅ **Centralized Configuration:** All settings in one place with documentation
✅ **Type-Safe:** No string-based configuration access
✅ **Defaults:** Sensible defaults for all settings
✅ **Environment-Aware:** Works in local dev, staging, and production
✅ **Flexible:** Change behavior without recompiling
✅ **Documented:** XML documentation on all properties
✅ **Dependency Injection:** Clean integration with Azure Functions DI
✅ **No Code Changes:** Behavior changes via environment variables only

## Notes

- Pre-existing nullability issues remain unchanged (in AnalyzeFrame.cs for `_kernel` and `_kernelBuilder`)
- `ListBlobs.cs` still returns `null` on error (design decision - can be addressed in future refactoring)
- All configuration values are required only if corresponding features are used (e.g., `DETAILED_ANALYSIS_PROMPT` only if OpenAI integration is enabled)
