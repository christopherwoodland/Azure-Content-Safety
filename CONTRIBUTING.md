# Contributing to Novel CSAM Detection

First off, thank you for considering contributing to Novel CSAM Detection! It's people like you that make Novel CSAM Detection such a great tool.

## Code of Conduct

This project and everyone participating in it is governed by our [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check the issue list as you might find out that you don't need to create one. When you are creating a bug report, please include as many details as possible:

* **Use a clear and descriptive title**
* **Describe the exact steps which reproduce the problem** in as much detail as possible
* **Provide specific examples to demonstrate the steps**
* **Describe the behavior you observed after following the steps** and point out what exactly is the problem with that behavior
* **Explain which behavior you expected to see instead and why**
* **Include screenshots and animated GIFs if possible**
* **Include your environment details**: OS, .NET version, Azure service versions

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion, please include:

* **Use a clear and descriptive title** for the feature or change
* **Provide a step-by-step description** of the suggested enhancement in as much detail as possible
* **Provide specific examples to demonstrate the steps**
* **Describe the current behavior** and **explain the expected behavior**
* **Explain why this enhancement would be useful** to most users

### Pull Requests

* Fill in the required template
* Follow the styleguides (see below)
* After you submit your pull request, verify that all status checks are passing

## Development Setup

### Prerequisites

- .NET 8 SDK or later
- Visual Studio Code or Visual Studio 2022
- Git
- Azure CLI (for Azure service testing)

### Getting Started with Development

1. **Fork and clone the repository**
   ```bash
   git clone https://github.com/your-username/Novel-CSAM-Detection.git
   cd Novel-CSAM-Detection
   ```

2. **Create a development branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Install dependencies**
   ```bash
   dotnet restore
   ```

4. **Build the solution**
   ```bash
   dotnet build
   ```

5. **Run tests**
   ```bash
   dotnet test
   ```

### Project Structure for Developers

```
NovelCsamDetection/
├── NovelCsam.Models/           # Shared data models
├── NovelCsam.Helpers/          # Business logic and utilities
├── NovelCsam.Functions/        # Azure Functions
├── NovelCsam.UI.Console/       # Console application
└── NovelCsamDetection.Tests/   # Unit and integration tests
```

## Styleguides

### C# Code Style

We follow Microsoft's C# Coding Conventions:

* Use **4 spaces** for indentation (no tabs)
* Use **PascalCase** for class and method names
* Use **camelCase** for local variables and parameters
* Use **UPPER_CASE** for constants
* Use **XML documentation comments** for public methods and classes

**Example:**

```csharp
/// <summary>
/// Uploads a file to Azure Blob Storage.
/// </summary>
/// <param name="containerName">The name of the container</param>
/// <param name="filePath">The path to the file to upload</param>
/// <returns>The path where the file was uploaded</returns>
public async Task<string> UploadFileAsync(string containerName, string filePath)
{
    // Implementation here
    LogHelper.LogInformation("File uploaded successfully", nameof(StorageHelper), nameof(UploadFileAsync));
    return uploadPath;
}
```

### Commit Messages

* Use the present tense ("Add feature" not "Added feature")
* Use the imperative mood ("Move cursor to..." not "Moves cursor to...")
* Limit the first line to 72 characters or less
* Reference issues and pull requests liberally after the first line

**Example:**

```
Add support for Durable Function orchestration

- Implement orchestrator pattern for batch processing
- Add retry logic for transient failures
- Fixes #123
```

### Documentation

* Use clear and concise language
* Include code examples where appropriate
* Update the README.md if you add new features
* Add XML documentation comments to all public APIs

## Testing

### Unit Tests

All new features should include unit tests:

```bash
dotnet test NovelCsamDetection.Tests
```

**Test File Naming:** `[ClassName]Tests.cs`

**Example:**

```csharp
[TestClass]
public class StorageHelperTests
{
    [TestMethod]
    public async Task UploadFileAsync_ValidFile_ReturnsPath()
    {
        // Arrange
        var helper = new StorageHelper();
        
        // Act
        var result = await helper.UploadFileAsync("container", "path");
        
        // Assert
        Assert.IsNotNull(result);
    }
}
```

### Integration Tests

For Azure service integration, create integration tests with appropriate setup/teardown:

```csharp
[TestClass]
public class AzureSQLHelperIntegrationTests
{
    private IAzureSQLHelper _helper;
    
    [TestInitialize]
    public void Setup()
    {
        // Initialize with test configuration
        _helper = new AzureSQLHelper();
    }
    
    [TestCleanup]
    public void Cleanup()
    {
        // Clean up test data
    }
}
```

## Logging Guidelines

Use the `LogHelper` class for all logging:

```csharp
// Information
LogHelper.LogInformation("Processing started", nameof(VideoHelper), nameof(ProcessAsync));

// Warning
LogHelper.LogWarning("Rate limit approaching", nameof(ContentSafetyHelper), nameof(AnalyzeAsync));

// Exception
LogHelper.LogException($"Operation failed: {ex.Message}", nameof(StorageHelper), nameof(UploadAsync), ex);
```

## Error Handling

Use proper exception handling patterns:

```csharp
try
{
    // Attempt operation
    await ProcessAsync();
}
catch (TimeoutException ex)
{
    LogHelper.LogException($"Operation timeout: {ex.Message}", nameof(VideoHelper), nameof(ProcessAsync), ex);
    throw; // Re-throw or handle as appropriate
}
catch (Exception ex)
{
    LogHelper.LogException($"Unexpected error: {ex.Message}", nameof(VideoHelper), nameof(ProcessAsync), ex);
    throw;
}
```

## Submitting Changes

### Before You Submit

1. **Run tests locally**
   ```bash
   dotnet test
   ```

2. **Build in Release mode**
   ```bash
   dotnet build -c Release
   ```

3. **Check your code**
   - Ensure all XML documentation is complete
   - Remove debug statements
   - Verify logging is appropriate
   - Check for unused using statements

### Creating a Pull Request

1. **Push to your fork**
   ```bash
   git push origin feature/your-feature-name
   ```

2. **Create a Pull Request on GitHub**
   - Use a descriptive title
   - Reference any related issues
   - Describe your changes in detail
   - Include screenshots for UI changes

3. **PR Template:**
   ```markdown
   ## Description
   Brief description of the changes

   ## Related Issue
   Fixes #(issue number)

   ## Changes Made
   - Change 1
   - Change 2
   - Change 3

   ## Testing
   Describe the tests you ran and how to reproduce them

   ## Screenshots (if applicable)
   Add screenshots for UI changes

   ## Checklist
   - [ ] My code follows the style guidelines of this project
   - [ ] I have performed a self-review of my own code
   - [ ] I have commented my code, particularly in hard-to-understand areas
   - [ ] I have made corresponding changes to the documentation
   - [ ] My changes generate no new warnings
   - [ ] I have added tests that prove my fix is effective or that my feature works
   - [ ] New and existing unit tests passed locally with my changes
   ```

## Additional Notes

### Issue and Pull Request Labels

* `bug` - Something isn't working
* `enhancement` - New feature or request
* `documentation` - Improvements or additions to documentation
* `good first issue` - Good for newcomers
* `help wanted` - Extra attention is needed
* `question` - Further information is requested

### Review Process

1. Code is reviewed for:
   - Correctness and functionality
   - Code style and quality
   - Test coverage
   - Documentation completeness

2. At least one maintainer approval is required

3. All status checks must pass before merging

### Acknowledgments

Contributors will be:
- Added to the CONTRIBUTORS.md file
- Acknowledged in release notes
- Thanked in the GitHub repository

## Questions?

* Open an issue with the `question` label
* Join our community discussions
* Contact the maintainers

Thank you for contributing! 🎉
