using Azure.AI.ContentSafety;
using NovelCsam.Helpers;
using NovelCsam.Helpers.Interfaces;

namespace NovelCsamDetection.Tests;

[TestClass]
public class VideoHelperExportTests
{
    [TestMethod]
    public async Task UploadFrameResultsAsync_WritesManifestToProvidedResultsFolder()
    {
        using var scope = new EnvironmentVariableScope();
        Environment.SetEnvironmentVariable("INVOKE_OPEN_AI", "false");

        var storage = new FakeStorageHelper();
        storage.Blobs["input/run/frame-1.jpg"] = new BinaryData(new byte[] { 1, 2, 3, 4 });

        var sut = new VideoHelper(
            storage,
            new NullContentSafetyHelper(),
            new FakeHttpClientFactory());

        var runId = await sut.UploadFrameResultsAsync(
            containerName: "videos",
            containerFolderPath: "input/run",
            containerFolderPathResults: "results",
            withBase64ofImage: false,
            getSummaryB: false,
            getChildYesNoB: false);

        Assert.IsFalse(string.IsNullOrWhiteSpace(runId));

        var manifestWrite = storage.UploadedText.Single(item => item.FileName == $"{runId}/job-result.json");
        Assert.AreEqual("videos", manifestWrite.ContainerName);
        Assert.AreEqual("results", manifestWrite.FolderPath);

        var frameWrite = storage.UploadedText.Single(item => item.FileName == $"{runId}/frame-1.json");
        Assert.AreEqual("videos", frameWrite.ContainerName);
        Assert.AreEqual("results", frameWrite.FolderPath);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _before = new(StringComparer.OrdinalIgnoreCase);
        private readonly string[] _keys = ["INVOKE_OPEN_AI"];

        public EnvironmentVariableScope()
        {
            foreach (var key in _keys)
            {
                _before[key] = Environment.GetEnvironmentVariable(key);
            }
        }

        public void Dispose()
        {
            foreach (var key in _keys)
            {
                Environment.SetEnvironmentVariable(key, _before[key]);
            }
        }
    }

    private sealed class FakeStorageHelper : IStorageHelper
    {
        public Dictionary<string, BinaryData> Blobs { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<UploadTextCall> UploadedText { get; } = [];

        public Task<Dictionary<string, BinaryData>> ListBlobsInFolderWithResizeAsync(string containerName, string folderPath, int maxDepth = 10, bool resize = true)
        {
            return Task.FromResult(new Dictionary<string, BinaryData>(Blobs, StringComparer.OrdinalIgnoreCase));
        }

        public Task<string> UploadTextAsync(string containerName, string folderPath, string fileName, string content)
        {
            UploadedText.Add(new UploadTextCall(containerName, folderPath, fileName, content));
            return Task.FromResult($"{folderPath}/{fileName}");
        }

        public Task<string> UploadFileAsync(string containerName, string folderPath, string fileName) => throw new NotImplementedException();
        public Task UploadFileAsync(string containerName, string folderPath, string fileName, string localVideoPath, string timestamp, string originalFileName) => throw new NotImplementedException();
        public Task DownloadFileAsync(string containerName, string containerFolderPath, string fileName, string localVideoPath) => throw new NotImplementedException();
        public Task<string?> DownloadTextAsync(string containerName, string blobPath) => throw new NotImplementedException();
        public Task<IReadOnlyList<string>> ListBlobPathsAsync(string containerName, string folderPath, int maxDepth = 10) => throw new NotImplementedException();
        public Task<BinaryData?> GetBlobAsBinaryDataAsync(string containerName, string blobPath, bool resize = true, int maxSizeBytes = 4194304) => throw new NotImplementedException();
        public Task<Dictionary<int, string>> ListDirectoriesInFolderAsync(string containerName, string folderPath, int maxDepth = 10) => throw new NotImplementedException();
        public Task<bool> MoveBlobAsync(string containerName, string sourceBlobPath, string destinationBlobPath, bool overwrite = false) => throw new NotImplementedException();
    }

    private sealed record UploadTextCall(string ContainerName, string FolderPath, string FileName, string Content);

    private sealed class NullContentSafetyHelper : IContentSafetyHelper
    {
        public Task<AnalyzeImageResult?> AnalyzeImageAsync(BinaryData inputImage) => Task.FromResult<AnalyzeImageResult?>(null);
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}