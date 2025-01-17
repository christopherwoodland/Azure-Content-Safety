namespace NovelCsam.Functions.Functions
{
	public class AnalyzeFrame
	{
		private readonly IKernelBuilder _kernelBuilder;
		private readonly Kernel _kernel;
		private readonly ILogHelper _logHelper;
		private readonly IStorageHelper _sth;
		private readonly IContentSafetyHelper _csh;
		private readonly IAzureSQLHelper _ash;
		private readonly IVideoHelper _videoHelper;
		private readonly AsyncRetryPolicy _retryPolicy;
		private enum FFMPEG_MODE { VSEG = 0, FSEG = 1 }
		private const string HATE = "hate";
		private const string SELF_HARM = "selfharm";
		private const string VIOLENCE = "violence";
		private const string SEXUAL = "sexual";
		private string? _ioapi = null;

		public AnalyzeFrame(IStorageHelper sth, ILogHelper logHelper, IContentSafetyHelper csh, IAzureSQLHelper ash, IVideoHelper videoHelper)
		{
			_logHelper = logHelper;
			_sth = sth;
			_csh = csh;
			_ash = ash;
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
			_videoHelper = videoHelper;

			_retryPolicy = Policy
				.Handle<HttpRequestException>(ex => ex.StatusCode == (HttpStatusCode)429)
				.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
					(exception, timeSpan, retryCount, context) =>
					{
						_logHelper.LogInformation($"Retry {retryCount} encountered an error: {exception.Message}. Waiting {timeSpan} before next retry.", nameof(AnalyzeFrame), "Constructor");
					});
		}

		[Function("AnalyzeFrame")]
		public async Task<string> RunAnalyzeFrameAsync([ActivityTrigger] AnalyzeFrameOrchestrationModel item, FunctionContext executionContext)
		{
			try
			{
				var frameBinaryData = new BinaryData(item.Frame.Data);
				var air = await _retryPolicy.ExecuteAsync(() => _videoHelper.GetContentSafteyDetailsAsync(frameBinaryData));
				
				var summary = "";
				var childYesNo = "";
				
				if (!string.IsNullOrEmpty(_ioapi) && _ioapi.ToLower() == "true")
				{
					summary = item.GetSummary ? await _retryPolicy.ExecuteAsync(() => _videoHelper.SummarizeImageAsync(frameBinaryData, "Can you do a detail analysis and tell me all the minute details about this image. Use no more than 450 words!!!")) : string.Empty;
					childYesNo = item.GetChildYesNo ? await _retryPolicy.ExecuteAsync(() => _videoHelper.SummarizeImageAsync(frameBinaryData, "Is there a younger person or child in this image? If you can't make a determination ANSWER No, ONLY ANSWER Yes or No!!")) : string.Empty;

				}
				var md5Hash = _videoHelper.CreateMD5Hash(frameBinaryData);

				var newItem = new FrameResult
				{
					MD5Hash = md5Hash,
					Summary = summary,
					RunId = item.RunId,
					Id = Guid.NewGuid().ToString(),
					Frame = item.Frame.Key,
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
				summary = summary.Contains("429") ? "" : summary;
				childYesNo = childYesNo.Contains("429") ? "" : childYesNo;
				await _ash.CreateFrameResult(newItem);
				await _ash.InsertBase64(newItem);
				return item.RunId;
			}
			catch (Exception ex)
			{
				_logHelper.LogException($"An error occurred when processing an image: {ex.Message}", nameof(AnalyzeFrame), nameof(RunAnalyzeFrameAsync), ex);
				return "";
			}
		}
	}
}