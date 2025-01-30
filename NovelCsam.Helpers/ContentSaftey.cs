using System.Collections.Generic;
using System.Linq;

namespace NovelCsam.Helpers
{
	public class ContentSafetyHelper : IContentSafetyHelper
	{
		private ContentSafetyClient _csc;
		private readonly Dictionary<string, ContentSafetyClient> _cscConnections;
		private static int _lastUsedIndex = -1;
		private readonly AsyncRetryPolicy _retryPolicy;
		private const int MAX_CONTENT_SAFETY_INSTANCES = 3;


		public ContentSafetyHelper()
		{
			_cscConnections = [];
			for (int i = 1; i <= MAX_CONTENT_SAFETY_INSTANCES; i++)
			{
				var cscs = Environment.GetEnvironmentVariable($"CONTENT_SAFETY_CONNECTION_STRING{i}") ?? "";
				var csck = Environment.GetEnvironmentVariable($"CONTENT_SAFETY_CONNECTION_KEY{i}") ?? "";

				if (!string.IsNullOrEmpty(cscs) && !string.IsNullOrEmpty(csck))
				{
					try
					{
						if (!_cscConnections.ContainsKey(csck))
						{
							_cscConnections.Add(csck, new ContentSafetyClient(new Uri(cscs), new AzureKeyCredential(csck)));
						}
					}
					catch (Exception ex)
					{

						LogHelper.LogInformation(ex.Message, nameof(ContentSafetyHelper), nameof(AnalyzeImageAsync));
						continue;
					}
				}
			}
			if (_cscConnections.Count > 0)
			{
				_retryPolicy = Policy.Handle<Exception>()
								.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
			}

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
