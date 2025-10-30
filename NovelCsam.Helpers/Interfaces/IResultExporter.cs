namespace NovelCsam.Helpers.Interfaces
{
	/// <summary>
	/// Defines methods for exporting analysis results to various storage backends.
	/// </summary>
	public interface IResultExporter
	{
		/// <summary>
		/// Exports multiple frame results to Azure Blob Storage as a single JSON file.
		/// </summary>
		/// <param name="results">Collection of frame results to export</param>
		/// <param name="containerName">Azure Storage container name</param>
		/// <param name="folderPath">Folder path within the container</param>
		/// <param name="runId">Unique run identifier for grouping results</param>
		/// <returns>True if export succeeded; otherwise false</returns>
		Task<bool> ExportFrameResultsAsJsonAsync(IEnumerable<FrameResult> results, string containerName, string folderPath, string runId);

		/// <summary>
		/// Exports a single frame result to Azure Blob Storage as JSON.
		/// </summary>
		/// <param name="result">Frame result to export</param>
		/// <param name="containerName">Azure Storage container name</param>
		/// <param name="folderPath">Folder path within the container</param>
		/// <returns>True if export succeeded; otherwise false</returns>
		Task<bool> ExportFrameResultAsJsonAsync(FrameResult result, string containerName, string folderPath);
	}
}
