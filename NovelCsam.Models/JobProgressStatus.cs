namespace NovelCsam.Models
{
	public record JobProgressStatus
	{
		public string JobId { get; init; } = string.Empty;
		public string RuntimeStatus { get; init; } = string.Empty;
		public int TotalFrames { get; init; }
		public int ProcessedFrames { get; init; }
		public int FailedFrames { get; init; }
		public int CurrentBatchSize { get; init; }
		public string? CurrentFrame { get; init; }
		public DateTime UpdatedAtUtc { get; init; }
	}
}