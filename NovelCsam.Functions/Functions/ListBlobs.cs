namespace NovelCsam.Functions.Functions
{
	public class ListBlobs
	{
		private readonly IStorageHelper _sth;
		private readonly FunctionSettings _settings;

		public ListBlobs(IStorageHelper sth, FunctionSettings settings)
		{
			_sth = sth;
			_settings = settings;
		}

		[Function("ListBlobs")]
		public async Task<Dictionary<string, CustomBinaryData>>? RunListBlobsAsync([ActivityTrigger] ListBlobModel item, FunctionContext executionContext)
		{
			try
			{
				var ret = new Dictionary<string, CustomBinaryData>();
				var list = await _sth.ListBlobsInFolderWithResizeAsync(item.ContainerName, item.ContainerDirectory, _settings.BlobListingMaxDepth);
				foreach (var i in list)
				{
					ret.Add(i.Key, new CustomBinaryData(i.Value.ToArray(),i.Key));
				}
				return ret;
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred when listing blobs: {ex.Message}", nameof(ListBlobs), nameof(RunListBlobsAsync), ex);
				return null;
			}
		}
	}
}
