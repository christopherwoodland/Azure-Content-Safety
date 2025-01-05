namespace NovelCsam.Helpers.Interfaces
{
	public interface IStorageHelper
	{
		public Task<string> UploadFileAsync(string containerName, string folderPath, string fileName);
		public Task UploadFileAsync(string containerName, string folderPath, string fileName, string localVideoPath, string timestamp, string originalFileName);
		public Task DownloadFileAsync(string containerName, string containerFolderPath, string fileName, string localVideoPath);
		public Task<Dictionary<string, BinaryData>> ListBlobsInFolderWithResizeAsync(string containerName, string folderPath, int maxDepth = 10, bool resize = true);
		public Task<Dictionary<int, string>> ListDirectoriesInFolderAsync(string containerName, string folderPath, int maxDepth = 10);
	}
}