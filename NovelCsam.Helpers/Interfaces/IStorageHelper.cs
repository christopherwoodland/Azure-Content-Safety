namespace NovelCsam.Helpers.Interfaces
{
	public interface IStorageHelper
	{
		public Task<string> UploadFileAsync(string containerName, string folderPath, string fileName);
		public Task UploadFileAsync(string containerName, string folderPath, string fileName, string localVideoPath, string timestamp, string originalFileName);
		public Task<string> UploadTextAsync(string containerName, string folderPath, string fileName, string content);
		public Task DownloadFileAsync(string containerName, string containerFolderPath, string fileName, string localVideoPath);
		public Task<string?> DownloadTextAsync(string containerName, string blobPath);
		public Task<Dictionary<string, BinaryData>> ListBlobsInFolderWithResizeAsync(string containerName, string folderPath, int maxDepth = 10, bool resize = true);
		public Task<IReadOnlyList<string>> ListBlobPathsAsync(string containerName, string folderPath, int maxDepth = 10);
		public Task<BinaryData?> GetBlobAsBinaryDataAsync(string containerName, string blobPath, bool resize = true, int maxSizeBytes = 4194304);
		public Task<Dictionary<int, string>> ListDirectoriesInFolderAsync(string containerName, string folderPath, int maxDepth = 10);
	}
}