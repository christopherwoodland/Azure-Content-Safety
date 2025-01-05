namespace NovelCsam.Helpers
{
	public class ContentSafetyHelper : IContentSafetyHelper
	{
		private readonly ILogHelper _logHelper;
		private readonly ContentSafetyClient _csc;

		public ContentSafetyHelper(ILogHelper logHelper)
		{
			_logHelper = logHelper;
			var cscs = Environment.GetEnvironmentVariable("CONTENT_SAFETY_CONNECTION_STRING") ?? "";
			var csck = Environment.GetEnvironmentVariable("CONTENT_SAFETY_CONNECTION_KEY") ?? "";
			_csc = new ContentSafetyClient(new Uri(cscs), new Azure.AzureKeyCredential(csck));

		}

		public async Task<AnalyzeImageResult?> AnalyzeImageAsync(BinaryData inputImage)
		{
			const int maxRetries = 3;
			const int delayMilliseconds = 2000;

			// Define a Polly retry policy
			var retryPolicy = Policy
				.Handle<HttpRequestException>(ex => ex.StatusCode == (HttpStatusCode)429)
				.WaitAndRetryAsync(maxRetries, retryAttempt => TimeSpan.FromMilliseconds(delayMilliseconds),
					(exception, timeSpan, retryCount, context) =>
					{
						_logHelper.LogInformation($"Retry {retryCount}/{maxRetries} after receiving 429 Too Many Requests. Waiting {timeSpan.TotalMilliseconds}ms before retrying.", nameof(ContentSafetyHelper), nameof(AnalyzeImageAsync));
					});

			try
			{
				return await retryPolicy.ExecuteAsync(async () =>
				{
					if (_csc != null)
					{
						ContentSafetyImageData image = new(inputImage);
						var request = new AnalyzeImageOptions(image);
						var response = await _csc.AnalyzeImageAsync(request);
						return response;
					}
					else
					{
						return null;
					}
				});
			}
			catch (Exception ex)
			{
				_logHelper.LogException(ex.Message, nameof(ContentSafetyHelper), nameof(AnalyzeImageAsync), ex);
				return null;
			}
		}

		public static ContentSafetyClient CreateContentSafetyClient(ILogHelper logHelper)
		{
			var cscs = Environment.GetEnvironmentVariable("CONTENT_SAFETY_CONNECTION_STRING") ?? "";
			var csck = Environment.GetEnvironmentVariable("CONTENT_SAFETY_CONNECTION_KEY") ?? "";

			if (string.IsNullOrEmpty(cscs) || string.IsNullOrEmpty(csck))
			{
				var message = "Content Safety connection string or key is not set in environment variables.";
				var ex = new InvalidOperationException("Content Safety connection string or key is not set.");
				logHelper.LogException(message, nameof(ContentSafetyHelper), nameof(CreateContentSafetyClient), ex);
				throw ex;
			}

			return new ContentSafetyClient(new Uri(cscs), new Azure.AzureKeyCredential(csck));
		}
	}
}
