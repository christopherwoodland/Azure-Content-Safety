using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace NovelCsam.Functions.Functions
{
	public class StorageAccess
	{
		private const string DefaultUploadContainer = "videos";
		private const string DefaultUploadPrefix = "input";
		private const string DefaultResultsContainer = "results";
		private const string DefaultResultsPrefix = "results";

		[Function("StorageAccess")]
		public static async Task<HttpResponseData> Run(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "storage/access")] HttpRequestData req)
		{
			var request = await req.ReadFromJsonAsync<StorageAccessRequest>();
			if (request == null || string.IsNullOrWhiteSpace(request.Mode) ||
				string.IsNullOrWhiteSpace(request.ContainerName) || string.IsNullOrWhiteSpace(request.BlobPath))
			{
				return await ErrorAsync(req, HttpStatusCode.BadRequest, "mode, containerName, and blobPath are required.");
			}

			var mode = request.Mode.Trim().ToLowerInvariant();
			var containerName = request.ContainerName.Trim().ToLowerInvariant();
			var blobPath = NormalizeBlobPath(request.BlobPath);
			if (string.IsNullOrWhiteSpace(blobPath) || blobPath.Contains("..", StringComparison.Ordinal))
			{
				return await ErrorAsync(req, HttpStatusCode.BadRequest, "blobPath is invalid.");
			}

			var uploadContainer = GetSetting("UPLOAD_CONTAINER_NAME", DefaultUploadContainer).ToLowerInvariant();
			var uploadPrefix = NormalizeBlobPath(GetSetting("UPLOAD_BLOB_PREFIX", DefaultUploadPrefix));
			var resultsContainer = GetSetting("JSON_EXPORT_CONTAINER_NAME", DefaultResultsContainer).ToLowerInvariant();
			var resultsPrefix = NormalizeBlobPath(GetSetting("JSON_EXPORT_FOLDER_PATH", DefaultResultsPrefix));

			BlobSasPermissions permissions;
			if (mode == "upload" && containerName == uploadContainer && IsWithinPrefix(blobPath, uploadPrefix))
			{
				permissions = BlobSasPermissions.Create | BlobSasPermissions.Write;
			}
			else if (mode == "read" && containerName == resultsContainer && IsWithinPrefix(blobPath, resultsPrefix))
			{
				permissions = BlobSasPermissions.Read;
			}
			else
			{
				return await ErrorAsync(req, HttpStatusCode.Forbidden, "The requested storage path is outside the allowed scope.");
			}

			var accountName = GetSetting("STORAGE_ACCOUNT_NAME", string.Empty);
			var blobServiceUrl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_BLOB_URL");
			if (string.IsNullOrWhiteSpace(blobServiceUrl))
			{
				if (string.IsNullOrWhiteSpace(accountName))
				{
					return await ErrorAsync(req, HttpStatusCode.InternalServerError, "Storage account configuration is missing.");
				}

				blobServiceUrl = $"https://{accountName}.blob.core.windows.net";
			}

			var serviceClient = new BlobServiceClient(new Uri(blobServiceUrl), new DefaultAzureCredential());
			var startsOn = DateTimeOffset.UtcNow.AddMinutes(-1);
			var expiresOn = DateTimeOffset.UtcNow.AddMinutes(5);
			var delegationKey = await serviceClient.GetUserDelegationKeyAsync(new BlobGetUserDelegationKeyOptions(expiresOn)
			{
				StartsOn = startsOn
			});
			var sasBuilder = new BlobSasBuilder
			{
				BlobContainerName = containerName,
				BlobName = blobPath,
				Resource = "b",
				StartsOn = startsOn,
				ExpiresOn = expiresOn,
				Protocol = SasProtocol.Https
			};
			sasBuilder.SetPermissions(permissions);

			var blobClient = serviceClient.GetBlobContainerClient(containerName).GetBlobClient(blobPath);
			var sas = sasBuilder.ToSasQueryParameters(delegationKey.Value, serviceClient.AccountName);
			var response = req.CreateResponse(HttpStatusCode.OK);
			await response.WriteAsJsonAsync(new StorageAccessResponse($"{blobClient.Uri}?{sas}", expiresOn));
			return response;
		}

		private static string GetSetting(string name, string fallback)
		{
			return Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } value ? value : fallback;
		}

		private static string NormalizeBlobPath(string value)
		{
			return value.Replace('\\', '/').Trim('/');
		}

		private static bool IsWithinPrefix(string blobPath, string prefix)
		{
			return blobPath.StartsWith($"{prefix}/", StringComparison.OrdinalIgnoreCase);
		}

		private static async Task<HttpResponseData> ErrorAsync(HttpRequestData req, HttpStatusCode status, string message)
		{
			var response = req.CreateResponse(status);
			await response.WriteAsJsonAsync(new { error = message });
			return response;
		}

		private sealed record StorageAccessRequest(string Mode, string ContainerName, string BlobPath);
		private sealed record StorageAccessResponse(string Url, DateTimeOffset ExpiresOn);
	}
}