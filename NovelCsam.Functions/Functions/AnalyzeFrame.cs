namespace NovelCsam.Functions.Functions
{
	public class AnalyzeFrame
	{
		private readonly IStorageHelper _sth;
		private readonly IAzureSQLHelper _ash;
		private readonly IVideoHelper _videoHelper;
		private readonly AsyncRetryPolicy _retryPolicy;
		private readonly bool _invokeOpenAi;
		private readonly bool _enableSqlPersistence;
		private readonly bool _enableJsonExport;
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

		public AnalyzeFrame(IStorageHelper sth, IAzureSQLHelper ash, IVideoHelper videoHelper)
		{
			_sth = sth;
			_ash = ash;
			_videoHelper = videoHelper;
			_invokeOpenAi = string.Equals(Environment.GetEnvironmentVariable("INVOKE_OPEN_AI"), "true", StringComparison.OrdinalIgnoreCase);
			_enableSqlPersistence = !string.Equals(Environment.GetEnvironmentVariable("ENABLE_SQL_PERSISTENCE"), "false", StringComparison.OrdinalIgnoreCase);
			_enableJsonExport = string.Equals(Environment.GetEnvironmentVariable("ENABLE_JSON_EXPORT"), "true", StringComparison.OrdinalIgnoreCase);
			_jsonExportContainerName = Environment.GetEnvironmentVariable("JSON_EXPORT_CONTAINER_NAME") ?? string.Empty;
			_jsonExportFolderPath = Environment.GetEnvironmentVariable("JSON_EXPORT_FOLDER_PATH") ?? "json-results";
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
		public async Task<bool> RunAnalyzeFrameAsync([ActivityTrigger] AnalyzeFrameOrchestrationModel item, FunctionContext executionContext)
		{
			try
			{
				var frameBinaryData = await _sth.GetBlobAsBinaryDataAsync(item.ContainerName, item.BlobPath, resize: true);
				if (frameBinaryData == null)
				{
					LogHelper.LogInformation($"Skipping frame because blob could not be loaded: {item.BlobPath}", nameof(AnalyzeFrame), nameof(RunAnalyzeFrameAsync));
					return false;
				}

				var air = await _retryPolicy.ExecuteAsync(() => _videoHelper.GetContentSafteyDetailsAsync(frameBinaryData));
				
				var summary = "";
				var childYesNo = "";
				
				if (_invokeOpenAi)
				{
					summary = item.GetSummary ? await _retryPolicy.ExecuteAsync(() => _videoHelper.SummarizeImageAsync(frameBinaryData, _detailedPrompt)) : string.Empty;
					childYesNo = item.GetChildYesNo ? await _retryPolicy.ExecuteAsync(() => _videoHelper.SummarizeImageAsync(frameBinaryData, _childDetectionPrompt)) : string.Empty;

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

				if (air != null)
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

				if (_enableSqlPersistence)
				{
					var created = await _ash.CreateFrameResult(newItem);
					if (created == null)
					{
						return false;
					}
				}

				if (_enableJsonExport)
				{
					var exportContainer = string.IsNullOrWhiteSpace(_jsonExportContainerName) ? item.ContainerName : _jsonExportContainerName;
					var exportFolder = string.IsNullOrWhiteSpace(_jsonExportFolderPath) ? "json-results" : _jsonExportFolderPath.Trim('/');
					var exportBlobName = $"{item.RunId}/{Path.GetFileNameWithoutExtension(item.BlobPath)}.json";
					var exportPath = $"{exportFolder}/{exportBlobName}";
					var jsonDocument = JsonConvert.SerializeObject(new
					{
						JobId = item.RunId,
						Frame = item.BlobPath,
						FrameResult = newItem with { ImageBase64 = null },
						ExportedAtUtc = DateTime.UtcNow
					}, Formatting.Indented);
					await _sth.UploadTextAsync(exportContainer, exportFolder, exportBlobName, jsonDocument);
				}

				return true;
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred when processing an image: {ex.Message}", nameof(AnalyzeFrame), nameof(RunAnalyzeFrameAsync), ex);
				return false;
			}
		}
	}
}