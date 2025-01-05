namespace NovelCsam.Helpers.Interfaces
{
	public interface IAzureSQLHelper
	{
		public Task<IFrameResult?> CreateFrameResult(IFrameResult item);
		public Task<IFrameResult?> InsertBase64(IFrameResult item);
		public Task<List<IFrameDetailResult>?> GetFrameResultWithLevelsAsync(string frameId);
	}
}
