namespace NovelCsam.Functions.Functions
{
	public class ListBlobs
	{
		private readonly IStorageHelper _sth;
		public ListBlobs(IStorageHelper sth)
		{
			_sth = sth;
		}

		[Function("ListBlobs")]
		public async Task<List<string>> RunListBlobsAsync([ActivityTrigger] ListBlobModel item, FunctionContext executionContext)
		{
			try
			{
				var list = await _sth.ListBlobPathsAsync(item.ContainerName, item.ContainerDirectory, 3);
				return new List<string>(list);
			}
			catch (Azure.RequestFailedException ex) when (string.Equals(ex.ErrorCode, "PathNotFound", StringComparison.OrdinalIgnoreCase))
			{
				LogHelper.LogInformation($"No blobs found because path does not exist yet: {item.ContainerDirectory}", nameof(ListBlobs), nameof(RunListBlobsAsync));
				return [];
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred when listing blobs: {ex.Message}", nameof(ListBlobs), nameof(RunListBlobsAsync), ex);
				throw;
			}
		}
	}
}
