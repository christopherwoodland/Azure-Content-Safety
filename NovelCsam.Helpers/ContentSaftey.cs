namespace NovelCsam.Helpers
{
	public class ContentSafetyHelper : IContentSafetyHelper
	{
		private ContentSafetyClient _csc;
		private readonly List<KeyValuePair<string, string>> _cscstrings;
		private static int _lastUsedIndex = -1; 
		private readonly AsyncRetryPolicy _retryPolicy;
		private const int MAX_CONTENT_SAFETY_INSTANCES = 3;


		public ContentSafetyHelper()
		{
			_cscstrings = new();
			for (int i = 1; i <= MAX_CONTENT_SAFETY_INSTANCES; i++)
			{
				var cscs = Environment.GetEnvironmentVariable($"CONTENT_SAFETY_CONNECTION_STRING{i}") ?? "";
				var csck = Environment.GetEnvironmentVariable($"CONTENT_SAFETY_CONNECTION_KEY{i}") ?? "";

				if (!string.IsNullOrEmpty(cscs) && !string.IsNullOrEmpty(csck))
					_cscstrings.Add(new KeyValuePair<string, string>(csck, cscs));
			}

			if (_cscstrings.Count > 0)
			{
				_retryPolicy = Policy.Handle<Exception>()
					.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
			}

		}
		public ContentSafetyClient GetNextContentSafetyClient()
		{
			if (_cscstrings.Count == 0)
				throw new InvalidOperationException("No Content Safety clients are configured.");

			int nextIndex;
			do
			{
				nextIndex = (_lastUsedIndex + 1) % _cscstrings.Count;
			} while (nextIndex == _lastUsedIndex && _cscstrings.Count > 1);

			_lastUsedIndex = nextIndex;

			var keyValuePair = _cscstrings.ElementAt(nextIndex);
			return new ContentSafetyClient(new Uri(keyValuePair.Value), new AzureKeyCredential(keyValuePair.Key));
		}

		public async Task<AnalyzeImageResult?> AnalyzeImageAsync(BinaryData inputImage)
		{
			try
			{
				return await _retryPolicy.ExecuteAsync(async () =>
				{
					_csc = GetNextContentSafetyClient();
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
				LogHelper.LogException(ex.Message, nameof(ContentSafetyHelper), nameof(AnalyzeImageAsync), ex);
				return null;
			}
		}

		public static ContentSafetyClient CreateContentSafetyClient()
		{
			var cscs = Environment.GetEnvironmentVariable("CONTENT_SAFETY_CONNECTION_STRING") ?? "";
			var csck = Environment.GetEnvironmentVariable("CONTENT_SAFETY_CONNECTION_KEY") ?? "";

			if (string.IsNullOrEmpty(cscs) || string.IsNullOrEmpty(csck))
			{
				var message = "Content Safety connection string or key is not set in environment variables.";
				var ex = new InvalidOperationException("Content Safety connection string or key is not set.");
				LogHelper.LogException(message, nameof(ContentSafetyHelper), nameof(CreateContentSafetyClient), ex);
				throw ex;
			}

			return new ContentSafetyClient(new Uri(cscs), new Azure.AzureKeyCredential(csck));
		}
	}
}
