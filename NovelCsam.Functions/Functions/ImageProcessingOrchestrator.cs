using Newtonsoft.Json;
using NovelCsam.Models.Orchestration;

namespace NovelCsam.Functions.Functions
{
	public class ImageProcessingOrchestrator
	{
		private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".jpg",
			".jpeg",
			".png",
			".bmp",
			".gif",
			".webp",
			".tiff",
			".tif"
		};

		private static readonly HashSet<string> SupportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".mp4",
			".avi",
			".mov",
			".wmv",
			".flv",
			".mkv",
			".webm",
			".mpeg",
			".mpg"
		};

		private readonly IStorageHelper _storageHelper;
		private readonly int _durableBatchSize;
		private readonly int _durableFrameIntervalSeconds;
		private readonly string _jsonExportFolderPath;
		private readonly string _jsonExportContainerName;
		private readonly string _processedSourceDirectory;
		private const int DEFAULT_DURABLE_BATCH_SIZE = 25;
		private const int DEFAULT_DURABLE_FRAME_INTERVAL_SECONDS = 1;
		private const string DEFAULT_EXTRACTED_FOLDER_PATH = "extracted";
		private const string DEFAULT_JSON_EXPORT_FOLDER_PATH = "results";
		private const string DEFAULT_PROCESSED_SOURCE_DIRECTORY = "processed";

		public ImageProcessingOrchestrator(IStorageHelper storageHelper)
		{
			_storageHelper = storageHelper;
			_durableBatchSize = int.TryParse(Environment.GetEnvironmentVariable("DURABLE_BATCH_SIZE"), out var parsedBatchSize)
				? Math.Max(1, parsedBatchSize)
				: DEFAULT_DURABLE_BATCH_SIZE;
			_durableFrameIntervalSeconds = int.TryParse(Environment.GetEnvironmentVariable("DURABLE_FRAME_INTERVAL_SECONDS"), out var parsedFrameInterval)
				? Math.Max(1, parsedFrameInterval)
				: DEFAULT_DURABLE_FRAME_INTERVAL_SECONDS;
			_jsonExportFolderPath = Environment.GetEnvironmentVariable("JSON_EXPORT_FOLDER_PATH") ?? DEFAULT_JSON_EXPORT_FOLDER_PATH;
			_jsonExportContainerName = Environment.GetEnvironmentVariable("JSON_EXPORT_CONTAINER_NAME") ?? string.Empty;
			_processedSourceDirectory = Environment.GetEnvironmentVariable("PROCESSED_SOURCE_DIRECTORY") ?? DEFAULT_PROCESSED_SOURCE_DIRECTORY;
		}

		private static bool IsSupportedImagePath(string path)
		{
			return SupportedImageExtensions.Contains(Path.GetExtension(path ?? string.Empty));
		}

		private static bool IsSupportedVideoPath(string path)
		{
			return SupportedVideoExtensions.Contains(Path.GetExtension(path ?? string.Empty));
		}

		private static string BuildArchiveDestinationPath(string sourceBlobPath, string sourceRootDirectory, string processedRootDirectory, string runId)
		{
			var normalizedSource = (sourceBlobPath ?? string.Empty).Replace('\\', '/').Trim('/');
			var normalizedSourceRoot = (sourceRootDirectory ?? string.Empty).Replace('\\', '/').Trim('/');
			var normalizedProcessedRoot = (processedRootDirectory ?? DEFAULT_PROCESSED_SOURCE_DIRECTORY).Replace('\\', '/').Trim('/');

			var relativePath = normalizedSource;
			if (!string.IsNullOrWhiteSpace(normalizedSourceRoot) &&
				normalizedSource.StartsWith(normalizedSourceRoot + "/", StringComparison.OrdinalIgnoreCase))
			{
				relativePath = normalizedSource[(normalizedSourceRoot.Length + 1)..];
			}

			return $"{normalizedProcessedRoot}/{runId}/{relativePath}".Trim('/');
		}

		[Function(nameof(ImageProcessingOrchestrator))]
		public async Task<string> RunOrchestrator(
			[OrchestrationTrigger] TaskOrchestrationContext context)
		{
			LogHelper.LogEvent("orchestration.start", nameof(ImageProcessingOrchestrator), nameof(RunOrchestrator));
			var fom = JsonConvert.DeserializeObject<FrameOrchestrationModel>(context.GetInput<string>() ?? "");
			if (fom == null)
			{
				throw new InvalidOperationException("Orchestration payload is null or invalid.");
			}

			var runId = string.IsNullOrWhiteSpace(fom.RunId) ? context.NewGuid().ToString() : fom.RunId;

			var totalFrames = 0;
			var processedFrames = 0;
			var failedFrames = 0;
			var skippedFrames = 0;
			var frameResultBlobs = new List<string>();
			var skippedItems = new List<string>();
			var startedAtUtc = context.CurrentUtcDateTime;

			var sourceBlobPaths = await context.CallActivityAsync<List<string>>("ListBlobs", new ListBlobModel
			{
				ContainerName = fom.ContainerName,
				ContainerDirectory = fom.ContainerDirectory
			});

			sourceBlobPaths ??= [];
			var processableSourcePaths = sourceBlobPaths
				.Where(path => IsSupportedImagePath(path) || IsSupportedVideoPath(path))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
			var framePaths = sourceBlobPaths.Where(IsSupportedImagePath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			var videoPaths = sourceBlobPaths.Where(IsSupportedVideoPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

			if (videoPaths.Count > 0)
			{
				var extractedRootBase = string.IsNullOrWhiteSpace(fom.ExtractedFramesDirectory)
					? DEFAULT_EXTRACTED_FOLDER_PATH
					: fom.ExtractedFramesDirectory.Trim('/');
				var extractedRootPath = $"{extractedRootBase}/{runId}";
				var frameIntervalSeconds = fom.FrameIntervalSeconds > 0
					? Math.Max(1, fom.FrameIntervalSeconds)
					: _durableFrameIntervalSeconds;

				foreach (var videoPath in videoPaths)
				{
					var extracted = await context.CallActivityAsync<bool>("ExtractFramesFromVideo", new ExtractFramesOrchestrationModel
					{
						ContainerName = fom.ContainerName,
						SourceBlobPath = videoPath,
						TargetFolderPath = extractedRootPath,
						FrameIntervalSeconds = frameIntervalSeconds
					});

					if (!extracted)
					{
						failedFrames++;
					}
				}

				var extractedBlobPaths = await context.CallActivityAsync<List<string>>("ListBlobs", new ListBlobModel
				{
					ContainerName = fom.ContainerName,
					ContainerDirectory = extractedRootPath
				});

				if (extractedBlobPaths != null)
				{
					framePaths.AddRange(extractedBlobPaths.Where(IsSupportedImagePath));
					framePaths = framePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
				}
			}

			framePaths ??= [];
			totalFrames = framePaths.Count;
			if (totalFrames == 0)
			{
				LogHelper.LogEvent("orchestration.no_frames", nameof(ImageProcessingOrchestrator), nameof(RunOrchestrator), $"container={fom.ContainerName}; directory={fom.ContainerDirectory}");
			}
			else
			{
				context.SetCustomStatus(new JobProgressStatus
				{
					JobId = runId,
					RuntimeStatus = "Running",
					TotalFrames = totalFrames,
					ProcessedFrames = 0,
					FailedFrames = 0,
					CurrentBatchSize = 0,
					UpdatedAtUtc = context.CurrentUtcDateTime
				});
			}

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
				var skippedInBatch = batchResults
					.Where(result => !string.IsNullOrWhiteSpace(result) && result!.StartsWith(AnalyzeFrame.SkippedResultPrefix, StringComparison.Ordinal))
					.Select(result => result![AnalyzeFrame.SkippedResultPrefix.Length..])
					.ToList();

				processedFrames += batchResults.Length;
				skippedFrames += skippedInBatch.Count;
				skippedItems.AddRange(skippedInBatch);
				failedFrames += batchResults.Count(result => string.IsNullOrWhiteSpace(result));
				frameResultBlobs.AddRange(batchResults
					.Where(result => !string.IsNullOrWhiteSpace(result) && !result!.StartsWith(AnalyzeFrame.SkippedResultPrefix, StringComparison.Ordinal))
					.Select(result => result!));
				context.SetCustomStatus(new JobProgressStatus
				{
					JobId = runId,
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
			var status = failedFrames > 0
				? "CompletedWithErrors"
				: skippedFrames > 0
					? "CompletedWithWarnings"
					: "Completed";

			var manifest = new JobResultManifest
			{
				JobId = runId,
				ContainerName = fom.ContainerName,
				ContainerDirectory = fom.ContainerDirectory,
				Status = status,
				TotalFrames = totalFrames,
				ProcessedFrames = processedFrames,
				SuccessfulFrames = Math.Max(0, processedFrames - failedFrames - skippedFrames),
				FailedFrames = failedFrames,
				SkippedFrames = skippedFrames,
				SkippedItems = skippedItems,
				StartedAtUtc = startedAtUtc,
				CompletedAtUtc = context.CurrentUtcDateTime,
				ExportContainerName = exportContainerName,
				ExportFolderPath = exportFolderPath,
				FrameResultBlobs = frameResultBlobs
			};

			await context.CallActivityAsync<bool>("WriteJobManifest", manifest);

			if (failedFrames == 0 && processableSourcePaths.Count > 0)
			{
				foreach (var sourcePath in processableSourcePaths)
				{
					var destinationPath = BuildArchiveDestinationPath(sourcePath, fom.ContainerDirectory, _processedSourceDirectory, runId);
					await context.CallActivityAsync<bool>("ArchiveProcessedSourceBlob", new ArchiveBlobModel
					{
						ContainerName = fom.ContainerName,
						SourceBlobPath = sourcePath,
						DestinationBlobPath = destinationPath
					});
				}
			}

			context.SetCustomStatus(new JobProgressStatus
			{
				JobId = runId,
				RuntimeStatus = status,
				TotalFrames = totalFrames,
				ProcessedFrames = processedFrames,
				FailedFrames = failedFrames,
				CurrentBatchSize = 0,
				UpdatedAtUtc = context.CurrentUtcDateTime
			});

			LogHelper.LogEvent(
				"orchestration.completed",
				nameof(ImageProcessingOrchestrator),
				nameof(RunOrchestrator),
				$"runId={runId}; status={status}; processed={processedFrames}; failed={failedFrames}; skipped={skippedFrames}"
			);

			return runId;
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
