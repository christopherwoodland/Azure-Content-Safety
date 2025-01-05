namespace NovelCsam.Helpers
{
	public class KeyVaultHelper
	{
		private readonly SecretClient _secretClient;
		private readonly ILogHelper _logHelper;

		public KeyVaultHelper(string keyVaultName, ILogHelper logHelper)
		{
			if (string.IsNullOrWhiteSpace(keyVaultName))
				throw new ArgumentException("Key vault name cannot be null or whitespace.", nameof(keyVaultName));
			_logHelper = logHelper ?? throw new ArgumentNullException(nameof(logHelper));

			var keyVaultUrl = $"https://{keyVaultName}.vault.azure.net/";
			_secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
		}

		public async Task<string> GetSecretAsync(string secretName)
		{
			if (string.IsNullOrWhiteSpace(secretName))
				throw new ArgumentException("Secret name cannot be null or whitespace.", nameof(secretName));

			try
			{
				KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName).ConfigureAwait(false);
				return secret.Value;
			}
			catch (RequestFailedException ex)
			{
				_logHelper.LogException($"Request failed while retrieving the secret: {ex.Message}", nameof(KeyVaultHelper), nameof(GetSecretAsync), ex);
				throw;
			}
			catch (Exception ex)
			{
				_logHelper.LogException($"An error occurred while retrieving the secret: {ex.Message}", nameof(KeyVaultHelper), nameof(GetSecretAsync), ex);
				throw;
			}
		}
	}
}