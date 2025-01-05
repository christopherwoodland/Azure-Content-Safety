namespace NovelCsam.Helpers.Interfaces
{
	public interface ICsvExporter
	{
		public Task ExportToCsvAsync(IEnumerable<IFrameDetailResult> records, string filePath);
	}
}