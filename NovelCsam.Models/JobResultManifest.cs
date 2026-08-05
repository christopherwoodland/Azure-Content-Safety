namespace NovelCsam.Models
{
	public record JobResultManifest
	{
		public string JobId { get; init; } = string.Empty;
		public string ContainerName { get; init; } = string.Empty;
		public string ContainerDirectory { get; init; } = string.Empty;
		public string Status { get; init; } = string.Empty;
		public int TotalFrames { get; init; }
		public int ProcessedFrames { get; init; }
		public int SuccessfulFrames { get; init; }
		public int FailedFrames { get; init; }
		public int SkippedFrames { get; init; }
		public DateTime StartedAtUtc { get; init; }
		public DateTime CompletedAtUtc { get; init; }
		public string ExportContainerName { get; init; } = string.Empty;
		public string ExportFolderPath { get; init; } = string.Empty;
		public IReadOnlyList<string> FrameResultBlobs { get; init; } = [];
		public IReadOnlyList<string> SkippedItems { get; init; } = [];
	}
}