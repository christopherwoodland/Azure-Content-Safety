using NovelCsam.Models.Interfaces.Orchestration;

namespace NovelCsam.Models.Orchestration
{
    public class FrameOrchestrationModel : IFrameOrchestrationModel
    {
        [JsonProperty(PropertyName = "containerName")]
        public string ContainerName { get; set; } = string.Empty;
        [JsonProperty(PropertyName = "containerDirectory")]
        public string ContainerDirectory { get; set; } = string.Empty;
        [JsonProperty(PropertyName = "imageBase64ToDB")]
        public bool ImageBase64ToDB { get; set; }
        [JsonProperty(PropertyName = "getSummary")]
        public bool GetSummary { get; set; }
        [JsonProperty(PropertyName = "getChildYesNo")]
        public bool GetChildYesNo { get; set; }

		[JsonProperty(PropertyName = "runId")]
        public string RunId{ get; set; } = string.Empty;
        [JsonProperty(PropertyName = "frameIntervalSeconds")]
        public int FrameIntervalSeconds { get; set; } = 1;
        [JsonProperty(PropertyName = "extractedFramesDirectory")]
        public string ExtractedFramesDirectory { get; set; } = string.Empty;
		[JsonProperty(PropertyName = "archiveSourceOnSuccess")]
		public bool ArchiveSourceOnSuccess { get; set; }
	}
}
