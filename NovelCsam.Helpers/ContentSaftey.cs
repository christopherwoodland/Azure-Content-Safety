using System.Collections.Generic;
using System.Linq;

namespace NovelCsam.Helpers
{
	public class ContentSafetyHelper : IContentSafetyHelper
	{
		private ContentSafetyClient? _csc;
		private readonly Dictionary<string, ContentSafetyClient> _cscConnections;
		private static int _lastUsedIndex = -1;
		private readonly AsyncRetryPolicy _retryPolicy;
		private const int MAX_CONTENT_SAFETY_INSTANCES = 3;


		public ContentSafetyHelper()
		{
			_cscConnections = [];
			var useManagedIdentity = string.Equals(Environment.GetEnvironmentVariable("CONTENT_SAFETY_USE_MANAGED_IDENTITY"), "true", StringComparison.OrdinalIgnoreCase);

			for (int i = 1; i <= MAX_CONTENT_SAFETY_INSTANCES; i++)
			{
				var endpoint = Environment.GetEnvironmentVariable($"CONTENT_SAFETY_ENDPOINT{i}")
					?? Environment.GetEnvironmentVariable($"CONTENT_SAFETY_CONNECTION_STRING{i}")
					?? string.Empty;
				var apiKey = Environment.GetEnvironmentVariable($"CONTENT_SAFETY_CONNECTION_KEY{i}") ?? string.Empty;

				if (!string.IsNullOrWhiteSpace(endpoint))
				{
					try
					{
						var cacheKey = $"{endpoint}|{i}";
						if (!_cscConnections.ContainsKey(cacheKey))
						{
							ContentSafetyClient client;
							if (useManagedIdentity || string.IsNullOrWhiteSpace(apiKey))
							{
								client = new ContentSafetyClient(new Uri(endpoint), new DefaultAzureCredential());
							}
							else
							{
								client = new ContentSafetyClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
							}

							_cscConnections.Add(cacheKey, client);
						}
					}
					catch (Exception ex)
					{

						LogHelper.LogInformation(ex.Message, nameof(ContentSafetyHelper), nameof(AnalyzeImageAsync));
						continue;
					}
				}
			}

			_retryPolicy = Policy.Handle<Exception>()
				.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

		}
		public ContentSafetyClient GetNextContentSafetyClient()
		{
			if (_cscConnections.Count == 0)
				throw new InvalidOperationException("No Content Safety clients are configured.");

			int nextIndex;
			do
			{
				nextIndex = (_lastUsedIndex + 1) % _cscConnections.Count;
			} while (nextIndex == _lastUsedIndex && _cscConnections.Count > 1);

			_lastUsedIndex = nextIndex;

			var keyValuePair = _cscConnections.ElementAt(nextIndex);
			return keyValuePair.Value;
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
			var endpoint = Environment.GetEnvironmentVariable("CONTENT_SAFETY_ENDPOINT")
				?? Environment.GetEnvironmentVariable("CONTENT_SAFETY_CONNECTION_STRING")
				?? string.Empty;
			var apiKey = Environment.GetEnvironmentVariable("CONTENT_SAFETY_CONNECTION_KEY") ?? string.Empty;
			var useManagedIdentity = string.Equals(Environment.GetEnvironmentVariable("CONTENT_SAFETY_USE_MANAGED_IDENTITY"), "true", StringComparison.OrdinalIgnoreCase);

			if (string.IsNullOrWhiteSpace(endpoint))
			{
				var message = "Content Safety endpoint is not set in environment variables.";
				var ex = new InvalidOperationException("Content Safety endpoint is not set.");
				LogHelper.LogException(message, nameof(ContentSafetyHelper), nameof(CreateContentSafetyClient), ex);
				throw ex;
			}

			if (useManagedIdentity || string.IsNullOrWhiteSpace(apiKey))
			{
				return new ContentSafetyClient(new Uri(endpoint), new DefaultAzureCredential());
			}

			return new ContentSafetyClient(new Uri(endpoint), new Azure.AzureKeyCredential(apiKey));
		}
	}
}
