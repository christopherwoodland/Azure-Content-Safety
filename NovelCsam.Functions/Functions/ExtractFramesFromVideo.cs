namespace NovelCsam.Functions.Functions
{
	public class ExtractFramesFromVideo
	{
		private readonly IVideoHelper _videoHelper;

		public ExtractFramesFromVideo(IVideoHelper videoHelper)
		{
			_videoHelper = videoHelper;
		}

		[Function("ExtractFramesFromVideo")]
		public async Task<bool> RunAsync([ActivityTrigger] ExtractFramesOrchestrationModel item, FunctionContext executionContext)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(item.SourceBlobPath) || string.IsNullOrWhiteSpace(item.ContainerName))
				{
					return false;
				}

				var fileName = Path.GetFileName(item.SourceBlobPath);
				if (string.IsNullOrWhiteSpace(fileName))
				{
					return false;
				}

				var sourceFolder = Path.GetDirectoryName(item.SourceBlobPath)?.Replace('\\', '/') ?? string.Empty;
				var frameIntervalSeconds = Math.Max(1, item.FrameIntervalSeconds);

				return await _videoHelper.UploadExtractedFramesToBlobAsync(
					frameIntervalSeconds,
					fileName,
					item.ContainerName,
					sourceFolder,
					item.TargetFolderPath,
					fileName);
			}
			catch (Exception ex)
			{
				LogHelper.LogException($"An error occurred while extracting frames: {ex.Message}", nameof(ExtractFramesFromVideo), nameof(RunAsync), ex);
				return false;
			}
		}
	}
}
