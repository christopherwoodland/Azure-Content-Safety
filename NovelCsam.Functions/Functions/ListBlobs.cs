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
		public async Task<Dictionary<string, CustomBinaryData>>? RunListBlobsAsync([ActivityTrigger] ListBlobModel item, FunctionContext executionContext)
		{
			try
			{
				var ret = new Dictionary<string, CustomBinaryData>();
				var list = await _sth.ListBlobsInFolderWithResizeAsync(item.ContainerName, item.ContainerDirectory, 3);
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
