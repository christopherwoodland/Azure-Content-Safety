namespace NovelCsam.Helpers
{
	public class StorageHelper : IStorageHelper
	{
		private readonly DataLakeServiceClient _serviceClient;
		private readonly ILogHelper _logHelper;

		public StorageHelper(ILogHelper logHelper)
		{
			_logHelper = logHelper;

			var san = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME") ?? "";
			var sak = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_KEY") ?? "";
			var saurl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL") ?? "";

			string serviceUri = saurl;
			_serviceClient = new DataLakeServiceClient(new Uri(serviceUri),
				new StorageSharedKeyCredential(san, sak));
		}

		public StorageHelper(string storageAccountName, string storageAccountKey, ILogHelper logHelper)
		{
			_logHelper = logHelper;
			string serviceUri = $"https://{storageAccountName}.dfs.core.windows.net";
			_serviceClient = new DataLakeServiceClient(new Uri(serviceUri), new StorageSharedKeyCredential(storageAccountName, storageAccountKey));
		}

		private async Task<BinaryData> GetFileAsBinaryDataWithResizeAsync(string fileName, string containerName, string folderPath, int maxSize)
		{
			var fileClient = GetFileClient(containerName, folderPath, fileName);
			var downloadResponse = await fileClient.ReadAsync();

			using var memoryStream = new MemoryStream();
			await downloadResponse.Value.Content.CopyToAsync(memoryStream);
			memoryStream.Position = 0;

			if (memoryStream.Length >= maxSize)
			{
				using var originalImage = Image.FromStream(memoryStream);
				var resizedImage = ResizeImage(originalImage, maxSize);
				using var resizedStream = new MemoryStream();
				resizedImage.Save(resizedStream, ImageFormat.Jpeg);
				return new BinaryData(resizedStream.ToArray());
			}
			else
			{
				using var originalImage = Image.FromStream(memoryStream);
				var resizedImage = ResizeImageIfNeeded(originalImage, 50, 50, 2048, 2048);
				using var resizedStream = new MemoryStream();
				resizedImage.Save(resizedStream, ImageFormat.Jpeg);
				return new BinaryData(resizedStream.ToArray());
			}
		}

		private async Task<BinaryData> GetFileAsBinaryDataAsync(string fileName, string containerName, string folderPath)
		{
			var fileClient = GetFileClient(containerName, folderPath, fileName);
			var downloadResponse = await fileClient.ReadAsync();

			using var memoryStream = new MemoryStream();
			await downloadResponse.Value.Content.CopyToAsync(memoryStream);
			memoryStream.Position = 0;
			return new BinaryData(memoryStream.ToArray());
		}

		private Image ResizeImageIfNeeded(Image originalImage, int minWidth, int minHeight, int maxWidth, int maxHeight)
		{
			int width = originalImage.Width;
			int height = originalImage.Height;

			if (width < minWidth || height < minHeight || width > maxWidth || height > maxHeight)
			{
				int newWidth = width < minWidth ? minWidth : (width > maxWidth ? maxWidth : width);
				int newHeight = height < minHeight ? minHeight : (height > maxHeight ? maxHeight : height);

				return ResizeImage(originalImage, newWidth, newHeight);
			}

			return originalImage;
		}

		private Image ResizeImage(Image image, int width, int height)
		{
			var destRect = new Rectangle(0, 0, width, height);
			var destImage = new Bitmap(width, height);

			destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

			using (var graphics = Graphics.FromImage(destImage))
			{
				graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
				graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
				graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
				graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

				using (var wrapMode = new ImageAttributes())
				{
					wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
					graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
				}
			}

			return destImage;
		}

		private Image ResizeImage(Image image, int maxSize)
		{
			int newWidth = image.Width > image.Height ? maxSize : (int)(image.Width * (maxSize / (double)image.Height));
			int newHeight = image.Width > image.Height ? (int)(image.Height * (maxSize / (double)image.Width)) : maxSize;

			var resizedImage = new Bitmap(newWidth, newHeight);
			using (var graphics = Graphics.FromImage(resizedImage))
			{
				graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
				graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
				graphics.DrawImage(image, 0, 0, newWidth, newHeight);
			}

			return resizedImage;
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
					var binaryData = resize
						? await GetFileAsBinaryDataWithResizeAsync(Path.GetFileName(pathItem.Name), containerName, folderPath, 4194304)
						: await GetFileAsBinaryDataAsync(Path.GetFileName(pathItem.Name), containerName, folderPath);
					ret.Add(pathItem.Name, binaryData);
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
				_logHelper.LogException($"An error occurred listing directories: {ex.Message}", nameof(StorageHelper), nameof(ListDirectoriesInFolderAsync), ex);
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
				_logHelper.LogException($"An error occurred listing blobs: {ex.Message}", nameof(StorageHelper), nameof(ListBlobsInFolderWithResizeAsync), ex);
				throw;
			}

			return ret;
		}

		public async Task<string> UploadFileAsync(string containerName, string folderPath, string fullFilePath)
		{
			try
			{
				var fileClient = GetFileClient(containerName, folderPath, Path.GetFileName(fullFilePath));
				using var fileStream = File.OpenRead(fullFilePath);

				var fur = await fileClient.UploadAsync(fileStream, true);
				_logHelper.LogInformation($"File '{fullFilePath}' uploaded to '{folderPath} {fur}' in container '{containerName}'.", nameof(StorageHelper), nameof(UploadFileAsync));
				return $"{folderPath}/{Path.GetFileName(fullFilePath)}";
			}
			catch (Exception ex)
			{
				_logHelper.LogException($"An error occurred while uploading the file: {ex.Message}", nameof(StorageHelper), nameof(UploadFileAsync), ex);
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
					_logHelper.LogInformation($"File '{fileName}' uploaded to '{folderPath} {fur}' in container '{containerName}'.", nameof(StorageHelper), nameof(UploadFileAsync));
				}
				else
				{
					var message = $"An error occurred while uploading the file: {fileName}. Direc is null";
					var arge = new ArgumentNullException(message);
					_logHelper.LogException(message, nameof(StorageHelper), nameof(UploadFileAsync), arge);
					throw arge;
				}
			}
			catch (Exception ex)
			{
				_logHelper.LogException($"An error occurred while uploading the file: {ex.Message}", nameof(StorageHelper), nameof(UploadFileAsync), ex);
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
				_logHelper.LogInformation($"File '{fileName}' downloaded to '{localVideoPath}' in container '{containerName}'.", nameof(StorageHelper), nameof(DownloadFileAsync));
			}
			catch (Exception ex)
			{
				_logHelper.LogException($"An error occurred while downloading the file: {ex.Message}", nameof(StorageHelper), nameof(DownloadFileAsync), ex);
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
