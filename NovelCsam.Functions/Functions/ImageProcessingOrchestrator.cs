using Newtonsoft.Json;
using NovelCsam.Models.Orchestration;

namespace NovelCsam.Functions.Functions
{
	public class ImageProcessingOrchestrator
	{
		private readonly IStorageHelper _storageHelper;
		private readonly int _durableBatchSize;
		private readonly string _jsonExportFolderPath;
		private readonly string _jsonExportContainerName;
		private const int DEFAULT_DURABLE_BATCH_SIZE = 25;
		private const string DEFAULT_JSON_EXPORT_FOLDER_PATH = "results";

		public ImageProcessingOrchestrator(IStorageHelper storageHelper)
		{
			_storageHelper = storageHelper;
			_durableBatchSize = int.TryParse(Environment.GetEnvironmentVariable("DURABLE_BATCH_SIZE"), out var parsedBatchSize)
				? Math.Max(1, parsedBatchSize)
				: DEFAULT_DURABLE_BATCH_SIZE;
			_jsonExportFolderPath = Environment.GetEnvironmentVariable("JSON_EXPORT_FOLDER_PATH") ?? DEFAULT_JSON_EXPORT_FOLDER_PATH;
			_jsonExportContainerName = Environment.GetEnvironmentVariable("JSON_EXPORT_CONTAINER_NAME") ?? string.Empty;
		}

		[Function(nameof(ImageProcessingOrchestrator))]
		public async Task<string> RunOrchestrator(
			[OrchestrationTrigger] TaskOrchestrationContext context)
		{
			LogHelper.LogInformation("ImageProcessingOrchestrator Started", nameof(ImageProcessingOrchestrator), nameof(RunOrchestrator));
			var fom = JsonConvert.DeserializeObject<FrameOrchestrationModel>(context.GetInput<string>() ?? "");
			if (fom == null)
			{
				throw new InvalidOperationException("Orchestration payload is null or invalid.");
			}

			var totalFrames = 0;
			var processedFrames = 0;
			var failedFrames = 0;
			var frameResultBlobs = new List<string>();
			var startedAtUtc = context.CurrentUtcDateTime;

			var framePaths = await context.CallActivityAsync<List<string>>("ListBlobs", new ListBlobModel
			{
				ContainerName = fom.ContainerName,
				ContainerDirectory = fom.ContainerDirectory
			});

			if (framePaths == null || framePaths.Count == 0)
			{
				LogHelper.LogInformation("No frames found for orchestration request.", nameof(ImageProcessingOrchestrator), nameof(RunOrchestrator));
				return fom.RunId;
			}

			totalFrames = framePaths.Count;
			context.SetCustomStatus(new JobProgressStatus
			{
				JobId = fom.RunId,
				RuntimeStatus = "Running",
				TotalFrames = totalFrames,
				ProcessedFrames = 0,
				FailedFrames = 0,
				CurrentBatchSize = 0,
				UpdatedAtUtc = context.CurrentUtcDateTime
			});

			var batchSize = _durableBatchSize;

			for (var i = 0; i < framePaths.Count; i += batchSize)
			{
				var tasks = new List<Task<string?>>();
				var limit = Math.Min(i + batchSize, framePaths.Count);

				for (var index = i; index < limit; index++)
				{
					var analyzePayload = new AnalyzeFrameOrchestrationModel
					{
						BlobPath = framePaths[index],
						GetSummary = fom.GetSummary,
						GetChildYesNo = fom.GetChildYesNo,
						ContainerDirectory = fom.ContainerDirectory,
						ContainerName = fom.ContainerName,
						ImageBase64ToDB = fom.ImageBase64ToDB,
						RunId = fom.RunId,
						RunDateTime = context.CurrentUtcDateTime
					};

					tasks.Add(context.CallActivityAsync<string?>("AnalyzeFrame", analyzePayload));
				}

				var batchResults = await Task.WhenAll(tasks);
				processedFrames += batchResults.Length;
				failedFrames += batchResults.Count(result => string.IsNullOrWhiteSpace(result));
				frameResultBlobs.AddRange(batchResults.Where(result => !string.IsNullOrWhiteSpace(result)).Select(result => result!));
				context.SetCustomStatus(new JobProgressStatus
				{
					JobId = fom.RunId,
					RuntimeStatus = "Running",
					TotalFrames = totalFrames,
					ProcessedFrames = processedFrames,
					FailedFrames = failedFrames,
					CurrentBatchSize = batchResults.Length,
					CurrentFrame = limit > 0 ? framePaths[limit - 1] : null,
					UpdatedAtUtc = context.CurrentUtcDateTime
				});
			}

			var exportFolderPath = _jsonExportFolderPath;
			var exportContainerName = string.IsNullOrWhiteSpace(_jsonExportContainerName) ? fom.ContainerName : _jsonExportContainerName;
			var manifest = new JobResultManifest
			{
				JobId = fom.RunId,
				ContainerName = fom.ContainerName,
				ContainerDirectory = fom.ContainerDirectory,
				Status = failedFrames > 0 ? "CompletedWithErrors" : "Completed",
				TotalFrames = totalFrames,
				ProcessedFrames = processedFrames,
				SuccessfulFrames = processedFrames - failedFrames,
				FailedFrames = failedFrames,
				StartedAtUtc = startedAtUtc,
				CompletedAtUtc = context.CurrentUtcDateTime,
				ExportContainerName = exportContainerName,
				ExportFolderPath = exportFolderPath,
				FrameResultBlobs = frameResultBlobs
			};

			await context.CallActivityAsync<bool>("WriteJobManifest", manifest);
			context.SetCustomStatus(new JobProgressStatus
			{
				JobId = fom.RunId,
				RuntimeStatus = manifest.Status,
				TotalFrames = totalFrames,
				ProcessedFrames = processedFrames,
				FailedFrames = failedFrames,
				CurrentBatchSize = 0,
				UpdatedAtUtc = context.CurrentUtcDateTime
			});

			return fom.RunId;
		}

		[Function("AnalyzeFrames_HttpStart")]
		public static async Task<HttpResponseData> HttpStart(
			[HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
			[DurableClient] DurableTaskClient client,
			FunctionContext executionContext)
		{
			ILogger logger = executionContext.GetLogger("Function1_HttpStart");
			string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

			// Function input comes from the request content.
			string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
				nameof(ImageProcessingOrchestrator), requestBody);

			logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

			// Returns an HTTP 202 response with an instance management payload.
			// See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
			return await client.CreateCheckStatusResponseAsync(req, instanceId);
		}

		[Function("WriteJobManifest")]
		public async Task<bool> WriteJobManifestAsync([ActivityTrigger] JobResultManifest item)
		{
			try
			{
				var manifestJson = JsonConvert.SerializeObject(item, Formatting.Indented);
				var exportFolder = string.IsNullOrWhiteSpace(item.ExportFolderPath) ? DEFAULT_JSON_EXPORT_FOLDER_PATH : item.ExportFolderPath.Trim('/');
				var exportContainer = string.IsNullOrWhiteSpace(item.ExportContainerName) ? item.ContainerName : item.ExportContainerName;
				await _storageHelper.UploadTextAsync(exportContainer, exportFolder, $"{item.JobId}/job-result.json", manifestJson);
				return true;
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred when writing the job manifest: {ex.Message}", nameof(ImageProcessingOrchestrator), nameof(WriteJobManifestAsync), ex);
				return false;
			}
		}
	}
}
