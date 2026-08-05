namespace NovelCsam.Functions.Functions
{
	public class AnalyzeFrame
	{
		public const string SkippedResultPrefix = "SKIPPED:";
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

		private readonly IStorageHelper _sth;
		private readonly IVideoHelper _videoHelper;
		private readonly AsyncRetryPolicy _retryPolicy;
		private readonly bool _invokeOpenAi;
		private readonly string _jsonExportContainerName;
		private readonly string _jsonExportFolderPath;
		private readonly string _detailedPrompt;
		private readonly string _childDetectionPrompt;
		private const string HATE = "hate";
		private const string SELF_HARM = "selfharm";
		private const string VIOLENCE = "violence";
		private const string SEXUAL = "sexual";
		private const string DEFAULT_DETAILED_PROMPT = "Can you do a detail analysis and tell me all the minute details about this image. Use no more than 450 words!!!";
		private const string DEFAULT_CHILD_PROMPT = "Is there a younger person or child in this image? If you can't make a determination ANSWER No, ONLY ANSWER Yes or No!!";

		public AnalyzeFrame(IStorageHelper sth, IVideoHelper videoHelper)
		{
			_sth = sth;
			_videoHelper = videoHelper;
			_invokeOpenAi = string.Equals(Environment.GetEnvironmentVariable("INVOKE_OPEN_AI"), "true", StringComparison.OrdinalIgnoreCase);
			_jsonExportContainerName = Environment.GetEnvironmentVariable("JSON_EXPORT_CONTAINER_NAME") ?? string.Empty;
			_jsonExportFolderPath = Environment.GetEnvironmentVariable("JSON_EXPORT_FOLDER_PATH") ?? "results";
			_detailedPrompt = Environment.GetEnvironmentVariable("DETAILED_ANALYSIS_PROMPT") ?? DEFAULT_DETAILED_PROMPT;
			_childDetectionPrompt = Environment.GetEnvironmentVariable("CHILD_DETECTION_PROMPT") ?? DEFAULT_CHILD_PROMPT;

			var retryAttempts = int.TryParse(Environment.GetEnvironmentVariable("RETRY_MAX_ATTEMPTS"), out var parsedAttempts)
				? parsedAttempts
				: 3;
			var retryBackoff = double.TryParse(Environment.GetEnvironmentVariable("RETRY_BACKOFF_MULTIPLIER"), out var parsedBackoff)
				? parsedBackoff
				: 2.0;

			_retryPolicy = Policy
				.Handle<HttpRequestException>(ex => ex.StatusCode == (HttpStatusCode)429)
				.WaitAndRetryAsync(retryAttempts, retryAttempt => TimeSpan.FromSeconds(Math.Pow(retryBackoff, retryAttempt)),
					(exception, timeSpan, retryCount, context) =>
					{
						LogHelper.LogInformation($"Retry {retryCount} encountered an error: {exception.Message}. Waiting {timeSpan} before next retry.", nameof(AnalyzeFrame), "Constructor");
					});
		}

		[Function("AnalyzeFrame")]
		public async Task<string?> RunAnalyzeFrameAsync([ActivityTrigger] AnalyzeFrameOrchestrationModel item, FunctionContext executionContext)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(item.BlobPath))
				{
					LogHelper.LogInformation("Skipping frame because blob path is empty.", nameof(AnalyzeFrame), nameof(RunAnalyzeFrameAsync));
					return $"{SkippedResultPrefix}<empty-path>";
				}

				var extension = Path.GetExtension(item.BlobPath);
				if (!SupportedImageExtensions.Contains(extension))
				{
					LogHelper.LogInformation($"Skipping unsupported file type '{extension}' for blob '{item.BlobPath}'.", nameof(AnalyzeFrame), nameof(RunAnalyzeFrameAsync));
					return $"{SkippedResultPrefix}{item.BlobPath}";
				}

				var frameBinaryData = await _sth.GetBlobAsBinaryDataAsync(item.ContainerName, item.BlobPath, resize: true);
				if (frameBinaryData == null)
				{
					LogHelper.LogInformation($"Skipping frame because blob could not be loaded: {item.BlobPath}", nameof(AnalyzeFrame), nameof(RunAnalyzeFrameAsync));
					return $"{SkippedResultPrefix}{item.BlobPath}";
				}

				LogHelper.LogEvent("service.content_safety.call.start", nameof(AnalyzeFrame), nameof(RunAnalyzeFrameAsync), $"blob={item.BlobPath}");
				var air = await _retryPolicy.ExecuteAsync(() => _videoHelper.GetContentSafteyDetailsAsync(frameBinaryData));
				LogHelper.LogEvent("service.content_safety.call.success", nameof(AnalyzeFrame), nameof(RunAnalyzeFrameAsync), $"blob={item.BlobPath}");
				
				var summary = "";
				var childYesNo = "";
				
				if (_invokeOpenAi)
				{
					if (item.GetSummary)
					{
						LogHelper.LogEvent("service.openai.summary.call.start", nameof(AnalyzeFrame), nameof(RunAnalyzeFrameAsync), $"blob={item.BlobPath}");
						summary = await _retryPolicy.ExecuteAsync(() => _videoHelper.SummarizeImageAsync(frameBinaryData, _detailedPrompt));
						LogHelper.LogEvent("service.openai.summary.call.success", nameof(AnalyzeFrame), nameof(RunAnalyzeFrameAsync), $"blob={item.BlobPath}");
					}

					if (item.GetChildYesNo)
					{
						LogHelper.LogEvent("service.openai.child.call.start", nameof(AnalyzeFrame), nameof(RunAnalyzeFrameAsync), $"blob={item.BlobPath}");
						childYesNo = await _retryPolicy.ExecuteAsync(() => _videoHelper.SummarizeImageAsync(frameBinaryData, _childDetectionPrompt));
						LogHelper.LogEvent("service.openai.child.call.success", nameof(AnalyzeFrame), nameof(RunAnalyzeFrameAsync), $"blob={item.BlobPath}");
					}

				}
				var md5Hash = _videoHelper.CreateMD5Hash(frameBinaryData);

				var newItem = new FrameResult
				{
					MD5Hash = md5Hash,
					Summary = summary,
					RunId = item.RunId,
					Id = Guid.NewGuid().ToString(),
					Frame = item.BlobPath,
					ChildYesNo = childYesNo,
					ImageBase64 = item.ImageBase64ToDB ? _videoHelper.ConvertToBase64(frameBinaryData) : "",
					RunDateTime = item.RunDateTime
				};

				if (air?.CategoriesAnalysis != null)
				{
					foreach (var citem in air.CategoriesAnalysis)
					{
						switch (citem.Category.ToString().ToLowerInvariant())
						{
							case HATE:
								newItem.Hate = citem.Severity ?? 0;
								break;
							case SELF_HARM:
								newItem.SelfHarm = citem.Severity ?? 0;
								break;
							case VIOLENCE:
								newItem.Violence = citem.Severity ?? 0;
								break;
							case SEXUAL:
								newItem.Sexual = citem.Severity ?? 0;
								break;
						}
					}
				}

				var exportContainer = string.IsNullOrWhiteSpace(_jsonExportContainerName) ? item.ContainerName : _jsonExportContainerName;
				var exportFolder = string.IsNullOrWhiteSpace(_jsonExportFolderPath) ? "results" : _jsonExportFolderPath.Trim('/');
				var exportBlobName = $"{item.RunId}/{Path.GetFileNameWithoutExtension(item.BlobPath)}.json";
				var jsonDocument = JsonConvert.SerializeObject(new
				{
					JobId = item.RunId,
					Frame = item.BlobPath,
					FrameResult = newItem,
					ExportedAtUtc = DateTime.UtcNow
				}, Formatting.Indented);
				var exportPath = await _sth.UploadTextAsync(exportContainer, exportFolder, exportBlobName, jsonDocument);

				return exportPath;
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred when processing an image: {ex.Message}", nameof(AnalyzeFrame), nameof(RunAnalyzeFrameAsync), ex);
				return null;
			}
		}
	}
}