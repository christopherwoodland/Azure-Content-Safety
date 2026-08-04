namespace NovelCsam.Helpers
{
	public class StorageHelper : IStorageHelper
	{
		private readonly DataLakeServiceClient _serviceClient;

		public StorageHelper()
		{
			var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") ?? string.Empty;
			var accountName = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME") ?? string.Empty;
			var accountKey = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_KEY") ?? string.Empty;
			var storageUrl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL") ?? string.Empty;
			var useManagedIdentity = string.Equals(
				Environment.GetEnvironmentVariable("STORAGE_USE_MANAGED_IDENTITY"),
				"true",
				StringComparison.OrdinalIgnoreCase);

			if (!string.IsNullOrWhiteSpace(connectionString))
			{
				_serviceClient = new DataLakeServiceClient(connectionString);
				return;
			}

			if (string.IsNullOrWhiteSpace(storageUrl))
			{
				if (string.IsNullOrWhiteSpace(accountName))
				{
					throw new InvalidOperationException("Storage configuration is missing. Set AZURE_STORAGE_CONNECTION_STRING or STORAGE_ACCOUNT_URL.");
				}

				storageUrl = $"https://{accountName}.dfs.core.windows.net";
			}

			if (useManagedIdentity || string.IsNullOrWhiteSpace(accountKey))
			{
				_serviceClient = new DataLakeServiceClient(new Uri(storageUrl), new DefaultAzureCredential());
				return;
			}

			if (string.IsNullOrWhiteSpace(accountName))
			{
				throw new InvalidOperationException("STORAGE_ACCOUNT_NAME is required when using account key authentication.");
			}

			_serviceClient = new DataLakeServiceClient(new Uri(storageUrl),
				new StorageSharedKeyCredential(accountName, accountKey));
		}

		public StorageHelper(string storageAccountName, string storageAccountKey, ILogHelper logHelper)
		{
			_ = logHelper;
			string serviceUri = $"https://{storageAccountName}.dfs.core.windows.net";
			_serviceClient = new DataLakeServiceClient(new Uri(serviceUri), new StorageSharedKeyCredential(storageAccountName, storageAccountKey));
		}

		private async Task<BinaryData?> GetFileAsBinaryDataWithResizeAsync(string fileName, string containerName, string folderPath, int maxSize)
		{

			var fileClient = GetFileClient(containerName, folderPath, fileName);
			var downloadResponse = await fileClient.ReadAsync();

			using var memoryStream = new MemoryStream();
			await downloadResponse.Value.Content.CopyToAsync(memoryStream);
			memoryStream.Position = 0;
			if (memoryStream.Length <= 0)
			{
				return null;
			}
			memoryStream.Position = 0;
			using var image = await Image.LoadAsync(memoryStream);
			if (memoryStream.Length <= maxSize)
			{
				using var passthroughStream = new MemoryStream();
				await image.SaveAsync(passthroughStream, image.Metadata.DecodedImageFormat ?? JpegFormat.Instance);
				return new BinaryData(passthroughStream.ToArray());
			}

			image.Mutate(operation => operation.AutoOrient());
			image.Mutate(operation => operation.Resize(new ResizeOptions
			{
				Mode = ResizeMode.Max,
				Size = new Size(2048, 2048)
			}));

			using var resizedStream = new MemoryStream();
			await image.SaveAsJpegAsync(resizedStream, new JpegEncoder
			{
				Quality = 85
			});
			return new BinaryData(resizedStream.ToArray());
		}


		private async Task<BinaryData?> GetFileAsBinaryDataAsync(string fileName, string containerName, string folderPath)
		{
			var fileClient = GetFileClient(containerName, folderPath, fileName);
			var downloadResponse = await fileClient.ReadAsync();

			using var memoryStream = new MemoryStream();
			await downloadResponse.Value.Content.CopyToAsync(memoryStream);
			memoryStream.Position = 0;
			if (memoryStream.Length <= 0)
			{
				return null;
			}
			return new BinaryData(memoryStream.ToArray());
		}

		public async Task<BinaryData?> GetBlobAsBinaryDataAsync(string containerName, string blobPath, bool resize = true, int maxSizeBytes = 4194304)
		{
			if (string.IsNullOrWhiteSpace(blobPath))
			{
				return null;
			}

			var normalizedPath = blobPath.Replace('\\', '/');
			var folderPath = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/') ?? string.Empty;
			var fileName = Path.GetFileName(normalizedPath);

			if (string.IsNullOrWhiteSpace(fileName))
			{
				return null;
			}

			return resize
				? await GetFileAsBinaryDataWithResizeAsync(fileName, containerName, folderPath, maxSizeBytes)
				: await GetFileAsBinaryDataAsync(fileName, containerName, folderPath);
		}

		public async Task<string> UploadTextAsync(string containerName, string folderPath, string fileName, string content)
		{
			try
			{
				var fileClient = GetFileClient(containerName, folderPath, fileName);
				using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
				await fileClient.UploadAsync(contentStream, overwrite: true);
				return $"{folderPath}/{fileName}";
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred while uploading text content: {ex.Message}", nameof(StorageHelper), nameof(UploadTextAsync), ex);
				throw;
			}
		}

		public async Task<string?> DownloadTextAsync(string containerName, string blobPath)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(blobPath))
				{
					return null;
				}

				var normalizedPath = blobPath.Replace('\\', '/');
				var folderPath = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/') ?? string.Empty;
				var fileName = Path.GetFileName(normalizedPath);

				if (string.IsNullOrWhiteSpace(fileName))
				{
					return null;
				}

				var fileClient = GetFileClient(containerName, folderPath, fileName);
				var response = await fileClient.ReadAsync();

				using var memoryStream = new MemoryStream();
				await response.Value.Content.CopyToAsync(memoryStream);
				return Encoding.UTF8.GetString(memoryStream.ToArray());
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred while downloading text content: {ex.Message}", nameof(StorageHelper), nameof(DownloadTextAsync), ex);
				throw;
			}
		}

		private async Task ListDirectoriesRecursive(DataLakeFileSystemClient fileSystemClient, string folderPath, Dictionary<int, string> directories, int currentDepth, int maxDepth, IndexHolder indexHolder)
		{
			if (currentDepth > maxDepth) return;

			var directoryClient = fileSystemClient.GetDirectoryClient(folderPath);
			await foreach (var pathItem in directoryClient.GetPathsAsync())
			{
				if (pathItem.IsDirectory == true)
				{
					if (currentDepth > 1)
					{
						directories.Add(indexHolder.Index++, pathItem.Name);
					}
					await ListDirectoriesRecursive(fileSystemClient, pathItem.Name, directories, currentDepth + 1, maxDepth, indexHolder);
				}
			}
		}

		private async Task ListBlobsRecursive(DataLakeFileSystemClient fileSystemClient, string folderPath, Dictionary<string, BinaryData> ret, int currentDepth, int maxDepth, string containerName, bool resize)
		{
			if (currentDepth > maxDepth) return;

			var directoryClient = fileSystemClient.GetDirectoryClient(folderPath);
			await foreach (var pathItem in directoryClient.GetPathsAsync())
			{
				if (pathItem.IsDirectory == true)
				{
					await ListBlobsRecursive(fileSystemClient, pathItem.Name, ret, currentDepth + 1, maxDepth, containerName, resize);
				}
				else
				{
					try
					{
						var binaryData = resize
							? await GetFileAsBinaryDataWithResizeAsync(Path.GetFileName(pathItem.Name), containerName, folderPath, 4194304)
							: await GetFileAsBinaryDataAsync(Path.GetFileName(pathItem.Name), containerName, folderPath);

						if (binaryData != null)
						{
							ret.Add(pathItem.Name, binaryData);
						}
					}
					catch (Exception ex)
					{
						LogHelper.LogException($"An error occurred listing blobs: {ex.Message}", nameof(StorageHelper), nameof(ListBlobsRecursive), ex);
						continue;
					}
				}
			}
		}

		public async Task<Dictionary<int, string>> ListDirectoriesInFolderAsync(string containerName, string folderPath, int maxDepth = 10)
		{
			var directories = new Dictionary<int, string>();
			try
			{
				var fileSystemClient = _serviceClient.GetFileSystemClient(containerName);
				var indexHolder = new IndexHolder { Index = 1 };
				await ListDirectoriesRecursive(fileSystemClient, folderPath, directories, 1, maxDepth, indexHolder);
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred listing directories: {ex.Message}", nameof(StorageHelper), nameof(ListDirectoriesInFolderAsync), ex);
				throw;
			}

			return directories;
		}

		public async Task<Dictionary<string, BinaryData>> ListBlobsInFolderWithResizeAsync(string containerName, string folderPath, int maxDepth = 10, bool resize = true)
		{
			var ret = new Dictionary<string, BinaryData>();
			try
			{
				var fileSystemClient = _serviceClient.GetFileSystemClient(containerName);
				await ListBlobsRecursive(fileSystemClient, folderPath, ret, 1, maxDepth, containerName, resize);
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred listing blobs: {ex.Message}", nameof(StorageHelper), nameof(ListBlobsInFolderWithResizeAsync), ex);
				throw;
			}

			return ret;
		}

		public async Task<IReadOnlyList<string>> ListBlobPathsAsync(string containerName, string folderPath, int maxDepth = 10)
		{
			var results = new List<string>();
			try
			{
				var fileSystemClient = _serviceClient.GetFileSystemClient(containerName);
				var normalizedFolder = (folderPath ?? string.Empty).Replace('\\', '/').Trim('/');
				var baseDepth = string.IsNullOrEmpty(normalizedFolder)
					? 0
					: normalizedFolder.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;

				await foreach (var pathItem in fileSystemClient.GetPathsAsync(path: normalizedFolder, recursive: true, userPrincipalName: false, cancellationToken: default))
				{
					if (pathItem.IsDirectory == true)
					{
						continue;
					}

					var currentDepth = pathItem.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).Length - baseDepth;
					if (currentDepth <= maxDepth)
					{
						results.Add(pathItem.Name);
					}
				}
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred listing blob paths: {ex.Message}", nameof(StorageHelper), nameof(ListBlobPathsAsync), ex);
				throw;
			}

			return results;
		}

		public async Task<string> UploadFileAsync(string containerName, string folderPath, string fullFilePath)
		{
			try
			{
				var fileClient = GetFileClient(containerName, folderPath, Path.GetFileName(fullFilePath));
				using var fileStream = File.OpenRead(fullFilePath);

				var fur = await fileClient.UploadAsync(fileStream, true);
				LogHelper.LogInformation($"File '{fullFilePath}' uploaded to '{folderPath} {fur}' in container '{containerName}'.", nameof(StorageHelper), nameof(UploadFileAsync));
				return $"{folderPath}/{Path.GetFileName(fullFilePath)}";
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred while uploading the file: {ex.Message}", nameof(StorageHelper), nameof(UploadFileAsync), ex);
				throw;
			}
		}

		public async Task UploadFileAsync(string containerName, string folderPath, string fileName, string localVideoPath, string timestamp, string originalFileName)
		{
			try
			{
				var directoryClient = _serviceClient.GetFileSystemClient(containerName).GetDirectoryClient($"{folderPath}/{originalFileName}/{timestamp}");
				var fileClient = directoryClient.GetFileClient(fileName);
				var direc = Path.GetDirectoryName(localVideoPath);

				if (direc != null)
				{
					string lvp = Path.Combine(direc, fileName);
					using var fileStream = File.OpenRead(lvp);

					var fur = await fileClient.UploadAsync(fileStream, true);
					LogHelper.LogInformation($"File '{fileName}' uploaded to '{folderPath} {fur}' in container '{containerName}'.", nameof(StorageHelper), nameof(UploadFileAsync));
				}
				else
				{
					var message = $"An error occurred while uploading the file: {fileName}. Direc is null";
					var arge = new ArgumentNullException(message);
					LogHelper.LogException(message, nameof(StorageHelper), nameof(UploadFileAsync), arge);
					throw arge;
				}
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred while uploading the file: {ex.Message}", nameof(StorageHelper), nameof(UploadFileAsync), ex);
				throw;
			}
		}

		public async Task DownloadFileAsync(string containerName, string containerFolderPath, string fileName, string localVideoPath)
		{
			try
			{
				var fileClient = GetFileClient(containerName, containerFolderPath, fileName);
				var downloadInfo = await fileClient.ReadAsync();

				using (var fs = File.OpenWrite(localVideoPath))
				{
					await downloadInfo.Value.Content.CopyToAsync(fs);
				}
				LogHelper.LogInformation($"File '{fileName}' downloaded to '{localVideoPath}' in container '{containerName}'.", nameof(StorageHelper), nameof(DownloadFileAsync));
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred while downloading the file: {ex.Message}", nameof(StorageHelper), nameof(DownloadFileAsync), ex);
				throw;
			}
		}

		private DataLakeFileClient GetFileClient(string containerName, string folderPath, string fileName)
		{
			var fileSystemClient = _serviceClient.GetFileSystemClient(containerName);
			var directoryClient = fileSystemClient.GetDirectoryClient(folderPath);
			return directoryClient.GetFileClient(fileName);
		}
	}

	public class IndexHolder
	{
		public int Index { get; set; }
	}
}
