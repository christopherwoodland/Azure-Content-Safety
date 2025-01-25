namespace NovelCsam.Helpers
{
	public class VideoHelper : IVideoHelper
	{
		private readonly IKernelBuilder _kernelBuilder;
		private readonly Kernel _kernel;
		private readonly IStorageHelper _sth;
		private readonly IContentSafetyHelper _csh;
		private readonly IAzureSQLHelper _ash;

		private readonly HttpClient _httpClient;
		private enum FFMPEG_MODE { VSEG = 0, FSEG = 1 }
		private const string HATE = "hate";
		private const string SELF_HARM = "selfharm";
		private const string VIOLENCE = "violence";
		private const string SEXUAL = "sexual";
		private string _requestUri = "";
		private string? _ioapi = null;

		public VideoHelper(IStorageHelper sth, IContentSafetyHelper csh, IAzureSQLHelper ash, HttpClient httpClient)
		{
			_requestUri = Environment.GetEnvironmentVariable("ANALYZE_FRAME_AZURE_FUNCTION_URL") ?? "";
			_sth = sth;
			_csh = csh;
			_ash = ash;
			_httpClient = httpClient;

			_ioapi = Environment.GetEnvironmentVariable("INVOKE_OPEN_AI");

			if (!string.IsNullOrEmpty(_ioapi) && _ioapi.ToLower() == "true")
			{
				var oaidnm = Environment.GetEnvironmentVariable("OPEN_AI_DEPLOYMENT_NAME") ?? "";
				var oaikey = Environment.GetEnvironmentVariable("OPEN_AI_KEY") ?? "";
				var oaiendpoint = Environment.GetEnvironmentVariable("OPEN_AI_ENDPOINT") ?? "";
				var oaimodel = Environment.GetEnvironmentVariable("OPEN_AI_MODEL") ?? "";
				_kernelBuilder = Kernel.CreateBuilder();

				_kernelBuilder.AddAzureOpenAIChatCompletion(
					deploymentName: oaidnm,
					apiKey: oaikey,
					endpoint: oaiendpoint,
					modelId: oaimodel,
					serviceId: Guid.NewGuid().ToString());

				_kernel = _kernelBuilder.Build();
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
			int cnt = 0;
			Console.WriteLine($"\n********************************************************************************");
			Console.WriteLine($"Total number of records to process: {totalCnt}");
			Console.WriteLine($"********************************************************************************\n");
			var runId = Guid.NewGuid().ToString();
			var runDateTime = DateTime.UtcNow;

			var tasks = list.Select(async item =>
			{
				var air = await GetContentSafteyDetailsAsync(item.Value);
				var summary = "";
				var childYesNo = "";
				if (!string.IsNullOrEmpty(_ioapi) && _ioapi.ToLower() == "true")
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
								newItem.Hate = (int)citem.Severity;
								break;
							case SELF_HARM:
								newItem.SelfHarm = (int)citem.Severity;
								break;
							case VIOLENCE:
								newItem.Violence = (int)citem.Severity;
								break;
							case SEXUAL:
								newItem.Sexual = (int)citem.Severity;
								break;
						}
					}
				}

				await _ash.CreateFrameResult(newItem);
				//await _ash.InsertBase64(newItem); I don't think we need this, will confirm...
				//This could be done better/different
				Console.WriteLine($"\n********************************************************************************");
				Console.WriteLine($"Record processed: {item.Key}");
				Console.WriteLine($"********************************************************************************\n");
				cnt++;
			});

			await Task.WhenAll(tasks);
			Console.WriteLine($"********************************************************************************");
			Console.WriteLine($"Total number of records processed: {cnt}/{totalCnt}");
			Console.WriteLine($"********************************************************************************\n");

			var message = $"RunId: {runId}: Total number of records processed: {cnt}/{totalCnt} ";
			LogHelper.LogInformation(message,nameof(VideoHelper), nameof(UploadFrameResultsAsync));
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
		bool getSummaryB = true, bool getChildYesNoB = true, string runId = "")
		{
			var item = new
			{
				ImageBase64ToDB = withBase64ofImage,
				GetSummary = getSummaryB,
				GetChildYesNo = getChildYesNoB,
				ContainerName = containerName,
				ContainerDirectory = containerFolderPath,
				RunId = runId
			};
			var ret = await CallFunctionHttpStartAsync(JsonConvert.SerializeObject(item));
			DurableTaskInstance instance = JsonConvert.DeserializeObject<DurableTaskInstance>(ret);
			var status = await CallFunctionHttpStatusAsync(instance.StatusQueryGetUri);
			OrchestrationStatus statusInstnace = JsonConvert.DeserializeObject<OrchestrationStatus>(status);
			do
			{
				//Keep checking while the status is Running.
				await Task.Delay(3000);

				status = await CallFunctionHttpStatusAsync(instance.StatusQueryGetUri);
				statusInstnace = JsonConvert.DeserializeObject<OrchestrationStatus>(status);
			} while (string.IsNullOrEmpty(statusInstnace.RuntimeStatus) && statusInstnace.RuntimeStatus != "Completed" && statusInstnace.RuntimeStatus != "Failed" && statusInstnace.RuntimeStatus != "Terminated" && statusInstnace.RuntimeStatus != "Pending" && statusInstnace.RuntimeStatus != "Suspended" && statusInstnace.RuntimeStatus != "ContinuedAsNew" && statusInstnace.RuntimeStatus != "Canceled");

			return statusInstnace.Input.ToString() ?? "";
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
					var chat = _kernel.GetRequiredService<IChatCompletionService>();
					var history = new ChatHistory();
					history.AddSystemMessage("You are a helpful assistant that responds to questions directly");
					var message = new ChatMessageContentItemCollection
					{
						new TextContent(userPrompt),
						new ImageContent(imageBytes, GetMimeType(imageBytes))
					};

					history.AddUserMessage(message);
					var res = await chat.GetChatMessageContentAsync(history);
					return res.Content ?? "NO SUMMARY GENERATED";
				});
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

			return true;
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
			using var image = Image.FromStream(ms);
			return image.RawFormat switch
			{
				var format when ImageFormat.Jpeg.Equals(format) => "image/jpeg",
				var format when ImageFormat.Png.Equals(format) => "image/png",
				var format when ImageFormat.Gif.Equals(format) => "image/gif",
				var format when ImageFormat.Bmp.Equals(format) => "image/bmp",
				var format when ImageFormat.Tiff.Equals(format) => "image/tiff",
				var format when ImageFormat.Icon.Equals(format) => "image/x-icon",
				var format when ImageFormat.Emf.Equals(format) => "image/emf",
				var format when ImageFormat.Exif.Equals(format) => "image/exif",
				var format when ImageFormat.Wmf.Equals(format) => "image/wmf",
				var format when ImageFormat.MemoryBmp.Equals(format) => "image/bmp",
				_ => "application/octet-stream",
			};
		}

		private async Task<List<string>> ExtractFramesAsync(string videoPath, int frameInterval = 1, string filename = "frame")
		{
			try
			{
				var outputDir = Path.GetDirectoryName(videoPath);
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
				// Combine the base directory with the relative path to the FFmpeg executables
				string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);

				// Set the FFmpeg executables path
				FFmpeg.SetExecutablesPath(ffmpegPath);

				var conversion = FFmpeg.Conversions.New();

				if (mode == FFMPEG_MODE.VSEG)
				{
					string framePattern = Path.Combine(outputFilePath, $"output_%010d.mkv");
					conversion.AddParameter($"-i \"{videoPath}\"")
						.AddParameter($"-c:v ffv1")
						.AddParameter($"-c:a copy")
						.AddParameter($"-map 0")
						.AddParameter($"-segment_time {segmentDuration.Value.TotalSeconds}")
						.AddParameter($"-force_key_frames \"expr:gte(t,n_forced*{segmentDuration.Value.TotalSeconds})\"")
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
