namespace NovelCsam.Helpers.Interfaces
{
	public interface ICsvExporter
	{
		public Task<bool> ExportToCsvAsync(IEnumerable<IFrameDetailResult> records, string filePath);
	}
}