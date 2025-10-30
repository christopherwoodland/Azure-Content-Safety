namespace NovelCsam.Functions.Configuration
{
	/// <summary>
	/// Configuration settings for Azure Functions.
	/// Values are loaded from environment variables or local.settings.json.
	/// </summary>
	public class FunctionSettings
	{
		/// <summary>
		/// Gets the maximum recursion depth when listing blobs in folders.
		/// Default: 3
		/// Environment variable: BLOB_LISTING_MAX_DEPTH
		/// </summary>
		public int BlobListingMaxDepth { get; set; } = 3;

		/// <summary>
		/// Gets the maximum number of retry attempts for transient failures (429 errors).
		/// Default: 3
		/// Environment variable: RETRY_MAX_ATTEMPTS
		/// </summary>
		public int RetryMaxAttempts { get; set; } = 3;

		/// <summary>
		/// Gets the base multiplier for exponential backoff retry delays.
		/// Default: 2 (results in 2s, 4s, 8s delays)
		/// Environment variable: RETRY_BACKOFF_MULTIPLIER
		/// </summary>
		public double RetryBackoffMultiplier { get; set; } = 2.0;

		/// <summary>
		/// Gets the HTTP status code that triggers rate-limit handling.
		/// Default: "429"
		/// Environment variable: RATE_LIMIT_ERROR_CODE
		/// </summary>
		public string RateLimitErrorCode { get; set; } = "429";

		/// <summary>
		/// Gets the AI prompt for detailed image analysis.
		/// Default: "Can you do a detail analysis and tell me all the minute details about this image. Use no more than 450 words!!!"
		/// Environment variable: DETAILED_ANALYSIS_PROMPT
		/// </summary>
		public string DetailedAnalysisPrompt { get; set; } = 
			"Can you do a detail analysis and tell me all the minute details about this image. Use no more than 450 words!!!";

		/// <summary>
		/// Gets the AI prompt for child detection in images.
		/// Default: "Is there a younger person or child in this image? If you can't make a determination ANSWER No, ONLY ANSWER Yes or No!!"
		/// Environment variable: CHILD_DETECTION_PROMPT
		/// </summary>
		public string ChildDetectionPrompt { get; set; } = 
			"Is there a younger person or child in this image? If you can't make a determination ANSWER No, ONLY ANSWER Yes or No!!";

		/// <summary>
		/// Gets a value indicating whether to export results to JSON format in blob storage.
		/// Default: false
		/// Environment variable: ENABLE_JSON_EXPORT
		/// </summary>
		public bool EnableJsonExport { get; set; } = false;

		/// <summary>
		/// Gets the Azure Storage container name for JSON result exports.
		/// Default: "results"
		/// Environment variable: JSON_EXPORT_CONTAINER_NAME
		/// </summary>
		public string JsonExportContainerName { get; set; } = "results";

		/// <summary>
		/// Gets the folder path within the container for JSON result exports.
		/// Default: "json-results"
		/// Environment variable: JSON_EXPORT_FOLDER_PATH
		/// </summary>
		public string JsonExportFolderPath { get; set; } = "json-results";

		/// <summary>
		/// Gets a value indicating whether to persist results to the SQL database.
		/// Default: true
		/// Environment variable: ENABLE_SQL_PERSISTENCE
		/// </summary>
		public bool EnableSqlPersistence { get; set; } = true;

		/// <summary>
		/// Loads configuration from environment variables.
		/// </summary>
		public static FunctionSettings FromEnvironment()
		{
			var settings = new FunctionSettings();

			// Load from environment variables with fallback to defaults
			if (int.TryParse(Environment.GetEnvironmentVariable("BLOB_LISTING_MAX_DEPTH"), out int blobDepth))
				settings.BlobListingMaxDepth = blobDepth;

			if (int.TryParse(Environment.GetEnvironmentVariable("RETRY_MAX_ATTEMPTS"), out int maxAttempts))
				settings.RetryMaxAttempts = maxAttempts;

			if (double.TryParse(Environment.GetEnvironmentVariable("RETRY_BACKOFF_MULTIPLIER"), out double backoff))
				settings.RetryBackoffMultiplier = backoff;

			var rateLimitCode = Environment.GetEnvironmentVariable("RATE_LIMIT_ERROR_CODE");
			if (!string.IsNullOrEmpty(rateLimitCode))
				settings.RateLimitErrorCode = rateLimitCode;

			var detailPrompt = Environment.GetEnvironmentVariable("DETAILED_ANALYSIS_PROMPT");
			if (!string.IsNullOrEmpty(detailPrompt))
				settings.DetailedAnalysisPrompt = detailPrompt;

			var childPrompt = Environment.GetEnvironmentVariable("CHILD_DETECTION_PROMPT");
			if (!string.IsNullOrEmpty(childPrompt))
				settings.ChildDetectionPrompt = childPrompt;

			if (bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_JSON_EXPORT"), out bool enableExport))
				settings.EnableJsonExport = enableExport;

			var containerName = Environment.GetEnvironmentVariable("JSON_EXPORT_CONTAINER_NAME");
			if (!string.IsNullOrEmpty(containerName))
				settings.JsonExportContainerName = containerName;

			var folderPath = Environment.GetEnvironmentVariable("JSON_EXPORT_FOLDER_PATH");
			if (!string.IsNullOrEmpty(folderPath))
				settings.JsonExportFolderPath = folderPath;

			if (bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_SQL_PERSISTENCE"), out bool enableSql))
				settings.EnableSqlPersistence = enableSql;

			return settings;
		}
	}
}
