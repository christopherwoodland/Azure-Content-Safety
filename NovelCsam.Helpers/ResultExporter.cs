namespace NovelCsam.Helpers
{
	/// <summary>
	/// Exports analysis results to various storage backends.
	/// Currently supports JSON export to Azure Blob Storage.
	/// </summary>
	public class ResultExporter : IResultExporter
	{
		private readonly IStorageHelper _storageHelper;
		private readonly ILogHelper _logHelper;

		/// <summary>
		/// Initializes a new instance of the ResultExporter class.
		/// </summary>
		/// <param name="storageHelper">Storage helper for blob operations</param>
		/// <param name="logHelper">Logger for diagnostics</param>
		public ResultExporter(IStorageHelper storageHelper, ILogHelper logHelper)
		{
			_storageHelper = storageHelper ?? throw new ArgumentNullException(nameof(storageHelper));
			_logHelper = logHelper ?? throw new ArgumentNullException(nameof(logHelper));
		}

		/// <summary>
		/// Exports frame results to Azure Blob Storage as a JSON file.
		/// Results are aggregated into a single JSON array file with timestamp-based naming.
		/// </summary>
		/// <param name="results">Collection of frame results to export</param>
		/// <param name="containerName">Azure Storage container name</param>
		/// <param name="folderPath">Folder path within the container for storage</param>
		/// <param name="runId">Unique run identifier for grouping results</param>
		/// <returns>True if export succeeded; otherwise false</returns>
		public async Task<bool> ExportFrameResultsAsJsonAsync(IEnumerable<FrameResult> results, string containerName, string folderPath, string runId)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(containerName))
					throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));

				if (string.IsNullOrWhiteSpace(folderPath))
					throw new ArgumentException("Folder path cannot be null or empty", nameof(folderPath));

				if (string.IsNullOrWhiteSpace(runId))
					throw new ArgumentException("Run ID cannot be null or empty", nameof(runId));

				var resultList = results?.ToList() ?? new List<FrameResult>();

				if (resultList.Count == 0)
				{
					_logHelper.LogInformation("No results to export", nameof(ResultExporter), nameof(ExportFrameResultsAsJsonAsync));
					return true;
				}

				// Create export metadata
				var exportData = new
				{
					exportedAt = DateTime.UtcNow,
					runId = runId,
					totalResults = resultList.Count,
					results = resultList
				};

				// Serialize to JSON
				string json = JsonConvert.SerializeObject(exportData, Formatting.Indented);

				// Generate filename with timestamp
				string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
				string fileName = $"{runId}_results_{timestamp}.json";
				string blobPath = Path.Combine(folderPath, fileName).Replace("\\", "/");

				// Upload to blob storage
				await UploadJsonToBlobAsync(containerName, blobPath, json);

				_logHelper.LogInformation(
					$"Successfully exported {resultList.Count} results to blob storage: {blobPath}",
					nameof(ResultExporter),
					nameof(ExportFrameResultsAsJsonAsync));

				return true;
			}
			catch (Exception ex)
			{
				_logHelper.LogException(
					$"An error occurred while exporting results to JSON: {ex.Message}",
					nameof(ResultExporter),
					nameof(ExportFrameResultsAsJsonAsync),
					ex);
				return false;
			}
		}

		/// <summary>
		/// Exports a single frame result to Azure Blob Storage as JSON.
		/// Each result is stored as an individual file for granular access patterns.
		/// </summary>
		/// <param name="result">Frame result to export</param>
		/// <param name="containerName">Azure Storage container name</param>
		/// <param name="folderPath">Folder path within the container</param>
		/// <returns>True if export succeeded; otherwise false</returns>
		public async Task<bool> ExportFrameResultAsJsonAsync(FrameResult result, string containerName, string folderPath)
		{
			try
			{
				if (result == null)
					throw new ArgumentNullException(nameof(result));

				if (string.IsNullOrWhiteSpace(containerName))
					throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));

				if (string.IsNullOrWhiteSpace(folderPath))
					throw new ArgumentException("Folder path cannot be null or empty", nameof(folderPath));

				// Serialize to JSON
				string json = JsonConvert.SerializeObject(result, Formatting.Indented);

				// Generate filename based on run ID and frame name
				string safeFrameName = SanitizeFileName(result.Frame ?? result.Id);
				string fileName = $"{result.RunId}_{safeFrameName}_{result.Id}.json";
				string blobPath = Path.Combine(folderPath, fileName).Replace("\\", "/");

				// Upload to blob storage
				await UploadJsonToBlobAsync(containerName, blobPath, json);

				_logHelper.LogInformation(
					$"Successfully exported result to blob storage: {blobPath}",
					nameof(ResultExporter),
					nameof(ExportFrameResultAsJsonAsync));

				return true;
			}
			catch (Exception ex)
			{
				_logHelper.LogException(
					$"An error occurred while exporting result to JSON: {ex.Message}",
					nameof(ResultExporter),
					nameof(ExportFrameResultAsJsonAsync),
					ex);
				return false;
			}
		}

		/// <summary>
		/// Uploads JSON content to Azure Blob Storage.
		/// </summary>
		/// <param name="containerName">Container name in blob storage</param>
		/// <param name="blobPath">Full path to the blob (folder/filename)</param>
		/// <param name="jsonContent">JSON content to upload</param>
		/// <returns>Task representing the asynchronous operation</returns>
		private async Task UploadJsonToBlobAsync(string containerName, string blobPath, string jsonContent)
		{
			try
			{
				// Create a temporary file for upload
				string tempFileName = Path.GetTempFileName();
				await File.WriteAllTextAsync(tempFileName, jsonContent);

				try
				{
					// Upload file to blob storage
					await _storageHelper.UploadFileAsync(
						containerName,
						blobPath,
						tempFileName,
						tempFileName,
						DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
						blobPath);
				}
				finally
				{
					// Clean up temporary file
					if (File.Exists(tempFileName))
					{
						File.Delete(tempFileName);
					}
				}
			}
			catch (Exception ex)
			{
				_logHelper.LogException(
					$"An error occurred while uploading JSON to blob: {ex.Message}",
					nameof(ResultExporter),
					nameof(UploadJsonToBlobAsync),
					ex);
				throw;
			}
		}

		/// <summary>
		/// Sanitizes a filename by removing or replacing invalid characters.
		/// </summary>
		/// <param name="fileName">Original filename</param>
		/// <returns>Sanitized filename safe for blob storage</returns>
		private static string SanitizeFileName(string fileName)
		{
			if (string.IsNullOrWhiteSpace(fileName))
				return "unknown";

			// Replace invalid filename characters with underscores
			var invalidChars = Path.GetInvalidFileNameChars();
			string sanitized = fileName;
			foreach (char c in invalidChars)
			{
				sanitized = sanitized.Replace(c, '_');
			}

			// Remove multiple consecutive underscores
			while (sanitized.Contains("__"))
			{
				sanitized = sanitized.Replace("__", "_");
			}

			// Limit length to 200 characters (Azure blob path limits)
			if (sanitized.Length > 200)
				sanitized = sanitized.Substring(0, 200);

			return sanitized;
		}
	}
}
