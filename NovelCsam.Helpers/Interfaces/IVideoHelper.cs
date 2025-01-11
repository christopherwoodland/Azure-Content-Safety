namespace NovelCsam.Helpers.Interfaces
{
	public interface IVideoHelper
	{
		public string ConvertToBase64(BinaryData imageData);
		public string CreateMD5Hash(BinaryData imageData);
		public Task<string> SummarizeImageAsync(BinaryData imageBytes, string userPrompt);
		public Task<AnalyzeImageResult?> GetContentSafteyDetailsAsync(BinaryData bd);
		public Task<bool> UploadExtractedFramesToBlobAsync(int frameInterval, string filename, string containerName, string containerFolderPath, string containerFolderPathExtracted, string sourceFileNameOrPath);
		public Task UploadSegmentVideoToBlobAsync(int splitTime, string fileName, string containerName, string containerFolderPath, string containerFolderPathSegmented);
		public Task<string> UploadFrameResultsAsync(string containerName, string containerFolderPath, string containerFolderPathResults, bool withBase64ofImage = false, bool getSummaryB = true, bool getChildYesNoB = true);
		public Task<string> UploadFrameResultsDurableFunctionAsync(string containerName, string containerFolderPath, string containerFolderPathResults, bool withBase64ofImage = false, bool getSummaryB = true, bool getChildYesNoB = true, string runId="");
		public Task<string> UploadFileToBlobAsync(string containerName, string containerFolderPath, string sourceFileNameOrPath, string containerFolderPostfix = "", bool isImages = false, string timestampIn = "", string customName = "");
	}
}