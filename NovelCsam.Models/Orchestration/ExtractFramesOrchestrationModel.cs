namespace NovelCsam.Models.Orchestration
{
	public class ExtractFramesOrchestrationModel
	{
		public string ContainerName { get; set; } = string.Empty;
		public string SourceBlobPath { get; set; } = string.Empty;
		public string TargetFolderPath { get; set; } = string.Empty;
		public int FrameIntervalSeconds { get; set; } = 1;
	}
}
