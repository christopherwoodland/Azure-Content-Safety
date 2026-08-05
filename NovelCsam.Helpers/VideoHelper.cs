#pragma warning disable OPENAI001

namespace NovelCsam.Helpers
{
	public class VideoHelper : IVideoHelper
	{
		private readonly ProjectResponsesClient? _projectResponsesClient;
		private readonly ResponsesClient? _responsesClient;
		private readonly string _openAiTargetName = string.Empty;
		private readonly IStorageHelper _sth;
		private readonly IContentSafetyHelper _csh;

		private readonly HttpClient _httpClient;
		private enum FFMPEG_MODE { VSEG = 0, FSEG = 1 }
		private const int MAX_CANCELLATION_TIMEOUT_SECONDS = 2_147_483;
		private const string HATE = "hate";
		private const string SELF_HARM = "selfharm";
		private const string VIOLENCE = "violence";
		private const string SEXUAL = "sexual";
		private string _requestUri = "";
		private readonly bool _invokeOpenAi;
		private readonly int _openAiTimeoutSeconds;

		private static TokenCredential CreatePreferredAzureCredential()
		{
			var isDevelopment = string.Equals(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
			return isDevelopment ? new AzureCliCredential() : new DefaultAzureCredential();
		}

		public VideoHelper(IStorageHelper sth, IContentSafetyHelper csh, IHttpClientFactory httpClientFactory)
		{
			_requestUri = Environment.GetEnvironmentVariable("ANALYZE_FRAME_AZURE_FUNCTION_URL") ?? "";
			_sth = sth;
			_csh = csh;
			_httpClient = httpClientFactory.CreateClient(nameof(VideoHelper));
			_invokeOpenAi = string.Equals(Environment.GetEnvironmentVariable("INVOKE_OPEN_AI"), "true", StringComparison.OrdinalIgnoreCase);
			_openAiTimeoutSeconds = int.TryParse(Environment.GetEnvironmentVariable("OPEN_AI_TIMEOUT_SECONDS"), out var parsedTimeout)
				? Math.Clamp(parsedTimeout, 30, MAX_CANCELLATION_TIMEOUT_SECONDS)
				: 600;

			if (_invokeOpenAi)
			{
				var openAiEndpoint = Environment.GetEnvironmentVariable("OPEN_AI_ENDPOINT");
				if (string.IsNullOrWhiteSpace(openAiEndpoint))
				{
					openAiEndpoint = Environment.GetEnvironmentVariable("OPEN_AI_PROJECT_ENDPOINT");
				}
				openAiEndpoint ??= string.Empty;
				var openAiModel = Environment.GetEnvironmentVariable("OPEN_AI_MODEL") ?? string.Empty;
				var openAiDeploymentName = Environment.GetEnvironmentVariable("OPEN_AI_DEPLOYMENT_NAME") ?? string.Empty;
				var openAiTargetName = string.IsNullOrWhiteSpace(openAiDeploymentName) ? openAiModel : openAiDeploymentName;
				_openAiTargetName = openAiTargetName;
				var openAiUseManagedIdentity = !string.Equals(Environment.GetEnvironmentVariable("OPEN_AI_USE_MANAGED_IDENTITY"), "false", StringComparison.OrdinalIgnoreCase);

				if (string.IsNullOrWhiteSpace(openAiEndpoint) || string.IsNullOrWhiteSpace(openAiTargetName))
				{
					throw new InvalidOperationException("OpenAI configuration is invalid. Set OPEN_AI_ENDPOINT or OPEN_AI_PROJECT_ENDPOINT, and set OPEN_AI_MODEL (or OPEN_AI_DEPLOYMENT_NAME).");
				}

				// Foundry project endpoints use /api/projects/{project}; direct model endpoints use /openai/v1.
				if (openAiEndpoint.Contains("/api/projects/", StringComparison.OrdinalIgnoreCase))
				{
					if (!openAiUseManagedIdentity)
					{
						throw new InvalidOperationException("OPEN_AI_USE_MANAGED_IDENTITY must be true for Foundry project endpoints.");
					}

					var projectClient = new AIProjectClient(
						endpoint: new Uri(openAiEndpoint),
						tokenProvider: CreatePreferredAzureCredential());

					_projectResponsesClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(openAiTargetName);
				}
				else
				{
					var normalizedEndpoint = NormalizeResponsesEndpoint(openAiEndpoint);
					OpenAIClient openAiClient;

					if (openAiUseManagedIdentity)
					{
						var tokenPolicy = new BearerTokenPolicy(CreatePreferredAzureCredential(), "https://ai.azure.com/.default");
						openAiClient = new OpenAIClient(
							authenticationPolicy: tokenPolicy,
							options: new OpenAIClientOptions { Endpoint = new Uri(normalizedEndpoint) });
					}
					else
					{
						var openAiKey = Environment.GetEnvironmentVariable("OPEN_AI_KEY") ?? string.Empty;
						if (string.IsNullOrWhiteSpace(openAiKey))
						{
							throw new InvalidOperationException("OPEN_AI_KEY must be set when OPEN_AI_USE_MANAGED_IDENTITY is false.");
						}

						openAiClient = new OpenAIClient(
							credential: new ApiKeyCredential(openAiKey),
							options: new OpenAIClientOptions { Endpoint = new Uri(normalizedEndpoint) });
					}

					_responsesClient = openAiClient.GetResponsesClient();
				}
			}
		}

		public async Task<string> UploadFileToBlobAsync(string containerName, string containerFolderPath, string sourceFileNameOrPath, string containerFolderPostfix = "", bool isImages = false, string timestampIn = "", string customName = "")
		{
			string timestamp = string.IsNullOrEmpty(timestampIn) ? DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") : timestampIn;
			string path = isImages ? $"{containerFolderPath}/{customName ?? "images"}/{timestamp}/{containerFolderPostfix}" : $"{containerFolderPath}/{Path.GetFileName(sourceFileNameOrPath)}/{timestamp}/{containerFolderPostfix}";
			return await _sth.UploadFileAsync(containerName, path, sourceFileNameOrPath);
		}
		public async Task<string> UploadFrameResultsAsync(string containerName,
			string containerFolderPath, string containerFolderPathResults, bool withBase64ofImage = false,
			bool getSummaryB = true, bool getChildYesNoB = true)
		{
			Console.WriteLine($"********************************************************************************");
			Console.WriteLine($"Pulling list of records....");
			Console.WriteLine($"********************************************************************************\n");

			Console.WriteLine($"\n----------------------------------------------------------------------");
			Console.WriteLine($"Pulling list of records. This could take a moment.....");
			Console.WriteLine($"----------------------------------------------------------------------\n");
			var list = await _sth.ListBlobsInFolderWithResizeAsync(containerName, containerFolderPath, 3);

			//This could be done better/different
			int totalCnt = list.Count;
			Console.WriteLine($"\n********************************************************************************");
			Console.WriteLine($"Total number of records to process: {totalCnt}");
			Console.WriteLine($"********************************************************************************\n");
			var runId = Guid.NewGuid().ToString();
			var runDateTime = DateTime.UtcNow;
			var exportFolder = string.IsNullOrWhiteSpace(containerFolderPathResults) ? "results" : containerFolderPathResults.Trim('/');

			var tasks = list.Select(async item =>
			{
				var air = await GetContentSafteyDetailsAsync(item.Value);
				var summary = "";
				var childYesNo = "";
				if (_invokeOpenAi)
				{
					summary = getSummaryB ? await SummarizeImageAsync(item.Value, "Can you do a detail analysis and tell me all the minute details about this image. Use no more than 450 words!!!") : string.Empty;
					childYesNo = getChildYesNoB ? await SummarizeImageAsync(item.Value, "Is there a younger person, adolescent, or child in this image? If you can't make a determination ANSWER No, ONLY ANSWER Yes or No!!") : string.Empty;
				}
				var md5Hash = CreateMD5Hash(item.Value);

				var newItem = new FrameResult
				{
					MD5Hash = md5Hash,
					Summary = summary,
					RunId = runId,
					Id = Guid.NewGuid().ToString(),
					Frame = item.Key,
					ChildYesNo = childYesNo,
					ImageBase64 = withBase64ofImage ? ConvertToBase64(item.Value) : "",
					RunDateTime = runDateTime
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

				var exportBlobName = $"{runId}/{Path.GetFileNameWithoutExtension(item.Key)}.json";
				var jsonDocument = JsonConvert.SerializeObject(new
				{
					JobId = runId,
					Frame = item.Key,
					FrameResult = newItem,
					ExportedAtUtc = DateTime.UtcNow
				}, Formatting.Indented);
				var exportPath = await _sth.UploadTextAsync(containerName, exportFolder, exportBlobName, jsonDocument);
				//This could be done better/different
				Console.WriteLine($"\n********************************************************************************");
				Console.WriteLine($"Record processed: {item.Key}");
				Console.WriteLine($"********************************************************************************\n");
				return exportPath;
			});

			var frameResultBlobs = (await Task.WhenAll(tasks))
				.Where(result => !string.IsNullOrWhiteSpace(result))
				.Select(result => result!)
				.ToList();
			var cnt = frameResultBlobs.Count;
			Console.WriteLine($"********************************************************************************");
			Console.WriteLine($"Total number of records processed: {cnt}/{totalCnt}");
			Console.WriteLine($"********************************************************************************\n");

			var message = $"RunId: {runId}: Total number of records processed: {cnt}/{totalCnt} ";
			LogHelper.LogInformation(message,nameof(VideoHelper), nameof(UploadFrameResultsAsync));

			var manifest = new JobResultManifest
			{
				JobId = runId,
				ContainerName = containerName,
				ContainerDirectory = containerFolderPath,
				Status = cnt == totalCnt ? "Completed" : "CompletedWithErrors",
				TotalFrames = totalCnt,
				ProcessedFrames = cnt,
				SuccessfulFrames = cnt,
				FailedFrames = totalCnt - cnt,
				StartedAtUtc = runDateTime,
				CompletedAtUtc = DateTime.UtcNow,
				ExportContainerName = containerName,
				ExportFolderPath = exportFolder,
				FrameResultBlobs = frameResultBlobs
			};
			var manifestJson = JsonConvert.SerializeObject(manifest, Formatting.Indented);
			await _sth.UploadTextAsync(containerName, exportFolder, $"{runId}/job-result.json", manifestJson);
			return runId;
		}



		private async Task<string> CallFunctionHttpStartAsync(string requestBody)
		{
			try
			{
				var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
				var response = await _httpClient.PostAsync(_requestUri, content);
				response.EnsureSuccessStatusCode();

				var responseBody = await response.Content.ReadAsStringAsync();
				return responseBody;
			}
			catch (HttpRequestException ex)
			{
				// Handle exception
				LogHelper.LogException($"An error occurred during CallFunctionHttpStartAsync: {ex.Message}", nameof(VideoHelper), nameof(CallFunctionHttpStartAsync), ex);
				return "";
			}
		}

		private async Task<string> CallFunctionHttpStatusAsync(string url)
		{
			var requestUri = url;

			try
			{
				var response = await _httpClient.GetAsync(requestUri);
				response.EnsureSuccessStatusCode();

				var responseBody = await response.Content.ReadAsStringAsync();
				return responseBody;
			}
			catch (HttpRequestException ex)
			{
				// Handle exception
				LogHelper.LogException($"An error occurred during CallFunctionHttpStatusAsync: {ex.Message}", nameof(VideoHelper), nameof(CallFunctionHttpStatusAsync), ex);
				return "";
			}
		}

		public async Task<string> UploadFrameResultsDurableFunctionAsync(string containerName,
		string containerFolderPath, string containerFolderPathResults, bool withBase64ofImage = false,
		bool getSummaryB = true, bool getChildYesNoB = true, string runId = "", int frameIntervalSeconds = 1, string extractedFramesDirectory = "")
		{
			if (string.IsNullOrWhiteSpace(_requestUri))
			{
				throw new InvalidOperationException("ANALYZE_FRAME_AZURE_FUNCTION_URL is not configured.");
			}

			var item = new
			{
				ImageBase64ToDB = withBase64ofImage,
				GetSummary = getSummaryB,
				GetChildYesNo = getChildYesNoB,
				ContainerName = containerName,
				ContainerDirectory = containerFolderPath,
				RunId = runId,
				FrameIntervalSeconds = Math.Max(1, frameIntervalSeconds),
				ExtractedFramesDirectory = extractedFramesDirectory
			};
			var ret = await CallFunctionHttpStartAsync(JsonConvert.SerializeObject(item));
			DurableTaskInstance? instance = JsonConvert.DeserializeObject<DurableTaskInstance>(ret);
			if (instance == null || string.IsNullOrWhiteSpace(instance.StatusQueryGetUri))
			{
				throw new InvalidOperationException("Durable orchestration start response is invalid.");
			}

			var status = await CallFunctionHttpStatusAsync(instance.StatusQueryGetUri);
			OrchestrationStatus? statusInstnace = JsonConvert.DeserializeObject<OrchestrationStatus>(status);
			if (statusInstnace == null)
			{
				throw new InvalidOperationException("Unable to parse durable orchestration status.");
			}

			var nonTerminalStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				"Pending",
				"Running",
				"ContinuedAsNew"
			};

			do
			{
				await Task.Delay(3000);

				status = await CallFunctionHttpStatusAsync(instance.StatusQueryGetUri);
				statusInstnace = JsonConvert.DeserializeObject<OrchestrationStatus>(status) ?? statusInstnace;
			}
			while (!string.IsNullOrWhiteSpace(statusInstnace.RuntimeStatus) && nonTerminalStatuses.Contains(statusInstnace.RuntimeStatus));

			if (!string.Equals(statusInstnace.RuntimeStatus, "Completed", StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(statusInstnace.RuntimeStatus, "CompletedWithErrors", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException($"Orchestration ended with status '{statusInstnace.RuntimeStatus}'.");
			}

			return statusInstnace.Input?.ToString() ?? "";
		}

		public string ConvertToBase64(BinaryData imageData)
		{
			return Convert.ToBase64String(imageData.ToArray());
		}

		public string CreateMD5Hash(BinaryData imageData)
		{
			using var md5 = MD5.Create();
			var hashBytes = md5.ComputeHash(imageData.ToArray());
			return string.Concat(hashBytes.Select(b => b.ToString("x2")));
		}

		public async Task<string> SummarizeImageAsync(BinaryData imageBytes, string userPrompt)
		{
			const int maxRetries = 3;
			const int delayMilliseconds = 3000;

			var retryPolicy = Policy
				.Handle<HttpRequestException>(ex => ex.StatusCode == (HttpStatusCode)429)
				.WaitAndRetryAsync(maxRetries, retryAttempt => TimeSpan.FromMilliseconds(delayMilliseconds),
					(exception, timeSpan, retryCount, context) =>
					{
						LogHelper.LogInformation($"Retry {retryCount}/{maxRetries} after receiving 429 Too Many Requests. Waiting {timeSpan.TotalMilliseconds}ms before retrying.",
							nameof(VideoHelper), nameof(SummarizeImageAsync));
					});

			try
			{
				return await retryPolicy.ExecuteAsync(async () =>
				{
					if (_responsesClient == null && _projectResponsesClient == null)
					{
						return string.Empty;
					}

					var options = new CreateResponseOptions
					{
						InputItems =
						{
							ResponseItem.CreateUserMessageItem(
							[
								ResponseContentPart.CreateInputTextPart(userPrompt),
								ResponseContentPart.CreateInputImagePart(new Uri(CreateImageDataUri(imageBytes)))
							])
						}
					};

					if (_projectResponsesClient == null)
					{
						options.Model = _openAiTargetName;
					}

					using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_openAiTimeoutSeconds));
					if (_projectResponsesClient != null)
					{
						var projectResponse = await _projectResponsesClient.CreateResponseAsync(options, timeoutCts.Token);
						return projectResponse.Value.GetOutputText() ?? "NO SUMMARY GENERATED";
					}

					var response = await _responsesClient!.CreateResponseAsync(options, timeoutCts.Token);
					return response.Value.GetOutputText() ?? "NO SUMMARY GENERATED";
				});
			}
			catch (OperationCanceledException ocex)
			{
				LogHelper.LogException($"OpenAI call timed out after {_openAiTimeoutSeconds} seconds: {ocex.Message}", nameof(VideoHelper), nameof(SummarizeImageAsync), ocex);
				return $"Error: OpenAI timeout after {_openAiTimeoutSeconds} seconds.";
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred summarizing an image: {ex.Message}", nameof(VideoHelper), nameof(SummarizeImageAsync), ex);
				return $"Error: {ex.Message}";
			}
		}

		public async Task<AnalyzeImageResult?> GetContentSafteyDetailsAsync(BinaryData bd)
		{
			return await _csh.AnalyzeImageAsync(bd);
		}

		public async Task<bool> UploadExtractedFramesToBlobAsync(int frameInterval, string fileName, string containerName, string containerFolderPath, string containerFolderPathExtracted, string sourceFileNameOrPath)
		{
			string guid = Guid.NewGuid().ToString();
			string tempPath = Path.Combine(Path.GetTempPath(), guid);
			Directory.CreateDirectory(tempPath);

			string localVideoPath = Path.Combine(tempPath, fileName);
			await _sth.DownloadFileAsync(containerName, containerFolderPath, fileName, localVideoPath);

			var extractedFrames = await ExtractFramesAsync(localVideoPath, frameInterval, fileName);
			string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

			foreach (var frame in extractedFrames)
			{
				string fileNameFrame = Path.GetFileName(frame);
				await _sth.UploadFileAsync(containerName, containerFolderPathExtracted, fileNameFrame, localVideoPath, timestamp, sourceFileNameOrPath);
				LogHelper.LogInformation($"Uploaded {fileNameFrame} to {timestamp}", nameof(VideoHelper), nameof(UploadExtractedFramesToBlobAsync));
			}

			if (extractedFrames.Count == 0)
			{
				LogHelper.LogInformation($"No frames were extracted from {sourceFileNameOrPath}.", nameof(VideoHelper), nameof(UploadExtractedFramesToBlobAsync));
			}

			return extractedFrames.Count > 0;
		}

		public async Task UploadSegmentVideoToBlobAsync(int splitTime, string fileName, string containerName, string containerFolderPath, string containerFolderPathSegmented)
		{
			string guid = Guid.NewGuid().ToString();
			string tempPath = Path.Combine(Path.GetTempPath(), guid);
			Directory.CreateDirectory(tempPath);

			string localVideoPath = Path.Combine(tempPath, fileName);
			await _sth.DownloadFileAsync(containerName, containerFolderPath, fileName, localVideoPath);

			var segmentedVideos = await SegmentVideoAsync(localVideoPath, splitTime);
			string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

			foreach (var video in segmentedVideos)
			{
				string fileNameVideo = Path.GetFileName(video);
				await _sth.UploadFileAsync(containerName, containerFolderPathSegmented, fileNameVideo, localVideoPath, timestamp, fileName);
				LogHelper.LogInformation($"Uploaded {fileNameVideo} to {timestamp}", nameof(VideoHelper), nameof(UploadSegmentVideoToBlobAsync));
			}
		}

		#region Private Methods

		private string GetMimeType(BinaryData imageData)
		{
			using var ms = new MemoryStream(imageData.ToArray());
			var format = Image.DetectFormat(ms);
			return format?.DefaultMimeType ?? "application/octet-stream";
		}

		private static string NormalizeResponsesEndpoint(string endpoint)
		{
			var trimmed = endpoint.Trim().TrimEnd('/');

			if (trimmed.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
			{
				return trimmed;
			}

			if (trimmed.Contains(".services.ai.azure.com", StringComparison.OrdinalIgnoreCase)
				|| trimmed.Contains(".openai.azure.com", StringComparison.OrdinalIgnoreCase))
			{
				return $"{trimmed}/openai/v1";
			}

			return trimmed;
		}

		private string CreateImageDataUri(BinaryData imageData)
		{
			var mimeType = GetMimeType(imageData);
			var base64Image = Convert.ToBase64String(imageData.ToArray());
			return $"data:{mimeType};base64,{base64Image}";
		}

		private async Task<List<string>> ExtractFramesAsync(string videoPath, int frameInterval = 1, string filename = "frame")
		{
			try
			{
				var outputDir = Path.GetDirectoryName(videoPath) ?? throw new InvalidOperationException("ExtractFramesAsync outputDir is null");
				Directory.CreateDirectory(outputDir);

				await RunFFmpegAsync(videoPath, outputDir, null, FFMPEG_MODE.FSEG, frameInterval);
				var extractedFrames = Directory.GetFiles(outputDir, "*.jpg").ToList();
				extractedFrames.Sort();
				return extractedFrames;
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred while extracting frames: {ex.Message}", nameof(VideoHelper), nameof(ExtractFramesAsync), ex);
				return new List<string>();
			}
		}

		private async Task<List<string>> SegmentVideoAsync(string videoPath, int splitTime)
		{
			try
			{
				var direcName = Path.GetDirectoryName(videoPath) ?? throw new NullReferenceException("SegmentVideoAsync outputDir is null");
				string outputDir = Path.Combine(direcName, "output");
				Directory.CreateDirectory(outputDir);

				var segmentDuration = TimeSpan.FromMinutes(splitTime);
				await RunFFmpegAsync(videoPath, outputDir, segmentDuration, FFMPEG_MODE.VSEG);
				var segmentedVideos = Directory.GetFiles(outputDir, "*.mkv").ToList();
				segmentedVideos.Sort();
				return segmentedVideos;
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred while segmenting video: {ex.Message}", nameof(VideoHelper), nameof(SegmentVideoAsync), ex);
				return new List<string>();
			}
		}

		private async Task RunFFmpegAsync(string videoPath, string outputFilePath, TimeSpan? segmentDuration, FFMPEG_MODE mode = 0, int frameInterval = 1)
		{
			try
			{
				var configuredFfmpegPath = Environment.GetEnvironmentVariable("FFMPEG_PATH");
				if (!string.IsNullOrWhiteSpace(configuredFfmpegPath) && File.Exists(Path.Combine(configuredFfmpegPath, "ffmpeg.exe")))
				{
					FFmpeg.SetExecutablesPath(configuredFfmpegPath);
				}
				else
				{
					var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
					if (File.Exists(Path.Combine(baseDirectory, "ffmpeg.exe")))
					{
						FFmpeg.SetExecutablesPath(baseDirectory);
					}
					else
					{
						var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
						var pathSegments = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
						foreach (var segment in pathSegments)
						{
							var candidate = segment.Trim();
							if (File.Exists(Path.Combine(candidate, "ffmpeg.exe")))
							{
								FFmpeg.SetExecutablesPath(candidate);
								break;
							}
						}
					}
				}

				var conversion = FFmpeg.Conversions.New();

				if (mode == FFMPEG_MODE.VSEG)
				{
					var segmentTimeSeconds = (segmentDuration ?? TimeSpan.Zero).TotalSeconds;
					string framePattern = Path.Combine(outputFilePath, $"output_%010d.mkv");
					conversion.AddParameter($"-i \"{videoPath}\"")
						.AddParameter($"-c:v ffv1")
						.AddParameter($"-c:a copy")
						.AddParameter($"-map 0")
						.AddParameter($"-segment_time {segmentTimeSeconds}")
						.AddParameter($"-force_key_frames \"expr:gte(t,n_forced*{segmentTimeSeconds})\"")
						.AddParameter($"-f segment")
						.AddParameter($"-reset_timestamps 1")
						.SetOutput(framePattern);
				}
				else if (mode == FFMPEG_MODE.FSEG)
				{
					string framePattern = Path.Combine(outputFilePath, $"frame_%010d.jpg");
					conversion.AddParameter($"-i \"{videoPath}\"")
						.AddParameter($"-vf \"fps=1/{frameInterval}\"")
						.AddParameter($"-compression_level 0")
						.AddParameter($"-fs 4194304")
						.SetOutput(framePattern);
				}

				var result = await conversion.Start();
				LogHelper.LogInformation($"File {videoPath}: {result.Arguments}", nameof(VideoHelper), nameof(RunFFmpegAsync));
				LogHelper.LogInformation($"File {videoPath}: {result}", nameof(VideoHelper), nameof(RunFFmpegAsync));
			}
			catch
			{
				throw;
			}
		}

		#endregion Private Methods
	}
}
