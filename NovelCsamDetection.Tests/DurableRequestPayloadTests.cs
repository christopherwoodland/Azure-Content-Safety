using Azure.AI.ContentSafety;
using NovelCsam.Helpers;
using NovelCsam.Helpers.Interfaces;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace NovelCsamDetection.Tests;

[TestClass]
public class DurableRequestPayloadTests
{
    [TestMethod]
    public async Task UploadFrameResultsDurableFunctionAsync_IncludesIntervalAndExtractedDirectoryInRequestPayload()
    {
        using var scope = new EnvironmentVariableScope();
        Environment.SetEnvironmentVariable("INVOKE_OPEN_AI", "false");
        Environment.SetEnvironmentVariable("ANALYZE_FRAME_AZURE_FUNCTION_URL", "http://localhost:7092/api/AnalyzeFrames_HttpStart");

        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:7092")
        };

        var sut = new VideoHelper(
            new NoOpStorageHelper(),
            new NullContentSafetyHelper(),
            new SingleClientFactory(httpClient));

        var returnedInput = await sut.UploadFrameResultsDurableFunctionAsync(
            containerName: "videos",
            containerFolderPath: "input",
            containerFolderPathResults: "results",
            withBase64ofImage: false,
            getSummaryB: false,
            getChildYesNoB: true,
            runId: "run-abc",
            frameIntervalSeconds: 5,
            extractedFramesDirectory: "extracted");

        StringAssert.Contains(handler.StartRequestBody, "\"FrameIntervalSeconds\":5");
        StringAssert.Contains(handler.StartRequestBody, "\"ExtractedFramesDirectory\":\"extracted\"");
        StringAssert.Contains(handler.StartRequestBody, "\"ContainerName\":\"videos\"");
        StringAssert.Contains(handler.StartRequestBody, "\"ContainerDirectory\":\"input\"");
        StringAssert.Contains(handler.StartRequestBody, "\"RunId\":\"run-abc\"");
        Assert.AreEqual("run-abc", returnedInput);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string StartRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestPath = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (request.Method == HttpMethod.Post && requestPath.EndsWith("/api/AnalyzeFrames_HttpStart", StringComparison.OrdinalIgnoreCase))
            {
                StartRequestBody = await (request.Content?.ReadAsStringAsync(cancellationToken) ?? Task.FromResult(string.Empty));
                var startResponse = """
                {
                  "id": "instance-1",
                  "statusQueryGetUri": "http://localhost:7092/runtime/webhooks/durabletask/instances/instance-1"
                }
                """;

                return new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = CreateJsonContent(startResponse)
                };
            }

            if (request.Method == HttpMethod.Get && requestPath.Contains("/runtime/webhooks/durabletask/instances/instance-1", StringComparison.OrdinalIgnoreCase))
            {
                var statusResponse = """
                {
                  "name": "ImageProcessingOrchestrator",
                  "instanceId": "instance-1",
                  "runtimeStatus": "Completed",
                  "input": "run-abc"
                }
                """;

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = CreateJsonContent(statusResponse)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = CreateJsonContent("{}")
            };
        }

        private static StringContent CreateJsonContent(string payload)
        {
            var content = new StringContent(payload, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return content;
        }
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class NoOpStorageHelper : IStorageHelper
    {
        public Task<string> UploadFileAsync(string containerName, string folderPath, string fullFilePath) => throw new NotImplementedException();
        public Task UploadFileAsync(string containerName, string folderPath, string fileName, string localVideoPath, string timestamp, string originalFileName) => throw new NotImplementedException();
        public Task DownloadFileAsync(string containerName, string containerFolderPath, string fileName, string localVideoPath) => throw new NotImplementedException();
        public Task<string> UploadTextAsync(string containerName, string folderPath, string fileName, string content) => throw new NotImplementedException();
        public Task<string?> DownloadTextAsync(string containerName, string blobPath) => throw new NotImplementedException();
        public Task<IReadOnlyList<string>> ListBlobPathsAsync(string containerName, string folderPath, int maxDepth = 10) => throw new NotImplementedException();
        public Task<BinaryData?> GetBlobAsBinaryDataAsync(string containerName, string blobPath, bool resize = true, int maxSizeBytes = 4194304) => throw new NotImplementedException();
        public Task<Dictionary<int, string>> ListDirectoriesInFolderAsync(string containerName, string folderPath, int maxDepth = 10) => throw new NotImplementedException();
        public Task<Dictionary<string, BinaryData>> ListBlobsInFolderWithResizeAsync(string containerName, string folderPath, int maxDepth = 10, bool resize = true) => throw new NotImplementedException();
    }

    private sealed class NullContentSafetyHelper : IContentSafetyHelper
    {
        public Task<AnalyzeImageResult?> AnalyzeImageAsync(BinaryData inputImage) => Task.FromResult<AnalyzeImageResult?>(null);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _before = new(StringComparer.OrdinalIgnoreCase);
        private readonly string[] _keys = ["INVOKE_OPEN_AI", "ANALYZE_FRAME_AZURE_FUNCTION_URL"];

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
}
