# Novel CSAM Detection - Architecture Guide

## Overview

This document describes the architecture and design patterns used in the Novel CSAM Detection accelerator.

## System Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                  Console Application Layer                   │
│  (NovelCsam.UI.Console - MenuHandler, Program)              │
└──────────────────────┬──────────────────────────────────────┘
                       │
        ┌──────────────┴──────────────┐
        │                             │
        ▼                             ▼
┌──────────────────────┐    ┌──────────────────────┐
│   Business Logic     │    │  Azure Functions     │
│  (Helpers Package)   │    │  (Orchestration)     │
└──────────────────────┘    └──────────────────────┘
        │                             │
        ├─────────────┬───────────────┤
        │             │               │
        ▼             ▼               ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│ Azure Blob   │ │ Azure Content│ │  Azure SQL   │
│  Storage     │ │  Safety API  │ │  Database    │
└──────────────┘ └──────────────┘ └──────────────┘
```

## Project Organization

### NovelCsam.Models
**Purpose**: Shared data models and contracts

**Contents**:
- `FrameResult` - Represents analysis results for a frame
- `AnalyzeFrameModel` - Input model for frame analysis
- `DurableTaskInstance` - Tracks Durable Function execution
- `OrchestrationStatus` - Orchestration state information
- Interfaces (`IFrameResult`, `IFrameDetailResult`, etc.)

**Design Pattern**: Records with XML documentation

```csharp
public record FrameResult : IFrameResult
{
    [JsonProperty("id")]
    public required string Id { get; set; }
    public string? Summary { get; set; }
    public int Hate { get; set; }
    public int Violence { get; set; }
    public int Sexual { get; set; }
    public int SelfHarm { get; set; }
}
```

### NovelCsam.Helpers
**Purpose**: Business logic and Azure service integration

**Key Components**:

#### VideoHelper
- **Responsibility**: Video processing and frame extraction
- **Dependencies**: FFmpegHelper, StorageHelper, ContentSafetyHelper, AzureSQLHelper
- **Key Methods**:
  - `UploadFileToBlobAsync` - Upload video/image files
  - `UploadFrameResultsAsync` - Analyze frames directly
  - `UploadFrameResultsDurableFunctionAsync` - Orchestrated analysis
  - `SummarizeImageAsync` - AI-powered summarization using OpenAI
  - `GetContentSafteyDetailsAsync` - Content safety analysis

#### StorageHelper
- **Responsibility**: Azure Blob Storage operations
- **Pattern**: Uses DataLakeFileClient for Gen2 operations
- **Key Methods**:
  - `ListDirectoriesInFolderAsync` - Navigate folder hierarchy
  - `ListBlobsInFolderWithResizeAsync` - Fetch blobs with image optimization
  - `UploadFileAsync` - Upload files to blob storage
  - `DownloadFileAsync` - Download files locally

**Note**: Image resizing logic extracted to `ImageResizeHelper`

#### AzureSQLHelper
- **Responsibility**: Database operations and persistence
- **Pattern**: Uses connection pooling with retry policies
- **Retry Logic**: Handles transient SQL errors
- **Key Methods**:
  - `CreateFrameResult` - Insert analysis results
  - `GetFrameResultWithLevelsAsync` - Query results with filtering

#### ContentSafetyHelper
- **Responsibility**: Content Safety API integration
- **Pattern**: Round-robin load balancing across 3 instances
- **Retry Policy**: Exponential backoff for transient failures
- **Key Methods**:
  - `AnalyzeImageAsync` - Analyze images for harmful content
  - `GetNextContentSafetyClient` - Load-balanced client selection

#### ResultExporter
- **Responsibility**: Export analysis results to multiple storage backends
- **Supported Formats**: JSON to Azure Blob Storage
- **Patterns**: Strategy pattern for pluggable exporters
- **Key Methods**:
  - `ExportFrameResultsAsJsonAsync` - Export multiple results as single JSON file
  - `ExportFrameResultAsJsonAsync` - Export individual result as JSON file
  - `UploadJsonToBlobAsync` (private) - Blob storage operations with cleanup
- **Features**:
  - Automatic filename sanitization for blob-safe paths
  - Timestamp-based naming for versioning
  - Graceful error handling with logging
  - Optional feature (configurable via ENABLE_JSON_EXPORT)

#### FFmpegHelper
- **Responsibility**: FFmpeg wrapper for video operations
- **Modes**:
  - `FRAME_SEGMENT` - Extract frames at intervals
  - `VIDEO_SEGMENT` - Split video into chunks
- **Key Methods**:
  - `ExtractFramesAsync` - Extract frames from video
  - `SegmentVideoAsync` - Create video segments

#### ImageResizeHelper
- **Responsibility**: Image optimization
- **Key Methods**:
  - `ResizeImageIfNeeded` - Conditional resizing based on dimensions
  - `ResizeImage` - Direct resizing with quality settings
  - `ResizeImageByMaxSize` - Aspect ratio-preserving resize

#### CsvExporter
- **Responsibility**: CSV export functionality
- **Format**: Columns for all detection categories and metadata

#### LogHelper
- **Responsibility**: Structured logging
- **Targets**: Console, Application Insights, Log file
- **Methods**:
  - `LogInformation` - Info-level logging
  - `LogWarning` - Warning-level logging
  - `LogException` - Exception logging with stack trace

### NovelCsam.Functions
**Purpose**: Azure Functions for distributed processing

**Functions**:
- `AnalyzeFrame` - HTTP-triggered frame analysis
- `ImageProcessingOrchestrator` - Durable Function orchestrator
- `ListBlobs` - Utility function for blob enumeration

**Pattern**: Async/await with error handling and logging

### NovelCsam.UI.Console
**Purpose**: User-facing console application

**Components**:

#### Program.cs
- **Responsibility**: Application initialization and entry point
- **Tasks**:
  - Configuration loading
  - Dependency injection setup
  - Service initialization
  - Menu loop management

**Architecture**:
```csharp
Main()
  ├─ SetEnvVariables()        // Load config
  ├─ ConfigureServices()       // DI setup
  ├─ Initialize Services      // Create instances
  └─ MenuHandler.PrintMenu()  // User interaction
```

#### MenuHandler.cs
- **Responsibility**: User interaction and menu orchestration
- **Pattern**: Single responsibility - handle UI and orchestrate workflows
- **Methods**:
  - `PrintMenu()` - Display menu options
  - `ProcessMenuSelectionAsync()` - Route to appropriate handler
  - Individual handlers for each menu option

**Design Pattern**:
- Extracted from Program.cs for separation of concerns
- All user input validation here
- Logging at operation level

## Data Flow Patterns

### Direct Processing Flow

```
User selects option 4 (Run Safety Analysis)
    ↓
MenuHandler.RunSafetyAnalysisAsync()
    ↓
StorageHelper.ListBlobsInFolderWithResizeAsync()
    ├─ Load images
    └─ Resize if needed
    ↓
VideoHelper.UploadFrameResultsAsync()
    ├─ For each frame:
    │  ├─ ContentSafetyHelper.AnalyzeImageAsync()
    │  ├─ VideoHelper.SummarizeImageAsync() (optional)
    │  ├─ Create FrameResult
    │  └─ AzureSQLHelper.CreateFrameResult()
    └─ Return RunId
    ↓
Export to CSV or display results
```

### Durable Function Orchestration Flow

```
User selects option 6 (Durable Function Analysis)
    ↓
VideoHelper.UploadFrameResultsDurableFunctionAsync()
    ├─ POST to Azure Function with parameters
    └─ Get orchestration instance
    ↓
ImageProcessingOrchestrator (Durable Function)
    ├─ Call AnalyzeFrame for each blob
    ├─ Aggregate results
    └─ Store in database
    ↓
Poll orchestration status
    ├─ Check every 3 seconds
    └─ Until completion
    ↓
Return results
```

### Results Export Flow (Optional)

```
After analysis completes (AnalyzeFrame activity)
    ↓
Check: ENABLE_SQL_PERSISTENCE setting
    ├─ TRUE: Store result in SQL database
    └─ FALSE: Skip database storage
    ↓
Check: ENABLE_JSON_EXPORT setting
    ├─ FALSE: Skip export (default)
    └─ TRUE: Continue to export
    ↓
ResultExporter.ExportFrameResultAsJsonAsync()
    ├─ Sanitize filename for blob-safe paths
    ├─ Serialize result to JSON with metadata
    ├─ Generate timestamped filename: {RunId}_{FrameName}_{Id}.json
    └─ Upload to Azure Blob Storage
    ↓
Export complete (non-blocking, logged for diagnostics)
    ↓
Next frame processing continues
```

**Note**: Both database and export operations are non-blocking. If either fails, errors are logged but analysis continuation is unaffected.

**Storage Options**:
- Database + JSON: `ENABLE_SQL_PERSISTENCE=true, ENABLE_JSON_EXPORT=true`
- Database only: `ENABLE_SQL_PERSISTENCE=true, ENABLE_JSON_EXPORT=false` (default)
- JSON only: `ENABLE_SQL_PERSISTENCE=false, ENABLE_JSON_EXPORT=true`

## Design Patterns Used

### 1. Repository Pattern (Storage Operations)
```csharp
// StorageHelper abstracts Azure Blob access
public async Task<Dictionary<string, BinaryData>> ListBlobsInFolderWithResizeAsync(...)
{
    // Implementation details hidden
    // Only interface matters
}
```

### 2. Factory Pattern (Client Creation)
```csharp
// ContentSafetyHelper creates clients on-demand
public ContentSafetyClient GetNextContentSafetyClient()
{
    // Returns appropriate client with load balancing
}
```

### 3. Retry Pattern (Resilience)
```csharp
// Polly retry policy for transient failures
_retryPolicy = Policy
    .Handle<SqlException>(ex => ex.Number == -2)
    .WaitAndRetryAsync(10, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(3, retryAttempt)));
```

### 4. Builder Pattern (Configuration)
```csharp
// FFmpeg command building
var conversion = FFmpeg.Conversions.New();
conversion.AddParameter($"-i \"{videoPath}\"")
    .AddParameter($"-vf \"fps=1/{frameInterval}\"")
    .SetOutput(framePattern);
```

### 5. Dependency Injection
```csharp
// Constructor injection for loose coupling
public VideoHelper(IStorageHelper sth, IContentSafetyHelper csh, 
    IAzureSQLHelper ash, HttpClient httpClient)
{
    _sth = sth;
    _csh = csh;
    // ...
}
```

### 6. Strategy Pattern (Processing Modes)
```csharp
// Direct processing vs. Durable Function orchestration
if (useDurableFunction)
    await UploadFrameResultsDurableFunctionAsync(...);
else
    await UploadFrameResultsAsync(...);
```

## Error Handling Strategy

### Layered Exception Handling

**Level 1: Service Layer**
```csharp
try
{
    // Operation
}
catch (SpecificException ex)
{
    LogHelper.LogException(...);
    throw; // Let caller decide
}
catch (Exception ex)
{
    LogHelper.LogException(...);
    return null; // Graceful degradation
}
```

**Level 2: Business Logic Layer**
```csharp
try
{
    result = await serviceLayer.OperationAsync();
}
catch (InvalidOperationException ex)
{
    // Handle expected exceptions
    return defaultValue;
}
```

**Level 3: UI Layer (MenuHandler)**
```csharp
try
{
    await ProcessMenuSelectionAsync(choice);
}
catch (Exception ex)
{
    LogHelper.LogException(...);
    PrintErrorMessage(ex.Message);
}
```

### Specific Error Patterns

**SQL Errors**: Retry with exponential backoff
```csharp
// AzureSQLHelper detects and retries transient errors
// Error numbers: -2 (timeout), 1205 (deadlock), 40613 (unavailable)
```

**Content Safety Rate Limiting**: Load balancing
```csharp
// ContentSafetyHelper distributes requests across 3 instances
// Implements round-robin selection
```

**API Timeouts**: Retry with increasing delays
```csharp
// VideoHelper uses Polly for HTTP retries
// Handles 429 Too Many Requests
```

## Configuration Management

### Configuration Hierarchy

```
appsettings.json (application defaults)
    ↓
Environment Variables (runtime overrides)
    ↓
SetEnvVariables() loads from appsettings.json into env vars
    ↓
Services read from environment variables
```

### Configuration Flow

```
Program.Main()
  ├─ SetEnvVariables() 
  │  ├─ Read appsettings.json
  │  └─ Set Environment.SetEnvironmentVariable()
  │
  └─ ConfigureServices()
     ├─ Create ServiceCollection
     ├─ Register services
     └─ Build ServiceProvider
        └─ Services read from Environment.GetEnvironmentVariable()
```

## Logging Architecture

### Logging Levels

- **Information**: Normal operations (file uploads, analysis started)
- **Warning**: Degraded functionality (missing config, retries)
- **Exception**: Errors requiring attention with stack trace

### Log Destinations

1. **Console**: When `DebugToConsole` = true
2. **Application Insights**: When connection string provided
3. **File**: Optional, configured in LogHelper

### Logging Pattern

```csharp
LogHelper.LogInformation(
    $"Frame processed: {frameId}", 
    nameof(VideoHelper),              // Class
    nameof(UploadFrameResultsAsync)   // Method
);
```

## Performance Considerations

### 1. Batch Processing
- Images grouped into batches for efficient processing
- Durable Functions for large-scale operations

### 2. Image Resizing
- Automatic resizing prevents memory issues
- High-quality interpolation maintains usability

### 3. Connection Pooling
- Multiple Content Safety instances distribute load
- SQL connection pooling via connection string

### 4. Caching Strategies
- Avoid repeated blob downloads (pass BinaryData around)
- Reuse HttpClient instance (singleton in DI)

### 5. Async/Await
- All I/O operations non-blocking
- UI thread not blocked by long operations
- Progress bars for user feedback

## Security Considerations

### 1. Credential Management
- Stored in appsettings.json (not committed to git)
- Set via environment variables in production
- Never logged or exposed

### 2. Input Validation
- File paths validated before use
- Container names checked
- User input sanitized

### 3. Error Messages
- Don't expose sensitive details
- Log full details, show generic messages
- "An error occurred" pattern for users

### 4. Azure Authentication
- Uses connection strings and API keys
- Key rotation supported via environment updates
- No hardcoded credentials

## Scalability Patterns

### Horizontal Scaling

**Multiple Content Safety Instances**:
```json
"ContentSafety": {
  "Instance1": "...",
  "Instance2": "...",
  "Instance3": "..."
}
```
Round-robin distribution prevents rate limiting.

**Durable Functions**: 
- Automatically scale with Azure Functions runtime
- Support thousands of concurrent frames
- Automatic retry and fault handling

### Vertical Scaling

**Image Resizing**:
- Reduces memory usage
- Faster processing
- Quality preserved

**Frame Interval**:
- Increase interval for fewer frames
- Decrease for higher detail

## Testing Strategy

### Unit Tests
- Test business logic in isolation
- Mock Azure services
- Fast execution

### Integration Tests
- Test with real Azure services (test resources)
- Verify end-to-end flows
- Slower but more realistic

### Test Organization
```
NovelCsamDetection.Tests/
├── Unit/
│   ├── VideoHelperTests.cs
│   ├── StorageHelperTests.cs
│   └── ContentSafetyHelperTests.cs
└── Integration/
    ├── AzureSQLHelperIntegrationTests.cs
    └── EndToEndTests.cs
```

## Deployment Architecture

### Local Development
```
Visual Studio Code / Visual Studio 2022
    ↓
Compile & Run locally
    ↓
Test against local Azure Emulator or test Azure resources
```

### Production Deployment
```
Console Application
    ├─ On Virtual Machine or App Service
    └─ References:
       ├─ Azure Blob Storage
       ├─ Azure Content Safety
       ├─ Azure SQL Database
       ├─ Azure OpenAI (optional)
       └─ Azure Application Insights

Azure Functions
    ├─ Consumption plan
    └─ Durable Functions storage
```

## Future Enhancement Opportunities

1. **Microservices**: Separate services for each major function
2. **Caching**: Redis for frequently accessed data
3. **Message Queues**: Azure Service Bus for async processing
4. **API Layer**: REST API for programmatic access
5. **Web UI**: React/Vue.js frontend
6. **Machine Learning**: Custom models for specialized detection
7. **Real-time Processing**: Event Hubs for streaming video
8. **Governance**: Policy enforcement and audit trails

## References

- [Azure Best Practices](https://learn.microsoft.com/en-us/azure/architecture/best-practices/)
- [Design Patterns in .NET](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles)
- [Azure Durable Functions](https://learn.microsoft.com/en-us/azure/azure-functions/durable/)
- [Polly Retry Policies](https://github.com/App-vNext/Polly)

## Recent Changes (v2.1.0 - October 2025)

### Added Components

#### ResultExporter Service
- **Location:** `NovelCsam.Helpers/ResultExporter.cs`
- **Interface:** `NovelCsam.Helpers/Interfaces/IResultExporter.cs`
- **Purpose:** Export analysis results to multiple storage backends
- **Key Methods:**
  - `ExportFrameResultsAsJsonAsync()` - Batch export with metadata wrapper
  - `ExportFrameResultAsJsonAsync()` - Individual result export
- **Features:**
  - Automatic filename sanitization
  - Timestamp-based versioning
  - Graceful error handling
  - Non-blocking operation (export failures don't interrupt analysis)

### Configuration Enhancements

New FunctionSettings Properties:
- `EnableJsonExport` - Toggle JSON export feature (default: false)
- `JsonExportContainerName` - Blob container name (default: "results")
- `JsonExportFolderPath` - Folder organization path (default: "json-results")
- `EnableSqlPersistence` - Toggle database storage (default: true)

### Storage Options (New in v2.1.0)

| SQL | JSON | Behavior | Use Case |
|-----|------|----------|----------|
| true | false | Database only | Traditional mode (default) |
| true | true | Database + JSON | Audit trail + structured export |
| false | true | JSON only | Blob-only storage |
| false | false | Neither | Not recommended |

### Test Coverage (v2.1.0)
- ResultExporterTests: 12 tests for export functionality
- AnalyzeFrameConfigurationTests: 12 tests for configuration combinations
- Total: 24/24 tests passing (100%)

### Documentation Updates (v2.1.0)
- Updated ARCHITECTURE.md with export patterns
- Updated CONFIGURATION_GUIDE.md with storage options table
- Integrated local settings guide into README.md
