namespace NovelCsam.Functions.Functions
{
    public class ArchiveProcessedSourceBlob
    {
        private readonly IStorageHelper _storageHelper;

        public ArchiveProcessedSourceBlob(IStorageHelper storageHelper)
        {
            _storageHelper = storageHelper;
        }

        [Function("ArchiveProcessedSourceBlob")]
        public async Task<bool> RunAsync([ActivityTrigger] ArchiveBlobModel item, FunctionContext executionContext)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item.ContainerName) ||
                    string.IsNullOrWhiteSpace(item.SourceBlobPath) ||
                    string.IsNullOrWhiteSpace(item.DestinationBlobPath))
                {
                    return false;
                }

                return await _storageHelper.MoveBlobAsync(item.ContainerName, item.SourceBlobPath, item.DestinationBlobPath, overwrite: false);
            }
            catch (Exception ex)
            {
                LogHelper.LogException($"Failed to archive source blob '{item.SourceBlobPath}': {ex.Message}", nameof(ArchiveProcessedSourceBlob), nameof(RunAsync), ex);
                return false;
            }
        }
    }
}
