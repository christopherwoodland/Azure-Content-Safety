namespace NovelCsam.Helpers
{
	public class VideoHelper : IVideoHelper
	{
		private readonly IKernelBuilder _kernelBuilder;
		private readonly Kernel _kernel;
		private readonly ILogHelper _logHelper;
		private readonly IStorageHelper _sth;
		private readonly IContentSafetyHelper _csh;
		private readonly IAzureSQLHelper _ash;
		private enum FFMPEG_MODE { VSEG = 0, FSEG = 1 }
		private const string HATE = "hate";
		private const string SELF_HARM = "selfharm";
		private const string VIOLENCE = "violence";
		private const string SEXUAL = "sexual";

		public VideoHelper(IStorageHelper sth, ILogHelper logHelper, IContentSafetyHelper csh, IAzureSQLHelper ash)
		{
			_logHelper = logHelper;
			_sth = sth;
			_csh = csh;
			_ash = ash;
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

		public async Task<string> UploadFileToBlobAsync(string containerName, string containerFolderPath, string sourceFileNameOrPath, string containerFolderPostfix = "", bool isImages = false, string timestampIn = "", string customName = "")
		{
			string timestamp = string.IsNullOrEmpty(timestampIn) ? DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") : timestampIn;
			string path = isImages ? $"{containerFolderPath}/{customName ?? "images"}/{timestamp}/{containerFolderPostfix}" : $"{containerFolderPath}/{Path.GetFileName(sourceFileNameOrPath)}/{timestamp}/{containerFolderPostfix}";
			return await _sth.UploadFileAsync(containerName, path, sourceFileNameOrPath);
		}
		public async Task<string> UploadFrameResultsAsync(string containerName, string containerFolderPath, string containerFolderPathResults, bool withBase64ofImage = false)
		{
			var list = await _sth.ListBlobsInFolderWithResizeAsync(containerName, containerFolderPath, 3);
			var runId = Guid.NewGuid().ToString();
			var runDateTime = DateTime.UtcNow;

			var tasks = list.Select(async item =>
			{
				var air = await GetContentSafteyDetailsAsync(item.Value);
				var summary = await SummarizeImageAsync(item.Value, "Can you do a detail analysis and tell me all the minute details about this image. Use no more than 450 words!!!");
				var childYesNo = await SummarizeImageAsync(item.Value, "Is there a younger person or child in this image? If you can't make a determination ANSWER No, ONLY ANSWER Yes or No!!");
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
				await _ash.InsertBase64(newItem);
			});

			await Task.WhenAll(tasks);

			return runId;
		}

		//public async Task<string> UploadFrameResultsAsync(string containerName, string containerFolderPath, string containerFolderPathResults, bool withBase64ofImage = false)
		//{
		//	var list = await _sth.ListBlobsInFolderWithResizeAsync(containerName, containerFolderPath, 3);
		//	var runId = Guid.NewGuid().ToString();
		//	var runDateTime = DateTime.UtcNow;

		//	foreach (var item in list)
		//	{
		//		var air = await GetContentSafteyDetailsAsync(item.Value);
		//		var summary = await SummarizeImageAsync(item.Value, "Can you do a detail analysis and tell me all the minute details about this image. Use no more than 450 words!!!");
		//		var childYesNo = await SummarizeImageAsync(item.Value, "Is there a younger person or child in this image? If you can't make a determination ANSWER No, ONLY ANSWER Yes or No!!");
		//		var md5Hash = CreateMD5Hash(item.Value);

		//		var newItem = new FrameResult
		//		{
		//			MD5Hash = md5Hash,
		//			Summary = summary,
		//			RunId = runId,
		//			Id = Guid.NewGuid().ToString(),
		//			Frame = item.Key,
		//			ChildYesNo = childYesNo,
		//			ImageBase64 = withBase64ofImage ? ConvertToBase64(item.Value) : "",
		//			RunDateTime = runDateTime
		//		};

		//		if (air != null)
		//		{
		//			foreach (var citem in air.CategoriesAnalysis)
		//			{
		//				switch (citem.Category.ToString().ToLowerInvariant())
		//				{
		//					case HATE:
		//						newItem.Hate = (int)citem.Severity;
		//						break;
		//					case SELF_HARM:
		//						newItem.SelfHarm = (int)citem.Severity;
		//						break;
		//					case VIOLENCE:
		//						newItem.Violence = (int)citem.Severity;
		//						break;
		//					case SEXUAL:
		//						newItem.Sexual = (int)citem.Severity;
		//						break;
		//				}
		//			}
		//		}

		//		await _ash.CreateFrameResult(newItem);
		//		await _ash.InsertBase64(newItem);
		//	}

		//	return runId;
		//}

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
			const int delayMilliseconds = 2000;

			var retryPolicy = Policy
				.Handle<HttpRequestException>(ex => ex.StatusCode == (HttpStatusCode)429)
				.WaitAndRetryAsync(maxRetries, retryAttempt => TimeSpan.FromMilliseconds(delayMilliseconds),
					(exception, timeSpan, retryCount, context) =>
					{
						_logHelper.LogInformation($"Retry {retryCount}/{maxRetries} after receiving 429 Too Many Requests. Waiting {timeSpan.TotalMilliseconds}ms before retrying.",
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
				_logHelper.LogException($"An error occurred summarizing an image: {ex.Message}", nameof(VideoHelper), nameof(SummarizeImageAsync), ex);
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
				_logHelper.LogInformation($"Uploaded {fileNameFrame} to {timestamp}", nameof(VideoHelper), nameof(UploadExtractedFramesToBlobAsync));
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
				_logHelper.LogInformation($"Uploaded {fileNameVideo} to {timestamp}", nameof(VideoHelper), nameof(UploadSegmentVideoToBlobAsync));
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
				_logHelper.LogException($"An error occurred while extracting frames: {ex.Message}", nameof(VideoHelper), nameof(ExtractFramesAsync), ex);
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
				_logHelper.LogException($"An error occurred while segmenting video: {ex.Message}", nameof(VideoHelper), nameof(SegmentVideoAsync), ex);
				return new List<string>();
			}
		}

		private async Task RunFFmpegAsync(string videoPath, string outputFilePath, TimeSpan? segmentDuration, FFMPEG_MODE mode = 0, int frameInterval = 1)
		{
			try
			{
				FFmpeg.SetExecutablesPath(@"ffmpeg-full\tools\ffmpeg\bin");
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
				_logHelper.LogInformation($"File {videoPath}: {result.Arguments}", nameof(VideoHelper), nameof(RunFFmpegAsync));
				_logHelper.LogInformation($"File {videoPath}: {result}", nameof(VideoHelper), nameof(RunFFmpegAsync));
			}
			catch
			{
				throw;
			}
		}

		#endregion Private Methods
	}
}
